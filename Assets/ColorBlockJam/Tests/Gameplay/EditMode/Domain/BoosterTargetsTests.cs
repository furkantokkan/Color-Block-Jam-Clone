using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The booster target rules and the opening Rocket's candidates (B-11, AC-7, AC-14): one rule
    /// for in-game and opening use, a stable candidate order and a repeatable seeded choice.
    /// </summary>
    public sealed class BoosterTargetsTests
    {
        // A (0) and C (2) have two cells, B (1) one.
        private static readonly string[] s_mixed =
        {
            "AB.",
            "A.C",
            "..C"
        };

        private static readonly string[] s_l =
        {
            "A..",
            "A..",
            "AA."
        };

        [Test]
        public void RocketNeedsABlockThatKeepsAnotherCell()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_mixed));

            Assert.That(BoosterTargets.CanRemoveCell(board, new Cell(0, 1), out int block, out CommandOutcome refusal), Is.True);
            Assert.That(block, Is.EqualTo(0));
            Assert.That(refusal, Is.EqualTo(CommandOutcome.Applied));

            Assert.That(BoosterTargets.CanRemoveCell(board, new Cell(1, 2), out block, out refusal), Is.False);
            Assert.That(block, Is.EqualTo(1), "an ineligible block is still named, so targeting can reject it");
            Assert.That(refusal, Is.EqualTo(CommandOutcome.FinalCell));

            Assert.That(BoosterTargets.CanRemoveCell(board, new Cell(1, 1), out block, out refusal), Is.False);
            Assert.That(block, Is.EqualTo(-1));
            Assert.That(refusal, Is.EqualTo(CommandOutcome.NotBlockCell), "an empty cell cancels");
            Assert.That(BoosterTargets.CanRemoveCell(board, new Cell(3, 0), out block, out refusal), Is.False);
            Assert.That(refusal, Is.EqualTo(CommandOutcome.NotBlockCell), "outside the board cancels");
        }

        [Test]
        public void HammerAndVacuumTargetOnlyOnBoardBlocks()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_mixed));

            Assert.That(BoosterTargets.CanRemoveBlock(board, 1), Is.True, "a one-cell block is a Hammer target");
            Assert.That(BoosterTargets.CanRemoveColor(board, 2), Is.True);
            Assert.That(BoosterTargets.CanRemoveBlock(board, -1), Is.False);
            Assert.That(BoosterTargets.CanRemoveBlock(board, board.BlockCount), Is.False, "an ID not issued yet");

            board.Release(2, BlockStatus.Exited);
            Assert.That(BoosterTargets.CanRemoveBlock(board, 2), Is.False);
            Assert.That(BoosterTargets.CanRemoveColor(board, 2), Is.False);
        }

        [Test]
        public void CandidatesListMultiCellBlocksInIdThenSliceOrder()
        {
            LevelModel model = BoardFixture.Create(s_mixed);
            BoardState board = new BoardState(model);
            Cell[] buffer = new Cell[model.TotalCellCount];

            int count = StartingRocketCandidates.Collect(board, buffer);

            Assert.That(Take(buffer, count), Is.EqualTo(new[] { new Cell(0, 2), new Cell(0, 1), new Cell(2, 1), new Cell(2, 0) }));
        }

        [Test]
        public void CandidatesIncludeSplittingCellsAndLaterParts()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_l);
            Cell[] buffer = new Cell[sim.Board.Model.TotalCellCount];

            int count = StartingRocketCandidates.Collect(sim.Board, buffer);
            Assert.That(Take(buffer, count), Is.EqualTo(new[] { new Cell(0, 2), new Cell(0, 1), new Cell(0, 0), new Cell(1, 0) }),
                "the bridge (0,0) is a candidate");

            // After the bridge is gone the one-cell part 1 is no candidate; its parent's two cells are.
            sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0)));
            count = StartingRocketCandidates.Collect(sim.Board, buffer);
            Assert.That(Take(buffer, count), Is.EqualTo(new[] { new Cell(0, 2), new Cell(0, 1) }));

            BoardSimulation line = BoardFixture.CreateSimulation(new[] { "AAAAA", "B...." });
            line.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 1), new Cell(1, 1)));
            Cell[] lineBuffer = new Cell[line.Board.Model.TotalCellCount];
            count = StartingRocketCandidates.Collect(line.Board, lineBuffer);
            Assert.That(Take(lineBuffer, count), Is.EqualTo(new[] { new Cell(2, 1), new Cell(3, 1), new Cell(4, 1) }),
                "a later part lists its cells under its own ID");
        }

        [Test]
        public void NoCandidateWhenEveryBlockHasOneCell()
        {
            LevelModel model = BoardFixture.Create(new[] { "A.B", ".C." });
            Cell[] buffer = new Cell[model.TotalCellCount];

            int count = StartingRocketCandidates.Collect(new BoardState(model), buffer);

            Assert.That(count, Is.Zero);
            Assert.That(StartingRocketCandidates.TrySample(buffer, count, 7, out Cell _), Is.False);
        }

        [Test]
        public void ASeedChoosesTheSameEligibleCellEveryTime()
        {
            LevelModel model = BoardFixture.Create(s_l);
            Cell[] buffer = new Cell[model.TotalCellCount];
            int count = StartingRocketCandidates.Collect(new BoardState(model), buffer);
            HashSet<Cell> chosen = new HashSet<Cell>();

            for (int seed = 0; seed < 400; seed++)
            {
                Assert.That(StartingRocketCandidates.TrySample(buffer, count, seed, out Cell first), Is.True);
                Assert.That(StartingRocketCandidates.TrySample(buffer, count, seed, out Cell again), Is.True);
                Assert.That(again, Is.EqualTo(first), "seed " + seed);

                // The chosen cell is always a valid target, including the splitting bridge.
                BoardSimulation sim = BoardFixture.CreateSimulation(s_l);
                Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, sim.Board.GetOrigin(0), first)),
                    Is.EqualTo(CommandOutcome.Applied));
                chosen.Add(first);
            }

            Assert.That(chosen, Is.EquivalentTo(Take(buffer, count)), "every candidate can be chosen");
        }

        [Test]
        public void ASmallBufferOrCountIsRefused()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_l));

            Assert.Throws<ArgumentException>(() => StartingRocketCandidates.Collect(board, new Cell[3]));
            Assert.Throws<ArgumentOutOfRangeException>(() => StartingRocketCandidates.TrySample(new Cell[2], 3, 0, out Cell _));
        }

        private static Cell[] Take(Cell[] buffer, int count)
        {
            Cell[] cells = new Cell[count];
            Array.Copy(buffer, cells, count);
            return cells;
        }
    }
}
