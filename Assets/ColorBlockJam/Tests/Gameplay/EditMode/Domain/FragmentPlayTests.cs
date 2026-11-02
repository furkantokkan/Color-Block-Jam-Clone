using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The parts of a Rocket split play as independent blocks: their own cells decide moves,
    /// collisions, exits, picks and later boosters, never the authored footprint (B-3, AC-6).
    /// </summary>
    public sealed class FragmentPlayTests
    {
        // After Rocket removes the bridge (0,0), block 0 is the I2 (0,2)-(0,1) and part 1 is (1,0),
        // both still at the L's origin (0,0). Each fits a one-lane door the whole L never could.
        private static readonly string[] s_l =
        {
            "....",
            "A...",
            "A...",
            "AA.."
        };

        private static readonly DoorSpec[] s_doors =
        {
            BoardFixture.Door(Side.Top, 0, 1, 'A'),
            BoardFixture.Door(Side.Bottom, 1, 1, 'A')
        };

        private static readonly Cell s_origin = new Cell(0, 0);

        [Test]
        public void PartsMoveAndCollideByTheirOwnCells()
        {
            BoardSimulation sim = CreateSplit(out RecordingListener _);

            // The whole L would need (1,0), where part 1 stands; the I2 does not.
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, s_origin, new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0), Is.EqualTo(new[] { new Cell(1, 2), new Cell(1, 1) }));
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(1, 0), new Cell(1, -1))),
                Is.EqualTo(CommandOutcome.Unreachable), "part 1 blocks the step down");

            Assert.That(sim.Commands.Execute(GameCommand.Move(1, s_origin, new Cell(2, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 1), Is.EqualTo(new[] { new Cell(3, 0) }));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(1, 0)), "moving one part leaves the other");

            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(1, 0), new Cell(1, -1))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0), Is.EqualTo(new[] { new Cell(1, 1), new Cell(1, 0) }));
        }

        [Test]
        public void EachPartDragsAndExitsThroughItsOwnLane()
        {
            BoardSimulation sim = CreateSplit(out RecordingListener listener);

            // Press part 1's cell and push 0.45 below the board: the drag resolver reads its one-cell footprint.
            Assert.That(sim.Drag.TryBegin(1, new BoardPoint(1f, 0f)), Is.True);
            Assert.That(sim.Drag.Update(new BoardPoint(1f, -0.45f), out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(exit, Is.EqualTo(GameCommand.MoveAndExit(1, s_origin, s_origin, Side.Bottom)));
            Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            sim.Drag.TryEnd(out GameCommand _);

            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Block, Is.EqualTo(1));

            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, s_origin, new Cell(0, 1), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied), "the I2 is one lane wide, so the one-lane Top door fits it");
            Assert.That(sim.IsCleared, Is.True);
        }

        [Test]
        public void PartsArePickedByTheirOwnCells()
        {
            BoardSimulation sim = CreateSplit(out RecordingListener _);
            FakeFootprintProjector projector = FakeFootprintProjector.CreateOrthographic();

            Assert.That(sim.Picker.Pick(new ScreenPoint(100f, 0f), sim.Board, projector), Is.EqualTo(1));
            Assert.That(sim.Picker.Pick(new ScreenPoint(0f, 100f), sim.Board, projector), Is.EqualTo(0));

            // The removed cell is nobody's: 0.4 cell right of its centre, part 1 is nearest.
            Assert.That(sim.Picker.Pick(new ScreenPoint(40f, 0f), sim.Board, projector), Is.EqualTo(1));

            // Its centre is equally near both, and the tie goes to the lower ID.
            Assert.That(sim.Picker.Pick(new ScreenPoint(0f, 0f), sim.Board, projector), Is.EqualTo(0));
        }

        [Test]
        public void LaterBoostersTargetEachPartAlone()
        {
            BoardSimulation sim = CreateSplit(out RecordingListener listener);

            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(1, s_origin)), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(listener.Removed.Count, Is.EqualTo(1));

            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, s_origin, new Cell(0, 2))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.BlockCount, Is.EqualTo(2), "an end cell creates no part");
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0), Is.EqualTo(new[] { new Cell(0, 1) }));
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, s_origin, new Cell(0, 1))),
                Is.EqualTo(CommandOutcome.FinalCell), "Rocket never removes the last cell");
        }

        private static BoardSimulation CreateSplit(out RecordingListener listener)
        {
            listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_l, s_doors);
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, s_origin, new Cell(0, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.BlockCount, Is.EqualTo(2));
            sim.Events.Clear();
            return sim;
        }
    }
}
