using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Plays one listed solution as whole-cell frames (the start board, then the board after each one-cell step and
    /// after each exit, each for its own time) and pauses, resumes, steps forward or back and stops it on its owner's
    /// call. It never changes a level; its owner, the Level Editor window, shows each frame and brings the draft back
    /// when the playback ends or stops.
    /// </summary>
    internal sealed class LevelSolutionPlayback
    {
        // The playback's pace (contract Revision 1 of TASK-20260928-091128541-cb48c84a, which slowed the first 0.1, 0.1,
        // 0.2 and 0.6 s on the user's order). These four constants are the only place the pace lives.
        /// <summary>How long the start board shows before the first step.</summary>
        internal const double k_startSeconds = 0.4;
        /// <summary>How long the board shows after a one-cell step.</summary>
        internal const double k_stepSeconds = 0.25;
        /// <summary>How long the board shows after an exit.</summary>
        internal const double k_exitSeconds = 0.4;
        /// <summary>How long the last board, a cleared one for a full solution, shows before the draft comes back.</summary>
        internal const double k_clearedSeconds = 1.0;

        private readonly Action<LevelData> m_showFrame;
        private readonly Action m_finished;
        private readonly EditorApplication.CallbackFunction m_tick;
        private LevelData[] m_frames;
        private double[] m_durations;
        private int m_frame = -1;
        private double m_frameEnd;

        /// <summary>
        /// showFrame shows one frame; finished runs once when the playback ends by itself, after the last frame's time or
        /// a Step on the last frame, and never after Stop.
        /// </summary>
        internal LevelSolutionPlayback(Action<LevelData> showFrame, Action finished)
        {
            m_showFrame = showFrame ?? throw new ArgumentNullException(nameof(showFrame));
            m_finished = finished ?? throw new ArgumentNullException(nameof(finished));
            m_tick = Tick;
        }

        /// <summary>The time in seconds. Tests replace it to step frames without waiting.</summary>
        internal Func<double> Clock { get; set; } = () => EditorApplication.timeSinceStartup;

        /// <summary>Subscribes the per-update callback while playing. Tests replace it and call Tick themselves.</summary>
        internal Action<EditorApplication.CallbackFunction> AddUpdate { get; set; } =
            callback => EditorApplication.update += callback;

        /// <summary>Unsubscribes the per-update callback. Tests replace it with AddUpdate.</summary>
        internal Action<EditorApplication.CallbackFunction> RemoveUpdate { get; set; } =
            callback => EditorApplication.update -= callback;

        /// <summary>True from Play to the stop or the end, playing or paused: a frame shows instead of the draft.</summary>
        internal bool IsActive => m_frames != null;

        /// <summary>True while an active playback waits on its shown frame for Step or Resume, with no update callback.</summary>
        internal bool IsPaused { get; private set; }

        /// <summary>True while an active playback moves on by itself; the per-update callback is subscribed exactly then.</summary>
        internal bool IsPlaying => IsActive && !IsPaused;

        /// <summary>The listed entry that plays or waits, or -1.</summary>
        internal int EntryIndex { get; private set; } = -1;

        /// <summary>The shown frame: 0 is the start board, i the board after step i; -1 when nothing is active.</summary>
        internal int FrameIndex => m_frame;

        /// <summary>The start board plus one frame per step; 0 when nothing is active.</summary>
        internal int FrameCount => m_frames?.Length ?? 0;

        /// <summary>How long a frame shows: the start board, then each step's frame by its kind, the last one held.</summary>
        internal static double GetFrameSeconds(IReadOnlyList<LevelSolutionStep> steps, int frame)
        {
            if (frame >= steps.Count)
            {
                return k_clearedSeconds;
            }

            if (frame == 0)
            {
                return k_startSeconds;
            }

            return steps[frame - 1].IsExit ? k_exitSeconds : k_stepSeconds;
        }

        /// <summary>
        /// Stops any playback and shows the entry's start board, built from the level the steps were listed for, at once.
        /// Unpaused, the next frames follow on the Editor update; paused (Auto Play off), the start board waits for Step
        /// or Resume.
        /// </summary>
        internal void Play(int entryIndex, LevelData level, IReadOnlyList<LevelSolutionStep> steps, bool paused = false)
        {
            Stop();
            IReadOnlyList<LevelData> after = LevelSolutionFrames.Create(level, steps);
            m_frames = new LevelData[after.Count + 1];
            m_durations = new double[m_frames.Length];
            m_frames[0] = level;
            for (int frame = 0; frame < m_frames.Length; frame++)
            {
                if (frame > 0)
                {
                    m_frames[frame] = after[frame - 1];
                }

                m_durations[frame] = GetFrameSeconds(steps, frame);
            }

            EntryIndex = entryIndex;
            m_frame = 0;
            IsPaused = paused;
            if (!paused)
            {
                StartFrameTime();
            }

            m_showFrame(m_frames[0]);
        }

        /// <summary>Freezes the shown frame and unsubscribes the update callback; false when nothing plays.</summary>
        internal bool Pause()
        {
            if (!IsPlaying)
            {
                return false;
            }

            RemoveUpdate(m_tick);
            IsPaused = true;
            return true;
        }

        /// <summary>
        /// Continues a paused playback: the next frame shows after the shown frame's full time. False when nothing waits.
        /// </summary>
        internal bool Resume()
        {
            if (!IsActive || !IsPaused)
            {
                return false;
            }

            IsPaused = false;
            StartFrameTime();
            return true;
        }

        /// <summary>
        /// Shows exactly the next frame and stays paused, pausing first when playing; a Step on the last frame, the
        /// cleared board of a full solution, ends the playback as its time would. False when nothing is active.
        /// </summary>
        internal bool Step()
        {
            if (!IsActive)
            {
                return false;
            }

            if (!StepForward())
            {
                // The last frame: the Step control ends the playback there, as the frame's time would.
                Stop();
                m_finished();
            }

            return true;
        }

        /// <summary>
        /// Shows the next frame and stays paused, pausing first when playing (contract Revision 2, the → key). On the last
        /// frame, the cleared board of a full solution, it only pauses: unlike Step it never ends the playback, so a held
        /// key stops there. It never subscribes the update callback. True when it showed a frame.
        /// </summary>
        internal bool StepForward()
        {
            if (!IsActive)
            {
                return false;
            }

            Pause();
            if (m_frame >= m_frames.Length - 1)
            {
                return false;
            }

            m_frame++;
            m_showFrame(m_frames[m_frame]);
            return true;
        }

        /// <summary>
        /// Shows the previous frame and stays paused, pausing first when playing (contract Revision 2, the ← key). On the
        /// start board nothing happens, playing or paused. It never subscribes the update callback. False when nothing
        /// was shown again.
        /// </summary>
        internal bool StepBack()
        {
            if (!IsActive || m_frame == 0)
            {
                return false;
            }

            Pause();
            m_frame--;
            m_showFrame(m_frames[m_frame]);
            return true;
        }

        /// <summary>Stops at once, playing or paused, with no update callback left; true when something was active.</summary>
        internal bool Stop()
        {
            if (!IsActive)
            {
                return false;
            }

            // A paused playback has already unsubscribed its update callback.
            if (!IsPaused)
            {
                RemoveUpdate(m_tick);
            }

            m_frames = null;
            m_durations = null;
            m_frame = -1;
            EntryIndex = -1;
            IsPaused = false;
            return true;
        }

        /// <summary>
        /// The per-update step: shows the frame whose time has come, skipping any a slow update passed, and ends after the
        /// last frame's time. Each frame's end follows the previous one's, so a late update does not slow the pace.
        /// </summary>
        internal void Tick()
        {
            if (!IsPlaying)
            {
                return;
            }

            double now = Clock();
            int frame = m_frame;
            while (now >= m_frameEnd)
            {
                frame++;
                if (frame >= m_frames.Length)
                {
                    Stop();
                    m_finished();
                    return;
                }

                m_frameEnd += m_durations[frame];
            }

            if (frame != m_frame)
            {
                m_frame = frame;
                m_showFrame(m_frames[frame]);
            }
        }

        // The shown frame's full time starts now, and the update callback shows the next frame when it ends.
        private void StartFrameTime()
        {
            m_frameEnd = Clock() + m_durations[m_frame];
            // Owner: this playback, which the Level Editor window owns. Stop points: Pause, Stop (which every window stop
            // point calls) and Tick after the last frame's time.
            AddUpdate(m_tick);
        }
    }
}
