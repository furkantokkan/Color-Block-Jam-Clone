using ColorBlockJam.Domain.Board;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class LevelModelTests
    {
        private static readonly BlockColor s_red = new BlockColor(0);

        [Test]
        public void ValidLevelKeepsItsSizeDoorsAndBlocks()
        {
            LevelModel model = BoardFixture.Create(
                new[]
                {
                    "....",
                    ".AA.",
                    "#..."
                },
                BoardFixture.Door(Side.Top, 1, 2, 'A'),
                BoardFixture.Door(Side.Left, 1, 1, 'B'));

            Assert.That(model.Width, Is.EqualTo(4));
            Assert.That(model.Height, Is.EqualTo(3));
            Assert.That(model.TimerSeconds, Is.EqualTo(BoardFixture.k_timerSeconds));
            Assert.That(model.DoorCount, Is.EqualTo(2));
            Assert.That(model.BlockCount, Is.EqualTo(1));
            Assert.That(model.GetBlock(0).Origin, Is.EqualTo(new Cell(1, 1)));
            Assert.That(model.IsFloor(0, 0), Is.False, "void");
            Assert.That(model.IsFloor(1, 0), Is.True);
            Assert.That(model.IsFloor(-1, 0), Is.False, "outside");
        }

        [Test]
        public void TotalCellCountSumsEveryAuthoredBlock()
        {
            LevelModel model = BoardFixture.Create(new[] { "AAB", "A.C", "..C" });

            Assert.That(model.BlockCount, Is.EqualTo(3));
            Assert.That(model.TotalCellCount, Is.EqualTo(3 + 1 + 2));
            Assert.That(BoardFixture.Create(new[] { "..." }).TotalCellCount, Is.Zero);
        }

        [Test]
        public void GetDoorAtLaneMapsEveryLaneOfEachDoorAndWallLanesToMinusOne()
        {
            LevelModel model = BoardFixture.Create(
                new[]
                {
                    "....",
                    "....",
                    "...."
                },
                BoardFixture.Door(Side.Top, 1, 2, 'A'),
                BoardFixture.Door(Side.Right, 2, 1, 'B'));

            Assert.That(model.GetDoorAtLane(Side.Top, 0), Is.EqualTo(-1));
            Assert.That(model.GetDoorAtLane(Side.Top, 1), Is.EqualTo(0));
            Assert.That(model.GetDoorAtLane(Side.Top, 2), Is.EqualTo(0));
            Assert.That(model.GetDoorAtLane(Side.Top, 3), Is.EqualTo(-1));
            Assert.That(model.GetDoorAtLane(Side.Right, 2), Is.EqualTo(1));
            Assert.That(model.GetDoorAtLane(Side.Right, 1), Is.EqualTo(-1));
            Assert.That(model.GetDoorAtLane(Side.Bottom, 1), Is.EqualTo(-1));
            Assert.That(model.GetDoorAtLane(Side.Right, 3), Is.EqualTo(-1), "a row beyond the height");
            Assert.That(model.GetDoorAtLane(Side.Top, -1), Is.EqualTo(-1));
        }

        [Test]
        public void RejectsABlockOutsideTheBoard()
        {
            AssertRejected(Floor(3, 3), new DoorDefinition[0], new[] { Block(0, new Cell(2, 2), new Cell(0, 0), new Cell(1, 0)) });
        }

        [Test]
        public void RejectsABlockOnAVoidCell()
        {
            bool[] floor = Floor(3, 3);
            floor[1 * 3 + 1] = false;
            AssertRejected(floor, new DoorDefinition[0], new[] { Block(0, new Cell(1, 1), new Cell(0, 0)) });
        }

        [Test]
        public void RejectsOverlappingBlocks()
        {
            AssertRejected(Floor(3, 3), new DoorDefinition[0], new[]
            {
                Block(0, new Cell(0, 0), new Cell(0, 0), new Cell(1, 0)),
                Block(1, new Cell(1, 0), new Cell(0, 0))
            });
        }

        [Test]
        public void RejectsABlockListingACellTwice()
        {
            AssertRejected(Floor(3, 3), new DoorDefinition[0], new[] { Block(0, new Cell(0, 0), new Cell(0, 0), new Cell(0, 0)) });
        }

        [Test]
        public void RejectsADisconnectedFootprint()
        {
            AssertRejected(Floor(3, 3), new DoorDefinition[0], new[] { Block(0, new Cell(0, 0), new Cell(0, 0), new Cell(1, 1)) });
        }

        [TestCase(Side.Top, 2, 2)]
        [TestCase(Side.Left, -1, 1)]
        [TestCase(Side.Right, 0, 0)]
        [TestCase(Side.Bottom, 0, 4)]
        public void RejectsADoorOutsideItsEdge(Side side, int start, int length)
        {
            AssertRejected(Floor(3, 3), new[] { new DoorDefinition(0, side, start, length, s_red) }, new BlockDefinition[0]);
        }

        [Test]
        public void RejectsADoorOpeningOntoAVoidCell()
        {
            bool[] floor = Floor(3, 3);
            floor[2 * 3 + 1] = false;
            AssertRejected(floor, new[] { new DoorDefinition(0, Side.Top, 1, 1, s_red) }, new BlockDefinition[0]);
        }

        [Test]
        public void RejectsDoorsSharingALaneOnOneSide()
        {
            AssertRejected(Floor(3, 3), new[]
            {
                new DoorDefinition(0, Side.Bottom, 0, 2, s_red),
                new DoorDefinition(1, Side.Bottom, 1, 2, s_red)
            }, new BlockDefinition[0]);
        }

        [Test]
        public void AcceptsDoorsOnTheSameLaneOfDifferentSides()
        {
            bool created = LevelModel.TryCreate(3, 3, Floor(3, 3), new[]
            {
                new DoorDefinition(0, Side.Top, 0, 1, s_red),
                new DoorDefinition(1, Side.Left, 2, 1, s_red)
            }, new BlockDefinition[0], 30, out LevelModel model, out string error);

            Assert.That(created, Is.True, error);
            Assert.That(model.GetDoorAtLane(Side.Top, 0), Is.EqualTo(0));
            Assert.That(model.GetDoorAtLane(Side.Left, 2), Is.EqualTo(1));
        }

        [Test]
        public void RejectsAFloorArrayOfTheWrongSizeAndAWrongIndex()
        {
            AssertRejected(3, 3, new bool[8], new DoorDefinition[0], new BlockDefinition[0]);
            AssertRejected(Floor(3, 3), new DoorDefinition[0], new[] { Block(1, new Cell(0, 0), new Cell(0, 0)) });
        }

        [Test]
        public void KeepsItsOwnCopyOfTheFloorArray()
        {
            bool[] floor = Floor(3, 3);
            Assert.That(LevelModel.TryCreate(3, 3, floor, new DoorDefinition[0], new BlockDefinition[0], 30,
                out LevelModel model, out _), Is.True);

            floor[0] = false;

            Assert.That(model.IsFloor(0, 0), Is.True);
        }

        private static bool[] Floor(int width, int height)
        {
            bool[] floor = new bool[width * height];
            for (int i = 0; i < floor.Length; i++)
            {
                floor[i] = true;
            }

            return floor;
        }

        private static BlockDefinition Block(int index, Cell origin, params Cell[] cells)
        {
            return new BlockDefinition(index, s_red, origin, cells);
        }

        private static void AssertRejected(bool[] floor, DoorDefinition[] doors, BlockDefinition[] blocks)
        {
            AssertRejected(3, floor.Length / 3, floor, doors, blocks);
        }

        private static void AssertRejected(int width, int height, bool[] floor, DoorDefinition[] doors, BlockDefinition[] blocks)
        {
            bool created = LevelModel.TryCreate(width, height, floor, doors, blocks, 30, out LevelModel model, out string error);

            Assert.That(created, Is.False);
            Assert.That(model, Is.Null);
            Assert.That(error, Is.Not.Empty);
        }
    }
}
