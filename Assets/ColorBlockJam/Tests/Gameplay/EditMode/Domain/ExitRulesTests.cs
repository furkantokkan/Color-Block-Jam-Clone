using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class ExitRulesTests
    {
        private static readonly AttemptHooks s_noHooks = AttemptHooks.CreateEmpty();

        private static readonly string[] s_topMiddle =
        {
            ".A.",
            "...",
            "..."
        };

        [TestCase(Side.Top, 1, 2)]
        [TestCase(Side.Bottom, 1, 0)]
        [TestCase(Side.Left, 0, 1)]
        [TestCase(Side.Right, 2, 1)]
        public void AFlushMatchingBlockFitsADoorCoveringItsWidth(Side side, int x, int y)
        {
            string[] rows = { "...", "...", "..." };
            char[] row = rows[2 - y].ToCharArray();
            row[x] = 'A';
            rows[2 - y] = new string(row);
            BoardState board = new BoardState(BoardFixture.Create(rows, BoardFixture.Door(side, 1, 1, 'A')));

            ExitVerdict verdict = ExitRules.Evaluate(board, s_noHooks, 0, new Cell(x, y), side, out ExitCandidate candidate);

            Assert.That(verdict, Is.EqualTo(ExitVerdict.Fits));
            Assert.That(candidate.Door, Is.EqualTo(0));
            Assert.That(candidate.Side, Is.EqualTo(side));
            Assert.That(candidate.Outcome, Is.EqualTo(ExitOutcome.Leave));
        }

        [Test]
        public void ABlockAwayFromTheSideIsNotFlush()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "...", ".A.", "..." },
                BoardFixture.Door(Side.Top, 1, 1, 'A')));

            Assert.That(Evaluate(board, new Cell(1, 1), Side.Top), Is.EqualTo(ExitVerdict.NotFlush));
        }

        [Test]
        public void ADoorOfAnotherColourIsWrongColor()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'B')));

            Assert.That(Evaluate(board, new Cell(1, 2), Side.Top), Is.EqualTo(ExitVerdict.WrongColor));
        }

        [Test]
        public void ABlockWiderThanTheSpanIsTooNarrow()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { ".AA", "...", "..." },
                BoardFixture.Door(Side.Top, 1, 1, 'A')));

            Assert.That(Evaluate(board, new Cell(1, 2), Side.Top), Is.EqualTo(ExitVerdict.TooNarrow));
        }

        [Test]
        public void ADoorCoveringOnlyPartOfTheBlockIsTooNarrow()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "AA..", "....", "...." },
                BoardFixture.Door(Side.Top, 1, 2, 'A')));

            Assert.That(Evaluate(board, new Cell(0, 2), Side.Top), Is.EqualTo(ExitVerdict.TooNarrow));
        }

        [Test]
        public void AdjacentSameColourDoorsAreSeparateSpans()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { ".AA.", "....", "...." },
                BoardFixture.Door(Side.Top, 1, 1, 'A'),
                BoardFixture.Door(Side.Top, 2, 1, 'A')));

            Assert.That(Evaluate(board, new Cell(1, 2), Side.Top), Is.EqualTo(ExitVerdict.TooNarrow));
        }

        [Test]
        public void AFlushBlockWithoutADoorInItsLanesHasNoDoor()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 0, 1, 'A')));

            Assert.That(Evaluate(board, new Cell(1, 2), Side.Top), Is.EqualTo(ExitVerdict.NoDoor));
        }

        [Test]
        public void ABlockHeldInAConcaveNotchIsPathBlocked()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
                {
                    "ABA.",
                    "AAA.",
                    "....",
                    "...."
                },
                BoardFixture.Door(Side.Top, 0, 3, 'A')));

            Assert.That(Evaluate(board, new Cell(0, 2), Side.Top), Is.EqualTo(ExitVerdict.PathBlocked));
        }

        [TestCase('#')]
        [TestCase('B')]
        public void AVoidOrABlockInFrontOfAnLArmIsPathBlocked(char obstacle)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
                {
                    "A...",
                    "A" + obstacle + "..",
                    "AA..",
                    "...."
                },
                BoardFixture.Door(Side.Top, 0, 2, 'A')));

            Assert.That(Evaluate(board, new Cell(0, 1), Side.Top), Is.EqualTo(ExitVerdict.PathBlocked));
        }

        [Test]
        public void ACellGateClosingANotchCellInFrontOfAnLArmIsPathBlocked()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
                {
                    "A...",
                    "A...",
                    "AA..",
                    "...."
                },
                BoardFixture.Door(Side.Top, 0, 2, 'A')));
            AttemptHooks closedNotch = new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(1, 2))).Build();
            AttemptHooks closedOwnCell = new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(0, 2))).Build();

            ExitVerdict blocked = ExitRules.Evaluate(board, closedNotch, 0, new Cell(0, 1), Side.Top, out _);
            ExitVerdict ownCell = ExitRules.Evaluate(board, closedOwnCell, 0, new Cell(0, 1), Side.Top, out _);

            Assert.That(blocked, Is.EqualTo(ExitVerdict.PathBlocked), "a closed cell in the swept notch is a wall (XP-3)");
            Assert.That(ownCell, Is.EqualTo(ExitVerdict.Fits), "cells the block already covers are never gated (DEC-MECH-18)");
        }

        [Test]
        public void AnExitGateVetoIsAskedLastAndRefusesTheExit()
        {
            VetoExitGate veto = new VetoExitGate();
            AttemptHooks hooks = new AttemptHooksBuilder().Add(veto).Build();
            BoardState wrongColour = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'B')));
            BoardState fitting = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'A')));

            ExitVerdict refused = ExitRules.Evaluate(wrongColour, hooks, 0, new Cell(1, 2), Side.Top, out _);
            ExitVerdict vetoed = ExitRules.Evaluate(fitting, hooks, 0, new Cell(1, 2), Side.Top, out _);

            Assert.That(refused, Is.EqualTo(ExitVerdict.WrongColor));
            Assert.That(vetoed, Is.EqualTo(ExitVerdict.Vetoed));
            Assert.That(veto.Calls, Is.EqualTo(1), "gates are asked only when the base rules fit");
        }

        [Test]
        public void AStayGateFitsWithTheStayOutcome()
        {
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new StayExitGate()).Build();
            BoardState board = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'A')));

            ExitVerdict verdict = ExitRules.Evaluate(board, hooks, 0, new Cell(1, 2), Side.Top, out ExitCandidate candidate);

            Assert.That(verdict, Is.EqualTo(ExitVerdict.Fits));
            Assert.That(candidate.Outcome, Is.EqualTo(ExitOutcome.Stay));
        }

        [Test]
        public void EvaluateNeverChangesTheBoard()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'A')));

            Evaluate(board, new Cell(1, 2), Side.Top);

            Assert.That(board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(board.GetOccupant(new Cell(1, 2)), Is.EqualTo(0));
        }

        [Test]
        public void AWrongColourDoorStopsTheDragLikeAWall()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_topMiddle, BoardFixture.Door(Side.Top, 1, 1, 'B'));
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(1, 2)), Is.True);

            DragResult result = sim.Drag.Update(new BoardPoint(1, 2.9f), out _);

            Assert.That(result, Is.EqualTo(DragResult.Unchanged));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(1, 2)));
            Assert.That(sim.Events.Count, Is.EqualTo(0));
        }

        private static ExitVerdict Evaluate(IBoardReader board, Cell origin, Side side)
        {
            return ExitRules.Evaluate(board, s_noHooks, 0, origin, side, out _);
        }
    }
}
