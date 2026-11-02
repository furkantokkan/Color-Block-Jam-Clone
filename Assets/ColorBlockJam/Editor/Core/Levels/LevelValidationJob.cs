using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One Validate Level search on a worker thread (DEC-175): the check and listing, or the listing alone for a kept
    /// verdict, on the job's own copy of the draft, so the Editor stays responsive. The main thread reads its phase,
    /// positions and time, cancels it and takes its result; the worker touches no Unity object and no window state.
    /// </summary>
    internal sealed class LevelValidationJob
    {
        private readonly WorkerProgress m_checkProgress = new WorkerProgress();
        private readonly WorkerProgress m_listProgress = new WorkerProgress();
        private readonly Stopwatch m_stopwatch = Stopwatch.StartNew();
        private readonly bool m_listingOnly;
        private readonly Task<LevelCheckResult> m_task;

        /// <summary>
        /// Starts the check and, for a Solvable verdict, the listing (LevelSolvabilityCheck.RunAndList); with a verdict,
        /// only the listing (LevelSolvabilityCheck.ListSolutions). The level must be a copy nothing else changes.
        /// </summary>
        internal LevelValidationJob(LevelData level, string json, int revision, LevelCheckOptions options,
            LevelCheckResult verdict)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Json = json;
            Revision = revision;
            m_listingOnly = verdict != null;
            // Owner: the LevelValidationFlow that started it. Stop point: the search returns (solved, proven, budget or
            // a cancel); Cancel ends it early, and the flow waits for it before a window close or a domain reload.
            m_task = Task.Run(() => m_listingOnly
                ? LevelSolvabilityCheck.ListSolutions(level, verdict, options, m_listProgress)
                : LevelSolvabilityCheck.RunAndList(level, options, m_checkProgress, m_listProgress));
        }

        /// <summary>The job's own copy of the draft; the answer belongs to this level.</summary>
        internal LevelData Level { get; }

        /// <summary>The draft's JSON when the job started; a Save of the same draft waits for this job.</summary>
        internal string Json { get; }

        /// <summary>The flow's draft revision when the job started.</summary>
        internal int Revision { get; }

        internal bool IsCompleted => m_task.IsCompleted;

        /// <summary>True once the listing phase has started, or from the start for a listing-only job.</summary>
        internal bool IsListing => m_listingOnly || m_listProgress.HasReported;

        /// <summary>True for a job that only lists a kept verdict's solutions.</summary>
        internal bool IsListingOnly => m_listingOnly;

        /// <summary>The positions the current phase has stored, as its last progress report said.</summary>
        internal int Positions => IsListing ? m_listProgress.Positions : m_checkProgress.Positions;

        /// <summary>Seconds since the job started, both phases together.</summary>
        internal double ElapsedSeconds => m_stopwatch.Elapsed.TotalSeconds;

        internal bool IsCancelRequested => m_checkProgress.IsCancelRequested;

        /// <summary>
        /// Asks the worker to stop at its next progress poll. As with the progress bar's Cancel, a check stopped early
        /// answers Not checked, and a listing stopped early keeps the verdict and the entries found so far.
        /// </summary>
        internal void Cancel()
        {
            m_checkProgress.RequestCancel();
            m_listProgress.RequestCancel();
        }

        /// <summary>Blocks the calling thread until the job ends or the time runs out; true when it ended.</summary>
        internal bool Wait(int milliseconds)
        {
            try
            {
                return m_task.Wait(milliseconds);
            }
            catch (AggregateException)
            {
                // A faulted job has ended; GetOutcome reports its exception.
                return true;
            }
        }

        /// <summary>
        /// The ended job's answer. A check stopped by Cancel is marked cancelled, as the progress bar's Cancel marks it;
        /// a worker exception becomes a Not checked answer that names it.
        /// </summary>
        internal LevelValidationOutcome GetOutcome(out Exception error)
        {
            if (!m_task.IsCompleted)
            {
                throw new InvalidOperationException("The validation job has not ended.");
            }

            error = null;
            if (m_task.IsFaulted || m_task.IsCanceled)
            {
                error = m_task.Exception?.GetBaseException() ?? new OperationCanceledException();
                return new LevelValidationOutcome(LevelCheckStatus.NotChecked,
                    "Not checked: the check failed (" + error.Message + ").", false);
            }

            LevelCheckResult result = m_task.Result;
            bool cancelled = !m_listingOnly && m_checkProgress.IsCancelRequested
                             && result.Status == LevelCheckStatus.NotChecked;
            return new LevelValidationOutcome(result, cancelled);
        }

        /// <summary>
        /// Carries one phase's progress from the worker to the main thread. Report runs on the worker about ten times a
        /// second; the main thread reads the last values and sets the cancel request.
        /// </summary>
        private sealed class WorkerProgress : ILevelCheckProgress
        {
            private int m_positions;
            private int m_reported;
            private volatile bool m_cancelRequested;

            public bool IsCancelRequested => m_cancelRequested;

            internal bool HasReported => Volatile.Read(ref m_reported) != 0;

            internal int Positions => Volatile.Read(ref m_positions);

            public void Report(int positions, double elapsedSeconds)
            {
                Volatile.Write(ref m_positions, positions);
                Volatile.Write(ref m_reported, 1);
            }

            internal void RequestCancel()
            {
                m_cancelRequested = true;
            }
        }
    }
}
