using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class BoardStateTests
    {
        private static readonly string[] s_map =
        {
            "....",
            "AA.B",
            "...B"
        };

        [Test]
        public void StartsWithEveryBlockOnItsAuthoredCells()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            Assert.That(board.RequiredRemaining, Is.EqualTo(2));
            Assert.That(board.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(board.GetOrigin(1), Is.EqualTo(new Cell(3, 0)));
            Assert.That(board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(board.GetOccupant(new Cell(0, 1)), Is.EqualTo(0));
            Assert.That(board.GetOccupant(new Cell(1, 1)), Is.EqualTo(0));
            Assert.That(board.GetOccupant(new Cell(3, 0)), Is.EqualTo(1));
            Assert.That(board.GetOccupant(new Cell(3, 1)), Is.EqualTo(1));
            Assert.That(board.GetOccupant(new Cell(2, 2)), Is.EqualTo(Occupant.k_empty));
            Assert.That(board.GetOccupant(new Cell(4, 0)), Is.EqualTo(Occupant.k_offBoard));
            Assert.That(board.GetOccupant(new Cell(0, -1)), Is.EqualTo(Occupant.k_offBoard));
        }

        [Test]
        public void MoveFreesTheOldCellsAndWritesTheNewOnes()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            board.Move(0, new Cell(1, 1));

            Assert.That(board.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
            Assert.That(board.GetOccupant(new Cell(0, 1)), Is.EqualTo(Occupant.k_empty));
            Assert.That(board.GetOccupant(new Cell(1, 1)), Is.EqualTo(0));
            Assert.That(board.GetOccupant(new Cell(2, 1)), Is.EqualTo(0));
        }

        [Test]
        public void MoveOntoAnotherBlockOrOffTheBoardThrows()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            Assert.Throws<InvalidOperationException>(() => board.Move(0, new Cell(2, 1)));
            Assert.Throws<InvalidOperationException>(() => board.Move(0, new Cell(3, 2)));
            Assert.That(board.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "a refused move changes nothing");
        }

        [Test]
        public void ReleaseFreesTheCellsOnceAndCountsTheRequiredBlock()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            board.Release(1, BlockStatus.Exited);

            Assert.That(board.GetStatus(1), Is.EqualTo(BlockStatus.Exited));
            Assert.That(board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(board.GetOccupant(new Cell(3, 0)), Is.EqualTo(Occupant.k_empty));
            Assert.That(board.GetOccupant(new Cell(3, 1)), Is.EqualTo(Occupant.k_empty));
            Assert.Throws<InvalidOperationException>(() => board.Release(1, BlockStatus.Removed));
            Assert.Throws<InvalidOperationException>(() => board.Move(1, new Cell(3, 1)));
            Assert.Throws<ArgumentException>(() => board.Release(0, BlockStatus.OnBoard));
            Assert.That(board.RequiredRemaining, Is.EqualTo(1));
        }

        [Test]
        public void ResetEqualsAFreshState()
        {
            LevelModel model = BoardFixture.Create(s_map);
            BoardState played = new BoardState(model);
            played.Move(0, new Cell(1, 2));
            played.Release(1, BlockStatus.Exited);

            played.Reset();

            BoardState fresh = new BoardState(model);
            Assert.That(played.RequiredRemaining, Is.EqualTo(fresh.RequiredRemaining));
            for (int block = 0; block < model.BlockCount; block++)
            {
                Assert.That(played.GetOrigin(block), Is.EqualTo(fresh.GetOrigin(block)));
                Assert.That(played.GetStatus(block), Is.EqualTo(fresh.GetStatus(block)));
            }

            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    Cell cell = new Cell(x, y);
                    Assert.That(played.GetOccupant(cell), Is.EqualTo(fresh.GetOccupant(cell)), cell.ToString());
                }
            }
        }

        [Test]
        public void ResetAfterASplitRestoresTheAuthoredFootprintsAndIds()
        {
            LevelModel model = BoardFixture.Create(new[] { "A...", "A..B", "AA.B" });
            BoardState played = new BoardState(model);
            int[] created = new int[3];
            Assert.That(played.RemoveCell(0, new Cell(0, 0), created), Is.EqualTo(1));
            Assert.That(created[0], Is.EqualTo(2));
            played.Move(2, new Cell(1, 1));

            played.Reset();

            Assert.That(played.BlockCount, Is.EqualTo(model.BlockCount));
            Assert.That(played.RequiredRemaining, Is.EqualTo(model.BlockCount));
            Assert.That(played.GetStatus(2), Is.EqualTo(BlockStatus.Removed), "the part's ID is no longer issued");
            for (int block = 0; block < model.BlockCount; block++)
            {
                BlockDefinition authored = model.GetBlock(block);
                BlockFootprint footprint = played.GetFootprint(block);
                Assert.That(footprint.CellCount, Is.EqualTo(authored.CellCount));
                for (int i = 0; i < authored.CellCount; i++)
                {
                    Assert.That(footprint.GetCell(i), Is.EqualTo(authored.GetCell(i)));
                }

                Assert.That(footprint.MaxY, Is.EqualTo(authored.MaxY));
                Assert.That(played.GetOrigin(block), Is.EqualTo(authored.Origin));
            }

            BoardState fresh = new BoardState(model);
            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    Cell cell = new Cell(x, y);
                    Assert.That(played.GetOccupant(cell), Is.EqualTo(fresh.GetOccupant(cell)), cell.ToString());
                }
            }
        }

        [Test]
        public void AnIdNotIssuedYetIsNeverOnTheBoard()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));

            Assert.That(board.BlockCount, Is.EqualTo(2));
            Assert.That(board.GetStatus(2), Is.EqualTo(BlockStatus.Removed));
            Assert.That(board.GetFootprint(2).CellCount, Is.Zero);
            Assert.That(board.GetFootprint(2).Source, Is.EqualTo(-1));
            Assert.That(board.GetFootprint(1).Source, Is.EqualTo(1));
        }

        [Test]
        public void RemoveCellThrowsForAnOnlyCellAForeignCellOrAShortIdBuffer()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            Assert.That(board.RemoveCell(1, new Cell(3, 0), new int[3]), Is.Zero);

            Assert.Throws<InvalidOperationException>(() => board.RemoveCell(1, new Cell(3, 1), new int[3]), "only cell");
            Assert.Throws<InvalidOperationException>(() => board.RemoveCell(0, new Cell(3, 1), new int[3]), "foreign cell");
            Assert.Throws<ArgumentException>(() => board.RemoveCell(0, new Cell(0, 1), new int[2]));
            Assert.That(board.GetFootprint(0).CellCount, Is.EqualTo(2), "a refused split changes nothing");
        }
    }
}
