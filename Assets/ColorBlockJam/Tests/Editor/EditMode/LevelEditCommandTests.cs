using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor block, door and board commands: each success returns a new level with exactly one
    /// edit and leaves the source untouched, and each refusal (off-board, overlap, missing block, door or colour,
    /// size limit) names what went wrong.
    /// </summary>
    public sealed class LevelEditCommandTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private static readonly Vector2Int[] s_upright = { Vector2Int.zero, Vector2Int.up };
        private static readonly BoardSide[] s_sides = { BoardSide.Top, BoardSide.Bottom, BoardSide.Left, BoardSide.Right };

        [Test]
        public void PlaceAddsOneBlockWithTheNextFreeIdAndKeepsTheSource()
        {
            LevelData level = CreateLevel();
            string before = JsonUtility.ToJson(level);

            bool placed = LevelEditCommands.TryPlaceBlock(level, s_domino, "Blue", new Vector2Int(0, 3),
                out LevelData result, out string issue);

            Assert.That(placed, Is.True, issue);
            Assert.That(issue, Is.Null);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source level never changes.");
            Assert.That(result.BlockCount, Is.EqualTo(3));
            LevelBlockData block = result.GetBlockAt(2);
            Assert.That(block.Id, Is.EqualTo("b2"), "b1 and b3 exist, so b2 is the lowest free id.");
            Assert.That(block.ColorId, Is.EqualTo("Blue"));
            Assert.That(block.Key, Is.EqualTo(LevelTestShapes.Key(s_domino)));
            Assert.That(block.Origin, Is.EqualTo(new Vector2Int(0, 3)));
            Assert.That(LevelRecordCopy.GetCells(block), Is.EqualTo(s_domino));
            Assert.That(result.GetBlockAt(0).Id, Is.EqualTo("b1"));
            Assert.That(result.GetBlockAt(1).Id, Is.EqualTo("b3"));
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void PlaceRefusesAShapeThatLeavesTheBoard()
        {
            LevelData level = CreateLevel();

            bool placed = LevelEditCommands.TryPlaceBlock(level, s_domino, "Blue", new Vector2Int(3, 3),
                out LevelData result, out string issue);

            Assert.That(placed, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Cell (4,3) is off the 4 x 5 board."));
        }

        [Test]
        public void PlaceRefusesAShapeThatOverlapsABlock()
        {
            LevelData level = CreateLevel();

            bool placed = LevelEditCommands.TryPlaceBlock(level, s_corner, "Blue", new Vector2Int(1, 0),
                out _, out string issue);

            Assert.That(placed, Is.False);
            Assert.That(issue, Is.EqualTo("Overlaps Red block b1 at (1,0)."));
        }

        [Test]
        public void PlaceRefusesWithoutALevelOrAShape()
        {
            Assert.That(LevelEditCommands.TryPlaceBlock(null, s_single, "Red", Vector2Int.zero, out _,
                out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a level first."));

            Assert.That(LevelEditCommands.TryPlaceBlock(CreateLevel(), new Vector2Int[0], "Red",
                new Vector2Int(0, 4), out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a shape with at least one cell first."));
        }

        [Test]
        public void MoveChangesOnlyTheOriginAndMayOverlapTheBlocksOwnOldCells()
        {
            LevelData level = CreateLevel();
            string before = JsonUtility.ToJson(level);

            bool moved = LevelEditCommands.TryMoveBlock(level, 0, new Vector2Int(1, 0), out LevelData result,
                out string issue);

            Assert.That(moved, Is.True, issue);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before));
            LevelBlockData block = result.GetBlockAt(0);
            Assert.That(block.Origin, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(block.Id, Is.EqualTo("b1"));
            Assert.That(block.ColorId, Is.EqualTo("Red"));
            Assert.That(block.Key, Is.EqualTo(LevelTestShapes.Key(s_domino)));
            Assert.That(LevelRecordCopy.GetCells(block), Is.EqualTo(s_domino));
            Assert.That(JsonUtility.ToJson(result.GetBlockAt(1)), Is.EqualTo(JsonUtility.ToJson(level.GetBlockAt(1))));
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void MoveRefusesOffBoardAndOverlap()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEditCommands.TryMoveBlock(level, 0, new Vector2Int(-1, 0), out _, out string issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Cell (-1,0) is off the 4 x 5 board."));

            Assert.That(LevelEditCommands.TryMoveBlock(level, 0, new Vector2Int(2, 2), out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Overlaps DarkGreen block b3 at (2,2)."));
        }

        [Test]
        public void RemoveKeepsTheOtherBlocksInOrder()
        {
            LevelData level = CreateLevel();

            bool removed = LevelEditCommands.TryRemoveBlock(level, 0, out LevelData result, out string issue);

            Assert.That(removed, Is.True, issue);
            Assert.That(level.BlockCount, Is.EqualTo(2), "The source keeps its blocks.");
            Assert.That(result.BlockCount, Is.EqualTo(1));
            Assert.That(result.GetBlockAt(0).Id, Is.EqualTo("b3"));
            Assert.That(result.DoorCount, Is.EqualTo(level.DoorCount), "Doors stay.");
        }

        [Test]
        public void CommandsOnAMissingBlockAreRefused()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEditCommands.TryRemoveBlock(level, 2, out _, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a block first."));
            Assert.That(LevelEditCommands.TryMoveBlock(level, -1, Vector2Int.zero, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a block first."));
            Assert.That(LevelEditCommands.TryRecolorBlock(level, 5, "Blue", out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a block first."));
            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 5, s_single, out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Select a block first."));
        }

        [Test]
        public void RecolourChangesOnlyTheColour()
        {
            LevelData level = CreateLevel();

            bool recoloured = LevelEditCommands.TryRecolorBlock(level, 1, "Pink", out LevelData result, out string issue);

            Assert.That(recoloured, Is.True, issue);
            LevelBlockData block = result.GetBlockAt(1);
            Assert.That(block.ColorId, Is.EqualTo("Pink"));
            Assert.That(block.Id, Is.EqualTo("b3"));
            Assert.That(block.Origin, Is.EqualTo(level.GetBlockAt(1).Origin));
            Assert.That(level.GetBlockAt(1).ColorId, Is.EqualTo("DarkGreen"), "The source keeps its colour.");

            Assert.That(LevelEditCommands.TryRecolorBlock(level, 1, " ", out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a colour first."));
        }

        [Test]
        public void RotationKeepsBlockIdentityEffectsAndOtherLevelData()
        {
            LevelData source = new LevelData("rotate", "Rotate", 4, 5, 60,
                new[] { new LevelDoorData("door", BoardSide.Top, 0, 2, "Red") },
                new[]
                {
                    new LevelBlockData("ice", "Red", Vector2Int.zero, s_domino,
                        new[] { LevelTestData.Effect("ice", ("clearsToThaw", "1")) }),
                    new LevelBlockData("other", "Blue", new Vector2Int(2, 2), s_single)
                });
            string before = JsonUtility.ToJson(source);

            Assert.That(LevelEditCommands.TryRotateBlock(source, 0, true, out LevelData rotated, out string issue),
                Is.True, issue);

            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            LevelBlockData block = rotated.GetBlockAt(0);
            Assert.That(block.Id, Is.EqualTo("ice"));
            Assert.That(block.ColorId, Is.EqualTo("Red"));
            Assert.That(block.Origin, Is.EqualTo(Vector2Int.zero), "The board kick keeps the turn inside the bottom edge.");
            Assert.That(LevelRecordCopy.GetCells(block), Is.EquivalentTo(s_upright));
            Assert.That(JsonUtility.ToJson(block.GetEffectAt(0)),
                Is.EqualTo(JsonUtility.ToJson(source.GetBlockAt(0).GetEffectAt(0))));
            Assert.That(JsonUtility.ToJson(rotated.GetBlockAt(1)), Is.EqualTo(JsonUtility.ToJson(source.GetBlockAt(1))));
            Assert.That(JsonUtility.ToJson(rotated.GetDoorAt(0)), Is.EqualTo(JsonUtility.ToJson(source.GetDoorAt(0))));
            Assert.That(rotated.Validate(out issue), Is.True, issue);
        }

        [TestCase("Block")]
        [TestCase("Void")]
        [TestCase("ClosedBarrier")]
        public void RotationRefusesAnOccupiedLandingWithoutChangingTheSource(string obstacle)
        {
            Vector2Int landing = new Vector2Int(0, 1);
            LevelBlockData block = new LevelBlockData("turn", "Red", Vector2Int.zero, s_domino);
            LevelData source = new LevelData("rotate", "Rotate", 4, 5, 60,
                obstacle == "Void" ? new[] { landing } : new Vector2Int[0],
                new[] { new LevelDoorData("door", BoardSide.Top, 0, 2, "Red") },
                obstacle == "Block"
                    ? new[] { block, new LevelBlockData("other", "Blue", landing, s_single) }
                    : new[] { block },
                obstacle == "ClosedBarrier"
                    ? new[] { LevelTestData.Object("barrier", LevelTestData.Effect("barrier"), landing) }
                    : new LevelCellObjectData[0]);
            string before = JsonUtility.ToJson(source);

            Assert.That(LevelEditCommands.TryRotateBlock(source, 0, true, out LevelData rotated, out string issue), Is.False);

            Assert.That(rotated, Is.Null);
            Assert.That(issue, Is.Not.Empty);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
        }

        [Test]
        public void ReplaceCellsKeepsOriginIdAndColour()
        {
            LevelData level = CreateLevel();
            Vector2Int[] rotated = LevelPieceTools.RotateCounterClockwise(s_domino);

            bool replaced = LevelEditCommands.TryReplaceBlockCells(level, 0, rotated, out LevelData result,
                out string issue);

            Assert.That(replaced, Is.True, issue);
            LevelBlockData block = result.GetBlockAt(0);
            Assert.That(LevelRecordCopy.GetCells(block), Is.EqualTo(rotated));
            Assert.That(block.Origin, Is.EqualTo(Vector2Int.zero));
            Assert.That(block.Key, Is.EqualTo(LevelTestShapes.Key(s_domino)),
                "A turn keeps the piece: its cells have the same key (RULE-5, DEC-193).");
            Assert.That(block.ColorId, Is.EqualTo("Red"));

            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, s_corner, out result, out issue),
                Is.True, issue);
            Assert.That(result.GetBlockAt(0).Key, Is.EqualTo(LevelTestShapes.Key(s_corner)),
                "Piece Type gives the block the new piece's cells, so its key.");
        }

        [Test]
        public void ReplaceCellsRefusesOffBoardOverlapAndEmptyFootprints()
        {
            LevelData level = CreateLevel();
            Vector2Int[] tall = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(0, 3),
                new Vector2Int(0, 4), new Vector2Int(0, 5) };

            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, tall, out _, out string issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Cell (0,5) is off the 4 x 5 board."));

            // b3 sits at (2,2)-(3,2); four cells right of b1's origin reach it.
            Vector2Int[] wide = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(2, 1),
                new Vector2Int(2, 2) };
            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, wide, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Overlaps DarkGreen block b3 at (2,2)."));

            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, new Vector2Int[0], out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Block b1 needs at least one cell."));
        }

        [Test]
        public void AReStampMayOverlapAnotherBlockButStillRefusesTheBoardEdge()
        {
            LevelData level = CreateLevel();
            // b3 sits at (2,2)-(3,2); four cells right of b1's origin reach it (contract D3-B).
            Vector2Int[] wide = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(2, 1),
                new Vector2Int(2, 2) };

            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, wide, true, out LevelData result,
                out string issue), Is.True, issue);
            Assert.That(LevelRecordCopy.GetCells(result.GetBlockAt(0)), Is.EqualTo(wide));
            Assert.That(LevelBlockOverlap.Describe(result), Is.EqualTo("b1 and b3"));
            Assert.That(result.Validate(out string error), Is.False, "Save Level still refuses the overlap.");
            Assert.That(error, Does.Contain("overlap"));

            Vector2Int[] tall = { Vector2Int.zero, Vector2Int.up, new Vector2Int(0, 2), new Vector2Int(0, 3),
                new Vector2Int(0, 4), new Vector2Int(0, 5) };
            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0, tall, true, out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Cell (0,5) is off the 4 x 5 board."));
        }

        [Test]
        public void NextBlockIdSkipsUsedIds()
        {
            Assert.That(LevelEditCommands.GetNextBlockId(CreateLevel()), Is.EqualTo("b2"));
            LevelData empty = new LevelData("l", "L", 3, 3, 60, new LevelDoorData[0], new LevelBlockData[0]);
            Assert.That(LevelEditCommands.GetNextBlockId(empty), Is.EqualTo("b1"));
        }

        [Test]
        public void PlaceDoorAddsANewDoorWithTheNextFreeIdAndKeepsTheSource()
        {
            LevelData level = CreateDoorLevel();
            string before = JsonUtility.ToJson(level);

            bool placed = LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Right, 2, "DarkGreen",
                out LevelData result, out int doorIndex, out string issue);

            Assert.That(placed, Is.True, issue);
            Assert.That(issue, Is.Null);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source level never changes.");
            Assert.That(doorIndex, Is.EqualTo(3), "A new door goes to the end of the list.");
            Assert.That(result.DoorCount, Is.EqualTo(4));
            AssertDoor(result.GetDoorAt(3), "d4", BoardSide.Right, 2, 1, "DarkGreen");
            for (int index = 0; index < level.DoorCount; index++)
            {
                Assert.That(JsonUtility.ToJson(result.GetDoorAt(index)),
                    Is.EqualTo(JsonUtility.ToJson(level.GetDoorAt(index))), "The other doors stay.");
            }

            Assert.That(result.BlockCount, Is.EqualTo(level.BlockCount), "Blocks stay.");
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void PlaceDoorBesideAnotherColourAddsANewDoor()
        {
            LevelData level = CreateDoorLevel();

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 0, "Blue", out LevelData result,
                out int doorIndex, out string issue), Is.True, issue);
            Assert.That(doorIndex, Is.EqualTo(3));
            AssertDoor(result.GetDoorAt(3), "d4", BoardSide.Top, 0, 1, "Blue");
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 1, 2, "Red");

            Assert.That(result.Validate(out string error), Is.True, error);

            // Colour ids compare ordinally, so "red" does not join the two Red doors around segment 3 (the issues
            // list then flags it: it is not a gate colour).
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 3, "red", out result,
                out doorIndex, out issue), Is.True, issue);
            Assert.That(result.DoorCount, Is.EqualTo(4));
            AssertDoor(result.GetDoorAt(doorIndex), "d4", BoardSide.Top, 3, 1, "red");
        }

        [Test]
        public void PlaceDoorExtendsASameColourDoorThatEndsBeforeTheSegment()
        {
            LevelData level = CreateDoorLevel();

            bool placed = LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 5, "Red",
                out LevelData result, out int doorIndex, out string issue);

            Assert.That(placed, Is.True, issue);
            Assert.That(doorIndex, Is.EqualTo(1));
            Assert.That(result.DoorCount, Is.EqualTo(3), "Extending adds no door.");
            AssertDoor(result.GetDoorAt(1), "d2", BoardSide.Top, 4, 2, "Red");
            AssertDoor(level.GetDoorAt(1), "d2", BoardSide.Top, 4, 1, "Red");
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void PlaceDoorExtendsASameColourDoorThatStartsAfterTheSegment()
        {
            LevelData level = CreateDoorLevel();

            bool placed = LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 0, "Red",
                out LevelData result, out int doorIndex, out string issue);

            Assert.That(placed, Is.True, issue);
            Assert.That(doorIndex, Is.EqualTo(0));
            Assert.That(result.DoorCount, Is.EqualTo(3));
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 0, 3, "Red");
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void PlaceDoorBetweenTwoSameColourDoorsMergesThemIntoTheLowerIndex()
        {
            LevelData level = new LevelData("level-doors", "Doors", 6, 5, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 2, 1, "Red"),
                    new LevelDoorData("d2", BoardSide.Top, 4, 1, "Red"),
                    new LevelDoorData("d3", BoardSide.Left, 0, 2, "Blue")
                },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });

            bool placed = LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 3, "Red",
                out LevelData result, out int doorIndex, out string issue);

            Assert.That(placed, Is.True, issue);
            Assert.That(doorIndex, Is.EqualTo(0));
            Assert.That(result.DoorCount, Is.EqualTo(2), "The two doors become one.");
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 2, 3, "Red");
            AssertDoor(result.GetDoorAt(1), "d3", BoardSide.Left, 0, 2, "Blue");
            Assert.That(result.Validate(out string error), Is.True, error);

            // The later door on the edge can be the earlier list entry; that entry keeps its id and index.
            LevelData reversed = new LevelData("level-doors", "Doors", 6, 5, 60,
                new[]
                {
                    new LevelDoorData("d7", BoardSide.Top, 4, 1, "Red"),
                    new LevelDoorData("d2", BoardSide.Top, 2, 1, "Red")
                },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(reversed, BoardSide.Top, 3, "Red", out result,
                out doorIndex, out issue), Is.True, issue);
            Assert.That(doorIndex, Is.EqualTo(0));
            Assert.That(result.DoorCount, Is.EqualTo(1));
            AssertDoor(result.GetDoorAt(0), "d7", BoardSide.Top, 2, 3, "Red");
        }

        [Test]
        public void PlaceDoorRefusesAGateLongerThanThreeCells()
        {
            LevelData level = CreateDoorLevel();
            string before = JsonUtility.ToJson(level);

            // d1 covers Top 1-2 and d2 covers Top 4: joining them through 3 would make one four-cell gate.
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 3, "Red", out LevelData result,
                out int doorIndex, out string issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(doorIndex, Is.EqualTo(-1));
            Assert.That(issue, Is.EqualTo("Gate d1 would span 4 cells; gates span 1 to 3."));
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 0, "Red", out result,
                out _, out issue), Is.True, issue);
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 0, 3, "Red");
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(result, BoardSide.Top, 3, "Red", out _, out _,
                out issue), Is.False, "A three-cell gate does not grow.");
            Assert.That(issue, Does.Contain("gates span 1 to 3"));
            Assert.That(LevelEditCommands.k_maxDoorLength, Is.EqualTo(3));
        }

        [Test]
        public void PlaceDoorRefusesASegmentAnotherDoorHolds()
        {
            LevelData level = CreateDoorLevel();

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 1, "Red", out LevelData result,
                out int doorIndex, out string issue), Is.False, "Same colour adds nothing either.");
            Assert.That(issue, Is.EqualTo("The Top edge at 1 already holds door d1."));
            Assert.That(result, Is.Null);
            Assert.That(doorIndex, Is.EqualTo(-1));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 4, "Blue", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("The Top edge at 4 already holds door d2."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Left, 1, "Blue", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("The Left edge at 1 already holds door d3."));
        }

        [Test]
        public void PlaceDoorRefusesOffTheEdgeWithoutAColourOrALevel()
        {
            LevelData level = CreateDoorLevel();

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Top, 6, "Red", out LevelData result,
                out int doorIndex, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("Segment 6 is off the Top edge."));
            Assert.That(result, Is.Null);
            Assert.That(doorIndex, Is.EqualTo(-1));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Bottom, -1, "Red", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Segment -1 is off the Bottom edge."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Left, 5, "Red", out _, out _,
                out issue), Is.False, "Left and Right segments run along the 5-cell height.");
            Assert.That(issue, Is.EqualTo("Segment 5 is off the Left edge."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Right, 2, " ", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a colour first."));
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Right, 2, null, out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a colour first."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(null, BoardSide.Right, 2, "Red", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a level first."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, (BoardSide)9, 0, "Red", out _, out _,
                out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a board side first."));
        }

        [Test]
        public void NextDoorIdSkipsUsedIds()
        {
            Assert.That(LevelEditCommands.GetNextDoorId(CreateDoorLevel()), Is.EqualTo("d4"));
            Assert.That(LevelEditCommands.GetNextDoorId(CreateLevel()), Is.EqualTo("d2"));
            LevelData gap = new LevelData("l", "L", 3, 3, 60,
                new[] { new LevelDoorData("d2", BoardSide.Top, 0, 1, "Red") }, new LevelBlockData[0]);
            Assert.That(LevelEditCommands.GetNextDoorId(gap), Is.EqualTo("d1"));
        }

        [Test]
        public void RemoveDoorRemovesTheWholeDoorAndKeepsTheOthersInOrder()
        {
            LevelData level = CreateDoorLevel();
            string before = JsonUtility.ToJson(level);

            bool removed = LevelEditCommands.TryRemoveDoor(level, 0, out LevelData result, out string issue);

            Assert.That(removed, Is.True, issue);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source keeps its doors.");
            Assert.That(result.DoorCount, Is.EqualTo(2));
            AssertDoor(result.GetDoorAt(0), "d2", BoardSide.Top, 4, 1, "Red");
            AssertDoor(result.GetDoorAt(1), "d3", BoardSide.Left, 0, 2, "Blue");
            Assert.That(result.BlockCount, Is.EqualTo(level.BlockCount), "Blocks stay.");
        }

        [Test]
        public void RecolourDoorChangesOnlyTheColour()
        {
            LevelData level = CreateDoorLevel();

            bool recoloured = LevelEditCommands.TryRecolorDoor(level, 2, "Pink", out LevelData result,
                out string issue);

            Assert.That(recoloured, Is.True, issue);
            AssertDoor(result.GetDoorAt(2), "d3", BoardSide.Left, 0, 2, "Pink");
            AssertDoor(level.GetDoorAt(2), "d3", BoardSide.Left, 0, 2, "Blue");
            Assert.That(result.DoorCount, Is.EqualTo(3));

            Assert.That(LevelEditCommands.TryRecolorDoor(level, 2, " ", out result, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Pick a colour first."));
            Assert.That(result, Is.Null);
        }

        [Test]
        public void CommandsOnAMissingDoorAreRefused()
        {
            LevelData level = CreateDoorLevel();

            Assert.That(LevelEditCommands.TryRemoveDoor(level, 3, out LevelData result, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a door first."));
            Assert.That(result, Is.Null);
            Assert.That(LevelEditCommands.TryRemoveDoor(level, -1, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a door first."));
            Assert.That(LevelEditCommands.TryRecolorDoor(level, 5, "Blue", out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a door first."));
            Assert.That(LevelEditCommands.TryRemoveDoor(null, 0, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a level first."));
            Assert.That(LevelEditCommands.TryRecolorDoor(null, 0, "Blue", out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Select a level first."));
        }

        [Test]
        public void GrowLeftShiftsBlocksAndTopAndBottomDoors()
        {
            LevelData level = CreateResizeLevel();
            string before = JsonUtility.ToJson(level);

            bool resized = LevelEditCommands.TryResizeBoard(level, BoardSide.Left, 1, out LevelData result,
                out LevelResizeReport report, out string issue);

            Assert.That(resized, Is.True, issue);
            Assert.That(issue, Is.Null);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source level never changes.");
            Assert.That(result.Width, Is.EqualTo(6));
            Assert.That(result.Height, Is.EqualTo(6));
            AssertOrigins(result, new Vector2Int(1, 0), new Vector2Int(3, 3), new Vector2Int(5, 4));
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 1, 2, "Red");
            AssertDoor(result.GetDoorAt(1), "d2", BoardSide.Bottom, 4, 2, "DarkGreen");
            AssertDoor(result.GetDoorAt(2), "d3", BoardSide.Left, 1, 2, "Blue");
            AssertDoor(result.GetDoorAt(3), "d4", BoardSide.Right, 0, 1, "Yellow");
            AssertDoor(result.GetDoorAt(4), "d5", BoardSide.Bottom, 1, 1, "Pink");
            AssertDoor(result.GetDoorAt(5), "d6", BoardSide.Right, 3, 3, "Orange");
            AssertNoRemovals(report);
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void GrowBottomShiftsBlocksAndLeftAndRightDoors()
        {
            LevelData level = CreateResizeLevel();

            bool resized = LevelEditCommands.TryResizeBoard(level, BoardSide.Bottom, 2, out LevelData result,
                out LevelResizeReport report, out string issue);

            Assert.That(resized, Is.True, issue);
            Assert.That(result.Width, Is.EqualTo(5));
            Assert.That(result.Height, Is.EqualTo(8));
            AssertOrigins(result, new Vector2Int(0, 2), new Vector2Int(2, 5), new Vector2Int(4, 6));
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 0, 2, "Red");
            AssertDoor(result.GetDoorAt(1), "d2", BoardSide.Bottom, 3, 2, "DarkGreen");
            AssertDoor(result.GetDoorAt(2), "d3", BoardSide.Left, 3, 2, "Blue");
            AssertDoor(result.GetDoorAt(3), "d4", BoardSide.Right, 2, 1, "Yellow");
            AssertDoor(result.GetDoorAt(4), "d5", BoardSide.Bottom, 0, 1, "Pink");
            AssertDoor(result.GetDoorAt(5), "d6", BoardSide.Right, 5, 3, "Orange");
            AssertNoRemovals(report);
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void GrowRightAndTopChangeOnlyTheSize()
        {
            LevelData level = CreateResizeLevel();

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Right, 1, out LevelData wider,
                out LevelResizeReport report, out string issue), Is.True, issue);
            Assert.That(wider.Width, Is.EqualTo(6));
            Assert.That(wider.Height, Is.EqualTo(6));
            AssertSameContent(level, wider);
            AssertNoRemovals(report);

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Top, 3, out LevelData taller,
                out report, out issue), Is.True, issue);
            Assert.That(taller.Width, Is.EqualTo(5));
            Assert.That(taller.Height, Is.EqualTo(9));
            AssertSameContent(level, taller);
            AssertNoRemovals(report);
        }

        [Test]
        public void FourGrowsThenFourShrinksOnOneSideRestoreTheLevelExactly()
        {
            LevelData level = CreateRoundTripLevel();
            string original = JsonUtility.ToJson(level);

            for (int sideIndex = 0; sideIndex < s_sides.Length; sideIndex++)
            {
                BoardSide side = s_sides[sideIndex];
                LevelData current = level;
                for (int step = 0; step < 4; step++)
                {
                    Assert.That(LevelEditCommands.TryResizeBoard(current, side, 1, out LevelData grown,
                        out LevelResizeReport report, out string issue), Is.True, $"{side} + {step}: {issue}");
                    AssertNoRemovals(report);
                    current = grown;
                }

                for (int step = 0; step < 4; step++)
                {
                    int lineLength = side == BoardSide.Left || side == BoardSide.Right ? current.Height : current.Width;
                    Assert.That(LevelEditCommands.TryResizeBoard(current, side, -1, out LevelData shrunk,
                        out LevelResizeReport report, out string issue), Is.True, $"{side} - {step}: {issue}");
                    Assert.That(report.HasRemovals, Is.False, $"{side} - {step} removes only empty lines.");
                    Assert.That(report.RemovedCells.Count, Is.EqualTo(lineLength),
                        "The report still lists the removed line's cells for the hatch.");
                    current = shrunk;
                }

                Assert.That(JsonUtility.ToJson(current), Is.EqualTo(original), $"{side} round trip.");
            }
        }

        [Test]
        public void ShrinkLeftRemovesBlocksInTheLineAndCutsDoorsThatCrossIt()
        {
            LevelData level = CreateResizeLevel();
            string before = JsonUtility.ToJson(level);

            bool resized = LevelEditCommands.TryResizeBoard(level, BoardSide.Left, -1, out LevelData result,
                out LevelResizeReport report, out string issue);

            Assert.That(resized, Is.True, issue);
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source level never changes.");
            Assert.That(result.Width, Is.EqualTo(4));
            Assert.That(result.Height, Is.EqualTo(6));
            Assert.That(result.BlockCount, Is.EqualTo(2), "b1 has a cell in column 0.");
            Assert.That(result.GetBlockAt(0).Id, Is.EqualTo("b2"));
            Assert.That(result.GetBlockAt(1).Id, Is.EqualTo("b3"));
            AssertOrigins(result, new Vector2Int(1, 3), new Vector2Int(3, 4));
            Assert.That(result.DoorCount, Is.EqualTo(5), "d5 held only the removed segment.");
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 0, 1, "Red");
            AssertDoor(result.GetDoorAt(1), "d2", BoardSide.Bottom, 2, 2, "DarkGreen");
            AssertDoor(result.GetDoorAt(2), "d3", BoardSide.Left, 1, 2, "Blue");
            AssertDoor(result.GetDoorAt(3), "d4", BoardSide.Right, 0, 1, "Yellow");
            AssertDoor(result.GetDoorAt(4), "d6", BoardSide.Right, 3, 3, "Orange");

            Assert.That(report.HasRemovals, Is.True);
            Assert.That(report.RemovedBlockCount, Is.EqualTo(1));
            Assert.That(report.RemovedDoorCount, Is.EqualTo(1));
            Assert.That(report.ShortenedDoorCount, Is.EqualTo(1));
            // The removed column, plus b1's cell at (1,0) that goes with it, so the hatch shows everything removed.
            Assert.That(report.RemovedCells, Is.EquivalentTo(new[]
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2),
                new Vector2Int(0, 3), new Vector2Int(0, 4), new Vector2Int(0, 5), new Vector2Int(1, 0)
            }));
            Assert.That(report.RemovedDoorSegments, Is.EquivalentTo(new[]
            {
                new LevelEdgeSegment(BoardSide.Top, 0), new LevelEdgeSegment(BoardSide.Bottom, 0)
            }));
            Assert.That(report.Describe(), Is.EqualTo("Remove 1 block and 1 door, and shorten 1 door?"));
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ShrinkTopByTwoRemovesTheTopRowsAndShortensSideDoors()
        {
            LevelData level = CreateResizeLevel();

            bool resized = LevelEditCommands.TryResizeBoard(level, BoardSide.Top, -2, out LevelData result,
                out LevelResizeReport report, out string issue);

            Assert.That(resized, Is.True, issue);
            Assert.That(result.Height, Is.EqualTo(4));
            Assert.That(result.BlockCount, Is.EqualTo(2), "b3 reaches rows 4 and 5.");
            AssertOrigins(result, Vector2Int.zero, new Vector2Int(2, 3));
            Assert.That(result.DoorCount, Is.EqualTo(6));
            AssertDoor(result.GetDoorAt(0), "d1", BoardSide.Top, 0, 2, "Red");
            AssertDoor(result.GetDoorAt(2), "d3", BoardSide.Left, 1, 2, "Blue");
            AssertDoor(result.GetDoorAt(5), "d6", BoardSide.Right, 3, 1, "Orange");

            Assert.That(report.RemovedBlockCount, Is.EqualTo(1));
            Assert.That(report.RemovedDoorCount, Is.EqualTo(0));
            Assert.That(report.ShortenedDoorCount, Is.EqualTo(1));
            Assert.That(report.RemovedCells.Count, Is.EqualTo(10));
            Assert.That(report.RemovedCells, Has.Member(new Vector2Int(4, 5)));
            Assert.That(report.RemovedCells, Has.No.Member(new Vector2Int(0, 3)));
            Assert.That(report.RemovedDoorSegments, Is.EquivalentTo(new[]
            {
                new LevelEdgeSegment(BoardSide.Right, 4), new LevelEdgeSegment(BoardSide.Right, 5)
            }));
            Assert.That(report.Describe(), Is.EqualTo("Remove 1 block and shorten 1 door?"));
            Assert.That(result.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ResizeByZeroReturnsAnEqualLevel()
        {
            LevelData level = CreateResizeLevel();

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, 0, out LevelData result,
                out LevelResizeReport report, out string issue), Is.True, issue);
            Assert.That(JsonUtility.ToJson(result), Is.EqualTo(JsonUtility.ToJson(level)));
            AssertNoRemovals(report);
        }

        [Test]
        public void ResizeRefusesSizesOutsideTheLimits()
        {
            LevelData smallest = CreateEmptyLevel(3, 3);
            LevelData largest = CreateEmptyLevel(8, 10);

            AssertResizeRefused(smallest, BoardSide.Left, -1, "The board is already at its smallest width.");
            AssertResizeRefused(smallest, BoardSide.Right, -1, "The board is already at its smallest width.");
            AssertResizeRefused(smallest, BoardSide.Top, -1, "The board is already at its smallest height.");
            AssertResizeRefused(smallest, BoardSide.Bottom, -1, "The board is already at its smallest height.");
            AssertResizeRefused(largest, BoardSide.Right, 1, "The board is already at its largest width.");
            AssertResizeRefused(largest, BoardSide.Left, 1, "The board is already at its largest width.");
            AssertResizeRefused(largest, BoardSide.Bottom, 1, "The board is already at its largest height.");
            AssertResizeRefused(largest, BoardSide.Top, 1, "The board is already at its largest height.");
            AssertResizeRefused(CreateLevel(), BoardSide.Right, -2, "A 2 x 5 board is outside 3-8 by 3-10.");
            AssertResizeRefused(CreateLevel(), BoardSide.Top, 6, "A 4 x 11 board is outside 3-8 by 3-10.");
            AssertResizeRefused(null, BoardSide.Top, 1, "Select a level first.");
            AssertResizeRefused(CreateLevel(), (BoardSide)9, 1, "Pick a board side first.");
        }

        [Test]
        public void ResizeReportDescribesExactCountsWithoutIds()
        {
            Assert.That(new LevelResizeReport(2, 1, 0, null, null).Describe(), Is.EqualTo("Remove 2 blocks and 1 door?"));
            Assert.That(new LevelResizeReport(3, 0, 0, null, null).Describe(), Is.EqualTo("Remove 3 blocks?"));
            Assert.That(new LevelResizeReport(0, 1, 0, null, null).Describe(), Is.EqualTo("Remove 1 door?"));
            Assert.That(new LevelResizeReport(0, 0, 2, null, null).Describe(), Is.EqualTo("Shorten 2 doors?"));
            Assert.That(new LevelResizeReport(0, 2, 1, null, null).Describe(),
                Is.EqualTo("Remove 2 doors and shorten 1 door?"));
            Assert.That(new LevelResizeReport(1, 2, 3, null, null).Describe(),
                Is.EqualTo("Remove 1 block and 2 doors, and shorten 3 doors?"));

            LevelResizeReport empty = default;
            Assert.That(empty.HasRemovals, Is.False);
            Assert.That(empty.Describe(), Is.Empty);
            Assert.That(empty.RemovedCells, Is.Empty);
            Assert.That(empty.RemovedDoorSegments, Is.Empty);
        }

        private static void AssertResizeRefused(LevelData level, BoardSide side, int delta, string expected)
        {
            string before = level == null ? null : JsonUtility.ToJson(level);

            bool resized = LevelEditCommands.TryResizeBoard(level, side, delta, out LevelData result,
                out LevelResizeReport report, out string issue);

            Assert.That(resized, Is.False, $"{side} {delta}");
            Assert.That(issue, Is.EqualTo(expected));
            Assert.That(result, Is.Null);
            Assert.That(report.HasRemovals, Is.False);
            if (level != null)
            {
                Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before));
            }
        }

        private static void AssertNoRemovals(LevelResizeReport report)
        {
            Assert.That(report.HasRemovals, Is.False);
            Assert.That(report.RemovedBlockCount, Is.EqualTo(0));
            Assert.That(report.RemovedDoorCount, Is.EqualTo(0));
            Assert.That(report.ShortenedDoorCount, Is.EqualTo(0));
            Assert.That(report.RemovedCells, Is.Empty);
            Assert.That(report.RemovedDoorSegments, Is.Empty);
            Assert.That(report.Describe(), Is.Empty);
        }

        private static void AssertSameContent(LevelData expected, LevelData actual)
        {
            Assert.That(actual.DoorCount, Is.EqualTo(expected.DoorCount));
            for (int index = 0; index < expected.DoorCount; index++)
            {
                Assert.That(JsonUtility.ToJson(actual.GetDoorAt(index)),
                    Is.EqualTo(JsonUtility.ToJson(expected.GetDoorAt(index))));
            }

            Assert.That(actual.BlockCount, Is.EqualTo(expected.BlockCount));
            for (int index = 0; index < expected.BlockCount; index++)
            {
                Assert.That(JsonUtility.ToJson(actual.GetBlockAt(index)),
                    Is.EqualTo(JsonUtility.ToJson(expected.GetBlockAt(index))));
            }
        }

        private static void AssertOrigins(LevelData level, params Vector2Int[] origins)
        {
            Assert.That(level.BlockCount, Is.EqualTo(origins.Length));
            for (int index = 0; index < origins.Length; index++)
            {
                Assert.That(level.GetBlockAt(index).Origin, Is.EqualTo(origins[index]), level.GetBlockAt(index).Id);
            }
        }

        private static void AssertDoor(LevelDoorData door, string id, BoardSide side, int start, int length,
            string colorId)
        {
            Assert.That(door.Id, Is.EqualTo(id));
            Assert.That(door.Side, Is.EqualTo(side), id);
            Assert.That(door.Start, Is.EqualTo(start), id);
            Assert.That(door.Length, Is.EqualTo(length), id);
            Assert.That(door.ColorId, Is.EqualTo(colorId), id);
        }

        // A 6 x 5 board: red Top doors d1 over 1-2 and d2 over 4 (segment 3 joins them), blue Left door d3 over 0-1.
        private static LevelData CreateDoorLevel()
        {
            return new LevelData("level-doors", "Doors", 6, 5, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red"),
                    new LevelDoorData("d2", BoardSide.Top, 4, 1, "Red"),
                    new LevelDoorData("d3", BoardSide.Left, 0, 2, "Blue")
                },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });
        }

        // A 5 x 6 board: b1 (0,0)-(1,0), b2 (2,3), b3 (4,4)-(4,5), and doors on every side; d5 holds only Bottom 0,
        // d1 crosses column 0 and d6 crosses rows 4-5.
        private static LevelData CreateResizeLevel()
        {
            return new LevelData("level-resize", "Resize", 5, 6, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red"),
                    new LevelDoorData("d2", BoardSide.Bottom, 3, 2, "DarkGreen"),
                    new LevelDoorData("d3", BoardSide.Left, 1, 2, "Blue"),
                    new LevelDoorData("d4", BoardSide.Right, 0, 1, "Yellow"),
                    new LevelDoorData("d5", BoardSide.Bottom, 0, 1, "Pink"),
                    new LevelDoorData("d6", BoardSide.Right, 3, 3, "Orange")
                },
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                    new LevelBlockData("b2", "DarkGreen", new Vector2Int(2, 3), s_single),
                    new LevelBlockData("b3", "Blue", new Vector2Int(4, 4), s_upright)
                });
        }

        // A 4 x 6 board, so four more lines on any side stay inside 8 x 10; content touches every edge.
        private static LevelData CreateRoundTripLevel()
        {
            return new LevelData("level-trip", "Trip", 4, 6, 75,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red"),
                    new LevelDoorData("d2", BoardSide.Bottom, 2, 2, "DarkGreen"),
                    new LevelDoorData("d3", BoardSide.Left, 1, 3, "Blue"),
                    new LevelDoorData("d4", BoardSide.Right, 4, 2, "Yellow")
                },
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                    new LevelBlockData("b2", "Blue", new Vector2Int(3, 4), s_upright),
                    new LevelBlockData("b3", "DarkGreen", new Vector2Int(1, 2), s_single)
                });
        }

        private static LevelData CreateEmptyLevel(int width, int height)
        {
            return new LevelData("level-empty", "Empty", width, height, 60, new LevelDoorData[0],
                new LevelBlockData[0]);
        }

        // A 4 x 5 board: red domino b1 at (0,0)-(1,0), green domino b3 at (2,2)-(3,2), one door.
        private static LevelData CreateLevel()
        {
            return new LevelData("level-test", "Test", 4, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red") },
                new[]
                {
                    new LevelBlockData("b1", "Red", Vector2Int.zero, s_domino),
                    new LevelBlockData("b3", "DarkGreen", new Vector2Int(2, 2), s_domino)
                });
        }
    }
}
