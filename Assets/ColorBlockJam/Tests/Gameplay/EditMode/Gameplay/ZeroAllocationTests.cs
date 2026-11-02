using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The Playing tick path allocates nothing in steady state (NFR-1, NFR-P2, V-E23). Each call runs
    /// once before the measurement, so one-time JIT and cache work is excluded. Editor Mono only:
    /// the device figure (V-M5) is measured with the Profiler.
    /// </summary>
    public sealed class ZeroAllocationTests
    {
        private static readonly string[] s_lane =
        {
            "......",
            "A.....",
            "......"
        };

        private static readonly string[] s_exitMap =
        {
            "...",
            ".A.",
            "..."
        };

        private static readonly DoorSpec[] s_exitDoor = { BoardFixture.Door(Side.Top, 1, 1, 'A') };

        // Removing the L's bridge (0,0) splits it into two blocks.
        private static readonly string[] s_l =
        {
            "A..",
            "A..",
            "AA."
        };

        // 'A' and 'a' share a colour.
        private static readonly string[] s_colours =
        {
            "AAA.",
            "....",
            "B..a"
        };

        // The first allocation assertion in a domain also measures the GC.Alloc recorder's own setup,
        // so a filtered run that starts here would fail. One empty measurement makes the tests
        // independent of which suite ran before them.
        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void SteadyDragUpdateAllocatesNothing()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            sim.Drag.TryBegin(0, new BoardPoint(0f, 1f));
            DragBackAndForth(sim);

            Assert.That(() => DragBackAndForth(sim), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void ExecutingMoveCommandsAllocatesNothing()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            MoveThereAndBack(sim);

            Assert.That(() => MoveThereAndBack(sim), Is.Not.AllocatingGCMemory());
            Assert.That(sim.Commands.Log.Count, Is.EqualTo(4));
        }

        [Test]
        public void ExecutingAnExitCommandAllocatesNothing()
        {
            GameCommand exit = GameCommand.MoveAndExit(0, new Cell(1, 1), new Cell(1, 2), Side.Top);
            BoardSimulation warm = BoardFixture.CreateSimulation(s_exitMap, s_exitDoor);
            warm.Commands.Execute(exit);
            BoardSimulation sim = BoardFixture.CreateSimulation(s_exitMap, s_exitDoor);

            Assert.That(() => { sim.Commands.Execute(exit); }, Is.Not.AllocatingGCMemory());
            Assert.That(sim.IsCleared, Is.True);
        }

        [Test]
        public void ExecutingARocketSplitAllocatesNothing()
        {
            // Ice hears the split too, so its part inheritance is measured with the handler.
            GameCommand rocket = GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0));
            BoardSimulation warm = CreateWithIce(s_l);
            warm.Commands.Execute(rocket);
            BoardSimulation sim = CreateWithIce(s_l);

            Assert.That(() =>
            {
                sim.Events.Clear();
                sim.Commands.Execute(rocket);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sim.Board.BlockCount, Is.EqualTo(2));
        }

        [Test]
        public void ExecutingAHammerRemovalAllocatesNothing()
        {
            GameCommand hammer = GameCommand.RemoveBlock(0, new Cell(0, 2));
            BoardSimulation warm = CreateWithIce(s_colours);
            warm.Commands.Execute(hammer);
            BoardSimulation sim = CreateWithIce(s_colours);

            Assert.That(() =>
            {
                sim.Events.Clear();
                sim.Commands.Execute(hammer);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Removed));
        }

        [Test]
        public void ExecutingAVacuumRemovalAllocatesNothing()
        {
            GameCommand vacuum = GameCommand.RemoveColor(2, new Cell(3, 0), BoardFixture.ColorOf('a'));
            BoardSimulation warm = CreateWithIce(s_colours);
            warm.Commands.Execute(vacuum);
            BoardSimulation sim = CreateWithIce(s_colours);

            Assert.That(() =>
            {
                sim.Events.Clear();
                sim.Commands.Execute(vacuum);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
        }

        [Test]
        public void ACountdownTickWithAFreezeAllocatesNothing()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(60);
            countdown.TryFreeze(10f);
            TickClock(countdown);

            Assert.That(() => TickClock(countdown), Is.Not.AllocatingGCMemory());
            Assert.That(countdown.IsFrozen, Is.True);
        }

        [Test]
        public void ASessionTickThatReleasesAMoveAllocatesNothing()
        {
            using (SessionFixture fixture = new SessionFixture(s_lane, new DoorSpec[0]))
            {
                fixture.PrepareAndPlay();
                DragAndReleaseThereAndBack(fixture);
                fixture.Journal.Clear();

                Assert.That(() => DragAndReleaseThereAndBack(fixture), Is.Not.AllocatingGCMemory());
                Assert.That(fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(4));
            }
        }

        [Test]
        public void ASessionTickThatPushesABlockOutAllocatesNothing()
        {
            // A Stay gate keeps the block on the board, so the same push-out repeats without reaching Won.
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(new StayExitGate()).Build());
            using (SessionFixture fixture = new SessionFixture(s_exitMap, s_exitDoor, hookSource: hooks))
            {
                fixture.PrepareAndPlay();
                PushOutThroughTheTop(fixture);
                fixture.Journal.Clear();

                Assert.That(() => PushOutThroughTheTop(fixture), Is.Not.AllocatingGCMemory());

                CommandLog log = fixture.Session.Attempt.Simulation.Commands.Log;
                Assert.That(log.Count, Is.EqualTo(2), "each push-out ran one command in the tick");
                Assert.That(log.GetAt(1).TryGetExit(out Side side) && side == Side.Top, Is.True);
                Assert.That(fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
            }
        }

        [Test]
        public void ASessionTickDraggingAnArrowBlockDiagonallyAllocatesNothing()
        {
            // The Game scene's hook source over the Arrow module binds the axis constraint and exit gate (BLK-01).
            using (SessionFixture fixture = ArrowSessionTests.Create(s_lane, new DoorSpec[0], LevelEffectAxis.Horizontal))
            {
                fixture.PrepareAndPlay();
                DragDiagonallyThereAndBack(fixture);
                fixture.Journal.Clear();

                Assert.That(() => DragDiagonallyThereAndBack(fixture), Is.Not.AllocatingGCMemory());
                Assert.That(fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "The Arrow block kept its row.");
                Assert.That(fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(4));
            }
        }

        [Test]
        public void ASessionTickThatPushesAnArrowBlockOutAllocatesNothing()
        {
            // The Arrow hooks as the module builds them, plus a Stay gate so the push-out repeats without Won.
            AxisMask[] axes = { AxisMask.Vertical };
            FixedHookSource hooks = new FixedHookSource(new AttemptHooksBuilder().Add(new ArrowAxisConstraint(axes))
                .Add(new ArrowExitGate(axes)).Add(new StayExitGate()).Build());
            using (SessionFixture fixture = new SessionFixture(s_exitMap, s_exitDoor, hookSource: hooks))
            {
                fixture.PrepareAndPlay();
                PushOutThroughTheTop(fixture);
                fixture.Journal.Clear();

                Assert.That(() => PushOutThroughTheTop(fixture), Is.Not.AllocatingGCMemory());
                Assert.That(fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void ASessionTickThatRunsASourceCommandAllocatesNothing()
        {
            using (SessionFixture fixture = new SessionFixture(s_lane, new DoorSpec[0]))
            {
                fixture.PrepareAndPlay();
                GameCommand there = GameCommand.Move(0, new Cell(0, 1), new Cell(2, 1));
                GameCommand back = GameCommand.Move(0, new Cell(2, 1), new Cell(0, 1));
                ReplayCommandSource source = new ReplayCommandSource(new[] { there, back, there, back });
                fixture.Session.TrySetCommandSource(source);
                fixture.TickIdle();
                fixture.TickIdle();
                fixture.Journal.Clear();

                Assert.That(() =>
                {
                    fixture.TickIdle();
                    fixture.TickIdle();
                }, Is.Not.AllocatingGCMemory());
                Assert.That(source.Position, Is.EqualTo(4));
            }
        }

        [Test]
        public void ExitEvaluationAllocatesNothing()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_exitMap, s_exitDoor);
            EvaluateExit(sim);

            Assert.That(() => EvaluateExit(sim), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void FollowOffsetAllocatesNothing()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            ComputeOffset(sim);

            Assert.That(() => ComputeOffset(sim), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void PickingAllocatesNothing()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            FakeFootprintProjector projector = FakeFootprintProjector.CreateOrthographic();
            ScreenPoint press = new ScreenPoint(250f, 180f);
            sim.Picker.Pick(press, sim.Board, projector);

            // A statement lambda: NUnit measures a TestDelegate, not the returned block index.
            Assert.That(() => { sim.Picker.Pick(press, sim.Board, projector); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void PublishingToAListenerAllocatesNothing()
        {
            CountingListener listener = new CountingListener();
            AttemptHooks hooks = new AttemptHooksBuilder().Add(listener).Build();
            Publish(hooks);

            Assert.That(() => Publish(hooks), Is.Not.AllocatingGCMemory());
            Assert.That(listener.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ASessionTickDuringAHeldDragAllocatesNothing()
        {
            using (SessionFixture fixture = new SessionFixture(s_lane, new DoorSpec[0]))
            {
                fixture.PrepareAndPlay();
                fixture.Tick(PointerPhase.Down, 0f, 1f);
                HoldBackAndForth(fixture);
                fixture.Journal.Clear();

                Assert.That(() => HoldBackAndForth(fixture), Is.Not.AllocatingGCMemory());
                Assert.That(fixture.Presenter.SyncCount, Is.GreaterThan(2));
            }
        }

        [Test]
        public void ASessionTickThatTapsARejectedRocketTargetAllocatesNothing()
        {
            // A Rocket tap on a one-cell block runs the router's targeting branch, the coordinator's target
            // rules and the blocked feedback, and keeps the mode, so the same tap repeats.
            using (SessionFixture fixture = new SessionFixture(s_lane, new DoorSpec[0], withTargeting: true))
            using (BoosterFixture boosters = new BoosterFixture())
            {
                BoosterUseCoordinator coordinator = boosters.CreateCoordinator(fixture);
                fixture.PrepareAndPlay();
                coordinator.Press(BoosterId.Rocket);
                fixture.Tap(0f, 1f);
                fixture.Journal.Clear();

                Assert.That(() => fixture.Tap(0f, 1f), Is.Not.AllocatingGCMemory());
                Assert.That(fixture.Targeting.Mode.Value, NUnit.Framework.Is.EqualTo(BoosterId.Rocket));
                Assert.That(fixture.Presenter.BlockedDragCount, NUnit.Framework.Is.EqualTo(2));
            }
        }

        [Test]
        public void ASessionTickWhileTimeFreezeRunsAllocatesNothing()
        {
            AttemptCountdown clock = new AttemptCountdown();
            using (SessionFixture fixture = new SessionFixture(s_lane, new DoorSpec[0], clock: clock))
            {
                fixture.PrepareAndPlay();
                fixture.StartTimer(0f, 1f);
                BoosterIntent intent = BoosterIntent.ForTimeFreeze(fixture.Session.Attempt.Generation, 10f);
                fixture.Session.TrySubmitBooster(intent);
                fixture.Session.CompleteBoosterCommit(intent.Generation, true);
                fixture.TickIdle(0.4f);
                fixture.Journal.Clear();

                // Two ticks cross a whole freeze second, so the HUD counter publishes a new value inside the measurement.
                Assert.That(() =>
                {
                    fixture.TickIdle(0.4f);
                    fixture.TickIdle(0.4f);
                }, Is.Not.AllocatingGCMemory());
                Assert.That(clock.IsFrozen, NUnit.Framework.Is.True);
                Assert.That(fixture.Session.FreezeDisplayedSeconds.Value, NUnit.Framework.Is.EqualTo(9));
            }
        }

        // Each simulation gets its own Ice rules, a listener that allocates nothing, sized for every block ID.
        private static BoardSimulation CreateWithIce(string[] rows)
        {
            LevelModel model = BoardFixture.Create(rows);
            IceRules ice = new IceRules(new int[model.BlockCount], model.TotalCellCount);
            return new BoardSimulation(model, new AttemptHooksBuilder().Add((IBoardEventListener)ice).Build(),
                DragSettings.Default);
        }

        // One frame of the timer phase: both clocks advance and the HUD reads their whole seconds.
        private static void TickClock(AttemptCountdown countdown)
        {
            countdown.Tick(1f / 60f);
            DisplaySeconds.Calculate(countdown.FreezeRemaining);
            DisplaySeconds.Calculate(countdown.Remaining);
        }

        private static void DragBackAndForth(BoardSimulation sim)
        {
            sim.Drag.Update(new BoardPoint(2f, 1f), out GameCommand _);
            sim.Drag.Update(new BoardPoint(0f, 1f), out GameCommand _);
        }

        // Each command raises one event, so the tick's log is cleared as the session does every tick.
        private static void MoveThereAndBack(BoardSimulation sim)
        {
            sim.Events.Clear();
            sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 1), new Cell(2, 1)));
            sim.Commands.Execute(GameCommand.Move(0, new Cell(2, 1), new Cell(0, 1)));
        }

        // Presses the block where it touches the top door and holds: the Held tick runs the exit command (DEC-164).
        private static void PushOutThroughTheTop(SessionFixture fixture)
        {
            Cell origin = fixture.GetOrigin(0);
            fixture.Tick(PointerPhase.Down, origin.X, origin.Y);
            fixture.Tick(PointerPhase.Held, origin.X, 2.4f);
            fixture.Tick(PointerPhase.Up, origin.X, 2.4f);
        }

        private static void DragAndReleaseThereAndBack(SessionFixture fixture)
        {
            fixture.Tick(PointerPhase.Down, 0f, 1f);
            fixture.Tick(PointerPhase.Held, 2f, 1f);
            fixture.Tick(PointerPhase.Up, 2f, 1f);
            fixture.Tick(PointerPhase.Down, 2f, 1f);
            fixture.Tick(PointerPhase.Held, 0f, 1f);
            fixture.Tick(PointerPhase.Up, 0f, 1f);
        }

        // A Horizontal Arrow block dragged toward a diagonal point keeps its row: two on-axis moves per call.
        private static void DragDiagonallyThereAndBack(SessionFixture fixture)
        {
            fixture.Tick(PointerPhase.Down, 0f, 1f);
            fixture.Tick(PointerPhase.Held, 2f, 2f);
            fixture.Tick(PointerPhase.Up, 2f, 2f);
            fixture.Tick(PointerPhase.Down, 2f, 1f);
            fixture.Tick(PointerPhase.Held, 0f, 0f);
            fixture.Tick(PointerPhase.Up, 0f, 0f);
        }

        private static void EvaluateExit(BoardSimulation sim)
        {
            ExitRules.Evaluate(sim.Board, sim.Hooks, 0, new Cell(1, 2), Side.Top, out ExitCandidate _);
            ExitRules.Evaluate(sim.Board, sim.Hooks, 0, new Cell(1, 1), Side.Left, out ExitCandidate _);
        }

        private static void ComputeOffset(BoardSimulation sim)
        {
            FollowOffset.Compute(sim.Board, sim.Hooks, 0, new Cell(0, 1), new BoardPoint(0.4f, 1.3f), AxisMask.Both, 0.5f);
        }

        private static void Publish(AttemptHooks hooks)
        {
            hooks.Publish(new BlockExitedEvent(0, 0, Side.Top, new Cell(1, 2), ExitOutcome.Leave));
            hooks.Publish(new MoveCommittedEvent(0, new Cell(0, 0), new Cell(1, 0)));
        }

        private static void HoldBackAndForth(SessionFixture fixture)
        {
            fixture.Tick(PointerPhase.Held, 2f, 1f);
            fixture.Tick(PointerPhase.Held, 0.2f, 1.1f);
        }
    }
}
