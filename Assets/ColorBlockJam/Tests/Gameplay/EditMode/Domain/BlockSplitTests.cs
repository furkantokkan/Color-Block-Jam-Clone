using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// Rocket removes exactly one cell; the remaining cells become one block per 4-connected part,
    /// with deterministic IDs, the parent's colour and source and unmoved world cells (B-4, AC-5).
    /// </summary>
    public sealed class BlockSplitTests
    {
        // The L's stored cells, in slice order, are (0,2), (0,1), (0,0) and (1,0); (0,0) is its bridge.
        private static readonly string[] s_l =
        {
            "A..",
            "A..",
            "AA."
        };

        private static readonly string[] s_plus =
        {
            ".A.",
            "AAA",
            ".A."
        };

        // B sits below the I5, so the I5's parts take IDs from 2.
        private static readonly string[] s_lineAndDot =
        {
            "AAAAA",
            "B...."
        };

        // A 5x5 square: removing its 12 odd cells (x + y odd) leaves 13 one-cell parts, the most it can
        // fall apart into. B keeps the second colour on the board.
        private static readonly string[] s_square =
        {
            "AAAAA.",
            "AAAAA.",
            "AAAAA.",
            "AAAAA.",
            "AAAAAB"
        };

        [Test]
        public void RemovingTheBridgeOfAnLLeavesTwoIndependentBlocks()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_l);
            GameCommand rocket = GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0));

            Assert.That(sim.Commands.Execute(rocket), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.BlockCount, Is.EqualTo(2));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(2), "every part is required");
            Assert.That(sim.IsCleared, Is.False);
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0), Is.EqualTo(new[] { new Cell(0, 2), new Cell(0, 1) }));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 1), Is.EqualTo(new[] { new Cell(1, 0) }));
            Assert.That(sim.Board.GetOrigin(1), Is.EqualTo(new Cell(0, 0)), "a part keeps the parent's origin, so nothing moves");
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Board.GetFootprint(1).Color, Is.EqualTo(BoardFixture.ColorOf('A')));
            Assert.That(sim.Board.GetFootprint(0).Source, Is.EqualTo(0));
            Assert.That(sim.Board.GetFootprint(1).Source, Is.EqualTo(0));
            Assert.That(sim.Board.GetOccupant(new Cell(0, 0)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.GetOccupant(new Cell(1, 0)), Is.EqualTo(1));
            Assert.That(sim.Board.GetOccupant(new Cell(0, 1)), Is.EqualTo(0));

            Assert.That(sim.Events.Count, Is.EqualTo(2));
            AssertFootprintEvent(sim.Events.GetAt(0), 0, 0, new Cell(0, 0));
            AssertFootprintEvent(sim.Events.GetAt(1), 1, 0, new Cell(0, 0));
            Assert.That(listener.Footprints, Is.EqualTo(new[]
            {
                new FootprintChangedEvent(0, 0, new Cell(0, 0)),
                new FootprintChangedEvent(1, 0, new Cell(0, 0))
            }));
            Assert.That(listener.Exited, Is.Empty, "a split is not an exit");
            Assert.That(listener.Removed, Is.Empty, "a split is not a whole-block clear");
            Assert.That(sim.Commands.Log.ToArray(), Is.EqualTo(new[] { rocket }));

            Assert.That(sim.Board.Model.BlockCount, Is.EqualTo(1), "the authored model is never changed");
            Assert.That(sim.Board.Model.GetBlock(0).CellCount, Is.EqualTo(4));
        }

        [Test]
        public void RemovingAnEndOfAnLLeavesAnI()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_l);

            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0))),
                Is.EqualTo(CommandOutcome.Applied));

            BlockFootprint footprint = sim.Board.GetFootprint(0);
            Assert.That(sim.Board.BlockCount, Is.EqualTo(1));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0),
                Is.EqualTo(new[] { new Cell(0, 2), new Cell(0, 1), new Cell(0, 0) }));
            Assert.That(footprint.MinX, Is.EqualTo(0));
            Assert.That(footprint.MaxX, Is.EqualTo(0));
            Assert.That(footprint.MinY, Is.EqualTo(0));
            Assert.That(footprint.MaxY, Is.EqualTo(2));
            Assert.That(footprint.Depth(Side.Top), Is.EqualTo(3));
            Assert.That(footprint.Depth(Side.Left), Is.EqualTo(1));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
            AssertFootprintEvent(sim.Events.GetAt(0), 0, 0, new Cell(1, 0));
        }

        [Test]
        public void RemovingTheCentreOfAPlusLeavesFourBlocksInOrderOfTheirFirstCell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_plus);

            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 1))),
                Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.BlockCount, Is.EqualTo(4));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(4));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 0), Is.EqualTo(new[] { new Cell(1, 2) }));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 1), Is.EqualTo(new[] { new Cell(0, 1) }));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 2), Is.EqualTo(new[] { new Cell(2, 1) }));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 3), Is.EqualTo(new[] { new Cell(1, 0) }));
            Assert.That(sim.Events.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                AssertFootprintEvent(sim.Events.GetAt(i), i, 0, new Cell(1, 1));
            }
        }

        [Test]
        public void RepeatedSplitsIssueTheSameIdsEveryTime()
        {
            BoardSimulation first = SplitLineTwice();
            BoardSimulation second = SplitLineTwice();

            Assert.That(first.Board.BlockCount, Is.EqualTo(4));
            Assert.That(first.Board.RequiredRemaining, Is.EqualTo(4));
            Assert.That(FootprintReader.GetWorldCells(first.Board, 0), Is.EqualTo(new[] { new Cell(0, 1) }));
            Assert.That(FootprintReader.GetWorldCells(first.Board, 2), Is.EqualTo(new[] { new Cell(2, 1) }));
            Assert.That(FootprintReader.GetWorldCells(first.Board, 3), Is.EqualTo(new[] { new Cell(4, 1) }));
            Assert.That(first.Board.GetFootprint(3).Source, Is.EqualTo(0), "a part of a part still names the authored block");
            for (int block = 0; block < first.Board.BlockCount; block++)
            {
                Assert.That(FootprintReader.GetWorldCells(second.Board, block),
                    Is.EqualTo(FootprintReader.GetWorldCells(first.Board, block)), "cells of " + block);
                Assert.That(second.Board.GetOrigin(block), Is.EqualTo(first.Board.GetOrigin(block)), "origin of " + block);
            }
        }

        [Test]
        public void TheWorstCaseFragmentationStaysInsideTheIdAndEventCapacities()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_square);
            int cells = sim.Board.Model.TotalCellCount;
            Cell[] candidates = new Cell[cells];
            int removed = 0;

            // Owner: this test. Stops: no Rocket candidate remains; each applied Rocket removes one of the 26 cells.
            for (int count = StartingRocketCandidates.Collect(sim.Board, candidates); count > 0;
                 count = StartingRocketCandidates.Collect(sim.Board, candidates))
            {
                Cell target = FirstOddOrFirst(candidates, count);
                int block = sim.Board.GetOccupant(target);
                sim.Events.Clear();

                Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(block, sim.Board.GetOrigin(block), target)),
                    Is.EqualTo(CommandOutcome.Applied), "Rocket on " + target);

                removed++;
                Assert.That(sim.Board.BlockCount, Is.LessThanOrEqualTo(cells - removed), "IDs never outnumber the cells left");
                AssertOccupancyMatchesFootprints(sim.Board);
            }

            Assert.That(removed, Is.EqualTo(12));
            Assert.That(sim.Board.BlockCount, Is.EqualTo(cells - removed), "every cell left is its own block: B-1's bound is reached");
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(14));

            sim.Events.Clear();
            GameCommand vacuum = GameCommand.RemoveColor(0, sim.Board.GetOrigin(0), BoardFixture.ColorOf('A'));
            Assert.That(sim.Commands.Execute(vacuum), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Events.Count, Is.EqualTo(13), "one removal per part fits the tick log");
            Assert.That(sim.Events.Count, Is.LessThanOrEqualTo(sim.Events.Capacity));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard), "B stays");
            AssertOccupancyMatchesFootprints(sim.Board);

            // The logged commands replay to the same IDs, footprints and statuses (AC-6).
            BoardSimulation replay = BoardFixture.CreateSimulation(s_square);
            GameCommand[] log = sim.Commands.Log.ToArray();
            for (int i = 0; i < log.Length; i++)
            {
                replay.Events.Clear();
                Assert.That(replay.Commands.Execute(log[i]), Is.EqualTo(CommandOutcome.Applied), log[i].ToString());
            }

            Assert.That(replay.Board.BlockCount, Is.EqualTo(sim.Board.BlockCount));
            for (int block = 0; block < sim.Board.BlockCount; block++)
            {
                Assert.That(replay.Board.GetStatus(block), Is.EqualTo(sim.Board.GetStatus(block)), "status of " + block);
                Assert.That(FootprintReader.GetWorldCells(replay.Board, block),
                    Is.EqualTo(FootprintReader.GetWorldCells(sim.Board, block)), "cells of " + block);
            }
        }

        // The first candidate on an odd cell (x + y), whose removal splits the square most; else the first candidate.
        private static Cell FirstOddOrFirst(Cell[] candidates, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (((candidates[i].X + candidates[i].Y) & 1) == 1)
                {
                    return candidates[i];
                }
            }

            return candidates[0];
        }

        // Every on-board block holds exactly its footprint's cells, no other cell is occupied, and the
        // required count is the number of on-board blocks.
        private static void AssertOccupancyMatchesFootprints(IBoardReader board)
        {
            int onBoard = 0;
            int footprintCells = 0;
            for (int block = 0; block < board.BlockCount; block++)
            {
                if (board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    continue;
                }

                onBoard++;
                Cell[] cells = FootprintReader.GetWorldCells(board, block);
                footprintCells += cells.Length;
                for (int i = 0; i < cells.Length; i++)
                {
                    Assert.That(board.GetOccupant(cells[i]), Is.EqualTo(block), "occupant of " + cells[i]);
                }
            }

            int occupied = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.GetOccupant(new Cell(x, y)) >= 0)
                    {
                        occupied++;
                    }
                }
            }

            Assert.That(occupied, Is.EqualTo(footprintCells), "occupied cells");
            Assert.That(board.RequiredRemaining, Is.EqualTo(onBoard), "required blocks");
        }

        private static BoardSimulation SplitLineTwice()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lineAndDot);

            // (1,1) splits A into (0,1) and the I3 part 2; (3,1) then splits part 2 into (2,1) and part 3.
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 1), new Cell(1, 1))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(FootprintReader.GetWorldCells(sim.Board, 2),
                Is.EqualTo(new[] { new Cell(2, 1), new Cell(3, 1), new Cell(4, 1) }));
            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(2, new Cell(0, 1), new Cell(3, 1))),
                Is.EqualTo(CommandOutcome.Applied));
            AssertFootprintEvent(sim.Events.GetAt(0), 2, 2, new Cell(3, 1));
            AssertFootprintEvent(sim.Events.GetAt(1), 3, 2, new Cell(3, 1));
            return sim;
        }

        private static void AssertFootprintEvent(in BoardEvent e, int block, int parent, Cell removed)
        {
            Assert.That(e.Kind, Is.EqualTo(BoardEventKind.FootprintChanged));
            Assert.That(e.Block, Is.EqualTo(block));
            Assert.That(e.Parent, Is.EqualTo(parent));
            Assert.That(e.From, Is.EqualTo(removed), "the removed cell rides in From");
            Assert.That(e.Door, Is.EqualTo(-1));
        }
    }
}
