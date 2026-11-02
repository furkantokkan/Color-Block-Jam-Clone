using ColorBlockJam.Domain.Board;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class PreviewBoardTests
    {
        private static readonly string[] s_board =
        {
            ".....",
            "AA.#.",
            "A..B."
        };

        [Test]
        public void WithNothingShownItReadsExactlyAsTheBoard()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_board));
            PreviewBoard preview = new PreviewBoard(board);

            AssertReadsAlike(preview, board);
            Assert.That(preview.ShownBlock, Is.EqualTo(-1));
        }

        [Test]
        public void ShowingABlockReadsAsTheBoardAfterThatMove()
        {
            LevelModel model = BoardFixture.Create(s_board);
            BoardState board = new BoardState(model);
            PreviewBoard preview = new PreviewBoard(board);

            // Every origin the L block may stand on, compared with a board that really moved it there.
            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    Cell origin = new Cell(x, y);
                    BoardState moved = new BoardState(model);
                    if (!CanStand(moved, 0, origin))
                    {
                        continue;
                    }

                    moved.Move(0, origin);
                    preview.Show(0, origin);

                    AssertReadsAlike(preview, moved);
                }
            }
        }

        [Test]
        public void ClearReadsAsTheBoardAgain()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_board));
            PreviewBoard preview = new PreviewBoard(board);
            preview.Show(0, new Cell(1, 1));

            preview.Clear();

            AssertReadsAlike(preview, board);
        }

        [Test]
        public void ShowingNeverWritesTheBoard()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_board));
            PreviewBoard preview = new PreviewBoard(board);

            preview.Show(0, new Cell(1, 1));

            Assert.That(board.GetOrigin(0), Is.EqualTo(new Cell(0, 0)));
            Assert.That(board.GetOccupant(new Cell(0, 0)), Is.EqualTo(0));
            Assert.That(board.GetOccupant(new Cell(2, 1)), Is.EqualTo(Occupant.k_empty));
        }

        [Test]
        public void ShowingARocketPartReadsAsTheBoardAfterThatPartMoved()
        {
            // Removing the L's corner (0, 1) leaves (1, 1) as block 0 and (0, 0) as the new part 2.
            LevelModel model = BoardFixture.Create(s_board);
            Cell corner = new Cell(0, 1);
            BoardState board = new BoardState(model);
            board.RemoveCell(0, corner, new int[3]);
            PreviewBoard preview = new PreviewBoard(board);
            Assert.That(board.BlockCount, Is.EqualTo(3));

            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    Cell origin = new Cell(x, y);
                    BoardState moved = new BoardState(model);
                    moved.RemoveCell(0, corner, new int[3]);
                    if (!CanStand(moved, 2, origin))
                    {
                        continue;
                    }

                    moved.Move(2, origin);
                    preview.Show(2, origin);

                    AssertReadsAlike(preview, moved);
                    BlockFootprint shown = preview.GetFootprint(2);
                    Assert.That(shown.CellCount, Is.EqualTo(1));
                    Assert.That(shown.GetCell(0), Is.EqualTo(moved.GetFootprint(2).GetCell(0)));
                }
            }
        }

        private static bool CanStand(IBoardReader board, int block, Cell origin)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell cell = origin + footprint.GetCell(i);
                int occupant = board.GetOccupant(cell);
                if (!board.IsFloor(cell) || (occupant != Occupant.k_empty && occupant != block))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AssertReadsAlike(IBoardReader actual, IBoardReader expected)
        {
            for (int y = -1; y <= expected.Height; y++)
            {
                for (int x = -1; x <= expected.Width; x++)
                {
                    Cell cell = new Cell(x, y);
                    Assert.That(actual.GetOccupant(cell), Is.EqualTo(expected.GetOccupant(cell)), "occupant at " + cell);
                    Assert.That(actual.IsFloor(cell), Is.EqualTo(expected.IsFloor(cell)), "floor at " + cell);
                }
            }

            for (int block = 0; block < expected.BlockCount; block++)
            {
                Assert.That(actual.GetOrigin(block), Is.EqualTo(expected.GetOrigin(block)), "origin of " + block);
                Assert.That(actual.GetStatus(block), Is.EqualTo(expected.GetStatus(block)), "status of " + block);
            }

            Assert.That(actual.RequiredRemaining, Is.EqualTo(expected.RequiredRemaining));
        }
    }
}
