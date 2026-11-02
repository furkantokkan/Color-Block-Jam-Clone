using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that the Level Editor's record copies carry every field of a block, door or level and change only
    /// the fields they name, without sharing arrays with the source.
    /// </summary>
    public sealed class LevelRecordCopyTests
    {
        private static readonly Vector2Int[] s_cells = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [Test]
        public void WithOriginChangesOnlyTheOrigin()
        {
            LevelBlockData block = CreateBlock();

            LevelBlockData copy = LevelRecordCopy.WithOrigin(block, new Vector2Int(3, 4));

            AssertBlock(copy, "b7", "Purple", new Vector2Int(3, 4), s_cells);
            AssertBlock(block, "b7", "Purple", new Vector2Int(1, 2), s_cells);
        }

        [Test]
        public void WithColorChangesOnlyTheColour()
        {
            LevelBlockData copy = LevelRecordCopy.WithColor(CreateBlock(), "Yellow");

            AssertBlock(copy, "b7", "Yellow", new Vector2Int(1, 2), s_cells);
        }

        [Test]
        public void WithCellsChangesOnlyCellsAndShape()
        {
            Vector2Int[] cells = { Vector2Int.zero, Vector2Int.up };

            LevelBlockData copy = LevelRecordCopy.WithCells(CreateBlock(), cells);
            cells[1] = new Vector2Int(9, 9);

            AssertBlock(copy, "b7", "Purple", new Vector2Int(1, 2), new[] { Vector2Int.zero, Vector2Int.up });
        }

        [Test]
        public void GetCellsReturnsANewArray()
        {
            LevelBlockData block = CreateBlock();

            Vector2Int[] cells = LevelRecordCopy.GetCells(block);
            cells[0] = new Vector2Int(5, 5);

            Assert.That(block.GetCellAt(0), Is.EqualTo(Vector2Int.zero), "Editing the copy leaves the block alone.");
        }

        [Test]
        public void WithBlocksCopiesEveryLevelFieldAndTheDoors()
        {
            LevelDoorData door = new LevelDoorData("d4", BoardSide.Right, 1, 2, "Orange");
            LevelData level = new LevelData("level-09", "Nine", 5, 7, 95, new[] { door }, new[] { CreateBlock() });
            LevelBlockData replacement = new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero });

            LevelData copy = LevelRecordCopy.WithBlocks(level, new[] { replacement });

            Assert.That(copy.Id, Is.EqualTo("level-09"));
            Assert.That(copy.Name, Is.EqualTo("Nine"));
            Assert.That(copy.Width, Is.EqualTo(5));
            Assert.That(copy.Height, Is.EqualTo(7));
            Assert.That(copy.TimerSeconds, Is.EqualTo(95));
            Assert.That(copy.DoorCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(copy.GetDoorAt(0)), Is.EqualTo(JsonUtility.ToJson(door)));
            Assert.That(copy.GetDoorAt(0), Is.Not.SameAs(door), "The level owns copies of its doors.");
            Assert.That(copy.BlockCount, Is.EqualTo(1));
            Assert.That(copy.GetBlockAt(0).Id, Is.EqualTo("b1"));
            Assert.That(copy.GetBlockAt(0), Is.Not.SameAs(replacement));
            Assert.That(level.GetBlockAt(0).Id, Is.EqualTo("b7"), "The source keeps its blocks.");
        }

        [Test]
        public void GetBlocksListsTheLevelsBlocksInOrder()
        {
            LevelData level = new LevelData("l", "L", 4, 4, 60, new LevelDoorData[0], new[]
            {
                CreateBlock(),
                new LevelBlockData("b2", "Red", new Vector2Int(3, 3), new[] { Vector2Int.zero })
            });

            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);

            Assert.That(blocks, Has.Length.EqualTo(2));
            Assert.That(blocks[0].Id, Is.EqualTo("b7"));
            Assert.That(blocks[1].Id, Is.EqualTo("b2"));
        }

        [Test]
        public void WithSpanChangesOnlyStartAndLength()
        {
            LevelDoorData door = CreateDoor();

            LevelDoorData copy = LevelRecordCopy.WithSpan(door, 0, 3);

            AssertDoor(copy, "d4", BoardSide.Right, 0, 3, "Orange");
            AssertDoor(door, "d4", BoardSide.Right, 1, 2, "Orange");
        }

        [Test]
        public void WithDoorColourChangesOnlyTheColour()
        {
            LevelDoorData door = CreateDoor();

            LevelDoorData copy = LevelRecordCopy.WithColor(door, "Blue");

            AssertDoor(copy, "d4", BoardSide.Right, 1, 2, "Blue");
            AssertDoor(door, "d4", BoardSide.Right, 1, 2, "Orange");
        }

        [Test]
        public void WithDoorsCopiesEveryLevelFieldAndTheBlocks()
        {
            LevelData level = new LevelData("level-09", "Nine", 5, 7, 95, new[] { CreateDoor() },
                new[] { CreateBlock() });
            LevelDoorData replacement = new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red");

            LevelData copy = LevelRecordCopy.WithDoors(level, new[] { replacement });

            Assert.That(copy.Id, Is.EqualTo("level-09"));
            Assert.That(copy.Name, Is.EqualTo("Nine"));
            Assert.That(copy.Width, Is.EqualTo(5));
            Assert.That(copy.Height, Is.EqualTo(7));
            Assert.That(copy.TimerSeconds, Is.EqualTo(95));
            Assert.That(copy.BlockCount, Is.EqualTo(1));
            Assert.That(JsonUtility.ToJson(copy.GetBlockAt(0)), Is.EqualTo(JsonUtility.ToJson(level.GetBlockAt(0))));
            Assert.That(copy.GetBlockAt(0), Is.Not.SameAs(level.GetBlockAt(0)), "The level owns copies of its blocks.");
            Assert.That(copy.DoorCount, Is.EqualTo(1));
            AssertDoor(copy.GetDoorAt(0), "d1", BoardSide.Top, 0, 1, "Red");
            Assert.That(copy.GetDoorAt(0), Is.Not.SameAs(replacement));
            Assert.That(level.GetDoorAt(0).Id, Is.EqualTo("d4"), "The source keeps its doors.");
        }

        [Test]
        public void WithBoardSetsSizeAndContentAndKeepsIdNameAndTimer()
        {
            LevelData level = new LevelData("level-09", "Nine", 5, 7, 95, new[] { CreateDoor() },
                new[] { CreateBlock() });
            LevelBlockData block = new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero });

            LevelData copy = LevelRecordCopy.WithBoard(level, 6, 4, new Vector2Int[0], new LevelDoorData[0], new[] { block },
                new LevelCellObjectData[0]);

            Assert.That(copy.Id, Is.EqualTo("level-09"));
            Assert.That(copy.Name, Is.EqualTo("Nine"));
            Assert.That(copy.TimerSeconds, Is.EqualTo(95));
            Assert.That(copy.Width, Is.EqualTo(6));
            Assert.That(copy.Height, Is.EqualTo(4));
            Assert.That(copy.DoorCount, Is.EqualTo(0));
            Assert.That(copy.BlockCount, Is.EqualTo(1));
            Assert.That(copy.GetBlockAt(0).Id, Is.EqualTo("b1"));
            Assert.That(copy.GetBlockAt(0), Is.Not.SameAs(block));
            Assert.That(level.Width, Is.EqualTo(5), "The source keeps its size.");
            Assert.That(level.DoorCount, Is.EqualTo(1));
        }

        [Test]
        public void GetDoorsListsTheLevelsDoorsInOrder()
        {
            LevelData level = new LevelData("l", "L", 4, 4, 60, new[]
            {
                CreateDoor(),
                new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red")
            }, new LevelBlockData[0]);

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            doors[0] = null;

            Assert.That(doors, Has.Length.EqualTo(2));
            Assert.That(doors[1].Id, Is.EqualTo("d1"));
            Assert.That(level.GetDoorAt(0).Id, Is.EqualTo("d4"), "Replacing an entry leaves the level alone.");
        }

        private static LevelBlockData CreateBlock()
        {
            return new LevelBlockData("b7", "Purple", new Vector2Int(1, 2), s_cells);
        }

        private static LevelDoorData CreateDoor()
        {
            return new LevelDoorData("d4", BoardSide.Right, 1, 2, "Orange");
        }

        private static void AssertDoor(LevelDoorData door, string id, BoardSide side, int start, int length,
            string colorId)
        {
            Assert.That(door.Id, Is.EqualTo(id));
            Assert.That(door.Side, Is.EqualTo(side));
            Assert.That(door.Start, Is.EqualTo(start));
            Assert.That(door.Length, Is.EqualTo(length));
            Assert.That(door.ColorId, Is.EqualTo(colorId));
        }

        // A block's piece is the key of its cells (DEC-193), so the cells check covers it.
        private static void AssertBlock(LevelBlockData block, string id, string colorId, Vector2Int origin,
            Vector2Int[] cells)
        {
            Assert.That(block.Id, Is.EqualTo(id));
            Assert.That(block.ColorId, Is.EqualTo(colorId));
            Assert.That(block.Origin, Is.EqualTo(origin));
            Assert.That(LevelRecordCopy.GetCells(block), Is.EqualTo(cells));
        }
    }
}
