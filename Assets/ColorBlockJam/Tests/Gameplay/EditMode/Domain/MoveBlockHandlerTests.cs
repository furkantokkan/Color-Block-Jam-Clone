using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>Drives MoveBlock commands through the attempt's executor, as every source does (DEC-122).</summary>
    public sealed class MoveBlockHandlerTests
    {
        // A can reach the Top door above its column; B sits in the bottom right corner.
        private static readonly string[] s_board =
        {
            "....",
            "A...",
            "...B"
        };

        private static readonly DoorSpec s_topDoor = BoardFixture.Door(Side.Top, 0, 1, 'A');

        private static readonly Cell s_start = new Cell(0, 1);

        [Test]
        public void AMoveAppliesOnceAndRaisesOneMoveCommitted()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = Create(new AttemptHooksBuilder().Add(listener).Build());
            GameCommand move = GameCommand.Move(0, s_start, new Cell(2, 2));

            Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(2, 2)));
            Assert.That(sim.Board.GetOccupant(s_start), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
            Assert.That(sim.Events.GetAt(0).Kind, Is.EqualTo(BoardEventKind.MoveCommitted));
            Assert.That(listener.Moves.Count, Is.EqualTo(1));
            Assert.That(listener.Moves[0].From, Is.EqualTo(s_start));
            Assert.That(listener.Moves[0].To, Is.EqualTo(new Cell(2, 2)));
            Assert.That(listener.Total, Is.EqualTo(1));
            Assert.That(sim.Commands.Log.ToArray(), Is.EqualTo(new[] { move }));
        }

        [Test]
        public void AnExitCommandMovesAndLeavesInOneStepAndRaisesOnlyBlockExited()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = Create(new AttemptHooksBuilder().Add(listener).Build());

            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, s_start, new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Board.GetOccupant(s_start), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.GetOccupant(new Cell(0, 2)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Exited[0].Origin, Is.EqualTo(new Cell(0, 2)));
            Assert.That(listener.Exited[0].Outcome, Is.EqualTo(ExitOutcome.Leave));
            Assert.That(listener.Moves.Count, Is.Zero, "an exit is not also a MoveCommitted (DEC-113)");
            Assert.That(sim.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void AStayExitKeepsTheBlockOnItsTarget()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = Create(new AttemptHooksBuilder().Add(new StayExitGate()).Add(listener).Build());

            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, s_start, new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(0, 2)));
            Assert.That(listener.Exited[0].Outcome, Is.EqualTo(ExitOutcome.Stay));
            Assert.That(listener.Total, Is.EqualTo(1));
        }

        [Test]
        public void AnExitOfABlockAlreadyFlushNeedsNoMove()
        {
            BoardSimulation sim = Create(AttemptHooks.CreateEmpty());
            sim.Commands.Execute(GameCommand.Move(0, s_start, new Cell(0, 2)));

            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, new Cell(0, 2), new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
        }

        [Test]
        public void UnknownAndOffBoardBlocksAreRefused()
        {
            BoardSimulation sim = Create(AttemptHooks.CreateEmpty());
            AssertRefused(sim, GameCommand.Move(7, s_start, new Cell(1, 1)), CommandOutcome.UnknownBlock);
            AssertRefused(sim, GameCommand.Move(-1, s_start, new Cell(1, 1)), CommandOutcome.UnknownBlock);

            sim.Commands.Execute(GameCommand.MoveAndExit(0, s_start, new Cell(0, 2), Side.Top));
            AssertRefused(sim, GameCommand.Move(0, new Cell(0, 2), new Cell(1, 2)), CommandOutcome.NotOnBoard);
        }

        [Test]
        public void AStaleFromIsRefused()
        {
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.Move(0, new Cell(1, 1), new Cell(2, 1)),
                CommandOutcome.StaleOrigin);
        }

        [Test]
        public void ADragGateRefusesTheMove()
        {
            AssertRefused(Create(new AttemptHooksBuilder().Add(new DenyDragGate(0)).Build()),
                GameCommand.Move(0, s_start, new Cell(1, 1)), CommandOutcome.DragRefused);
        }

        [Test]
        public void AMoveOrExitOffTheAllowedAxesIsRefused()
        {
            AssertRefused(Create(new AttemptHooksBuilder().Add(new AxisGate(AxisMask.Vertical)).Build()),
                GameCommand.Move(0, s_start, new Cell(2, 1)), CommandOutcome.AxisRefused);
            AssertRefused(Create(new AttemptHooksBuilder().Add(new AxisGate(AxisMask.Horizontal)).Build()),
                GameCommand.Move(0, s_start, new Cell(0, 2)), CommandOutcome.AxisRefused);

            // Flush against the Top door, a horizontal-only block still cannot push up through it.
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(new AxisGate(AxisMask.Horizontal)).Build(),
                new[] { "A...", "...B" }, s_topDoor);
            AssertRefused(sim, GameCommand.MoveAndExit(0, new Cell(0, 1), new Cell(0, 1), Side.Top), CommandOutcome.AxisRefused);
        }

        [Test]
        public void AMoveThatChangesNothingIsNoMove()
        {
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.Move(0, s_start, s_start), CommandOutcome.NoChange);
        }

        [Test]
        public void AnOccupiedOrEnclosedTargetIsUnreachable()
        {
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.Move(0, s_start, new Cell(3, 0)), CommandOutcome.Unreachable);

            BoardSimulation walled = BoardFixture.CreateSimulation(new[]
            {
                "..B.",
                "A.B.",
                "..B."
            });
            AssertRefused(walled, GameCommand.Move(0, new Cell(0, 1), new Cell(3, 1)), CommandOutcome.Unreachable);
        }

        [Test]
        public void AnExitThatDoesNotFitIsRefused()
        {
            // No door above column 1, A is not flush at its start, and the Top door wants colour A, not B.
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.MoveAndExit(0, s_start, new Cell(1, 2), Side.Top),
                CommandOutcome.ExitRefused);
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.MoveAndExit(0, s_start, s_start, Side.Top),
                CommandOutcome.ExitRefused);
            AssertRefused(Create(AttemptHooks.CreateEmpty()), GameCommand.MoveAndExit(1, new Cell(3, 0), new Cell(0, 2), Side.Top),
                CommandOutcome.ExitRefused);
        }

        [Test]
        public void AnOptionThatIsNoSideIsMalformed()
        {
            AssertRefused(Create(AttemptHooks.CreateEmpty()),
                new GameCommand(CommandKind.MoveBlock, 0, s_start, new Cell(1, 1), 9), CommandOutcome.Malformed);
        }

        [Test]
        public void AnExitSweepsTheCellsTheMoveVacated()
        {
            // The L leaves its top-right cell (1, 2) when it steps right. That cell is closed, so its
            // new notch above (1, 1) blocks the Top exit, exactly as the live drag sees it.
            string[] rows =
            {
                ".A..",
                "AA..",
                "...."
            };
            DoorSpec door = BoardFixture.Door(Side.Top, 1, 2, 'A');
            AttemptHooks closed = new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(1, 2))).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(closed, rows, door);

            AssertRefused(sim, GameCommand.MoveAndExit(0, new Cell(0, 1), new Cell(1, 1), Side.Top), CommandOutcome.ExitRefused);

            BoardSimulation live = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(1, 2))).Build(), rows, door);
            Assert.That(live.Drag.TryBegin(0, new BoardPoint(0f, 1f)), Is.True);
            Assert.That(live.Drag.Update(new BoardPoint(1f, 1.45f), out GameCommand _), Is.Not.EqualTo(DragResult.ExitReady));
            Assert.That(live.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 1)), "the drag reached the flush cell and did not exit");
        }

        [Test]
        public void AFullTickLogFailsBeforeTheBoardChanges()
        {
            // Capacity is the cell count + 4 = 6; a caller that never clears the tick log hits it on the 7th move.
            BoardSimulation sim = Create(AttemptHooks.CreateEmpty());
            Cell at = s_start;
            for (int i = 0; i < sim.Events.Capacity; i++)
            {
                Cell next = at == s_start ? new Cell(1, 1) : s_start;
                Assert.That(sim.Commands.Execute(GameCommand.Move(0, at, next)), Is.EqualTo(CommandOutcome.Applied));
                at = next;
            }

            Cell target = at == s_start ? new Cell(1, 1) : s_start;
            Assert.Throws<System.InvalidOperationException>(() => sim.Commands.Execute(GameCommand.Move(0, at, target)));

            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(at), "the board did not change");
            Assert.That(sim.Commands.Log.Count, Is.EqualTo(sim.Events.Capacity));
        }

        private static BoardSimulation Create(AttemptHooks hooks)
        {
            return BoardFixture.CreateSimulation(hooks, s_board, s_topDoor);
        }

        // A refusal changes neither the board, the tick's events nor the log.
        private static void AssertRefused(BoardSimulation sim, GameCommand command, CommandOutcome expected)
        {
            int blocks = sim.Board.BlockCount;
            Cell[] origins = new Cell[blocks];
            BlockStatus[] statuses = new BlockStatus[blocks];
            for (int block = 0; block < blocks; block++)
            {
                origins[block] = sim.Board.GetOrigin(block);
                statuses[block] = sim.Board.GetStatus(block);
            }

            int events = sim.Events.Count;
            int logged = sim.Commands.Log.Count;

            Assert.That(sim.Commands.Execute(command), Is.EqualTo(expected), command.ToString());

            for (int block = 0; block < blocks; block++)
            {
                Assert.That(sim.Board.GetOrigin(block), Is.EqualTo(origins[block]));
                Assert.That(sim.Board.GetStatus(block), Is.EqualTo(statuses[block]));
            }

            Assert.That(sim.Events.Count, Is.EqualTo(events));
            Assert.That(sim.Commands.Log.Count, Is.EqualTo(logged));
        }
    }
}
