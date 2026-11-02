#if UNITY_EDITOR
using System;
using System.Collections;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the presenter drives the matching environment gate around a valid exit
    /// (environment-runs-gate-motion AC-2 to AC-5) with the real environment and shape prefabs: a held
    /// block touching its door exits at once without leaning in (DEC-164), an exit waits inside its own
    /// time until the gate is down, fragments start with the crossing, the gate stays down while any exit
    /// through it runs and rises after the last one inside the Won Finish bound, moves and Stay outcomes
    /// move nothing, Suspend holds everything, Teardown resets it and the held and exit syncs allocate
    /// nothing once warm.
    /// </summary>
    public sealed class BoardPresenterGateTests
    {
        // DEC-93: LevelSession caps the Won Finish at 0.8 foreground seconds.
        private const float k_wonFinishSeconds = 0.8f;
        private const float k_margin = 0.1f;
        private const float k_leanCells = 0.2f;

        // The exit pointer of BoardPresenterLifecycleTests: 0.4 cell above the exiting block.
        private static readonly BoardPoint s_pushOut =
            new BoardPoint(PresenterScene.ExitBlockOrigin.X, PresenterScene.ExitBlockOrigin.Y + 0.4f);

        // A slight lean into the door, under the retired 0.3 cell push (DEC-111): contact exits anyway (DEC-164).
        private static readonly BoardPoint s_lean =
            new BoardPoint(PresenterScene.ExitBlockOrigin.X, PresenterScene.ExitBlockOrigin.Y + k_leanCells);

        private PresenterScene m_scene;

        // The first allocation assertion in a domain also measures the recorder's own setup.
        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [SetUp]
        public void SetUp()
        {
            m_scene = new PresenterScene();
        }

        [TearDown]
        public void TearDown()
        {
            m_scene.Dispose();
        }

        [Test]
        public void TheLongestExitAndItsGateRiseFitTheWonFinishBound()
        {
            // The gate's sink wait is taken out of the exit's own time, so only the rise adds to it.
            Assert.That(ExitMotionSettings.k_maxSeconds + EnvironmentGate.k_riseSeconds, Is.LessThanOrEqualTo(k_wonFinishSeconds));
            Assert.That(EnvironmentGate.k_sinkSeconds, Is.LessThan(ExitMotionSettings.k_minSeconds), "the sink ends inside every exit");
        }

        [UnityTest]
        public IEnumerator AnExitFromARaisedGateWaitsForItsSinkThenCrossesAndTheGateRisesAfter()
        {
            BoardSimulation simulation = Build(PresenterScene.CreateLevel(withSecondBlock: true));
            BoardPresenter presenter = m_scene.Presenter;
            EnvironmentGate gate = m_scene.Environment.GetGate(0);
            Transform root = presenter.GetPiece(0).transform;
            Vector3 start = root.localPosition;

            // A command exit (replay, bot, or a flick that never drew a lean) starts at a raised gate.
            ExitBlock(simulation);
            presenter.Sync(simulation, s_pushOut);

            // The whole exit, its wait included, keeps its time; only the gate's rise follows it.
            Assert.That(gate.IsMoving, Is.True);
            Assert.That(presenter.FinishSeconds, Is.EqualTo(GetExitSeconds(2) + EnvironmentGate.k_riseSeconds).Within(1e-3f));
            Assert.That(presenter.FinishSeconds, Is.LessThanOrEqualTo(k_wonFinishSeconds));
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "fragments wait for the gate too");

            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds * 0.5f);
            Assert.That(Vector3.Distance(root.localPosition, start), Is.LessThan(1e-4f), "the block waits while the gate sinks");
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero);

            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds);
            Assert.That(presenter.ActiveExitFragmentCount, Is.EqualTo(1), "fragments start once the block moves");
            simulation.Events.Clear();
            float deadline = Time.realtimeSinceStartup + 2f;
            while (presenter.FinishSeconds > EnvironmentGate.k_riseSeconds + 0.02f && Time.realtimeSinceStartup < deadline)
            {
                AssertOffset(gate, EnvironmentGate.k_sinkDepth, "down while the body passes the door");
                yield return null;
                presenter.Sync(simulation, s_pushOut);
            }

            yield return new WaitForSeconds(EnvironmentGate.k_riseSeconds + k_margin);
            presenter.Sync(simulation, s_pushOut);
            Assert.That(presenter.GetPiece(0), Is.Null, "the body is gone");
            Assert.That(gate.IsMoving, Is.False);
            AssertOffset(gate, 0f, "back at its exact built height");
        }

        [Test]
        public void AMoveOrAStayExitMovesNoGate()
        {
            BoardSimulation simulation = Build(PresenterScene.CreateLevel(withSecondBlock: true));
            BoardPresenter presenter = m_scene.Presenter;
            EnvironmentGate gate = m_scene.Environment.GetGate(0);
            Assert.That(simulation.Commands.Execute(GameCommand.Move(1, new Cell(0, 0), new Cell(1, 0))),
                Is.EqualTo(CommandOutcome.Applied));
            presenter.Sync(simulation, default);
            Assert.That(gate.IsMoving, Is.False, "a move leaves the gate up");

            simulation.Events.Clear();
            simulation.Events.Add(BoardEvent.Exited(
                new BlockExitedEvent(0, 0, Side.Top, PresenterScene.ExitBlockOrigin, ExitOutcome.Stay)));
            presenter.Sync(simulation, default);
            Assert.That(gate.IsMoving, Is.False, "a Stay outcome keeps the block and the gate");
            Assert.That(presenter.FinishSeconds, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AHeldBlockTouchingItsDoorExitsAtOnceAndWaitsForTheGateInsteadOfLeaning()
        {
            BoardSimulation simulation = Build(PresenterScene.CreateLevel(withSecondBlock: true));
            BoardPresenter presenter = m_scene.Presenter;
            EnvironmentGate gate = m_scene.Environment.GetGate(0);
            Transform root = presenter.GetPiece(0).transform;
            Vector3 start = root.localPosition;

            // The user's 2026-09-28 report: a slight lean into the door exits on contact (DEC-164), so the
            // held block can no longer rest leaning into a gate and be pulled back.
            Assert.That(simulation.Drag.TryBegin(0, BoardPoint.FromCell(PresenterScene.ExitBlockOrigin)), Is.True);
            presenter.SetHeld(0);
            Assert.That(simulation.Drag.Update(s_lean, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            presenter.SetHeld(-1);
            presenter.Sync(simulation, s_lean);

            Assert.That(gate.IsMoving, Is.True, "the exit starts the sink");
            Assert.That(presenter.FinishSeconds, Is.EqualTo(GetExitSeconds(2) + EnvironmentGate.k_riseSeconds).Within(1e-3f));
            Assert.That(Vector3.Distance(root.localPosition, start), Is.LessThan(1e-4f), "no lean into the raised gate");

            // The command phase's events were read once, as the session clears them after each tick.
            simulation.Events.Clear();
            yield return new WaitForSeconds(GetExitSeconds(2) + EnvironmentGate.k_riseSeconds + k_margin);
            presenter.Sync(simulation, s_lean);
            Assert.That(presenter.GetPiece(0), Is.Null, "the body is gone");
            AssertOffset(gate, 0f, "up after the exit");
        }

        [UnityTest]
        public IEnumerator AHeldBlockDrawnBesideItsCellLinesUpBeforeItCrossesTheGate()
        {
            // The user's second 2026-09-28 report: the exit cut diagonally to the gate's centre (DEC-164). A block
            // drawn beside its cell slides into line while the gate sinks, then leaves straight out (DEC-197).
            BoardSimulation simulation = Build(PresenterScene.CreateLevel(withSecondBlock: true));
            BoardPresenter presenter = m_scene.Presenter;
            Transform root = presenter.GetPiece(0).transform;
            float cellX = root.localPosition.x;
            float cellZ = root.localPosition.z;
            BoardPoint beside = new BoardPoint(PresenterScene.ExitBlockOrigin.X + 0.15f, PresenterScene.ExitBlockOrigin.Y);

            Assert.That(simulation.Drag.TryBegin(0, BoardPoint.FromCell(PresenterScene.ExitBlockOrigin)), Is.True);
            presenter.SetHeld(0);
            presenter.Sync(simulation, beside);
            float drawnX = root.localPosition.x;
            Assert.That(drawnX - cellX, Is.EqualTo(0.15f).Within(1e-3f), "drawn beside its cell toward the free neighbour");

            Assert.That(simulation.Drag.Update(beside, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            presenter.SetHeld(-1);
            presenter.Sync(simulation, beside);
            simulation.Events.Clear();

            bool crossed = false;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (presenter.GetPiece(0) != null && Time.realtimeSinceStartup < deadline)
            {
                if (root.localPosition.z > cellZ + 0.5f)
                {
                    Assert.That(root.localPosition.x, Is.EqualTo(cellX).Within(1e-3f), "lined up before it crosses the gate");
                    crossed = true;
                }
                yield return null;
                presenter.Sync(simulation, beside);
            }

            Assert.That(crossed, Is.True, "it went out through the top door");
            Assert.That(presenter.GetPiece(0), Is.Null, "the body is gone");
        }

        [UnityTest]
        public IEnumerator AStaggeredShorterExitKeepsTheGateDownForTheEarlierLongerOne()
        {
            BoardSimulation simulation = Build(CreateSharedDoorLevel());
            BoardPresenter presenter = m_scene.Presenter;
            EnvironmentGate gate = m_scene.Environment.GetGate(0);
            float longest = GetExitSeconds(4);

            // Real domain exits in separate command phases: the triple (4 cells of travel) leaves first
            // and waits for the gate, then the single (2 cells) leaves 0.1 s later and ends first.
            ExitBlockAt(simulation, 0, new Cell(0, 1));
            presenter.Sync(simulation, default);
            simulation.Events.Clear();
            Assert.That(presenter.FinishSeconds, Is.EqualTo(longest + EnvironmentGate.k_riseSeconds).Within(1e-3f));
            Assert.That(presenter.FinishSeconds, Is.LessThanOrEqualTo(k_wonFinishSeconds));

            yield return new WaitForSeconds(0.1f);
            presenter.Sync(simulation, default);
            // Pointer up ends the consumed exit press, so the next press can start a drag.
            Assert.That(simulation.Drag.TryEnd(out _), Is.False, "an exit press ends with no move");
            ExitBlockAt(simulation, 1, new Cell(1, 3));
            presenter.Sync(simulation, default);
            simulation.Events.Clear();

            // The single ends about 0.34 s after the triple started; the triple still runs until 0.48 s.
            yield return new WaitForSeconds(GetExitSeconds(2) + 0.04f);
            presenter.Sync(simulation, default);
            AssertOffset(gate, EnvironmentGate.k_sinkDepth, "still down: the earlier, longer exit is still passing");

            yield return new WaitForSeconds(longest + EnvironmentGate.k_riseSeconds + k_margin - 0.38f);
            presenter.Sync(simulation, default);
            Assert.That(presenter.GetPiece(0), Is.Null);
            Assert.That(presenter.GetPiece(1), Is.Null);
            Assert.That(gate.IsMoving, Is.False);
            AssertOffset(gate, 0f, "up once the last exit through it ended");
        }

        [UnityTest]
        public IEnumerator SuspendHoldsTheGateWithItsBlockAndTeardownRestoresIt()
        {
            BoardSimulation simulation = Build(PresenterScene.CreateLevel(withSecondBlock: true));
            BoardPresenter presenter = m_scene.Presenter;
            EnvironmentGate gate = m_scene.Environment.GetGate(0);
            ExitBlock(simulation);
            presenter.Sync(simulation, s_pushOut);
            yield return null;

            // Suspend step 3, while the block still waits for its gate.
            presenter.SetTweensPaused(true);
            Assert.That(gate.IsPaused, Is.True);
            float held = gate.Offset;
            float remaining = presenter.FinishSeconds;
            yield return new WaitForSeconds(0.3f);
            AssertOffset(gate, held, "Suspend holds the gate");
            Assert.That(presenter.FinishSeconds, Is.EqualTo(remaining).Within(1e-5f), "and its block");
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "and the delayed fragment start");

            // Resume: the delayed start fires once the rest of the wait has passed.
            presenter.SetTweensPaused(false);
            Assert.That(gate.IsPaused, Is.False);
            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + 0.05f);
            Assert.That(presenter.ActiveExitFragmentCount, Is.EqualTo(1));

            // Teardown steps 3 and 6.
            presenter.StopTweens();
            Assert.That(gate.IsMoving, Is.False);
            presenter.ReturnPieces();
            AssertOffset(gate, 0f, "Teardown returns the gate to its built height");
            Assert.That(presenter.FinishSeconds, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TeardownBeforeTheFragmentCueFiresCannotStartFragmentsInTheNextAttempt()
        {
            AttemptLevel level = PresenterScene.CreateLevel(withSecondBlock: true);
            BoardSimulation simulation = Build(level);
            BoardPresenter presenter = m_scene.Presenter;
            ExitBlock(simulation);
            presenter.Sync(simulation, s_pushOut);
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "the cue is still waiting for the gate");

            presenter.StopTweens();
            presenter.ReturnPieces();
            Assert.That(presenter.GetPiece(0), Is.Null);
            AssertOffset(m_scene.Environment.GetGate(0), 0f, "teardown resets the gate");

            // Restart before the old callback's deadline: it must not affect the new root or particles.
            BoardSimulation restarted = Build(level);
            Transform root = presenter.GetPiece(0).transform;
            Vector3 home = root.localPosition;
            yield return new WaitForSeconds(EnvironmentGate.k_sinkSeconds + k_margin);
            presenter.Sync(restarted, default);
            Assert.That(presenter.ActiveExitFragmentCount, Is.Zero, "the old cue was stopped, not merely hidden");
            Assert.That(presenter.GetPiece(0).transform, Is.SameAs(root));
            Assert.That(Vector3.Distance(root.localPosition, home), Is.LessThan(1e-4f));
            Assert.That(presenter.FinishSeconds, Is.Zero);
            AssertOffset(m_scene.Environment.GetGate(0), 0f, "the new attempt leaves the gate closed");
        }

        [Test]
        public void HeldAndContactExitSyncsThatMoveAGateAfterRestartAllocateNothing()
        {
            // The first attempt warms every path (JIT, tween, sequence, delay and particle state); Restart
            // replays it: a held block, then its contact exit before the gate is down.
            AttemptLevel level = PresenterScene.CreateLevel(withSecondBlock: true);
            BoardSimulation simulation = Build(level);
            BoardPresenter presenter = m_scene.Presenter;
            HoldThenExitOnContact(presenter, simulation);
            presenter.StopTweens();
            presenter.ReturnPieces();
            BoardSimulation restarted = new BoardSimulation(level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(presenter.TryBuild(level, out string error), Is.True, error);
            presenter.Place(restarted.Preview);

            Assert.That(restarted.Drag.TryBegin(0, BoardPoint.FromCell(PresenterScene.ExitBlockOrigin)), Is.True);
            presenter.SetHeld(0);
            Assert.That(() => presenter.Sync(restarted, s_lean), Is.Not.AllocatingGCMemory());
            Assert.That(restarted.Drag.Update(s_lean, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(restarted.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            presenter.SetHeld(-1);
            Assert.That(() => presenter.Sync(restarted, s_lean), Is.Not.AllocatingGCMemory());
            Assert.That(m_scene.Environment.GetGate(0).IsMoving, Is.True);
        }

        private static void HoldThenExitOnContact(BoardPresenter presenter, BoardSimulation simulation)
        {
            Assert.That(simulation.Drag.TryBegin(0, BoardPoint.FromCell(PresenterScene.ExitBlockOrigin)), Is.True);
            presenter.SetHeld(0);
            presenter.Sync(simulation, s_lean);
            Assert.That(simulation.Drag.Update(s_lean, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            presenter.SetHeld(-1);
            presenter.Sync(simulation, s_lean);
            simulation.Events.Clear();
        }

        private BoardSimulation Build(AttemptLevel level)
        {
            m_scene.Environment.Build(level.Layout);
            BoardSimulation simulation = new BoardSimulation(level.Model, AttemptHooks.CreateEmpty(), DragSettings.Default);
            Assert.That(m_scene.Presenter.TryBuild(level, out string error), Is.True, error);
            m_scene.Presenter.Place(simulation.Preview);
            return simulation;
        }

        private static float GetExitSeconds(int travelCells)
        {
            return Mathf.Clamp(ExitMotionSettings.k_secondsPerCell * travelCells,
                ExitMotionSettings.k_minSeconds, ExitMotionSettings.k_maxSeconds);
        }

        // Drags block 0 upward into its door and runs the contact exit command, as the pointer router does.
        private static void ExitBlock(BoardSimulation simulation)
        {
            ExitBlockAt(simulation, 0, PresenterScene.ExitBlockOrigin);
        }

        // Presses a block at its origin cell and drags 0.4 cell up, into a top door it is flush with.
        private static void ExitBlockAt(BoardSimulation simulation, int block, Cell origin)
        {
            BoardPoint push = new BoardPoint(origin.X, origin.Y + 0.4f);
            Assert.That(simulation.Drag.TryBegin(block, BoardPoint.FromCell(origin)), Is.True);
            Assert.That(simulation.Drag.Update(push, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(simulation.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
        }

        // Gate pieces stand on BoardRoot at grid height 0, so a piece's local height is minus the offset.
        private static void AssertOffset(EnvironmentGate gate, float offset, string message)
        {
            Assert.That(gate.Offset, Is.EqualTo(offset).Within(1e-4f), message);
            for (int piece = 0; piece < gate.PieceCount; piece++)
            {
                Assert.That(gate.GetPiece(piece).transform.localPosition.y, Is.EqualTo(-offset).Within(1e-4f), message);
            }
        }

        // A 3 x 4 board with a red two-cell top door over columns 0 and 1: block 0 is a red vertical triple
        // in column 0 flush with the door and block 1 a red single at (1, 3), flush as well.
        private static AttemptLevel CreateSharedDoorLevel()
        {
            const int width = 3;
            const int height = 4;
            BlockColor red = new BlockColor((int)PaletteColor.Red);
            BlockDefinition[] blocks =
            {
                new BlockDefinition(0, red, new Cell(0, 1), new[] { new Cell(0, 0), new Cell(0, 1), new Cell(0, 2) }),
                new BlockDefinition(1, red, new Cell(1, 3), new[] { new Cell(0, 0) }),
            };

            BlockVisualSpec[] visuals =
            {
                new BlockVisualSpec(PaletteColor.Red,
                    new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2) }, new Vector2Int(0, 1)),
                new BlockVisualSpec(PaletteColor.Red, new[] { Vector2Int.zero }, new Vector2Int(1, 3)),
            };
            bool[] floor = new bool[width * height];
            for (int i = 0; i < floor.Length; i++)
            {
                floor[i] = true;
            }

            DoorDefinition[] doors = { new DoorDefinition(0, Side.Top, 0, 2, red) };
            if (!LevelModel.TryCreate(width, height, floor, doors, blocks, 60, out LevelModel model, out string error)
                || !BoardLayout.TryCreate(width, height, Array.Empty<Vector2Int>(),
                    new[] { new LevelDoorData("top", BoardSide.Top, 0, 2, "Red") }, out BoardLayout layout, out error))
            {
                throw new InvalidOperationException(error);
            }

            return new AttemptLevel(model, layout, "gate-test", visuals, new[] { "triple", "single" });
        }
    }
}
#endif
