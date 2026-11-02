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
    public sealed class ExitCommitTests
    {
        private static readonly string[] s_topMiddle =
        {
            ".A.",
            "...",
            "..."
        };

        private static readonly string[] s_topRightCorner =
        {
            "..A",
            "...",
            "..."
        };

        [Test]
        public void MovingIntoContactWithAFittingDoorCommitsTheExitWithoutAPush()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(),
                new[] { "...", ".A.", "..." }, BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            // The pointer stops exactly on the flush cell: no outward push at all (DEC-164).
            DragResult result = DragTo(sim, 1f, 2f);

            Assert.That(result, Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Board.GetOccupant(new Cell(1, 2)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(0));
            Assert.That(sim.IsCleared, Is.True);
            Assert.That(sim.Drag.IsConsumed, Is.True);

            Assert.That(sim.Events.Count, Is.EqualTo(1));
            ref readonly BoardEvent logged = ref sim.Events.GetAt(0);
            Assert.That(logged.Kind, Is.EqualTo(BoardEventKind.Exited));
            Assert.That(logged.Block, Is.EqualTo(0));
            Assert.That(logged.Door, Is.EqualTo(0));
            Assert.That(logged.Side, Is.EqualTo(Side.Top));
            Assert.That(logged.From, Is.EqualTo(new Cell(1, 2)));

            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Outcome, Is.EqualTo(ExitOutcome.Leave));
            Assert.That(listener.Exited[0].Origin, Is.EqualTo(new Cell(1, 2)));
        }

        [TestCase(0f)]
        [TestCase(0.2f)]
        public void AHeldBlockTouchingItsDoorExitsAndCannotBePulledBack(float lean)
        {
            // The user's 2026-09-28 report: a block flush under its door, leaning in less than the old
            // 0.3 cell push, could be dragged back. Contact now exits on the first update (DEC-164).
            BoardSimulation sim = CreateTopDoor(null);
            Begin(sim, 0);

            Assert.That(DragTo(sim, 1f, 2f + lean), Is.EqualTo(DragResult.ExitReady));
            Assert.That(DragTo(sim, 1f, 0f), Is.EqualTo(DragResult.Unchanged), "The consumed press owns nothing more.");
            Release(sim);

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void SlidingAlongTheSideExitsOnceTheCellIsUnderTheDoorEvenWhenDrawnBesideIt()
        {
            // The user's 2026-09-29 report: a block flush under its gate stayed there while the finger rested
            // 0.4 cell beside it, and went in only after a release and a new press. A sideways lead no longer
            // holds it back (DEC-197); the view slides it into line on the way out.
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(),
                new[] { "A...", "....", "...." }, BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, 0.4f, 2f), Is.EqualTo(DragResult.Unchanged), "the cell is still beside the door");
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(DragTo(sim, 0.6f, 2f), Is.EqualTo(DragResult.ExitReady), "the cell is under the door, drawn 0.4 beside it");
            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Origin, Is.EqualTo(new Cell(1, 2)));
            Assert.That(listener.Exited[0].Side, Is.EqualTo(Side.Top));
        }

        [Test]
        public void ABlockCanNeverRestInFrontOfItsOwnGate()
        {
            // The user's 2026-09-29 order: a block of the gate's colour brought in front of it leaves at once;
            // there is no distance at which it can be put down flush against that gate (DEC-197).
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "...", ".A.", "..." },
                BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, 1f, 1.4f), Is.EqualTo(DragResult.Unchanged), "one cell below the gate, not touching it");
            Assert.That(DragTo(sim, 1f, 1.5f), Is.EqualTo(DragResult.ExitReady), "the step that touches the gate exits");
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Drag.TryEnd(out GameCommand _), Is.False, "nothing is left to put down");
        }

        [Test]
        public void PushingIntoTheWallBesideTheDoorStillExitsBecauseTheBlockIsDrawnOnItsCell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_topRightCorner, BoardFixture.Door(Side.Top, 2, 1, 'A'));
            Begin(sim, 0);

            // The pointer is 0.6 cell into the right wall; the block cannot lead there, so it lines up.
            Assert.That(DragTo(sim, 2.6f, 2f), Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
        }

        [Test]
        public void ANarrowOrWrongColourDoorIsNeverAnExitOnContact()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "AA.", "...", "..." },
                BoardFixture.Door(Side.Top, 0, 1, 'A'),
                BoardFixture.Door(Side.Left, 2, 1, 'B'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, 0f, 2.4f), Is.EqualTo(DragResult.Unchanged));
            Release(sim);

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Events.Count, Is.EqualTo(0));
        }

        [Test]
        public void AFastPointerReachesTheDoorAndExitsInOneUpdate()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "...", "...", ".A." },
                BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, 1f, 2.6f), Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
        }

        [Test]
        public void ABlockExitsExactlyOnce()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = CreateTopDoor(listener);
            Begin(sim, 0);
            DragTo(sim, 1f, 2.4f);

            Assert.That(DragTo(sim, 1f, 2.9f), Is.EqualTo(DragResult.Unchanged));
            Release(sim);

            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Total, Is.EqualTo(1));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
        }

        [TestCase(2.35f, 2.6f, Side.Top, 0)]
        [TestCase(2.6f, 2.35f, Side.Right, 1)]
        [TestCase(2.5f, 2.5f, Side.Right, 1)]
        public void ACornerBlockBesideTwoFittingDoorsLeavesOnceThroughTheLargerPush(float x, float y, Side side, int door)
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(),
                s_topRightCorner,
                BoardFixture.Door(Side.Top, 2, 1, 'A'),
                BoardFixture.Door(Side.Right, 2, 1, 'A'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, x, y), Is.EqualTo(DragResult.ExitReady));

            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Side, Is.EqualTo(side));
            Assert.That(listener.Exited[0].Door, Is.EqualTo(door));
        }

        [Test]
        public void AnExitedBlockCannotBePickedOrBegun()
        {
            BoardSimulation sim = CreateTopDoor(null);
            Begin(sim, 0);
            DragTo(sim, 1f, 2.4f);
            Release(sim);

            FakeFootprintProjector projector = FakeFootprintProjector.CreateOrthographic();
            int picked = sim.Picker.Pick(projector.Project(1f, 2f), sim.Board, projector);

            Assert.That(picked, Is.EqualTo(-1));
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(1f, 2f)), Is.False);
        }

        [Test]
        public void AStayOutcomeKeepsTheBlockOnTheBoard()
        {
            RecordingListener listener = new RecordingListener();
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new StayExitGate()).Add(listener).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            Assert.That(DragTo(sim, 1f, 2.4f), Is.EqualTo(DragResult.ExitReady));

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Outcome, Is.EqualTo(ExitOutcome.Stay));
            Assert.That(sim.Drag.IsConsumed, Is.True);
        }

        private static BoardSimulation CreateTopDoor(RecordingListener listener)
        {
            AttemptHooksBuilder builder = new AttemptHooksBuilder();
            if (listener != null)
            {
                builder.Add(listener);
            }

            return BoardFixture.CreateSimulation(builder.Build(), s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'A'));
        }

        private static void Begin(BoardSimulation sim, int block)
        {
            BoardPoint pointer = BoardPoint.FromCell(sim.Board.GetOrigin(block));
            Assert.That(sim.Drag.TryBegin(block, pointer), Is.True);
        }

        // Runs a contact exit's command at once, as the pointer router does.
        private static DragResult DragTo(BoardSimulation sim, float x, float y)
        {
            DragResult result = sim.Drag.Update(new BoardPoint(x, y), out GameCommand exit);
            if (result == DragResult.ExitReady)
            {
                Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            }

            return result;
        }

        private static void Release(BoardSimulation sim)
        {
            if (sim.Drag.TryEnd(out GameCommand move))
            {
                Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));
            }
        }
    }
}
