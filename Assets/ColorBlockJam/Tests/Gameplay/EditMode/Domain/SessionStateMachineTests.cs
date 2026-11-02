using ColorBlockJam.Domain.Session;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class SessionStateMachineTests
    {
        [Test]
        public void StartsOnHomeWithNoAttempt()
        {
            SessionStateMachine machine = new SessionStateMachine();

            Assert.That(machine.State, Is.EqualTo(SessionState.Home));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.None));
            Assert.That(machine.AttemptNumber, Is.EqualTo(0));
        }

        [TestCase(SessionRequest.Pause, PauseReason.User)]
        [TestCase(SessionRequest.Background, PauseReason.Background)]
        [TestCase(SessionRequest.BoosterCommit, PauseReason.Saving)]
        [TestCase(SessionRequest.ZeroStockTap, PauseReason.Purchase)]
        public void EachPauseRequestRecordsItsReason(SessionRequest request, PauseReason reason)
        {
            SessionStateMachine machine = CreatePlaying();

            Assert.That(machine.TryApply(request), Is.True);

            Assert.That(machine.State, Is.EqualTo(SessionState.Paused));
            Assert.That(machine.PauseReason, Is.EqualTo(reason));
        }

        [TestCase(PauseReason.User)]
        [TestCase(PauseReason.Background)]
        [TestCase(PauseReason.Saving)]
        [TestCase(PauseReason.Purchase)]
        public void TryPauseAppliesTheRequestOfEachReason(PauseReason reason)
        {
            SessionStateMachine machine = CreatePlaying();

            Assert.That(machine.TryPause(reason), Is.True);

            Assert.That(machine.State, Is.EqualTo(SessionState.Paused));
            Assert.That(machine.PauseReason, Is.EqualTo(reason));
        }

        [Test]
        public void TryPauseRefusesNoneAndChangesNothing()
        {
            SessionStateMachine machine = CreatePlaying();

            Assert.That(machine.TryPause(PauseReason.None), Is.False);

            Assert.That(machine.State, Is.EqualTo(SessionState.Playing));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.None));
        }

        [Test]
        public void ResumeClearsTheReasonWithoutStartingANewAttempt()
        {
            SessionStateMachine machine = CreatePlaying();
            machine.TryApply(SessionRequest.Pause);

            Assert.That(machine.TryApply(SessionRequest.Resume), Is.True);

            Assert.That(machine.State, Is.EqualTo(SessionState.Playing));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.None));
            Assert.That(machine.AttemptNumber, Is.EqualTo(1));
        }

        [Test]
        public void BackgroundLatchesAnExistingPause()
        {
            SessionStateMachine machine = CreatePlaying();
            machine.TryApply(SessionRequest.Pause);

            Assert.That(machine.TryApply(SessionRequest.Background), Is.True);

            Assert.That(machine.State, Is.EqualTo(SessionState.Paused));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.Background));
        }

        [Test]
        public void ALateSaveCompletionAfterBackgroundingDoesNotResume()
        {
            SessionStateMachine machine = CreatePlaying();
            machine.TryApply(SessionRequest.BoosterCommit);
            machine.TryApply(SessionRequest.Background);

            Assert.That(machine.TryApply(SessionRequest.CommitSucceeded), Is.False);
            Assert.That(machine.TryApply(SessionRequest.CommitFailed), Is.False);
            Assert.That(machine.State, Is.EqualTo(SessionState.Paused));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.Background));

            Assert.That(machine.TryApply(SessionRequest.Resume), Is.True, "an explicit Resume still works");
            Assert.That(machine.State, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void CompletionsResumeOnlyTheirOwnPause()
        {
            SessionStateMachine saving = CreatePlaying();
            saving.TryApply(SessionRequest.BoosterCommit);
            Assert.That(saving.TryApply(SessionRequest.PurchaseClosed), Is.False);
            Assert.That(saving.TryApply(SessionRequest.CommitSucceeded), Is.True);

            SessionStateMachine purchase = CreatePlaying();
            purchase.TryApply(SessionRequest.ZeroStockTap);
            Assert.That(purchase.TryApply(SessionRequest.CommitFailed), Is.False);
            Assert.That(purchase.TryApply(SessionRequest.PurchaseClosed), Is.True);

            SessionStateMachine user = CreatePlaying();
            user.TryApply(SessionRequest.Pause);
            Assert.That(user.TryApply(SessionRequest.CommitSucceeded), Is.False);
            Assert.That(user.TryApply(SessionRequest.PurchaseClosed), Is.False);
            Assert.That(user.State, Is.EqualTo(SessionState.Paused));
        }

        [TestCase(SessionRequest.AllRequiredCleared, SessionState.Won, SessionRequest.TimerExpired)]
        [TestCase(SessionRequest.TimerExpired, SessionState.Failed, SessionRequest.AllRequiredCleared)]
        public void TheFirstTerminalRequestWinsAndTheSecondIsIgnored(SessionRequest first, SessionState terminal, SessionRequest second)
        {
            SessionStateMachine machine = CreatePlaying();

            Assert.That(machine.TryApply(first), Is.True);
            Assert.That(machine.TryApply(second), Is.False);
            Assert.That(machine.TryApply(first), Is.False);
            Assert.That(machine.TryApply(SessionRequest.Pause), Is.False);

            Assert.That(machine.State, Is.EqualTo(terminal));
            Assert.That(machine.AttemptNumber, Is.EqualTo(1));
        }

        [Test]
        public void EveryAttemptStartCountsAndResumesDoNot()
        {
            SessionStateMachine machine = CreatePlaying();
            machine.TryApply(SessionRequest.Pause);
            machine.TryApply(SessionRequest.Restart);
            Assert.That(machine.AttemptNumber, Is.EqualTo(2));

            machine.TryApply(SessionRequest.TimerExpired);
            machine.TryApply(SessionRequest.Restart);
            Assert.That(machine.AttemptNumber, Is.EqualTo(3));

            machine.TryApply(SessionRequest.AllRequiredCleared);
            machine.TryApply(SessionRequest.NextLevel);
            Assert.That(machine.AttemptNumber, Is.EqualTo(4));

            machine.TryApply(SessionRequest.BoosterCommit);
            machine.TryApply(SessionRequest.CommitSucceeded);
            machine.TryApply(SessionRequest.Pause);
            machine.TryApply(SessionRequest.Resume);
            Assert.That(machine.AttemptNumber, Is.EqualTo(4));

            machine.TryApply(SessionRequest.Pause);
            machine.TryApply(SessionRequest.Home);
            machine.TryApply(SessionRequest.Play);
            Assert.That(machine.AttemptNumber, Is.EqualTo(5));
        }

        [Test]
        public void ARejectedRequestChangesNothing()
        {
            SessionStateMachine machine = CreatePlaying();
            machine.TryApply(SessionRequest.Pause);

            Assert.That(machine.TryApply(SessionRequest.Play), Is.False);

            Assert.That(machine.State, Is.EqualTo(SessionState.Paused));
            Assert.That(machine.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(machine.AttemptNumber, Is.EqualTo(1));
        }

        private static SessionStateMachine CreatePlaying()
        {
            SessionStateMachine machine = new SessionStateMachine();
            Assert.That(machine.TryApply(SessionRequest.Play), Is.True);
            return machine;
        }
    }
}
