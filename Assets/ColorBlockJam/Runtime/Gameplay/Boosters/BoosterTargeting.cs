using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The targeting mode of the booster bar (B-12): none, Rocket, Hammer or Vacuum, and where a tapped target goes.
    /// The HUD selects, toggles and cancels it; DragInputRouter reads it to route presses away from dragging and
    /// hands the chosen cell to the attached use coordinator. It holds no board state, so selecting, switching and
    /// cancelling are free.
    /// </summary>
    /// <remarks>
    /// The coordinator attaches itself instead of being a router constructor argument: the router is a dependency
    /// of LevelSession, and the coordinator depends on LevelSession, so a constructor edge would be a cycle.
    /// Owner: the Game scene container (AsSingle); Dispose ends the Mode property with the scene.
    /// </remarks>
    public sealed class BoosterTargeting : IDisposable
    {
        private readonly ReactiveProperty<BoosterId> m_mode = new ReactiveProperty<BoosterId>(BoosterId.None);

        private IBoosterTargetSink m_sink;
        private bool m_disposed;

        /// <summary>The booster waiting for a target, or None; it changes only on Select, Toggle and Cancel.</summary>
        public IReadOnlyReactiveProperty<BoosterId> Mode => m_mode;

        public bool IsActive => m_mode.Value != BoosterId.None;

        /// <summary>True for the boosters that wait for a target: Rocket, Hammer and Vacuum.</summary>
        public static bool IsTargeted(BoosterId id)
        {
            return id == BoosterId.Rocket || id == BoosterId.Hammer || id == BoosterId.Vacuum;
        }

        /// <summary>Starts targeting for id, switching from any other mode for free.</summary>
        public void Select(BoosterId id)
        {
            if (!IsTargeted(id))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id + " takes no target.");
            }

            if (!m_disposed)
            {
                m_mode.SetValue(id);
            }
        }

        /// <summary>The same button cancels its own mode; any other targeted button switches to it. True when id is now selected.</summary>
        public bool Toggle(BoosterId id)
        {
            if (m_mode.Value == id)
            {
                Cancel();
                return false;
            }

            Select(id);
            return IsActive;
        }

        /// <summary>Ends targeting; does nothing when no mode is set or after disposal.</summary>
        public void Cancel()
        {
            if (!m_disposed)
            {
                m_mode.SetValue(BoosterId.None);
            }
        }

        /// <summary>Connects the one use coordinator of the scene; a second, different sink is a wiring error.</summary>
        public void Attach(IBoosterTargetSink sink)
        {
            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (m_sink != null && !ReferenceEquals(m_sink, sink))
            {
                throw new InvalidOperationException("Booster targeting already has a target sink.");
            }

            m_sink = sink;
        }

        /// <summary>Disconnects sink when it is the attached one.</summary>
        public void Detach(IBoosterTargetSink sink)
        {
            if (ReferenceEquals(m_sink, sink))
            {
                m_sink = null;
            }
        }

        /// <summary>
        /// Hands the tapped cell to the attached sink for the current mode. Without a mode or a sink nothing can
        /// take the target, so the result is Empty and the router cancels.
        /// </summary>
        public BoosterTargetResult TrySubmit(LevelAttempt attempt, Cell cell)
        {
            BoosterId mode = m_mode.Value;
            if (mode == BoosterId.None || m_sink == null)
            {
                return BoosterTargetResult.Empty;
            }

            return m_sink.TrySubmitTarget(mode, attempt, cell);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_sink = null;
            m_mode.Dispose();
        }
    }
}
