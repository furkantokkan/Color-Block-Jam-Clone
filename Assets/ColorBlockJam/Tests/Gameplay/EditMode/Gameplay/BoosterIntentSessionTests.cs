using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The session side of an in-game booster use (B-13, B-15): the accepting tick ends at once, a committed intent
    /// applies once as the first command before the timer, failures, duplicates and stale results change nothing,
    /// backgrounding never resumes by itself, Home and Restart discard a committed intent, the parked drag is
    /// dropped, and Time Freeze runs through the real attempt countdown (AC-3, AC-4, AC-10, AC-11).
    /// </summary>
    public sealed class BoosterIntentSessionTests
    {
        private static readonly string[] s_single =
        {
            "...",
            ".A.",
            "..."
        };

        // A at (0, 1), B at (3, 0): removing one block does not win.
        private static readonly string[] s_twoBlocks =
        {
            "....",
            "A...",
            "...B"
        };

        private static readonly string[] s_suspendSteps =
        {
            "pointer.Disable", "presenter.SnapHeld", "countdown.Pause", "presenter.PauseTweens"
        };

        private static readonly string[] s_resumeSteps = { "presenter.ResumeTweens", "countdown.Resume", "pointer.Enable" };

        private SessionFixture m_fixture;

        [TearDown]
        public void TearDown()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [Test]
        public void TheAcceptingTickReturnsBeforeTheWonCheckTheTimerAndTheViewSync()
        {
            m_fixture = new SessionFixture(s_single, new DoorSpec[0], withTargeting: true);
            RecordingTargetSink sink = new RecordingTargetSink { Session = m_fixture.Session };
            m_fixture.Targeting.Attach(sink);
            m_fixture.PrepareAndPlay();
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Tick(PointerPhase.Down, 1f, 1f);
            int timerTicks = m_fixture.Countdown.TickCount;
            int syncs = m_fixture.Presenter.SyncCount;
            m_fixture.Journal.Clear();

            m_fixture.Tick(PointerPhase.Up, 1f, 1f);

            Assert.That(sink.Count, Is.EqualTo(1));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.True);
            Assert.That(m_fixture.Journal, Is.EqualTo(s_suspendSteps), "Suspend only: no Won, no timer, no sync");
            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(timerTicks));
            Assert.That(m_fixture.Presenter.SyncCount, Is.EqualTo(syncs));
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "Suspend's router reset ended targeting");
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard),
                "nothing applies before the debit commits");
        }

        [Test]
        public void ACommittedHammerThatClearsTheBoardWinsBeforeTheTimerSlot()
        {
            m_fixture = new SessionFixture(s_single, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(0);
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);
            m_fixture.Journal.Clear();

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);

            Assert.That(m_fixture.Journal, Is.EqualTo(s_resumeSteps));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.True);
            int timerTicks = m_fixture.Countdown.TickCount;
            CommandLog log = m_fixture.Session.Attempt.Simulation.Commands.Log;

            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(timerTicks), "the timer slot does not run in the Won tick");
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.GetAt(0), Is.EqualTo(intent.Command));
        }

        [Test]
        public void ACommittedIntentAppliesOnceAsTheFirstCommandOfTheResumedPhase()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.StartTimer(3f, 0f);
            GameCommand sourceMove = GameCommand.Move(0, new Cell(0, 1), new Cell(1, 1));
            m_fixture.Session.TrySetCommandSource(new ReplayCommandSource(new[] { sourceMove }));
            BoosterIntent intent = Hammer(1);
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            int timerTicks = m_fixture.Countdown.TickCount;

            m_fixture.TickIdle();
            m_fixture.TickIdle();

            CommandLog log = m_fixture.Session.Attempt.Simulation.Commands.Log;
            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log.GetAt(0), Is.EqualTo(intent.Command), "the booster is the first command");
            Assert.That(log.GetAt(1), Is.EqualTo(sourceMove));
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.Removed));
            Assert.That(m_fixture.Countdown.TickCount, Is.EqualTo(timerTicks + 2), "the timer ran after the command phase");
        }

        [Test]
        public void AFailedSaveChangesNothingAndResumes()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Journal.Clear();

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, false);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Journal[0], Is.EqualTo(s_resumeSteps[0]));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.False);
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(m_fixture.Session.TrySubmitBooster(Hammer(1)), Is.True, "a new use may follow");
        }

        [Test]
        public void DuplicateCompletionsDoNothing()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.Journal.Clear();

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, false);
            Assert.That(m_fixture.Journal, Is.Empty);
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.True, "a late failure cannot drop a committed intent");

            m_fixture.TickIdle();
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1), "applied once");
        }

        [Test]
        public void AStaleCompletionFromAnEarlierAttemptDoesNothing()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            BoosterIntent first = Hammer(1);
            m_fixture.Session.TrySubmitBooster(first);
            m_fixture.Session.CompleteBoosterCommit(first.Generation, false);
            m_fixture.Session.Pause(PauseReason.User);
            Assert.That(m_fixture.Session.Restart(), Is.True);
            Assert.That(m_fixture.Session.TrySubmitBooster(first), Is.False, "an intent of the old attempt is stale");
            BoosterIntent second = Hammer(1);
            Assert.That(m_fixture.Session.TrySubmitBooster(second), Is.True);

            m_fixture.Session.CompleteBoosterCommit(first.Generation, true);

            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.True);
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Saving));

            m_fixture.Session.CompleteBoosterCommit(second.Generation, true);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void BackgroundDuringTheWriteThenALateSuccessWaitsForAnExplicitResume()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: false);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);
            m_fixture.Session.NotifyBackground(false);

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.True);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero, "nothing applies while paused");

            Assert.That(m_fixture.Session.Resume(), Is.True);
            m_fixture.TickIdle();
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(1));
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.Removed));
        }

        [Test]
        public void AForegroundDuringTheWriteResumesOnlyAfterTheWriteResolves()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: true);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);

            m_fixture.Session.NotifyBackground(false);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused), "never resumes during the write");
            Assert.That(m_fixture.Session.IsBoosterWritePending, Is.True);

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.Removed));
        }

        [Test]
        public void ABackgroundAfterTheForegroundCancelsTheDeferredResume()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: true);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);
            m_fixture.Session.NotifyBackground(false);
            m_fixture.Session.NotifyBackground(true);

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.True);
        }

        [Test]
        public void ALateFailureAfterBackgroundStaysPausedWithNothingApplied()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: false);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);

            m_fixture.Session.CompleteBoosterCommit(intent.Generation, false);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Background));
            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.Resume(), Is.True);
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
        }

        [Test]
        public void ResumeRestartAndHomeAreRefusedWhileTheWriteIsPending()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Session.TrySubmitBooster(Hammer(1));
            m_fixture.Journal.Clear();

            Assert.That(m_fixture.Session.Resume(), Is.False);
            Assert.That(m_fixture.Session.Restart(), Is.False);
            Assert.That(m_fixture.Session.GoHome(), Is.False);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.False);
            Assert.That(m_fixture.Session.TrySubmitBooster(Hammer(0)), Is.False, "one use at a time");
            Assert.That(m_fixture.Session.TryOpenPurchase(), Is.False);

            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.Saving));
            Assert.That(m_fixture.Journal, Is.Empty);
        }

        [Test]
        public void HomeAfterACommittedIntentDiscardsItWithoutApplying()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: false);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);

            Assert.That(m_fixture.Session.GoHome(), Is.True);

            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
            Assert.That(m_fixture.Session.Play(), Is.True);
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
        }

        [Test]
        public void RestartAfterACommittedIntentDiscardsItWithoutApplying()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], resumesOnForeground: false);
            m_fixture.PrepareAndPlay();
            BoosterIntent intent = Hammer(1);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.NotifyBackground(true);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);

            Assert.That(m_fixture.Session.Restart(), Is.True);
            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.HasCommittedBooster, Is.False);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
        }

        [Test]
        public void SavingDiscardsTheParkedDragMove()
        {
            RecordingListener listener = new RecordingListener();
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(listener).Build());
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], hookSource: hooks);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
            Assert.That(m_fixture.Presenter.GetShownOrigin(0), Is.EqualTo(new Cell(2, 1)), "the root follows the drag");
            BoosterIntent intent = Hammer(1);
            m_fixture.Journal.Clear();

            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);

            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.HasPending, Is.False);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "the preview returns to the committed board");
            Assert.That(m_fixture.Presenter.GetShownOrigin(0), Is.EqualTo(new Cell(0, 1)),
                "Suspend snaps the dropped block's root onto the committed cell");
            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(-1));
            Assert.That(m_fixture.Journal, Is.EqualTo(s_suspendSteps), "the Suspend steps stay the same");
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle();
            m_fixture.TickIdle();

            CommandLog log = m_fixture.Session.Attempt.Simulation.Commands.Log;
            Assert.That(log.Count, Is.EqualTo(1));
            Assert.That(log.GetAt(0), Is.EqualTo(intent.Command));
            Assert.That(listener.Moves, Is.Empty, "the interrupted move never commits");
            Assert.That(listener.Removed.Count, Is.EqualTo(1));
        }

        [Test]
        public void SavingBeforeTheFirstTickAfterResumeDropsTheParkedMoveAndPutsItsRootBack()
        {
            RecordingListener listener = new RecordingListener();
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(listener).Build());
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], hookSource: hooks);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);
            m_fixture.Session.Pause(PauseReason.User);
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.HasPending, Is.True, "the pause parked the move");
            Assert.That(m_fixture.Presenter.GetShownOrigin(0), Is.EqualTo(new Cell(2, 1)), "the pause snapped the root");
            Assert.That(m_fixture.Session.Resume(), Is.True);
            BoosterIntent intent = Hammer(1);

            // A HUD press can land after Resume and before the tick that runs the parked move.
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);

            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.HasPending, Is.False);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(m_fixture.Presenter.GetShownOrigin(0), Is.EqualTo(new Cell(0, 1)),
                "the root returns to the committed cell the domain keeps");
            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(-1));
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle();

            Assert.That(listener.Moves, Is.Empty, "the parked move never commits");
            Assert.That(m_fixture.Session.Attempt.Simulation.Board.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(m_fixture.Presenter.GetShownOrigin(0), Is.EqualTo(new Cell(0, 1)));
        }

        [Test]
        public void SubmitIsRefusedOutsidePlaying()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out _), Is.True);
            Assert.That(m_fixture.Session.TrySubmitBooster(Hammer(1)), Is.False, "Home");

            m_fixture.Session.Play();
            m_fixture.Session.Pause(PauseReason.User);
            Assert.That(m_fixture.Session.TrySubmitBooster(Hammer(1)), Is.False, "Paused");
            Assert.That(m_fixture.Session.PauseReason, Is.EqualTo(PauseReason.User));
        }

        [Test]
        public void TimeFreezeNeedsAClockThatCanFreeze()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            Assert.That(m_fixture.Session.SupportsTimeBoosters, Is.False);
            Assert.That(m_fixture.Session.TrySubmitBooster(BoosterIntent.ForTimeFreeze(m_fixture.Session.Attempt.Generation, 10f)),
                Is.False);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void TimeFreezeHoldsTheMainCountdownWithItsOwnCounterAndUsesOnlyTheLeftoverDelta()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], clock: clock);
            m_fixture.PrepareAndPlay();
            m_fixture.StartTimer(0f, 1f);
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds));
            m_fixture.TickIdle(BoardFixture.k_timerSeconds - 3f);
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(3));
            BoosterIntent intent = BoosterIntent.ForTimeFreeze(m_fixture.Session.Attempt.Generation, 10f);
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);

            m_fixture.TickIdle(4f);

            Assert.That(clock.Remaining, Is.EqualTo(3f), "the freeze starts before the timer slot of its tick");
            Assert.That(clock.FreezeRemaining, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(3));
            Assert.That(m_fixture.Session.FreezeDisplayedSeconds.Value, Is.EqualTo(6));
            Assert.That(m_fixture.Session.IsFrozen.Value, Is.True);
            Assert.That(m_fixture.Session.TrySubmitBooster(BoosterIntent.ForTimeFreeze(intent.Generation, 10f)), Is.False,
                "a second freeze is refused while one runs");
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));

            m_fixture.TickIdle(6.25f);

            Assert.That(clock.Remaining, Is.EqualTo(2.75f).Within(1e-4f), "only the leftover 0.25 s reached the main clock");
            Assert.That(clock.IsFrozen, Is.False);
            Assert.That(m_fixture.Session.FreezeDisplayedSeconds.Value, Is.Zero);
            Assert.That(m_fixture.Session.IsFrozen.Value, Is.False);
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(3), "2.75 rounds up to 3");
        }

        [Test]
        public void PauseHoldsBothClocksAndRestartResetsTheFreeze()
        {
            AttemptCountdown clock = new AttemptCountdown();
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], clock: clock);
            m_fixture.PrepareAndPlay();
            m_fixture.StartTimer(0f, 1f);
            BoosterIntent intent = BoosterIntent.ForTimeFreeze(m_fixture.Session.Attempt.Generation, 10f);
            m_fixture.Session.TrySubmitBooster(intent);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle(2f);
            m_fixture.Session.Pause(PauseReason.User);

            m_fixture.TickIdle(5f);
            Assert.That(clock.FreezeRemaining, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(clock.Remaining, Is.EqualTo(BoardFixture.k_timerSeconds));

            Assert.That(m_fixture.Session.Restart(), Is.True);

            Assert.That(clock.IsFrozen, Is.False);
            Assert.That(m_fixture.Session.IsFrozen.Value, Is.False);
            Assert.That(m_fixture.Session.FreezeDisplayedSeconds.Value, Is.Zero);
            Assert.That(m_fixture.Session.DisplayedSeconds.Value, Is.EqualTo(BoardFixture.k_timerSeconds));
        }

        [Test]
        public void BoardRevisionCountsViewSyncsWithBoardEvents()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            Assert.That(m_fixture.Session.BoardRevision.Value, Is.Zero);

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 1f);
            Assert.That(m_fixture.Session.BoardRevision.Value, Is.Zero, "a held preview is not a board event");
            m_fixture.Tick(PointerPhase.Up, 1f, 1f);
            Assert.That(m_fixture.Session.BoardRevision.Value, Is.EqualTo(1));
            m_fixture.TickIdle();
            Assert.That(m_fixture.Session.BoardRevision.Value, Is.EqualTo(1));
        }

        [Test]
        public void TheHomeBackgroundLatchFollowsTheAppOnHomeOnly()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0]);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out _), Is.True);

            m_fixture.Session.NotifyBackground(true);
            Assert.That(m_fixture.Session.IsHomeBackgrounded.Value, Is.True);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Home));

            m_fixture.Session.NotifyBackground(false);
            Assert.That(m_fixture.Session.IsHomeBackgrounded.Value, Is.False);

            m_fixture.Session.Play();
            m_fixture.Session.NotifyBackground(true);
            Assert.That(m_fixture.Session.IsHomeBackgrounded.Value, Is.False, "a Playing background pauses instead");
        }

        private BoosterIntent Hammer(int block)
        {
            IBoardReader board = m_fixture.Session.Attempt.Simulation.Board;
            return BoosterIntent.ForBoard(m_fixture.Session.Attempt.Generation, BoosterId.Hammer,
                GameCommand.RemoveBlock(block, board.GetOrigin(block)));
        }
    }
}
