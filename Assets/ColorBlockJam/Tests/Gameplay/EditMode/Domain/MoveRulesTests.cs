using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class MoveRulesTests
    {
        private static readonly string[] s_map =
        {
            "....",
            "AA#.",
            "...B"
        };

        private static readonly AttemptHooks s_noHooks = AttemptHooks.CreateEmpty();

        [Test]
        public void CanOccupyAcceptsFreeFloorAndTheBlocksOwnCells()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(0, 1)), Is.True, "its own cells");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(0, 2)), Is.True, "free floor");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(0, 0)), Is.True, "free floor");
        }

        [Test]
        public void CanOccupyRefusesTheEdgeVoidAndOtherBlocks()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(-1, 1)), Is.False, "left edge");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(3, 1)), Is.False, "right edge");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(0, 3)), Is.False, "top edge");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(1, 1)), Is.False, "void");
            Assert.That(MoveRules.CanOccupy(board, s_noHooks, 0, new Cell(2, 0)), Is.False, "block B");
        }

        [Test]
        public void CanOccupyAsksEveryCellGate()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(1, 2))).Build();

            Assert.That(MoveRules.CanOccupy(board, hooks, 0, new Cell(0, 2)), Is.False);
            Assert.That(MoveRules.CanOccupy(board, hooks, 0, new Cell(0, 0)), Is.True);
        }

        // I: a vertical bar of three cells at x = 0.
        [TestCase(0, 0, Side.Bottom, true)]
        [TestCase(0, 0, Side.Left, true)]
        [TestCase(0, 0, Side.Top, false)]
        [TestCase(0, 2, Side.Top, true)]
        [TestCase(0, 2, Side.Bottom, false)]
        [TestCase(3, 1, Side.Right, true)]
        [TestCase(2, 1, Side.Right, false)]
        public void IsFlushForAnIShape(int x, int y, Side side, bool expected)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "....",
                "....",
                "A...",
                "A...",
                "A..."
            }));

            Assert.That(MoveRules.IsFlush(board, 0, new Cell(x, y), side), Is.EqualTo(expected));
        }

        // L: cells (0,0), (1,0), (0,1), (0,2) relative to its origin.
        [TestCase(0, 0, Side.Left, true)]
        [TestCase(0, 0, Side.Bottom, true)]
        [TestCase(2, 0, Side.Right, true)]
        [TestCase(1, 0, Side.Right, false)]
        [TestCase(0, 1, Side.Top, true)]
        [TestCase(0, 0, Side.Top, false)]
        public void IsFlushForAnLShape(int x, int y, Side side, bool expected)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "....",
                "A...",
                "A...",
                "AA.."
            }));

            Assert.That(MoveRules.IsFlush(board, 0, new Cell(x, y), side), Is.EqualTo(expected));
        }

        // T: cells (0,1), (1,1), (2,1), (1,0) relative to its origin.
        [TestCase(0, 0, Side.Bottom, true)]
        [TestCase(0, 2, Side.Top, true)]
        [TestCase(0, 1, Side.Top, false)]
        [TestCase(1, 0, Side.Right, true)]
        [TestCase(1, 0, Side.Left, false)]
        public void IsFlushForATShape(int x, int y, Side side, bool expected)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "....",
                "....",
                "AAA.",
                ".A.."
            }));

            Assert.That(MoveRules.IsFlush(board, 0, new Cell(x, y), side), Is.EqualTo(expected));
        }
    }
}
