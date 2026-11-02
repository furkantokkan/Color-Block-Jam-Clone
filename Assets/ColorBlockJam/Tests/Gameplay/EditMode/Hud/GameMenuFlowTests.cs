using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The in-game menu flow's rules (Game HUD design 4-5; contract AC-5 to AC-10, AC-13) with a fake session and fake
    /// popups: Pause and Retry pause first and then show one overlay, Close/scrim/Back resume and hide only when the
    /// resume is accepted, the confirmation restarts once, HOME exits once through the real exit navigator over a fake
    /// Hub transition, backgrounding opens nothing and the return resumes unless the player's overlay is up (DEC-182),
    /// Won/Failed show it without a session call, and Dispose stops every
    /// handler and subscription.
    /// </summary>
    public sealed class GameMenuFlowTests
    {
        private FakeSessionControl m_session;
        private FakePauseMenu m_menu;
        private FakeRestartConfirm m_confirm;
        private FakeOverlayBackInput m_back;
        private FakeHubTransition m_transition;
        private GameExitNavigator m_exit;
        private AttemptLevelNumber m_levelNumber;
        private GameMenuFlow m_flow;

        public enum CloseSource
        {
            CloseButtonOrScrim,
            OverlayBack
        }

        // NUnit reuses the fixture instance, so every collaborator is rebuilt here.
        [SetUp]
        public void SetUp()
        {
            m_session = new FakeSessionControl();
            m_menu = new FakePauseMenu(m_session);
            m_confirm = new FakeRestartConfirm(m_session);
            m_back = new FakeOverlayBackInput();
            m_transition = new FakeHubTransition();
            m_exit = new GameExitNavigator(m_session, m_transition);
            m_levelNumber = new AttemptLevelNumber();
            m_levelNumber.Set(29);
            m_flow = new GameMenuFlow(m_session, m_menu, m_confirm, m_back, m_exit, m_levelNumber);
        }

        [TearDown]
        public void TearDown()
        {
            m_flow.Dispose();
            m_exit.Dispose();
        }

        [Test]
        public void PauseInPlayingPausesForThePlayerAndThenShowsTheMenu()
        {
            m_flow.RequestPause();

            Assert.That(m_session.PauseCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_menu.ShowCount, Is.EqualTo(1));
            Assert.That(m_menu.StateAtShow, Is.EqualTo(SessionState.Paused), "the menu opens only after Suspend");
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(m_back.IsEnabled, Is.True, "overlay Back listens while the menu shows");
            Assert.That(m_confirm.ShowCount, Is.Zero);
        }

        [Test]
        public void PauseInTheOpeningHoldIsStillAccepted()
        {
            m_session.SetOpening(true);

            m_flow.RequestPause();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
        }

        [Test]
        public void ARefusedPauseShowsNothing()
        {
            m_session.AcceptsPause = false;

            m_flow.RequestPause();

            Assert.That(m_session.PauseCalls, Is.EqualTo(1));
            Assert.That(m_menu.ShowCount, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_back.IsEnabled, Is.False);
        }

        [Test]
        public void PauseWithoutAMenuToShowResumesTheSessionAgain()
        {
            m_menu.CanShow = false;

            m_flow.RequestPause();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "never paused without a way back");
            Assert.That(m_session.ResumeCalls, Is.EqualTo(1));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [TestCase(SessionState.Home)]
        [TestCase(SessionState.Paused)]
        public void PauseOutsidePlayingWonAndFailedDoesNothing(SessionState state)
        {
            m_session.SetState(state, PauseReason.Purchase);

            m_flow.RequestPause();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_menu.ShowCount, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void PauseAndRetryWhileAnOverlayShowsDoNothingMore()
        {
            m_flow.RequestPause();

            m_flow.RequestPause();
            m_flow.RequestRestart();

            Assert.That(m_session.PauseCalls, Is.EqualTo(1));
            Assert.That(m_menu.ShowCount, Is.EqualTo(1));
            Assert.That(m_confirm.ShowCount, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
        }

        [Test]
        public void PauseInWonShowsTheMenuWithoutASessionCall()
        {
            const SessionState state = SessionState.Won;
            m_session.SetState(state);

            m_flow.RequestPause();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_menu.ShowCount, Is.EqualTo(1));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));

            m_menu.RaiseClose();

            Assert.That(m_session.ResumeCalls, Is.Zero, "closing only hides it");
            Assert.That(m_menu.IsShowing, Is.False);
            Assert.That(m_session.State.Value, Is.EqualTo(state));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void OverlayBackInWonHidesTheMenu()
        {
            m_session.SetState(SessionState.Won);
            m_flow.RequestPause();

            m_back.Press();

            Assert.That(m_menu.IsShowing, Is.False);
            Assert.That(m_back.IsEnabled, Is.False);
            Assert.That(m_session.ResumeCalls, Is.Zero);
        }

        [Test]
        public void RetryInPlayingPausesThenShowsTheConfirmationTitledWithTheLevel()
        {
            m_flow.RequestRestart();

            Assert.That(m_session.PauseCalls, Is.EqualTo(1));
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_confirm.ShowCount, Is.EqualTo(1));
            Assert.That(m_confirm.StateAtShow, Is.EqualTo(SessionState.Paused));
            Assert.That(m_confirm.LastTitle, Is.EqualTo("Level 29"));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
            Assert.That(m_menu.ShowCount, Is.Zero);
            Assert.That(m_session.RestartCalls, Is.Zero, "nothing restarts before the confirmation");
            Assert.That(m_back.IsEnabled, Is.True);
        }

        [Test]
        public void RetryWithoutAConfirmationToShowResumesTheSessionAgain()
        {
            m_confirm.CanShow = false;

            m_flow.RequestRestart();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void RetryInTheOpeningHoldIsIgnored()
        {
            m_session.SetOpening(true);

            m_flow.RequestRestart();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_confirm.ShowCount, Is.Zero);
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void RetryWhileABoosterWriteIsPendingIsIgnored()
        {
            m_session.SetWritePending(true);

            m_flow.RequestRestart();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_confirm.ShowCount, Is.Zero);
        }

        [TestCase(SessionState.Won)]
        [TestCase(SessionState.Home)]
        [TestCase(SessionState.Paused)]
        public void RetryOutsidePlayingAndFailedIsIgnored(SessionState state)
        {
            m_session.SetState(state, PauseReason.User);

            m_flow.RequestRestart();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_confirm.ShowCount, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void EnteringFailedShowsTheFailPopupTitledWithTheLevelAndItsRetryRestarts()
        {
            m_session.SetState(SessionState.Failed);

            Assert.That(m_session.PauseCalls, Is.Zero, "Failed is not paused");
            Assert.That(m_confirm.ShowCount, Is.EqualTo(1));
            Assert.That(m_confirm.LastTitle, Is.EqualTo("Level 29"));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
            Assert.That(m_back.IsEnabled, Is.True);

            m_flow.RequestPause();
            m_flow.RequestRestart();

            Assert.That(m_menu.ShowCount, Is.Zero, "the top bar does nothing more while the fail popup shows");
            Assert.That(m_confirm.ShowCount, Is.EqualTo(1));

            m_confirm.RaiseConfirm();

            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_confirm.IsShowing, Is.False);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void EnteringFailedWithoutAFailPopupToShowShowsTheMenuSoHomeStaysReachable()
        {
            m_confirm.CanShow = false;

            m_session.SetState(SessionState.Failed);

            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(m_menu.IsShowing, Is.True);
            Assert.That(m_session.PauseCalls, Is.Zero, "Failed is not paused");
        }

        [Test]
        public void PauseInFailedDoesNothingMore()
        {
            m_confirm.CanShow = false;
            m_session.SetState(SessionState.Failed);
            m_menu.RaiseClose();

            m_flow.RequestPause();

            Assert.That(m_menu.ShowCount, Is.EqualTo(1), "only the fallback show");
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [TestCase(CloseSource.CloseButtonOrScrim)]
        [TestCase(CloseSource.OverlayBack)]
        public void ClosingTheFailPopupExitsToHubOnceWhileItStaysUp(CloseSource source)
        {
            m_session.SetState(SessionState.Failed);

            Close(source, onConfirmation: true);
            Close(source, onConfirmation: true);

            Assert.That(m_session.RestartCalls, Is.Zero);
            Assert.That(m_session.ResumeCalls, Is.Zero);
            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_transition.Calls, Is.EqualTo(1), "a second close does nothing");
            Assert.That(m_confirm.IsShowing, Is.True, "the popup covers the torn-down board while Hub loads");
        }

        [Test]
        public void ConfirmRestartsExactlyOnceAndHides()
        {
            m_flow.RequestRestart();

            m_confirm.RaiseConfirm();
            m_confirm.RaiseConfirm();

            Assert.That(m_session.RestartCalls, Is.EqualTo(1), "a second press after the hide does nothing");
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_confirm.IsShowing, Is.False);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_back.IsEnabled, Is.False);
            Assert.That(m_session.ResumeCalls, Is.Zero, "a restart is not a resume");
        }

        [Test]
        public void ConfirmLocksRetryBeforeTheRestartAndTheNextShowUnlocksIt()
        {
            bool enabledAtRestart = true;
            m_session.BeforeRestart = () => enabledAtRestart = m_confirm.ConfirmEnabled;
            m_flow.RequestRestart();
            Assert.That(m_confirm.ConfirmEnabled, Is.True, "Retry takes the first press");

            m_confirm.RaiseConfirm();

            Assert.That(enabledAtRestart, Is.False, "Retry is off before the session restarts (one press, one Restart)");
            Assert.That(m_confirm.ConfirmEnabled, Is.False, "it stays off while the popup is hidden");
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));

            m_flow.RequestRestart();

            Assert.That(m_confirm.ShowCount, Is.EqualTo(2));
            Assert.That(m_confirm.ConfirmEnabled, Is.True, "the next show enables Retry again");
        }

        [Test]
        public void ConfirmOnThePauseMenuDoesNotRestart()
        {
            m_flow.RequestPause();

            m_confirm.RaiseConfirm();

            Assert.That(m_session.RestartCalls, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
        }

        [Test]
        public void ARefusedRestartKeepsTheConfirmationWithItsActionsDisabled()
        {
            m_flow.RequestRestart();
            m_session.SetWritePending(true);

            Assert.That(m_confirm.ActionsEnabled, Is.False);
            Assert.That(m_menu.ActionsEnabled, Is.False);

            m_confirm.RaiseConfirm();

            Assert.That(m_session.AcceptedRestarts, Is.Zero);
            Assert.That(m_confirm.IsShowing, Is.True);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
            Assert.That(m_confirm.ConfirmDisableCount, Is.EqualTo(1), "the press locked Retry");
            Assert.That(m_confirm.ConfirmEnabled, Is.True, "a refused Restart unlocks Retry again");

            m_session.SetWritePending(false);
            Assert.That(m_confirm.ActionsEnabled, Is.True);
            m_confirm.RaiseConfirm();
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
        }

        [TestCase(CloseSource.CloseButtonOrScrim)]
        [TestCase(CloseSource.OverlayBack)]
        public void ClosingThePauseMenuResumesAndHides(CloseSource source)
        {
            m_flow.RequestPause();

            Close(source, onConfirmation: false);

            Assert.That(m_session.ResumeCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_menu.IsShowing, Is.False);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_back.IsEnabled, Is.False, "overlay Back stops with the overlay");
        }

        [TestCase(CloseSource.CloseButtonOrScrim)]
        [TestCase(CloseSource.OverlayBack)]
        public void ClosingTheConfirmationResumesWithoutARestart(CloseSource source)
        {
            m_flow.RequestRestart();

            Close(source, onConfirmation: true);

            Assert.That(m_session.ResumeCalls, Is.EqualTo(1));
            Assert.That(m_session.RestartCalls, Is.Zero);
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_confirm.IsShowing, Is.False);
            Assert.That(m_back.IsEnabled, Is.False);
        }

        [Test]
        public void ARefusedResumeKeepsTheMenuWithCloseDisabledUntilTheWriteEnds()
        {
            m_flow.RequestPause();
            m_session.SetWritePending(true);

            Assert.That(m_menu.ActionsEnabled, Is.False, "Close and HOME are off while the write is pending");
            m_menu.RaiseClose();
            m_back.Press();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused), "nothing resumes");
            Assert.That(m_menu.IsShowing, Is.True);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));

            m_session.SetWritePending(false);
            Assert.That(m_menu.ActionsEnabled, Is.True);
            m_menu.RaiseClose();
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_menu.IsShowing, Is.False);
        }

        [Test]
        public void BackInPlayingOpensThePauseMenuAndReportsThatPlayingEnded()
        {
            Assert.That(m_flow.OnBack(), Is.True);

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_menu.ShowCount, Is.EqualTo(1));
        }

        [Test]
        public void BackWithARefusedPauseReportsThatPlayingContinues()
        {
            m_session.AcceptsPause = false;

            Assert.That(m_flow.OnBack(), Is.False);
            Assert.That(m_menu.ShowCount, Is.Zero);
        }

        [Test]
        public void OneOverlayBackPressIsHandledOnce()
        {
            Assert.That(m_flow.OnBack(), Is.True);
            int enables = m_back.EnableCount;

            m_back.Press();
            m_back.Press();

            Assert.That(m_session.ResumeCalls, Is.EqualTo(1), "the second press finds the action disabled");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_back.EnableCount, Is.EqualTo(enables));
        }

        [Test]
        public void ABackgroundPauseOpensNothingAndTheForegroundReturnResumes()
        {
            m_session.SetState(SessionState.Paused, PauseReason.Background);
            m_flow.OnApplicationPauseChanged(true);

            Assert.That(m_menu.ShowCount, Is.Zero, "backgrounding only suspends play (DEC-182)");
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_back.IsEnabled, Is.False);
            Assert.That(m_session.ResumeCalls, Is.Zero, "never resumes while going to the background");

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_session.ResumeFromBackgroundCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_menu.ShowCount, Is.Zero);
        }

        [TestCase(PauseReason.Saving)]
        [TestCase(PauseReason.Purchase)]
        public void AnotherPauseReasonLatchedToBackgroundResumesOnTheForegroundReturn(PauseReason reason)
        {
            m_session.SetState(SessionState.Paused, reason);
            m_session.LatchBackground();
            m_flow.OnApplicationPauseChanged(true);

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_menu.ShowCount, Is.Zero, "no menu on the return");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void AForegroundReturnDuringABoosterWriteLeavesTheResumeToTheSession()
        {
            m_session.SetState(SessionState.Paused, PauseReason.Saving);
            m_session.SetWritePending(true);
            m_session.LatchBackground();

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused), "the write is still unresolved");
            Assert.That(m_session.IsForegroundResumeDeferred, Is.True, "the session resumes once the write resolves");
            Assert.That(m_menu.ShowCount, Is.Zero);
        }

        [Test]
        public void PauseWhileABoosterWriteIsPendingIsIgnored()
        {
            m_session.SetWritePending(true);

            m_flow.RequestPause();

            Assert.That(m_session.PauseCalls, Is.Zero);
            Assert.That(m_menu.ShowCount, Is.Zero);
        }

        [Test]
        public void AForegroundReturnKeepsThePlayersPauseMenuAndItsPause()
        {
            m_flow.RequestPause();
            m_session.LatchBackground();
            m_flow.OnApplicationPauseChanged(true);

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_session.ResumeFromBackgroundCalls, Is.Zero, "the player's own menu decides");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_menu.ShowCount, Is.EqualTo(1), "the same menu, shown once");
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));

            m_menu.RaiseClose();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing), "Close resumes");
        }

        [Test]
        public void AForegroundReturnKeepsTheRetryConfirmationAndItsPause()
        {
            m_flow.RequestRestart();
            m_session.LatchBackground();

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
        }

        [TestCase(SessionState.Playing, PauseReason.None)]
        [TestCase(SessionState.Paused, PauseReason.User)]
        [TestCase(SessionState.Paused, PauseReason.Saving)]
        [TestCase(SessionState.Won, PauseReason.None)]
        public void AFocusReturnWithoutABackgroundPauseChangesNothing(SessionState state, PauseReason reason)
        {
            m_session.SetState(state, reason);

            m_flow.OnApplicationPauseChanged(false);

            Assert.That(m_menu.ShowCount, Is.Zero);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_session.State.Value, Is.EqualTo(state));
        }

        [Test]
        public void ReturningToPlayingHidesAnyOverlay()
        {
            m_flow.RequestRestart();

            // A resume from somewhere else (the purchase popup's close under a Background pause, design 5).
            m_session.SetState(SessionState.Playing);

            Assert.That(m_confirm.IsShowing, Is.False);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
            Assert.That(m_back.IsEnabled, Is.False);
        }

        [Test]
        public void HomeWithoutAHomeRequestHidesTheMenu()
        {
            m_session.SetState(SessionState.Won);
            m_flow.RequestPause();

            m_session.SetState(SessionState.Home);

            Assert.That(m_menu.IsShowing, Is.False);
            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.None));
        }

        [Test]
        public void HomeBeforeAnyBlockPressTearsDownOnceAndStartsOneHubTransitionWhileTheMenuStaysUp()
        {
            m_session.SetTimerStarted(false);
            m_flow.RequestPause();

            m_menu.RaiseHome();
            m_menu.RaiseHome();

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_transition.Calls, Is.EqualTo(1), "a second HOME does nothing");
            Assert.That(m_exit.IsExiting, Is.True);
            Assert.That(m_menu.IsShowing, Is.True, "the menu covers the torn-down board while Hub loads");

            m_menu.RaiseClose();
            m_back.Press();

            Assert.That(m_menu.IsShowing, Is.True, "Close does nothing while the exit runs");
            Assert.That(m_session.ResumeCalls, Is.Zero);
            Assert.That(m_confirm.ShowCount, Is.Zero, "an untouched level asks nothing (DEC-189)");
        }

        [Test]
        public void HomeAfterABlockPressShowsTheConfirmationReadingLeave()
        {
            m_flow.RequestPause();

            m_menu.RaiseHome();

            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.LeaveConfirm));
            Assert.That(m_menu.IsShowing, Is.False);
            Assert.That(m_confirm.IsShowing, Is.True);
            Assert.That(m_confirm.LastAction, Is.EqualTo(LifeConfirmAction.Leave));
            Assert.That(m_confirm.LastTitle, Is.EqualTo(m_levelNumber.Title));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused), "asking changes no session state");
            Assert.That(m_session.GoHomeCalls, Is.Zero);
            Assert.That(m_back.IsEnabled, Is.True);
        }

        [Test]
        public void LeaveTearsDownOnceAndStartsOneHubTransitionWhileTheConfirmationStaysUp()
        {
            m_flow.RequestPause();
            m_menu.RaiseHome();

            m_confirm.RaiseConfirm();
            m_confirm.RaiseConfirm();

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_transition.Calls, Is.EqualTo(1), "a second Leave does nothing");
            Assert.That(m_exit.IsExiting, Is.True);
            Assert.That(m_confirm.IsShowing, Is.True, "the popup covers the torn-down board while Hub loads");
            Assert.That(m_session.RestartCalls, Is.Zero, "Leave never restarts");
        }

        [TestCase(CloseSource.CloseButtonOrScrim)]
        [TestCase(CloseSource.OverlayBack)]
        public void ClosingTheLeaveConfirmationReturnsToThePauseMenu(CloseSource source)
        {
            m_flow.RequestPause();
            m_menu.RaiseHome();

            Close(source, onConfirmation: true);

            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            Assert.That(m_confirm.IsShowing, Is.False);
            Assert.That(m_menu.IsShowing, Is.True);
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused), "the level stays paused");
            Assert.That(m_session.ResumeCalls, Is.Zero);
            Assert.That(m_session.GoHomeCalls, Is.Zero);
        }

        [Test]
        public void TheRetryConfirmationReadsRetry()
        {
            m_flow.RequestPause();
            m_menu.RaiseHome();
            Close(CloseSource.CloseButtonOrScrim, onConfirmation: true);
            Close(CloseSource.CloseButtonOrScrim, onConfirmation: false);

            m_flow.RequestRestart();

            Assert.That(m_flow.Current, Is.EqualTo(GameOverlay.RestartConfirm));
            Assert.That(m_confirm.LastAction, Is.EqualTo(LifeConfirmAction.Retry));
        }

        [Test]
        public void HomeWhileABoosterWriteIsPendingIsRefused()
        {
            m_flow.RequestPause();
            m_session.SetWritePending(true);

            m_menu.RaiseHome();

            Assert.That(m_menu.ActionsEnabled, Is.False, "HOME is disabled while the write is pending");
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_transition.Calls, Is.Zero);
            Assert.That(m_exit.IsExiting, Is.False);
            Assert.That(m_menu.IsShowing, Is.True);
        }

        [Test]
        public void HomeOnTheConfirmationIsIgnored()
        {
            m_flow.RequestRestart();

            m_menu.RaiseHome();

            Assert.That(m_session.GoHomeCalls, Is.Zero);
            Assert.That(m_transition.Calls, Is.Zero);
        }

        [Test]
        public void DisposeStopsEveryHandlerAndSubscription()
        {
            m_flow.RequestPause();

            m_flow.Dispose();

            Assert.That(m_back.IsEnabled, Is.False, "the Back action is disabled");
            int shows = m_menu.ShowCount;
            m_menu.RaiseClose();
            m_menu.RaiseHome();
            m_confirm.RaiseConfirm();
            m_back.Enable();
            m_back.Press();
            Assert.That(m_session.ResumeCalls, Is.Zero);
            Assert.That(m_session.GoHomeCalls, Is.Zero);
            Assert.That(m_session.RestartCalls, Is.Zero);

            m_session.SetState(SessionState.Playing);
            m_session.SetState(SessionState.Paused, PauseReason.Background);
            m_session.SetWritePending(true);
            m_flow.OnApplicationPauseChanged(false);
            m_flow.RequestPause();
            m_flow.RequestRestart();
            Assert.That(m_flow.OnBack(), Is.False);

            Assert.That(m_menu.ShowCount, Is.EqualTo(shows), "no state change reaches a disposed flow");
            Assert.That(m_menu.ActionsEnabled, Is.True, "the pending-write subscription is gone");
            Assert.That(m_confirm.ShowCount, Is.Zero);
            Assert.That(m_session.PauseCalls, Is.EqualTo(1));
        }

        private void Close(CloseSource source, bool onConfirmation)
        {
            if (source == CloseSource.OverlayBack)
            {
                m_back.Press();
            }
            else if (onConfirmation)
            {
                m_confirm.RaiseClose();
            }
            else
            {
                m_menu.RaiseClose();
            }
        }
    }
}
