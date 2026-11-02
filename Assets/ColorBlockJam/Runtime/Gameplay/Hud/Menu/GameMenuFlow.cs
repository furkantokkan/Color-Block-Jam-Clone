using System;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Player.Lives;
using Onity.DI;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Decides which in-game overlay shows (none, the pause menu or the Retry confirmation) and what the session does
    /// for each press: Pause, Retry, Close, the scrim, Back, Retry confirm and HOME, plus backgrounding (Game HUD design
    /// 4-5, AC-5 to AC-10). One overlay at a time; the session state stays the only source of truth for play.
    /// </summary>
    /// <remarks>
    /// Rules: Pause (or Back during Playing without targeting) pauses Playing into Paused(User) and then shows the menu;
    /// in Won it shows the menu without a session call, so HOME stays reachable; in Failed the fail popup is the one
    /// overlay and its close is the way home; it does nothing while a booster write is pending. Retry pauses Playing (outside the opening hold and without a pending write) and shows
    /// the confirmation; in Failed it shows it directly; in Won it does nothing. Entering Failed (the timer ran out)
    /// shows the same confirmation at once as the fail popup. The top bar's enabled matrix follows these same rules.
    /// Close, the scrim and overlay Back resume Paused (the overlay stays while Resume is refused), exit to Hub from the
    /// confirmation in Failed, and hide otherwise in Won or Failed. The confirmation's Retry is locked on its press and
    /// restarts once. HOME and the fail popup's close exit once through the navigator; the overlay stays up while the
    /// exit runs. Backgrounding only suspends play and opens nothing; the foreground return resumes the session unless
    /// the player's pause menu or confirmation is up (DEC-182). Owner: the Game
    /// scene container (AsSingle, HUD branch), registered after the session so it is disposed first. Its
    /// subscriptions live in one CompositeDisposable and its host and Back handlers are removed in
    /// <see cref="Dispose"/>; nothing runs per frame.
    /// Lives (DEC-174): entering Failed, the in-level Retry's confirm and HOME from a level in progress each charge the
    /// attempt's one life through <see cref="AttemptLifeCost"/>; the overlay's actions stay off while it saves, and
    /// with no life left Retry leaves for Hub (which offers the refill) instead of restarting. HOME after Won is free.
    /// HOME from a level in progress first shows the Retry confirmation reading Leave, whose Close returns to the pause
    /// menu; before the first block press (the countdown has not started) HOME leaves at once, for free (DEC-189).
    /// </remarks>
    public sealed class GameMenuFlow : IGameMenu, IGameplayBackFallback, IApplicationPauseListener, IDisposable
    {
        private readonly IGameSessionControl m_session;
        private readonly IPauseMenu m_pauseMenu;
        private readonly IRestartConfirm m_restartConfirm;
        private readonly IOverlayBackInput m_back;
        private readonly IGameExitNavigator m_exit;
        private readonly AttemptLevelNumber m_levelNumber;
        private readonly AttemptLifeCost m_lifeCost;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Action m_onCloseRequested;
        private readonly Action m_onHomeRequested;
        private readonly Action m_onConfirmRequested;

        private GameOverlay m_current = GameOverlay.None;
        private bool m_homeRequested;
        private bool m_disposed;

        /// <summary>A flow without lives (test harnesses): no attempt costs a life.</summary>
        public GameMenuFlow(IGameSessionControl session, IPauseMenu pauseMenu, IRestartConfirm restartConfirm,
            IOverlayBackInput back, IGameExitNavigator exit, AttemptLevelNumber levelNumber)
            : this(session, pauseMenu, restartConfirm, back, exit, levelNumber, null)
        {
        }

        /// <param name="lives">The project's lives (DEC-174); null only in test harnesses without lives.</param>
        [Inject]
        public GameMenuFlow(IGameSessionControl session, IPauseMenu pauseMenu, IRestartConfirm restartConfirm,
            IOverlayBackInput back, IGameExitNavigator exit, AttemptLevelNumber levelNumber, ILives lives)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_pauseMenu = pauseMenu ?? throw new ArgumentNullException(nameof(pauseMenu));
            m_restartConfirm = restartConfirm ?? throw new ArgumentNullException(nameof(restartConfirm));
            m_back = back ?? throw new ArgumentNullException(nameof(back));
            m_exit = exit ?? throw new ArgumentNullException(nameof(exit));
            m_levelNumber = levelNumber ?? throw new ArgumentNullException(nameof(levelNumber));
            m_lifeCost = lives != null ? new AttemptLifeCost(lives) : null;

            m_onCloseRequested = RequestClose;
            m_onHomeRequested = RequestHome;
            m_onConfirmRequested = ConfirmRestart;
            m_pauseMenu.CloseRequested += m_onCloseRequested;
            m_pauseMenu.HomeRequested += m_onHomeRequested;
            m_restartConfirm.CloseRequested += m_onCloseRequested;
            m_restartConfirm.ConfirmRequested += m_onConfirmRequested;
            m_back.Pressed += m_onCloseRequested;

            // Stop point: Dispose. Each emits its current value at once.
            m_subscriptions.Add(m_session.State.Subscribe(OnStateChanged));
            m_subscriptions.Add(m_session.IsWritePending.Subscribe(OnWritePendingChanged));
            if (m_lifeCost != null)
            {
                m_subscriptions.Add(m_lifeCost.IsCharging.Subscribe(OnWritePendingChanged));
            }
        }

        /// <summary>The overlay shown now.</summary>
        public GameOverlay Current => m_current;

        public void RequestPause()
        {
            if (m_disposed || m_current != GameOverlay.None || m_session.IsWritePending.Value)
            {
                return;
            }

            switch (m_session.State.Value)
            {
                case SessionState.Playing:
                    if (m_session.Pause() && !Show(GameOverlay.PauseMenu))
                    {
                        // No menu to show: never leave the session paused without a way back.
                        m_session.Resume();
                    }

                    break;
                case SessionState.Won:
                    Show(GameOverlay.PauseMenu);
                    break;
            }
        }

        public void RequestRestart()
        {
            if (m_disposed || m_current != GameOverlay.None)
            {
                return;
            }

            switch (m_session.State.Value)
            {
                case SessionState.Playing:
                    // A restart in the opening hold would discard starting boosters already debited (design 1).
                    if (m_session.IsOpening.Value || m_session.IsWritePending.Value)
                    {
                        return;
                    }

                    if (m_session.Pause() && !Show(GameOverlay.RestartConfirm))
                    {
                        m_session.Resume();
                    }

                    break;
                case SessionState.Failed:
                    Show(GameOverlay.RestartConfirm);
                    break;
            }
        }

        /// <summary>Back during Playing without targeting (the router's fallback): the Pause button's rule.</summary>
        public bool OnBack()
        {
            if (m_disposed)
            {
                return false;
            }

            RequestPause();
            return m_session.State.Value != SessionState.Playing;
        }

        public void OnApplicationPauseChanged(bool paused)
        {
            // Backgrounding only suspends play; no menu opens (DEC-182). On the return the session resumes, unless the
            // player's own pause menu or Retry confirmation is up: its Close resumes, as before. A zero-stock purchase
            // popup hides with the resume (BoosterUseCoordinator), and a pending booster write delays it (B-13).
            if (!paused && !m_disposed && m_current == GameOverlay.None)
            {
                m_session.ResumeFromBackground();
            }
        }

        /// <summary>The attempt's life loss while it is being saved, or a completed task. Tests await it.</summary>
        public Task PendingLifeCharge => m_lifeCost != null ? m_lifeCost.PendingCharge : Task.CompletedTask;

        /// <summary>The Retry confirm or HOME that waits for the life charge, or a completed task. Tests await it.</summary>
        public Task PendingAction { get; private set; } = Task.CompletedTask;

        /// <summary>Close, the scrim or overlay Back.</summary>
        public void RequestClose()
        {
            // While the attempt's life is saving, the overlay's actions are off (DEC-174).
            if (m_disposed || m_current == GameOverlay.None || m_exit.IsExiting || IsChargingLife)
            {
                return;
            }

            // Closing the Leave confirmation goes back to the pause menu it came from; the session stays Paused.
            if (m_current == GameOverlay.LeaveConfirm)
            {
                SwitchTo(GameOverlay.PauseMenu);
                return;
            }

            switch (m_session.State.Value)
            {
                case SessionState.Paused:
                    // Refused while a booster write is pending: the overlay stays with its actions disabled.
                    if (m_session.Resume())
                    {
                        Hide();
                    }

                    break;
                case SessionState.Home:
                    // Only after a failed exit: HOME is the way out, so the menu stays.
                    break;
                case SessionState.Failed when m_current == GameOverlay.RestartConfirm:
                    // The fail popup's X leaves for Hub (Failed leaves to Restart or Home); it stays up during the exit.
                    TryExitHome();
                    break;
                default:
                    Hide();
                    break;
            }
        }

        /// <summary>
        /// The confirmation's green button: Retry restarts once from the authored level (no refund, no refill); Leave
        /// charges the attempt's life and exits to Hub once (DEC-189).
        /// </summary>
        public void ConfirmRestart()
        {
            if (m_disposed)
            {
                return;
            }

            if (m_current == GameOverlay.LeaveConfirm)
            {
                ConfirmLeave();
                return;
            }

            if (m_current != GameOverlay.RestartConfirm)
            {
                return;
            }

            // One press, one Restart (design 4): Retry stays off until the next Show, or comes back when refused.
            m_restartConfirm.SetConfirmEnabled(false);
            if (m_lifeCost == null)
            {
                RestartOrReenable();
                return;
            }

            // The in-level Retry costs this attempt's life; on the fail popup it was charged when the attempt failed,
            // so the charge returns the same task and takes nothing more (DEC-174).
            PendingAction = RestartAfterChargeAsync();
        }

        /// <summary>The pause menu's HOME.</summary>
        public void RequestHome()
        {
            // HOME is off while a booster write or the attempt's life is saving (AC-9, DEC-174).
            if (m_disposed || m_current != GameOverlay.PauseMenu || m_session.IsWritePending.Value || IsChargingLife)
            {
                return;
            }

            // Leaving a level in progress costs its life (DEC-174), so HOME first asks on the Retry confirmation reading
            // Leave. A level whose countdown never started (no block touched) and a won level leave for free (DEC-189).
            if (m_session.State.Value == SessionState.Paused && m_session.IsTimerStarted.Value)
            {
                SwitchTo(GameOverlay.LeaveConfirm);
                return;
            }

            TryExitHome();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_subscriptions.Dispose();
            m_lifeCost?.Dispose();
            m_pauseMenu.CloseRequested -= m_onCloseRequested;
            m_pauseMenu.HomeRequested -= m_onHomeRequested;
            m_restartConfirm.CloseRequested -= m_onCloseRequested;
            m_restartConfirm.ConfirmRequested -= m_onConfirmRequested;
            m_back.Pressed -= m_onCloseRequested;
            m_back.Disable();
        }

        private void OnStateChanged(SessionState state)
        {
            switch (state)
            {
                case SessionState.Playing:
                    Hide();
                    break;
                case SessionState.Home:
                    if (!m_homeRequested)
                    {
                        Hide();
                    }

                    break;
                case SessionState.Failed:
                    // The failed attempt costs its life (DEC-174); the popup's actions stay off until it is saved.
                    // The charge task is kept by the life cost, never fire-and-forget.
                    m_lifeCost?.ChargeAsync();

                    // The fail popup: the timer ran out, and the session already tore the attempt down (RULE-19), so
                    // it opens at once. Failed is entered only from Playing, where no overlay shows.
                    // Without the popup (an authoring error, logged by the host) the menu keeps HOME reachable.
                    if (m_current == GameOverlay.None && !Show(GameOverlay.RestartConfirm))
                    {
                        Show(GameOverlay.PauseMenu);
                    }

                    break;
            }
        }

        // One Game-to-Hub exit for HOME and the fail popup's X. Set before the call: the session raises Home inside it,
        // and the overlay must stay up during the exit.
        private void TryExitHome()
        {
            m_homeRequested = true;
            if (!m_exit.TryGoHome())
            {
                m_homeRequested = m_exit.IsExiting;
            }
        }

        private bool IsChargingLife => m_lifeCost != null && m_lifeCost.IsCharging.Value;

        // A pending booster write or a life being saved turns the overlay's actions off; either change re-evaluates both.
        private void OnWritePendingChanged(bool _)
        {
            bool enabled = !m_session.IsWritePending.Value && !IsChargingLife;
            m_pauseMenu.SetActionsEnabled(enabled);
            m_restartConfirm.SetActionsEnabled(enabled);
        }

        private void RestartOrReenable()
        {
            if (m_session.Restart())
            {
                m_lifeCost?.StartNewAttempt();
                Hide();
            }
            else
            {
                m_restartConfirm.SetConfirmEnabled(true);
            }
        }

        // Owner: this flow, which keeps the task in PendingAction. Stops: after one Restart or exit, or when the flow is
        // disposed or the confirmation closed while the charge was saving (then nothing is touched).
        private async Task RestartAfterChargeAsync()
        {
            await m_lifeCost.ChargeAsync();
            if (m_disposed || m_current != GameOverlay.RestartConfirm)
            {
                return;
            }

            if (m_lifeCost.HasLife)
            {
                RestartOrReenable();
                return;
            }

            // No life left: back to the lobby, which opens the refill popup at 0 lives (DEC-174). The popup stays up
            // during the exit; when the exit is refused, Retry comes back.
            TryExitHome();
            if (!m_exit.IsExiting)
            {
                m_restartConfirm.SetConfirmEnabled(true);
            }
        }

        private void ConfirmLeave()
        {
            // One press, one exit: Leave stays off until the next show, or comes back when the exit is refused.
            m_restartConfirm.SetConfirmEnabled(false);
            if (m_lifeCost == null)
            {
                ExitOrReenable();
                return;
            }

            PendingAction = ExitHomeAfterChargeAsync();
        }

        private void ExitOrReenable()
        {
            TryExitHome();
            if (!m_exit.IsExiting)
            {
                m_restartConfirm.SetConfirmEnabled(true);
            }
        }

        // Owner: this flow, which keeps the task in PendingAction. Stops: after one exit, or when the flow is disposed or
        // the Leave confirmation closed while the charge was saving.
        private async Task ExitHomeAfterChargeAsync()
        {
            await m_lifeCost.ChargeAsync();
            if (m_disposed || m_current != GameOverlay.LeaveConfirm)
            {
                return;
            }

            ExitOrReenable();
        }

        // Replaces the shown overlay without a session call; if the new one cannot show, the pause menu keeps HOME and
        // Close reachable.
        private void SwitchTo(GameOverlay overlay)
        {
            Hide();
            if (!Show(overlay) && overlay != GameOverlay.PauseMenu)
            {
                Show(GameOverlay.PauseMenu);
            }
        }

        private bool Show(GameOverlay overlay)
        {
            bool shown = overlay == GameOverlay.PauseMenu
                ? m_pauseMenu.Show()
                : m_restartConfirm.Show(m_levelNumber.Title,
                    overlay == GameOverlay.LeaveConfirm ? LifeConfirmAction.Leave : LifeConfirmAction.Retry);
            if (!shown)
            {
                return false;
            }

            m_current = overlay;
            m_back.Enable();
            return true;
        }

        private void Hide()
        {
            if (m_current == GameOverlay.None)
            {
                return;
            }

            m_back.Disable();
            if (m_current == GameOverlay.PauseMenu)
            {
                m_pauseMenu.Hide();
            }
            else
            {
                m_restartConfirm.Hide();
            }

            m_current = GameOverlay.None;
        }
    }
}
