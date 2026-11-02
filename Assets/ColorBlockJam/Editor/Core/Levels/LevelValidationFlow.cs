using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor's side of the solvability check (DEC-144): runs it on the main thread behind a delayed
    /// cancelable progress bar, refuses to start in Play Mode, keeps the last result until the draft changes, and
    /// words Save Level's warning. Validate Level also lists a Solvable level's solutions in a second phase, on the
    /// check's tables, with its own progress text and budget. With <see cref="RunsInBackground"/> Validate Level runs
    /// as one <see cref="LevelValidationJob"/> on a worker thread instead, and Save Level waits for a run of the same
    /// draft (DEC-175). The check itself is <see cref="LevelSolvabilityCheck"/>; this class only reads levels.
    /// </summary>
    internal sealed class LevelValidationFlow
    {
        // How long a window close or a domain reload waits for stopped runs; a run polls its cancel request about ten
        // times a second, so it ends well before this.
        private const int k_shutdownWaitMilliseconds = 2000;
        // Save Level's wait for a background run checks the progress bar's Cancel this often.
        private const int k_waitSliceMilliseconds = 50;

        /// <summary>A check shorter than this shows no progress bar (contract: 0.25 s).</summary>
        internal const double k_progressDelaySeconds = 0.25;
        /// <summary>The progress bar changes at most this often.</summary>
        internal const double k_progressIntervalSeconds = 0.1;
        internal const string k_progressTitle = "Validate Level";
        internal const string k_unsolvableTitle = "Level Cannot Be Solved";
        internal const string k_notConfirmedTitle = "Level Not Confirmed";
        internal const string k_playModeText = "Not checked: the Editor is in Play Mode.";
        internal const string k_saveCancelledPrefix = "Save cancelled: ";
        /// <summary>The listing phase's progress line starts with this; the check's reads "Checked N positions. …".</summary>
        internal const string k_listingProgressPrefix = "Listing solutions: checked ";

        // Bumped by every draft change; a kept result is reused only at the same revision and for the same level.
        private int m_revision;
        private bool m_hasKept;
        private int m_keptRevision;
        private string m_keptJson;
        private LevelValidationOutcome m_kept;

        // The Validate Level run on a worker thread, or null. Stopped runs whose worker has not returned yet wait in
        // m_stoppedJobs until Shutdown, so no worker outlives the window or a domain reload.
        private LevelValidationJob m_job;
        private readonly List<LevelValidationJob> m_stoppedJobs = new List<LevelValidationJob>();
        private static readonly Predicate<LevelValidationJob> s_isCompleted = job => job.IsCompleted;

        /// <summary>
        /// Raised on the main thread when a background run ends with an answer for the unchanged draft: from
        /// <see cref="TryFinishRun"/> or from Save Level's wait in <see cref="Check"/>. The level is the run's copy of the
        /// draft. Subscriber: the Level Editor window.
        /// </summary>
        internal event Action<LevelData, LevelValidationOutcome> RunFinished;

        /// <summary>
        /// Validate Level runs on a worker thread with <see cref="StartCheckAndList"/> (DEC-175). The Level Editor's Open
        /// sets it; a window a test creates keeps the synchronous <see cref="CheckAndList"/>.
        /// </summary>
        internal bool RunsInBackground { get; set; }

        /// <summary>True while a background Validate Level run goes on.</summary>
        internal bool IsRunning => m_job != null;

        /// <summary>True while the Editor plays or is about to; tests replace it.</summary>
        internal Func<bool> IsPlaying { get; set; } = () => EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>
        /// Shows or updates the progress bar with a line and a 0-1 fraction; true means Cancel was pressed. Tests
        /// replace it so no progress bar opens.
        /// </summary>
        internal Func<string, float, bool> ShowProgress { get; set; } = (info, fraction) =>
            EditorUtility.DisplayCancelableProgressBar(k_progressTitle, info, fraction);

        /// <summary>Removes the progress bar; called once after every call that searched, shown or not. Tests replace it.</summary>
        internal Action ClearProgress { get; set; } = EditorUtility.ClearProgressBar;

        /// <summary>How long a check runs before the progress bar appears; tests set 0 to reach Cancel at once.</summary>
        internal double ProgressDelaySeconds { get; set; } = k_progressDelaySeconds;

        /// <summary>
        /// The check's and the listing's budgets and wording; null uses their defaults. Tests use a small budget.
        /// </summary>
        internal LevelCheckOptions Options { get; set; }

        /// <summary>
        /// Names pieces in the check's reasons (DEC-193): the window sets a lookup over names it read on the main thread
        /// before each check, because a background run calls it on its worker thread (DEC-175). Null reads "n-cell".
        /// </summary>
        internal Func<ShapeKey, string> ShapeNames { get; set; }

        /// <summary>How many checks actually searched, not counting reused results or Play Mode refusals.</summary>
        internal int RunCount { get; private set; }

        /// <summary>How many solution listings actually searched (Validate Level only), counted apart from RunCount.</summary>
        internal int ListRunCount { get; private set; }

        /// <summary>
        /// Drops the kept result and stops a background run, whose answer would be stale; the window calls it on every
        /// draft change, undo, level or JSON switch.
        /// </summary>
        internal void Invalidate()
        {
            m_revision++;
            m_hasKept = false;
            m_keptJson = null;
            StopRun();
        }

        /// <summary>
        /// Validate Level in the background (DEC-175). True with the answer when it needs no search: the Play Mode
        /// refusal, or an unchanged kept result that has its list or is not Solvable. Otherwise false: one run starts
        /// for this draft (the check and listing, or only the listing for a kept Solvable verdict without a list), and
        /// its answer comes through <see cref="RunFinished"/>.
        /// </summary>
        internal bool StartCheckAndList(LevelData level, out LevelValidationOutcome outcome)
        {
            StopRun();
            if (IsPlaying())
            {
                outcome = new LevelValidationOutcome(LevelCheckStatus.NotChecked, k_playModeText, false);
                return true;
            }

            string json = JsonUtility.ToJson(level);
            bool hasKept = TryGetKept(json, out outcome);
            if (hasKept && !NeedsListing(outcome))
            {
                return true;
            }

            LevelCheckResult verdict = hasKept ? outcome.Result : null;
            if (verdict == null)
            {
                RunCount++;
            }
            else
            {
                ListRunCount++;
            }

            // The caller's level is a draft snapshot nothing else changes (GetDraftLevel), so the worker reads it alone.
            m_job = new LevelValidationJob(level, json, m_revision, GetRunOptions(), verdict);
            outcome = default;
            return false;
        }

        /// <summary>
        /// The running run's progress line for the validation panel and the status line, or null when none runs.
        /// </summary>
        internal string GetRunProgressText()
        {
            return m_job == null
                ? null
                : FormatProgress(m_job.IsListing, m_job.Positions, GetMaxPositions(m_job.IsListing),
                    m_job.ElapsedSeconds, GetMaxSeconds(m_job.IsListing));
        }

        /// <summary>
        /// The text Validate Level shows while it runs in the background, refreshed about ten times a second: listing is
        /// false while the check searches and true while it lists a Solvable level's solutions; positions counts that
        /// phase's stored positions up to maxPositions, its budget (int.MaxValue for none); seconds counts from the
        /// run's start up to maxSeconds. One line, so it fits the panel summary and the status line:
        /// "Checked 338,036 positions in 1.2 of 15 s. Cancel stops the check."
        /// </summary>
        internal static string FormatProgress(bool listing, int positions, int maxPositions, double seconds,
            double maxSeconds)
        {
            string count = positions.ToString("N0", CultureInfo.InvariantCulture);
            if (maxPositions < int.MaxValue)
            {
                count += " of " + maxPositions.ToString("N0", CultureInfo.InvariantCulture);
            }

            string time = seconds.ToString("0.0", CultureInfo.InvariantCulture) + " of "
                + maxSeconds.ToString("0.#", CultureInfo.InvariantCulture) + " s";
            return listing
                ? k_listingProgressPrefix + count + " positions in " + time + ". Cancel stops the listing."
                : "Checked " + count + " positions in " + time + ". Cancel stops the check.";
        }

        /// <summary>
        /// Stops the background run at its next progress poll (the Validate Level button reads Cancel while it runs): a
        /// check stopped early answers Not checked, a listing stopped early keeps the verdict and the entries so far.
        /// The answer arrives through <see cref="TryFinishRun"/> as usual.
        /// </summary>
        internal void CancelRun()
        {
            m_job?.Cancel();
        }

        /// <summary>
        /// Main thread, called about every Editor update while a run goes on: when the run has ended, keeps its answer
        /// (a cancelled check is not kept), raises <see cref="RunFinished"/> and returns true.
        /// </summary>
        internal bool TryFinishRun()
        {
            PruneStoppedJobs();
            if (m_job == null || !m_job.IsCompleted)
            {
                return false;
            }

            FinishRun();
            return true;
        }

        /// <summary>
        /// Stops a background run without an answer (a draft change, Play Mode): it no longer counts as running, and its
        /// worker ends at its next progress poll.
        /// </summary>
        internal void StopRun()
        {
            if (m_job == null)
            {
                return;
            }

            m_job.Cancel();
            m_stoppedJobs.Add(m_job);
            m_job = null;
        }

        /// <summary>
        /// The window's OnDisable (a close or a domain reload): stops the run and waits for every stopped worker, so no
        /// search thread outlives the window or the assemblies.
        /// </summary>
        internal void Shutdown()
        {
            StopRun();
            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int index = 0; index < m_stoppedJobs.Count; index++)
            {
                int left = k_shutdownWaitMilliseconds - (int)stopwatch.ElapsedMilliseconds;
                if (left <= 0 || !m_stoppedJobs[index].Wait(left))
                {
                    Debug.LogWarning("Validate Level: a stopped check was still running when the Level Editor closed.");
                    break;
                }
            }

            m_stoppedJobs.Clear();
        }

        /// <summary>
        /// Save Level, Test Level and save-on-close: the check's verdict for this level, without a solution list. The
        /// kept result, listed or not, is reused when the draft has not changed since; a background Validate Level run of
        /// this unchanged draft is waited for behind the progress bar (DEC-175); otherwise the check runs. Refuses in
        /// Play Mode. A cancelled run is not kept.
        /// </summary>
        internal LevelValidationOutcome Check(LevelData level)
        {
            if (IsPlaying())
            {
                return new LevelValidationOutcome(LevelCheckStatus.NotChecked, k_playModeText, false);
            }

            string json = JsonUtility.ToJson(level);
            if (TryGetKept(json, out LevelValidationOutcome kept))
            {
                return kept;
            }

            if (m_job != null)
            {
                if (m_job.Revision == m_revision && string.Equals(m_job.Json, json, StringComparison.Ordinal))
                {
                    return WaitForRun();
                }

                // A run of other content answers another question; the check below replaces it.
                StopRun();
            }

            LevelValidationOutcome outcome;
            try
            {
                outcome = RunCheck(level);
            }
            finally
            {
                ClearProgress();
            }

            Keep(outcome, json);
            return outcome;
        }

        /// <summary>
        /// Validate Level: the verdict and, for a Solvable level, its listed solutions. An unchanged kept result with a
        /// list, or one that is not Solvable, is reused without a search; a kept Solvable verdict without a list gets
        /// only the listing. Otherwise the check and the listing run together on one set of tables. A Cancel during the
        /// check gives the cancelled Not checked answer, which is not kept; a Cancel during the listing keeps the
        /// verdict with the entries found so far. Refuses in Play Mode.
        /// </summary>
        internal LevelValidationOutcome CheckAndList(LevelData level)
        {
            if (IsPlaying())
            {
                return new LevelValidationOutcome(LevelCheckStatus.NotChecked, k_playModeText, false);
            }

            string json = JsonUtility.ToJson(level);
            bool hasKept = TryGetKept(json, out LevelValidationOutcome outcome);
            if (hasKept && !NeedsListing(outcome))
            {
                return outcome;
            }

            try
            {
                outcome = hasKept ? RunListing(level, outcome.Result) : RunCheckAndListing(level);
            }
            finally
            {
                // One progress bar serves both phases, so it is cleared once.
                ClearProgress();
            }

            // A cancelled check is not kept.
            Keep(outcome, json);
            return outcome;
        }

        /// <summary>The Save Level warning's title for an outcome that needs one.</summary>
        internal static string GetWarningTitle(LevelValidationOutcome outcome)
        {
            return outcome.Status == LevelCheckStatus.Unsolvable ? k_unsolvableTitle : k_notConfirmedTitle;
        }

        /// <summary>The Save Level warning's message: the result sentence and what each answer does.</summary>
        internal static string GetWarningMessage(LevelValidationOutcome outcome, string levelName)
        {
            string name = string.IsNullOrWhiteSpace(levelName) ? "this level" : levelName;
            return outcome.Text + "\n\nSave " + name + " anyway? Cancel or Keep Editing writes nothing and keeps " +
                   "your changes; Save Anyway writes the level as it is.";
        }

        private bool TryGetKept(string json, out LevelValidationOutcome kept)
        {
            kept = m_kept;
            return m_hasKept && m_keptRevision == m_revision && string.Equals(m_keptJson, json, StringComparison.Ordinal);
        }

        private void Keep(LevelValidationOutcome outcome, string json)
        {
            if (outcome.IsCancelled)
            {
                return;
            }

            m_hasKept = true;
            m_keptRevision = m_revision;
            m_keptJson = json;
            m_kept = outcome;
        }

        // Save Level's wait for the background run of its unchanged draft, behind the same progress bar as a check on
        // the main thread. The bar's Cancel cancels the run as it would cancel that check: during the check the save is
        // cancelled, during the listing the verdict stays and the save goes on. The run's answer also reaches the panel
        // through RunFinished. Owner: this call. Stop point: the run ends (answer, budget or cancel); the bar is cleared.
        private LevelValidationOutcome WaitForRun()
        {
            LevelValidationJob job = m_job;
            double lastShown = double.NegativeInfinity;
            try
            {
                while (!job.Wait(k_waitSliceMilliseconds))
                {
                    double elapsed = job.ElapsedSeconds;
                    if (job.IsCancelRequested || elapsed - lastShown < k_progressIntervalSeconds)
                    {
                        continue;
                    }

                    lastShown = elapsed;
                    // Whichever budget runs out first fills the bar, as in the check on the main thread.
                    float fraction = Mathf.Clamp01(Mathf.Max(
                        job.Positions / (float)Math.Max(1, GetMaxPositions(job.IsListing)),
                        (float)(elapsed / Math.Max(0.001, GetMaxSeconds(job.IsListing)))));
                    if (ShowProgress(GetRunProgressText(), fraction))
                    {
                        job.Cancel();
                    }
                }
            }
            finally
            {
                ClearProgress();
            }

            return FinishRun();
        }

        // The ended run's answer: logged if the worker threw, kept unless the check was cancelled or failed, counted as
        // a listing when one ran, and raised through RunFinished.
        private LevelValidationOutcome FinishRun()
        {
            LevelValidationJob job = m_job;
            m_job = null;
            LevelValidationOutcome outcome = job.GetOutcome(out Exception error);
            if (error != null)
            {
                Debug.LogException(error);
            }
            else
            {
                if (!job.IsListingOnly && outcome.Result != null
                    && outcome.Result.SolutionListState != LevelSolutionListState.NotListed)
                {
                    ListRunCount++;
                }

                Keep(outcome, job.Json);
            }

            RunFinished?.Invoke(job.Level, outcome);
            return outcome;
        }

        // Forgets stopped runs whose worker has returned; it runs every Editor update during a run, so the predicate
        // is cached.
        private void PruneStoppedJobs()
        {
            if (m_stoppedJobs.Count > 0)
            {
                m_stoppedJobs.RemoveAll(s_isCompleted);
            }
        }

        // The options a check or listing runs with: Options, or the defaults, with this flow's piece names. The names are
        // set on that same object; a worker still running an earlier check reads either lookup, and each only reads
        // its own snapshot, so both stay safe off the main thread (DEC-175).
        private LevelCheckOptions GetRunOptions()
        {
            LevelCheckOptions options = Options ?? new LevelCheckOptions();
            if (ShapeNames != null)
            {
                options.ShapeNames = ShapeNames;
            }

            return options;
        }

        // The phase's position budget: the listing's or the check's, from Options or the defaults.
        private int GetMaxPositions(bool listing)
        {
            LevelCheckOptions options = Options;
            return listing
                ? options != null ? options.MaxListPositions : LevelCheckOptions.DefaultMaxListPositions
                : options != null ? options.MaxPositions : LevelCheckOptions.DefaultMaxPositions;
        }

        // The phase's time budget in seconds: the listing's or the check's, from Options or the defaults.
        private double GetMaxSeconds(bool listing)
        {
            LevelCheckOptions options = Options;
            return listing
                ? options != null ? options.MaxListSeconds : LevelCheckOptions.DefaultMaxListSeconds
                : options != null ? options.MaxSeconds : LevelCheckOptions.DefaultMaxSeconds;
        }

        // A Solvable verdict that has no solution list yet.
        private static bool NeedsListing(LevelValidationOutcome outcome)
        {
            return outcome.Status == LevelCheckStatus.Solvable && outcome.Result != null
                   && outcome.Result.SolutionListState == LevelSolutionListState.NotListed;
        }

        // Owner: this call. Stop point: the check returns (solved, proven, budget or cancel); the caller clears the bar.
        private LevelValidationOutcome RunCheck(LevelData level)
        {
            ProgressBar progress = new ProgressBar(this, false);
            RunCount++;
            LevelCheckResult result = LevelSolvabilityCheck.Run(level, GetRunOptions(), progress);
            return new LevelValidationOutcome(result, progress.IsCancelRequested);
        }

        // Owner: this call. Stop point: the check returns (solved, proven, budget or cancel) and, for a Solvable verdict,
        // the listing returns (exhausted, 20+, budget or cancel); the caller clears the bar. The listing starts its
        // positions and time from 0, so each phase has its own progress adapter; only the check's Cancel cancels the
        // answer.
        private LevelValidationOutcome RunCheckAndListing(LevelData level)
        {
            ProgressBar checkProgress = new ProgressBar(this, false);
            ProgressBar listProgress = new ProgressBar(this, true);
            RunCount++;
            LevelCheckResult result = LevelSolvabilityCheck.RunAndList(level, GetRunOptions(), checkProgress,
                listProgress);
            if (result.SolutionListState != LevelSolutionListState.NotListed)
            {
                ListRunCount++;
            }

            return new LevelValidationOutcome(result, checkProgress.IsCancelRequested);
        }

        // Owner: this call. Stop point: the listing returns (exhausted, 20+, budget or cancel); the caller clears the
        // bar. Only the listing searches, on tables built again for it, so it has its own progress adapter.
        private LevelValidationOutcome RunListing(LevelData level, LevelCheckResult verdict)
        {
            ProgressBar progress = new ProgressBar(this, true);
            ListRunCount++;
            LevelCheckResult listed = LevelSolvabilityCheck.ListSolutions(level, verdict, GetRunOptions(), progress);
            return new LevelValidationOutcome(listed, false);
        }

        /// <summary>
        /// Feeds one phase's progress, the check's or the listing's, to the flow's progress bar: nothing before the
        /// delay, then at most one update per interval, and a pressed Cancel stops that phase.
        /// </summary>
        private sealed class ProgressBar : ILevelCheckProgress
        {
            private readonly LevelValidationFlow m_flow;
            private readonly bool m_listing;
            private double m_lastShown = double.NegativeInfinity;

            internal ProgressBar(LevelValidationFlow flow, bool listing)
            {
                m_flow = flow;
                m_listing = listing;
            }

            public bool IsCancelRequested { get; private set; }

            public void Report(int positions, double elapsedSeconds)
            {
                if (IsCancelRequested || elapsedSeconds < m_flow.ProgressDelaySeconds
                    || elapsedSeconds - m_lastShown < k_progressIntervalSeconds)
                {
                    return;
                }

                m_lastShown = elapsedSeconds;
                LevelCheckOptions options = m_flow.Options;
                int maxPositions = m_listing
                    ? options != null ? options.MaxListPositions : LevelCheckOptions.DefaultMaxListPositions
                    : options != null ? options.MaxPositions : LevelCheckOptions.DefaultMaxPositions;
                double maxSeconds = m_listing
                    ? options != null ? options.MaxListSeconds : LevelCheckOptions.DefaultMaxListSeconds
                    : options != null ? options.MaxSeconds : LevelCheckOptions.DefaultMaxSeconds;
                float fraction = Mathf.Clamp01(Mathf.Max(positions / (float)Math.Max(1, maxPositions),
                    (float)(elapsedSeconds / Math.Max(0.001, maxSeconds))));
                string count = positions.ToString("N0", CultureInfo.InvariantCulture);
                string info = m_listing
                    ? k_listingProgressPrefix + count + " positions. Cancel stops the listing."
                    : "Checked " + count + " positions. Cancel stops the check.";
                if (m_flow.ShowProgress(info, fraction))
                {
                    IsCancelRequested = true;
                }
            }
        }
    }

    /// <summary>
    /// One Level Editor validation answer: the check's status and sentence, its whole result with any solution list,
    /// and whether Cancel stopped the check.
    /// </summary>
    internal readonly struct LevelValidationOutcome
    {
        /// <summary>An answer without a check result, such as the Play Mode refusal.</summary>
        internal LevelValidationOutcome(LevelCheckStatus status, string text, bool cancelled)
        {
            Status = status;
            Text = text;
            IsCancelled = cancelled;
            Result = null;
        }

        /// <summary>The check's answer; cancelled means the progress bar's Cancel stopped the check itself.</summary>
        internal LevelValidationOutcome(LevelCheckResult result, bool cancelled)
        {
            Status = result.Status;
            Text = result.Text;
            IsCancelled = cancelled;
            Result = result;
        }

        internal LevelCheckStatus Status { get; }

        /// <summary>The designer sentence for the status line, for example "Solvable in 4 moves.".</summary>
        internal string Text { get; }

        /// <summary>True when the progress bar's Cancel stopped the check (then Status is NotChecked).</summary>
        internal bool IsCancelled { get; }

        /// <summary>The check's whole result, with the solution list Validate Level asked for; null for the Play Mode refusal.</summary>
        internal LevelCheckResult Result { get; }

        /// <summary>The validation panel's summary: the sentence and the count for a listed result, else the sentence.</summary>
        internal string SummaryText => Result != null ? Result.SummaryText : Text;

        /// <summary>Proven unsolvable or not confirmed: Save Level asks before it writes.</summary>
        internal bool NeedsConfirmation => Status == LevelCheckStatus.Unsolvable || Status == LevelCheckStatus.Unknown;

        /// <summary>The note Save Level adds after its own message for a level the check did not cover, or null.</summary>
        internal string SaveNote => Status == LevelCheckStatus.NotChecked ? Text : null;
    }
}
