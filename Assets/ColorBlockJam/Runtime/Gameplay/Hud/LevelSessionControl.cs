using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Session;
using Onity.DI;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Hud
{
    /// <summary>
    /// Adapts the Game scene's <see cref="LevelSession"/> and its booster use coordinator to
    /// <see cref="IGameSessionControl"/>: requests go straight to the session, and the pending write is the
    /// coordinator's reactive busy flag (Game HUD design 4).
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). It holds no subscription and starts no work.
    /// </remarks>
    public sealed class LevelSessionControl : IGameSessionControl
    {
        private readonly LevelSession m_session;
        private readonly IReadOnlyReactiveProperty<bool> m_isWritePending;

        /// <summary>The Game scene's control: the coordinator's busy flag is the pending write.</summary>
        [Inject]
        public LevelSessionControl(LevelSession session, BoosterUseCoordinator coordinator)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            if (coordinator == null)
            {
                throw new ArgumentNullException(nameof(coordinator));
            }

            m_isWritePending = coordinator.IsBusy;
        }

        public IReadOnlyReactiveProperty<SessionState> State => m_session.State;

        public PauseReason PauseReason => m_session.PauseReason;

        public IReadOnlyReactiveProperty<bool> IsOpening => m_session.IsOpening;

        public IReadOnlyReactiveProperty<bool> IsWritePending => m_isWritePending;

        public IReadOnlyReactiveProperty<bool> IsFinishing => m_session.IsFinishing;

        public IReadOnlyReactiveProperty<bool> IsTimerStarted => m_session.IsTimerStarted;

        public bool Pause()
        {
            return m_session.Pause(PauseReason.User);
        }

        public bool Resume()
        {
            return m_session.Resume();
        }

        public bool ResumeFromBackground()
        {
            return m_session.ResumeFromBackground();
        }

        public bool Restart()
        {
            return m_session.Restart();
        }

        public bool GoHome()
        {
            return m_session.GoHome();
        }
    }
}
