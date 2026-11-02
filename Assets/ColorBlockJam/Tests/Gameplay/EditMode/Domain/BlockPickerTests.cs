using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class BlockPickerTests
    {
        private static readonly FakeFootprintProjector s_topView = FakeFootprintProjector.CreateOrthographic();

        private static readonly string[] s_corners =
        {
            "A...",
            "....",
            "...B"
        };

        [Test]
        public void APressOnABlockIsADirectHit()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "....", ".A..", "..B." }));

            Assert.That(Pick(board, 2.1f, 0.1f), Is.EqualTo(1));
            Assert.That(Pick(board, 1.4f, 1.4f), Is.EqualTo(0));
        }

        [TestCase(-0.8f, 2f, 0)]
        [TestCase(0f, 3.3f, 0)]
        [TestCase(3f, -1.2f, 1)]
        [TestCase(3.8f, 0f, 1)]
        [TestCase(1.4f, 1f, 0)]
        public void AWallGateBorderOrEmptyCellPressPicksTheNearestFootprint(float x, float y, int expected)
        {
            BoardState board = new BoardState(BoardFixture.Create(s_corners));

            Assert.That(Pick(board, x, y), Is.EqualTo(expected));
        }

        [Test]
        public void TheEmptyCornerOfAnLIsNotFootprint()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                ".....",
                "A.B..",
                "AA..."
            }));

            // A bounding-box pick would call (1.2, 1) a direct hit on the L; its occupied cells are 0.5 away, B is 0.3 away.
            Assert.That(Pick(board, 1.2f, 1f), Is.EqualTo(1));
        }

        [Test]
        public void TheNearestFootprintBeatsTheNearestCentre()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "....B",
                ".....",
                "AAAA."
            }));

            // The long block's end cell is 0.4 away; the short block is nearer by centre but 0.61 away by footprint.
            Assert.That(Pick(board, 3.4f, 0.9f), Is.EqualTo(0));
        }

        [TestCase("B.A")]
        [TestCase("A.B")]
        public void ATieGoesToTheLowerAuthoredIndex(string row)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { row }));

            Assert.That(Pick(board, 1f, 0f), Is.EqualTo(0));
        }

        [Test]
        public void ExitedBlocksAreExcludedAndAnEmptyBoardPicksNothing()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "A.B" }));

            board.Release(0, BlockStatus.Exited);
            Assert.That(Pick(board, 0f, 0f), Is.EqualTo(1));

            board.Release(1, BlockStatus.Removed);
            Assert.That(Pick(board, 0f, 0f), Is.EqualTo(-1));
        }

        [Test]
        public void APerspectiveViewPicksByScreenDistanceNotBoardDistance()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "B..",
                "...",
                "..A"
            }));
            FakeFootprintProjector perspective = FakeFootprintProjector.CreatePerspective(0.5f);
            BlockPicker picker = new BlockPicker(board.Width, board.Height);

            // Both footprints are 1.5 cells from (0, 0) on the board, so the top view breaks the tie by index.
            int topViewPick = picker.Pick(s_topView.Project(0f, 0f), board, s_topView);
            int perspectivePick = picker.Pick(perspective.Project(0f, 0f), board, perspective);

            Assert.That(topViewPick, Is.EqualTo(0));
            Assert.That(perspectivePick, Is.EqualTo(1), "far rows shrink on screen, so B is nearer in pixels");
        }

        [Test]
        public void APickerRefusesABoardOfAnotherSize()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_corners));
            BlockPicker picker = new BlockPicker(3, 3);

            Assert.Throws<ArgumentException>(() => picker.Pick(new ScreenPoint(0f, 0f), board, s_topView));
        }

        [TestCase(-1.5f, -1.5f, true)]
        [TestCase(4.5f, 3.5f, true)]
        [TestCase(1.5f, 1f, true)]
        [TestCase(-1.6f, 1f, false)]
        [TestCase(4.6f, 1f, false)]
        [TestCase(1f, -1.6f, false)]
        [TestCase(1f, 3.6f, false)]
        public void TheInteractionAreaIsTheBoardPlusTheWallRingAndABorder(float x, float y, bool expected)
        {
            Assert.That(InteractionArea.Contains(new BoardPoint(x, y), 4, 3), Is.EqualTo(expected));
        }

        private static int Pick(BoardState board, float x, float y)
        {
            BlockPicker picker = new BlockPicker(board.Width, board.Height);
            return picker.Pick(s_topView.Project(x, y), board, s_topView);
        }
    }
}
