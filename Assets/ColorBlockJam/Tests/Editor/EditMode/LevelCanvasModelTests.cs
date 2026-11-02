using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Verifies the Level Editor canvas model's occupancy, door segments and selection resolve.</summary>
    public sealed class LevelCanvasModelTests
    {
        [Test]
        public void ModelMapsBlockCellsAndDoorSegments()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(CreateLevelOne());

            Assert.That(model.Width, Is.EqualTo(4));
            Assert.That(model.Height, Is.EqualTo(5));
            Assert.That(model.BlockCount, Is.EqualTo(2));
            Assert.That(model.DoorCount, Is.EqualTo(2));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(2, 2)), Is.EqualTo(0));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(3, 3)), Is.EqualTo(0));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(0, 1)), Is.EqualTo(1));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(1, 2)), Is.EqualTo(1));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(0, 0)), Is.EqualTo(-1));
            Assert.That(model.GetBlockIndexAt(new Vector2Int(4, 0)), Is.EqualTo(-1), "Off the board.");

            Assert.That(model.GetDoorIndexAt(BoardSide.Top, 0), Is.EqualTo(-1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Top, 1), Is.EqualTo(0));
            Assert.That(model.GetDoorIndexAt(BoardSide.Top, 3), Is.EqualTo(0));
            Assert.That(model.GetDoorIndexAt(BoardSide.Bottom, 2), Is.EqualTo(1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Bottom, 3), Is.EqualTo(-1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Left, 0), Is.EqualTo(-1));
            Assert.That(model.GetSideLength(BoardSide.Left), Is.EqualTo(5));

            Assert.That(model.GetBlock(0).Id, Is.EqualTo("b1"));
            Assert.That(model.GetBlock(0).HasColor, Is.True);
            Assert.That(model.GetBlock(0).Color, Is.EqualTo(PaletteColor.LightBlue));
            Assert.That(model.GetDoor(1).Color, Is.EqualTo(PaletteColor.Pink));
        }

        [Test]
        public void ModelKeepsOffBoardCellsOutOfOccupancyAndFlagsUnknownColours()
        {
            LevelData level = new LevelData(
                "l", "L", 3, 3, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Right, 2, 3, "NotAColour"),
                    new LevelDoorData("d2", BoardSide.Right, 1, 2, "Red")
                },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(2, 2), new[] { Vector2Int.zero, Vector2Int.right }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), new[] { Vector2Int.zero })
                });

            LevelCanvasModel model = LevelCanvasModel.Create(level);

            Assert.That(model.GetBlockIndexAt(new Vector2Int(2, 2)), Is.EqualTo(1),
                "The last block, the one painted on top, wins on overlap.");
            Assert.That(model.GetBlockIndexAt(new Vector2Int(3, 2)), Is.EqualTo(-1), "Off the board.");
            Assert.That(model.GetBlock(0).CellCount, Is.EqualTo(2), "The off-board cell stays in the footprint.");
            Assert.That(model.GetDoorIndexAt(BoardSide.Right, 2), Is.EqualTo(1),
                "The last door, the one painted on top, wins on overlap.");
            Assert.That(model.GetDoorIndexAt(BoardSide.Right, 1), Is.EqualTo(1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Right, 0), Is.EqualTo(-1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Right, 3), Is.EqualTo(-1), "Segments past the side are ignored.");
            Assert.That(model.GetDoor(0).HasColor, Is.False);
        }

        [Test]
        public void LabelSitsOnTheBlockCellNearestItsCentre()
        {
            // An L: (0,0) (0,1) (0,2) (1,0); the centre (0.25, 0.75) is nearest (0,1).
            LevelData level = new LevelData(
                "l", "L", 5, 5, 60, new LevelDoorData[0],
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 1),
                        new[] { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), Vector2Int.right })
                });

            Assert.That(LevelCanvasModel.Create(level).GetBlock(0).LabelCell, Is.EqualTo(new Vector2Int(1, 2)));
        }

        [Test]
        public void BlocksThatShareACellAreMarkedOverlappingAndNamedInBlockOrder()
        {
            LevelData level = new LevelData(
                "l", "L", 5, 5, 60, new LevelDoorData[0],
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero, Vector2Int.right }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(4, 4), new[] { Vector2Int.zero }),
                    new LevelBlockData("b3", "Pink", Vector2Int.right, new[] { Vector2Int.zero }),
                    new LevelBlockData("b4", "Red", new Vector2Int(2, 2), new[] { Vector2Int.zero, Vector2Int.zero }),
                    new LevelBlockData("b5", "Blue", Vector2Int.zero, new[] { Vector2Int.zero })
                });

            LevelCanvasModel model = LevelCanvasModel.Create(level);

            Assert.That(model.IsOverlapping(0), Is.True, "b1 shares (1,0) with b3 and (0,0) with b5.");
            Assert.That(model.IsOverlapping(1), Is.False);
            Assert.That(model.IsOverlapping(2), Is.True);
            Assert.That(model.IsOverlapping(3), Is.False, "A block listing its own cell twice overlaps nothing.");
            Assert.That(model.IsOverlapping(4), Is.True);
            Assert.That(LevelBlockOverlap.Describe(level), Is.EqualTo("b1, b3 and b5"));
            Assert.That(LevelBlockOverlap.Describe(CreateLevelOne()), Is.Null);
            Assert.That(LevelBlockOverlap.Find(null), Is.Empty);
        }

        [Test]
        public void NullLevelGivesAnEmptyBoard()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(null);

            Assert.That(model, Is.SameAs(LevelCanvasModel.Empty));
            Assert.That(model.Width, Is.Zero);
            Assert.That(model.BlockCount, Is.Zero);
            Assert.That(model.GetBlockIndexAt(Vector2Int.zero), Is.EqualTo(-1));
            Assert.That(model.GetDoorIndexAt(BoardSide.Top, 0), Is.EqualTo(-1));
        }

        [Test]
        public void SelectionResolvesByUniqueIdThenIndexThenClears()
        {
            LevelCanvasModel model = LevelCanvasModel.Create(CreateLevelOne());

            LevelCanvasSelection byId = model.Resolve(LevelCanvasSelection.ForBlock(0, "b2"));
            Assert.That(byId, Is.EqualTo(LevelCanvasSelection.ForBlock(1, "b2")), "A moved block is found by its id.");

            LevelCanvasSelection byIndex = model.Resolve(LevelCanvasSelection.ForBlock(1, "renamed"));
            Assert.That(byIndex, Is.EqualTo(LevelCanvasSelection.ForBlock(1, "b2")), "A renamed block keeps its index.");

            Assert.That(model.Resolve(LevelCanvasSelection.ForBlock(5, "gone")), Is.EqualTo(LevelCanvasSelection.None));
            Assert.That(model.Resolve(LevelCanvasSelection.ForDoor(0, "d2")), Is.EqualTo(LevelCanvasSelection.ForDoor(1, "d2")));
            Assert.That(model.Resolve(LevelCanvasSelection.None), Is.EqualTo(LevelCanvasSelection.None));

            LevelData duplicates = new LevelData(
                "l", "L", 4, 4, 60, new LevelDoorData[0],
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }),
                    new LevelBlockData("b1", "Red", Vector2Int.one, new[] { Vector2Int.zero })
                });
            Assert.That(LevelCanvasModel.Create(duplicates).Resolve(LevelCanvasSelection.ForBlock(1, "b1")),
                Is.EqualTo(LevelCanvasSelection.ForBlock(1, "b1")), "A repeated id falls back to the index.");
        }

        [Test]
        public void BuildingTheModelLeavesTheLevelUnchanged()
        {
            LevelData level = CreateLevelOne();
            string before = JsonUtility.ToJson(level);

            LevelCanvasModel.Create(level);

            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before));
        }

        // The shape of the shipped level 1: a 4 x 5 board, two doors and two 2 x 2 blocks.
        internal static LevelData CreateLevelOne()
        {
            Vector2Int[] square = { Vector2Int.zero, Vector2Int.up, Vector2Int.right, Vector2Int.one };
            return new LevelData(
                "level-01", "Level 1", 4, 5, 300,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 1, 3, "LightBlue"),
                    new LevelDoorData("d2", BoardSide.Bottom, 0, 3, "Pink")
                },
                new[]
                {
                    new LevelBlockData("b1", "LightBlue", new Vector2Int(2, 2), square),
                    new LevelBlockData("b2", "Pink", new Vector2Int(0, 1), square)
                });
        }
    }
}
