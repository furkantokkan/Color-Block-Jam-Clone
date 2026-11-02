using System.Threading;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Drives the level session with fakes that share one journal, so the tick phases, transitions and
    /// the fixed Suspend, Teardown and Finish orders are asserted call by call (RULE-15..18, V-E1..V-E26).
    /// </summary>
    public sealed class LevelSessionTests
    {
        // A single block under a one-lane Top door of its colour.
        private static readonly string[] s_exitMap =
        {
            "...",
            ".A.",
            "..."
        };

        private static readonly DoorSpec[] s_exitDoor = { BoardFixture.Door(Side.Top, 1, 1, 'A') };

        // Two blocks, so one exit does not win the level.
        private static readonly string[] s_twoBlockMap =
        {
            "....",
            "A...",
            "...B"
        };

        private SessionFixture m_fixture;

        [TearDown]
        public void TearDown()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [Test]
        public void TicksDoNothingUnlessPlaying()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out _), Is.True);

            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero, "Home");

            Assert.That(m_fixture.Session.Play(), Is.True);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);

            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero, "Paused");
            Assert.That(m_fixture.Presenter.SyncCount, Is.Zero);
            Assert.That(m_fixture.Countdown.TickCount, Is.Zero);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void APressOverUiNeverStartsADrag()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Ui.OverUi = true;

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Ui.OverUi = false;
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);

            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
        }

        [Test]
        public void APressOutsideTheInteractionAreaSelectsNothing()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            m_fixture.Tick(PointerPhase.Down, -2f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);

            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
        }

        [Test]
        public void APressThatMissesTheBoardPlaneSelectsNothing()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Projector.MissesPlane = true;

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);

            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero);
        }

        [Test]
        public void AWallRingPressHoldsTheNearestBlockAndKeepsTheGrabOffset()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            m_fixture.Tick(PointerPhase.Down, -1f, 1f);
            m_fixture.Tick(PointerPhase.Held, -0.6f, 1f);

            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(0));
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "the block never teleports to the press");

            m_fixture.Tick(PointerPhase.Held, 0f, 1f);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void ReleaseEndsTheDragAndClearsTheHeldBlock()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 1f);
            m_fixture.Tick(PointerPhase.Up, 1f, 1f);

            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(-1));
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
            Assert.That(m_fixture.Presenter.LastFirstEventKind, Is.EqualTo(BoardEventKind.MoveCommitted));
        }

        [Test]
        public void DraggingIntoAMatchingDoorWinsAfterTheCommandPhase()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.PrepareAndPlay();

            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            int timerTicks = m_fixture.Countdown.TickCount;
            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(timerTicks), "the timer slot does not run in the Won tick");
            Assert.That(m_fixture.Presenter.LastExitCount, Is.EqualTo(1), "Sync plays the final exit once");
            Assert.That(m_fixture.Session.Attempt, Is.Null, "no finish time: Teardown ran at once");
        }

        [Test]
        public void TheBoundHookSourceSuppliesEachAttemptsHooks()
        {
            RecordingListener listener = new RecordingListener();
            FixedHookSource source = new FixedHookSource(new AttemptHooksBuilder().Add(listener).Build());
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor, hookSource: source);
            m_fixture.PrepareAndPlay();

            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);

            Assert.That(source.CreateCount, Is.EqualTo(1), "one attempt asks the source once");
            Assert.That(listener.Exited.Count, Is.EqualTo(1), "effects registered by the source hear the exit (XP-5)");
        }

        [Test]
        public void WonFinishRunsBeforeTeardownInTheFixedOrder()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.PrepareAndPlay();
            LevelAttempt attempt = m_fixture.Session.Attempt;
            attempt.Lifetime.Token.Register(() => m_fixture.Journal.Add("token.Cancelled"));
            attempt.Lifetime.Disposables.Add(new JournalDisposable(m_fixture.Journal, "disposables.Disposed"));
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Journal.Clear();

            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);

            Assert.That(m_fixture.Journal, Is.EqualTo(new[]
            {
                "pointer.Disable", "presenter.SnapHeld", "countdown.Stop", "presenter.Sync",
                "pointer.Disable", "presenter.SnapHeld", "countdown.Stop", "presenter.StopTweens",
                "token.Cancelled", "disposables.Disposed", "presenter.ReturnPieces"
            }));
        }

        [Test]
        public void AfterWonCommandsAndTicksChangeNothing()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);
            int syncs = m_fixture.Presenter.SyncCount;
            m_fixture.Journal.Clear();

            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Countdown.ExpireOnTick = true;
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.Play(), Is.False);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.False);
            Assert.That(m_fixture.Session.Resume(), Is.False);
            Assert.That(m_fixture.Session.Restart(), Is.False);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_fixture.Presenter.SyncCount, Is.EqualTo(syncs));
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        [TestCase(0.25f, 0.25f)]
        [TestCase(5f, 0.8f)]
        public void WonUsesThePresentersDurationUpToTheFinishCap(float presentationSeconds, float expectedSeconds)
        {
            WinWithPendingFinish(presentationSeconds);

            Assert.That(m_fixture.Deadline.IsPending, Is.True);
            Assert.That(m_fixture.Deadline.Seconds, Is.EqualTo(expectedSeconds));
            Assert.That(m_fixture.Session.IsFinishing.Value, Is.True);
            Assert.That(m_fixture.Pointer.IsEnabled, Is.False);
            Assert.That(m_fixture.Session.Attempt, Is.Not.Null);

            m_fixture.Journal.Clear();
            m_fixture.Deadline.Complete();
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Session.IsFinishing.Value, Is.False);
            Assert.That(m_fixture.Journal.FindAll(entry => entry == "presenter.ReturnPieces"), Has.Count.EqualTo(1));

            m_fixture.Journal.Clear();
            m_fixture.Deadline.Complete();
            Assert.That(m_fixture.Journal, Is.Empty, "Completion cannot tear down twice.");
        }

        [TestCase("Home")]
        [TestCase("Background")]
        [TestCase("Dispose")]
        public void LeavingTheFinishCancelsItsDeadline(string reason)
        {
            WinWithPendingFinish(0.5f);
            switch (reason)
            {
                case "Home":
                    Assert.That(m_fixture.Session.GoHome(), Is.True);
                    break;
                case "Background":
                    m_fixture.Session.NotifyBackground(true);
                    break;
                case "Dispose":
                    m_fixture.Session.Dispose();
                    break;
            }

            Assert.That(m_fixture.Deadline.IsPending, Is.False);
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            m_fixture.Journal.Clear();
            m_fixture.Deadline.Complete();
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        [Test]
        public void ACancelledFinishCannotReleaseTheNextAttempt()
        {
            WinWithPendingFinish(0.5f);
            Assert.That(m_fixture.Session.GoHome(), Is.True);
            m_fixture.PrepareAndPlay();
            LevelAttempt next = m_fixture.Session.Attempt;
            m_fixture.Journal.Clear();

            m_fixture.Deadline.Complete();

            Assert.That(m_fixture.Session.Attempt, Is.SameAs(next));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(next.Lifetime.Token.IsCancellationRequested, Is.False);
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        private void WinWithPendingFinish(float seconds)
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.Presenter.FinishSeconds = seconds;
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
        }

        [Test]
        public void ALastExitAndTimerZeroInOneTickGiveWon()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            m_fixture.Countdown.ExpireOnTick = true;

            m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
        }

        [Test]
        public void TimerExpiryFailsAndTearsDownAtOnce()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Countdown.ExpireOnTick = true;

            // The press starts the countdown (DEC-189), and its own tick's timer slot expires it.
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Failed));
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Journal, Does.Contain("presenter.ReturnPieces"));
            Assert.That(m_fixture.Presenter.SyncCount, Is.Zero, "view sync never runs after the timer phase fails");
        }

        [Test]
        public void TheCountdownWaitsForTheFirstPressOnABlock()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Countdown.ExpireOnTick = true;

            m_fixture.TickIdle(5f);
            m_fixture.Tick(PointerPhase.Down, -2f, 1f);
            m_fixture.Tick(PointerPhase.Up, -2f, 1f);

            Assert.That(m_fixture.Countdown.TickCount, Is.Zero, "idle time and a press on nothing start no countdown");
            Assert.That(m_fixture.Session.IsTimerStarted.Value, Is.False);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));

            m_fixture.Countdown.ExpireOnTick = false;
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);

            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(1), "the press's own tick runs the timer slot");
            Assert.That(m_fixture.Session.IsTimerStarted.Value, Is.True);

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_fixture.Session.Restart(), Is.True);
            Assert.That(m_fixture.Session.IsTimerStarted.Value, Is.False, "a restarted attempt waits again");
            m_fixture.TickIdle();
            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void SuspendRunsItsStepsInOrder()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Journal.Clear();

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);

            Assert.That(m_fixture.Journal, Is.EqualTo(new[]
            {
                "pointer.Disable", "presenter.SnapHeld", "countdown.Pause", "presenter.PauseTweens"
            }));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.User));
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
        }

        [Test]
        public void ResumePublishesThePendingMoveCommittedFirst()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 1f);
            m_fixture.Session.Pause(PauseReason.User);
            m_fixture.Journal.Clear();

            Assert.That(m_fixture.Session.Resume(), Is.True);
            Assert.That(m_fixture.Journal, Is.EqualTo(new[] { "presenter.ResumeTweens", "countdown.Resume", "pointer.Enable" }));

            m_fixture.Tick(PointerPhase.Down, 3f, 0f);

            Assert.That(m_fixture.Presenter.LastFirstEventKind, Is.EqualTo(BoardEventKind.MoveCommitted));
            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(1), "the flushed move does not block a new press");
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)), "Resume replays nothing");
        }

        [Test]
        public void BackgroundPausesAndTheInterimOverlayResumesOnForeground()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            m_fixture.Session.NotifyBackground(true);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Background));

            m_fixture.Session.NotifyBackground(false);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void WithoutAutoResumeTheForegroundKeepsThePause()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0], resumesOnForeground: false);
            m_fixture.PrepareAndPlay();

            m_fixture.Session.NotifyBackground(true);
            m_fixture.Session.NotifyBackground(false);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Background));
        }

        [Test]
        public void BackgroundLatchesAUserPauseToBackground()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Session.Pause(PauseReason.User);
            m_fixture.Journal.Clear();

            m_fixture.Session.NotifyBackground(true);

            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_fixture.Journal, Is.Empty, "an already suspended attempt is not suspended again");
        }

        [Test]
        public void RestartRebuildsTheAuthoredBoardAndCancelsTheOldToken()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);
            m_fixture.Session.Pause(PauseReason.User);
            LevelAttempt first = m_fixture.Session.Attempt;
            CancellationToken firstToken = first.Lifetime.Token;

            Assert.That(m_fixture.Session.Restart(), Is.True);

            LevelAttempt second = m_fixture.Session.Attempt;
            Assert.That(firstToken.IsCancellationRequested, Is.True);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(second.Generation, Is.EqualTo(first.Generation + 1));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));

            // The old attempt's interrupted move would flush as a MoveCommitted in this first command phase.
            int syncs = m_fixture.Presenter.SyncCount;
            m_fixture.TickIdle();
            Assert.That(m_fixture.Presenter.SyncCount, Is.EqualTo(syncs + 1));
            Assert.That(m_fixture.Presenter.LastEventCount, Is.Zero, "the interrupted move stays with the old attempt");
            BoardState board = second.Simulation.Board;
            for (int block = 0; block < board.BlockCount; block++)
            {
                Assert.That(board.GetOrigin(block), Is.EqualTo(board.Model.GetBlock(block).Origin));
                Assert.That(board.GetStatus(block), Is.EqualTo(BlockStatus.OnBoard));
            }
        }

        [Test]
        public void GoHomeTearsDownAndNeedsANewPrepareToPlay()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Session.Pause(PauseReason.User);

            Assert.That(m_fixture.Session.GoHome(), Is.True);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Session.Play(), Is.False);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out _), Is.True);
            Assert.That(m_fixture.Session.Play(), Is.True);
        }

        [Test]
        public void ALevelThePresenterRejectsCommitsNoAttempt()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.Presenter.BuildError = "Block b0 uses shape 'Single', whose prefab has no BlockPiece.";

            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.False);

            Assert.That(error, Is.EqualTo(m_fixture.Presenter.BuildError));
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Journal, Does.Not.Contain("presenter.Place"));
            Assert.That(m_fixture.Session.Play(), Is.False, "Play needs a committed attempt");
            Assert.That(m_fixture.Session.Restart(), Is.False);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Home));
        }

        [Test]
        public void PrepareIsRefusedOutsideHome()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void WonObserverCanReturnHomeAfterFinalExitSync()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.Presenter.FinishSeconds = 0.5f;
            m_fixture.PrepareAndPlay();
            bool homeAccepted = false;
            bool gameplayStopped = false;
            int exitsAtNotification = 0;
            using var subscription = m_fixture.Session.State.Subscribe(state =>
            {
                if (state == SessionState.Won)
                {
                    gameplayStopped = !m_fixture.Pointer.IsEnabled && m_fixture.Journal.Contains("countdown.Stop");
                    exitsAtNotification = m_fixture.Presenter.LastExitCount;
                    homeAccepted = m_fixture.Session.GoHome();
                }
            });

            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            Assert.DoesNotThrow(() => m_fixture.Tick(PointerPhase.Held, 1f, 2.4f));

            Assert.That(homeAccepted, Is.True);
            Assert.That(gameplayStopped, Is.True);
            Assert.That(exitsAtNotification, Is.EqualTo(1));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Home));
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Deadline.IsPending, Is.False, "A synchronous Home observer cancels the deadline.");
            m_fixture.Journal.Clear();
            m_fixture.Deadline.Complete();
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        [Test]
        public void FailedObserverCanRestartWithoutLosingTheNewAttempt()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            LevelAttempt first = m_fixture.Session.Attempt;
            CancellationToken firstToken = first.Lifetime.Token;
            bool restartAccepted = false;
            bool gameplayStopped = false;
            bool oldAttemptReleased = false;
            using var subscription = m_fixture.Session.State.Subscribe(state =>
            {
                if (state == SessionState.Failed)
                {
                    gameplayStopped = !m_fixture.Pointer.IsEnabled && m_fixture.Journal.Contains("countdown.Stop");
                    oldAttemptReleased = m_fixture.Session.Attempt == null && firstToken.IsCancellationRequested;
                    restartAccepted = m_fixture.Session.Restart();
                }
            });
            m_fixture.Countdown.ExpireOnTick = true;

            // The press starts the countdown (DEC-189), which expires in the same tick.
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);

            Assert.That(restartAccepted, Is.True);
            Assert.That(gameplayStopped, Is.True);
            Assert.That(m_fixture.Session.Attempt, Is.Not.Null);
            Assert.That(m_fixture.Session.Attempt, Is.Not.SameAs(first));
            Assert.That(oldAttemptReleased, Is.True);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            m_fixture.Countdown.ExpireOnTick = false;
            Assert.DoesNotThrow(m_fixture.TickIdle);
            Assert.That(m_fixture.Presenter.SyncCount, Is.EqualTo(1));
        }

        [TestCase(SessionState.Home)]
        [TestCase(SessionState.Playing)]
        [TestCase(SessionState.Paused)]
        [TestCase(SessionState.Won)]
        [TestCase(SessionState.Failed)]
        public void DisposedSessionRefusesWorkWithoutChangingState(SessionState state)
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out _), Is.True);
            if (state != SessionState.Home)
            {
                Assert.That(m_fixture.Session.Play(), Is.True);
            }

            if (state == SessionState.Paused)
            {
                m_fixture.Session.Pause(PauseReason.User);
            }
            else if (state == SessionState.Won)
            {
                m_fixture.Tick(PointerPhase.Down, 1f, 1f);
                m_fixture.Tick(PointerPhase.Held, 1f, 2.4f);
            }
            else if (state == SessionState.Failed)
            {
                m_fixture.Countdown.ExpireOnTick = true;
                m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            }

            m_fixture.Session.Dispose();
            m_fixture.Journal.Clear();
            GameCommand hammer = GameCommand.RemoveBlock(0, new Cell(1, 1));
            GameCommand rocket = GameCommand.RemoveCell(0, new Cell(1, 1), new Cell(1, 1));

            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(m_fixture.Session.Play(), Is.False);
            Assert.That(m_fixture.Session.Play(new OpeningPlan(false, 0, true, rocket)), Is.False);
            Assert.That(m_fixture.Session.Play(new OpeningPlan(true, 10, false, default)), Is.False,
                "refused before the countdown check, so no throw");
            Assert.That(m_fixture.Session.TrySubmitBooster(BoosterIntent.ForBoard(1, BoosterId.Hammer, hammer)), Is.False);
            Assert.That(m_fixture.Session.TryOpenPurchase(), Is.False);
            Assert.That(m_fixture.Session.ClosePurchase(), Is.False);
            Assert.DoesNotThrow(() => m_fixture.Session.CompleteBoosterCommit(1, true));
            Assert.That(m_fixture.Session.Pause(PauseReason.Background), Is.False);
            Assert.That(m_fixture.Session.Resume(), Is.False);
            Assert.That(m_fixture.Session.Restart(), Is.False);
            Assert.That(m_fixture.Session.GoHome(), Is.False);
            Assert.That(m_fixture.Session.TrySetCommandSource(null), Is.False);
            Assert.DoesNotThrow(m_fixture.TickIdle);
            Assert.DoesNotThrow(() => m_fixture.Session.NotifyBackground(true));
            Assert.DoesNotThrow(() => m_fixture.Session.NotifyBackground(false));
            Assert.DoesNotThrow(m_fixture.Session.CompleteFinish);
            Assert.DoesNotThrow(m_fixture.Session.Dispose);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(state));
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        [Test]
        public void DisposingDuringABoosterWriteIgnoresItsLateResult()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            int generation = m_fixture.Session.Attempt.Generation;
            GameCommand hammer = GameCommand.RemoveBlock(1, m_fixture.Session.Attempt.Simulation.Board.GetOrigin(1));
            Assert.That(m_fixture.Session.TrySubmitBooster(BoosterIntent.ForBoard(generation, BoosterId.Hammer, hammer)),
                Is.True);
            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.True);

            // A scene unload disposes the session while the debit is still being written (AC-15).
            m_fixture.Session.Dispose();
            m_fixture.Journal.Clear();

            Assert.DoesNotThrow(() => m_fixture.Session.CompleteBoosterCommit(generation, true));
            Assert.DoesNotThrow(() => m_fixture.Session.CompleteBoosterCommit(generation, false));
            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.False);
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.Attempt, Is.Null);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Journal, Is.Empty);
        }
    }
}
