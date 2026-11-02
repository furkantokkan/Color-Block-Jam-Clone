using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Obstacles Wall brush commands on level void cells: painting and erasing any board cell, the
    /// refusal messages, stroke skip and order rules, a valid 2 x 2 wall, board objects in the canvas model
    /// and the reference 17 Level 32 wall as valid v2 JSON wrapped by the rule-set walls
    /// (TASK-20260925-145628329-306ba760-level-editor-obstacles-wall).
    /// </summary>
    public sealed class LevelObstacleWallCommandsTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_level32Wall = { new Vector2Int(1, 1), new Vector2Int(2, 1) };

        [Test]
        public void PaintVoidTurnsAnInteriorCellIntoAWallAndKeepsEveryOtherField()
        {
            LevelData level = CreateLevel();

            Assert.That(LevelEditCommands.TryPaintVoid(level, new Vector2Int(2, 1), out LevelData painted,
                out string issue), Is.True, issue);

            Assert.That(painted.VoidCellCount, Is.EqualTo(1));
            Assert.That(painted.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(2, 1)));
            Assert.That(painted.IsFloor(new Vector2Int(2, 1)), Is.False);
            Assert.That(painted.BlockCount, Is.EqualTo(level.BlockCount), "Blocks are kept.");
            Assert.That(painted.DoorCount, Is.EqualTo(level.DoorCount), "Gates are kept.");
            Assert.That(painted.CellObjectCount, Is.EqualTo(level.CellObjectCount), "Board objects are kept.");
            Assert.That(painted.Validate(out string error), Is.True, error);
            Assert.That(level.VoidCellCount, Is.Zero, "The command never changes the level it is given.");

            Assert.That(LevelEditCommands.TryEraseVoid(painted, new Vector2Int(2, 1), out LevelData erased, out issue),
                Is.True, issue);
            Assert.That(erased.VoidCellCount, Is.Zero);
            Assert.That(erased.CellObjectCount, Is.EqualTo(level.CellObjectCount));
        }

        [Test]
        public void PaintVoidNamesWhyACellCannotBecomeAWall()
        {
            LevelData level = CreateLevel();

            AssertPaintRefused(level, new Vector2Int(1, 3), "Block b1 covers the cell (1,3).");
            AssertPaintRefused(level, new Vector2Int(3, 3), "Board object o1 covers the cell (3,3).");
            AssertPaintRefused(level, new Vector2Int(1, 4), "Gate d1 opens onto the cell (1,4).");
            AssertPaintRefused(level, new Vector2Int(5, 0), "Cell (5,0) is off the 5 x 5 board.");
            AssertPaintRefused(null, Vector2Int.zero, "Select a level first.");

            Assert.That(LevelEditCommands.TryPaintVoid(level, new Vector2Int(2, 1), out LevelData painted, out _), Is.True);
            AssertPaintRefused(painted, new Vector2Int(2, 1), "The cell (2,1) is already a void.");

            AssertPaintRefused(CreateU(), new Vector2Int(0, 1), "Cutting the cell (0,1) would split the floor.");
        }

        [Test]
        public void EraseVoidRestoresAnyWallCellIncludingACutCorner()
        {
            Assert.That(LevelEditCommands.TryCutCorner(CreateLevel(), new Vector2Int(4, 0), out LevelData cut, out _),
                Is.True);
            Assert.That(LevelEditCommands.TryEraseVoid(cut, new Vector2Int(4, 0), out LevelData filled, out string issue),
                Is.True, issue);
            Assert.That(filled.VoidCellCount, Is.Zero, "A cut corner is a void cell like any other.");

            Assert.That(LevelEditCommands.TryEraseVoid(cut, new Vector2Int(2, 2), out LevelData result, out issue),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("The cell (2,2) is already floor."));

            // The middle of a 3 x 3 wall touches no floor, so filling it alone would leave a floor island.
            LevelData wide = CreateWideWall();
            Assert.That(LevelEditCommands.TryEraseVoid(wide, new Vector2Int(2, 2), out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("The cell (2,2) touches no floor; restore a cell next to it first."));
            Assert.That(LevelEditCommands.TryEraseVoid(wide, new Vector2Int(2, 1), out _, out issue), Is.True, issue);
        }

        [Test]
        public void StrokeSkipsRefusedCellsAppliesTheRestAndNamesTheFirstRefusal()
        {
            LevelData level = CreateLevel();
            // (1,3) holds block b1 and (3,3) board object o1; (0,3), (2,3) and (4,3) are free.
            Vector2Int[] stroke =
            {
                new Vector2Int(0, 3), new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3), new Vector2Int(4, 3)
            };

            Assert.That(LevelEditCommands.TryApplyVoidStroke(level, stroke, true, out LevelData painted,
                out string issue), Is.True);

            Assert.That(issue, Is.EqualTo("Block b1 covers the cell (1,3)."), "The first refusal is named.");
            Assert.That(GetVoidCells(painted),
                Is.EquivalentTo(new[] { new Vector2Int(0, 3), new Vector2Int(2, 3), new Vector2Int(4, 3) }));
            Assert.That(painted.Validate(out string error), Is.True, error);

            Assert.That(LevelEditCommands.TryApplyVoidStroke(painted, stroke, false, out LevelData erased, out issue),
                Is.True, issue);
            Assert.That(issue, Is.Null, "Floor cells in an erase stroke are kept, not refused.");
            Assert.That(erased.VoidCellCount, Is.Zero);
        }

        [Test]
        public void StrokeThatChangesNothingGivesNoResult()
        {
            LevelData level = CreateLevel();
            Assert.That(LevelEditCommands.TryPaintVoid(level, new Vector2Int(2, 1), out LevelData painted, out _), Is.True);

            Assert.That(LevelEditCommands.TryApplyVoidStroke(painted, new[] { new Vector2Int(2, 1) }, true,
                out LevelData result, out string issue), Is.False, "A wall painted again is kept.");
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.Null);

            Assert.That(LevelEditCommands.TryApplyVoidStroke(level, new[] { new Vector2Int(1, 3) }, true, out result,
                out issue), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Block b1 covers the cell (1,3)."));
        }

        [Test]
        public void StrokeOrderDoesNotDecideWhichCellsApply()
        {
            // In the U floor (2,0) is a dead end behind (1,0), so (1,0) can be walled only after (2,0). The stroke
            // crosses (1,0) first.
            Vector2Int[] stroke = { new Vector2Int(1, 0), new Vector2Int(2, 0) };
            Assert.That(LevelEditCommands.TryApplyVoidStroke(CreateU(), stroke, true, out LevelData painted,
                out string issue), Is.True);
            Assert.That(issue, Is.Null);
            Assert.That(painted.VoidCellCount, Is.EqualTo(4));
            Assert.That(painted.Validate(out string error), Is.True, error);

            // An erase stroke that starts in the middle of a 3 x 3 wall still restores the middle.
            Assert.That(LevelEditCommands.TryApplyVoidStroke(CreateWideWall(),
                new[] { new Vector2Int(2, 2), new Vector2Int(2, 1) }, false, out LevelData erased, out issue), Is.True);
            Assert.That(issue, Is.Null);
            Assert.That(erased.VoidCellCount, Is.EqualTo(7));
            Assert.That(erased.IsFloor(new Vector2Int(2, 2)), Is.True);
            Assert.That(erased.Validate(out error), Is.True, error);
        }

        [Test]
        public void AWideWallAreaIsAValidLevel()
        {
            // Wide wall areas get a solid top from the environment (TASK-20260925-160721096-2c8da893-solid-obstacle-art),
            // so a 2 x 2 wall is ordinary content with no warning.
            LevelData level = CreateLevel();
            Vector2Int[] ell = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
            Assert.That(LevelEditCommands.TryApplyVoidStroke(level, ell, true, out LevelData painted, out string issue),
                Is.True, issue);
            Assert.That(LevelEditCommands.TryPaintVoid(painted, new Vector2Int(1, 1), out LevelData square, out issue),
                Is.True, issue);
            Assert.That(square.Validate(out string error), Is.True, "A wide wall stays a valid level: " + error);
        }

        [Test]
        public void ModelWallGhostIsRedOnABoardObjectAndTheBlockGhostOnAClosedBarrier()
        {
            LevelData level = CreateLevel();
            LevelCanvasModel model = LevelCanvasModel.Create(level);

            // Board object o1, a closed barrier, holds (3,3): since the Barrier task neither the block commands nor
            // the wall command allow it (TASK-20260925-102907480-9e5cbf34).
            Assert.That(model.IsFree(new Vector2Int(3, 3), -1), Is.False, "The block ghost matches the block commands.");
            Assert.That(LevelEditCommands.TryPlaceBlock(level, s_single, "Red", new Vector2Int(3, 3), out _,
                out string issue), Is.False);
            Assert.That(issue, Does.Contain("cell object o1 effect barrier starts closed on block b2"));
            Assert.That(model.CanPaintWall(new Vector2Int(3, 3)), Is.False, "The wall ghost is red on a board object.");

            Assert.That(model.CanPaintWall(new Vector2Int(2, 2)), Is.True);
            Assert.That(model.CanPaintWall(new Vector2Int(1, 3)), Is.False, "Block b1 covers it.");
            Assert.That(model.CanPaintWall(new Vector2Int(1, 4)), Is.False, "Gate d1 opens onto it.");
            Assert.That(model.CanPaintWall(new Vector2Int(9, 9)), Is.False, "Off the board.");
            Assert.That(model.IsFree(new Vector2Int(1, 3), -1), Is.False, "A block cell is not free.");
            Assert.That(model.IsFree(new Vector2Int(1, 3), 0), Is.True, "The moved block's own cell is.");

            Assert.That(LevelEditCommands.TryPaintVoid(level, new Vector2Int(2, 2), out LevelData walled, out issue),
                Is.True, issue);
            LevelCanvasModel walledModel = LevelCanvasModel.Create(walled);
            Assert.That(walledModel.CanPaintWall(new Vector2Int(2, 2)), Is.False, "An existing wall.");
            Assert.That(walledModel.IsFree(new Vector2Int(2, 2), -1), Is.False);
            Assert.That(LevelCanvasModel.Create(CreateU()).CanPaintWall(new Vector2Int(0, 1)), Is.False,
                "Walling the U's bridge would split the floor.");
        }

        [Test]
        public void Level32WallStrokeGivesExactV2VoidCellsAndOneTwoByOneWall()
        {
            LevelData level = CreateLevel32();

            Assert.That(LevelEditCommands.TryApplyVoidStroke(level, s_level32Wall, true, out LevelData walled,
                out string issue), Is.True);
            Assert.That(issue, Is.Null);
            Assert.That(GetVoidCells(walled), Is.EquivalentTo(s_level32Wall));
            Assert.That(walled.Validate(out string error), Is.True, error);
            Assert.That(walled.ValidateRuntimeSupport(out error), Is.True, error);

            // Written in memory only; nothing is saved to Data/Json/Levels.
            Assert.That(LevelDatabaseJson.TryWrite(new[] { walled }, out string json, out error), Is.True, error);
            JObject document = JObject.Parse(json);
            Assert.That((int)document["formatVersion"], Is.EqualTo(3));
            // Version 3 writes void cells as one cell text, "x,y x,y" (DEC-193).
            string voidCells = (string)document["levels"][0]["board"]["voidCells"];
            Assert.That(CellText.TryParseCells(voidCells, out Vector2Int[] written, out string problem), Is.True, problem);
            Assert.That(written, Has.Length.EqualTo(2));
            Assert.That(written, Is.EquivalentTo(s_level32Wall));

            EnvironmentDatabase database = LevelCanvasWalls.LoadDatabase();
            Assume.That(database, Is.Not.Null, LevelCanvasWalls.k_databasePath);
            LevelCanvasWalls walls = new LevelCanvasWalls();
            var plainGlyphs = new List<LevelWallGlyph>();
            var wallGlyphs = new List<LevelWallGlyph>();
            Assert.That(walls.TryCompose(level, database, plainGlyphs, out issue), Is.True, issue);
            Assert.That(walls.TryCompose(walled, database, wallGlyphs, out issue), Is.True, issue);

            // Every quadrant of both cells touches floor, so the rule set walls in all eight: an inner corner at each
            // end quadrant and a straight wall along each long side, one 2x1 wall block.
            Assert.That(wallGlyphs.Count, Is.EqualTo(plainGlyphs.Count + 8));
            int corners = 0;
            int straight = 0;
            for (int y = 0; y < walled.Height; y++)
            {
                for (int x = 0; x < walled.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    int inside = CountInsideCell(wallGlyphs, cell, ref corners, ref straight);
                    Assert.That(inside, Is.EqualTo(walled.IsFloor(cell) ? 0 : 4), $"glyphs inside {cell}");
                }
            }

            Assert.That(corners, Is.EqualTo(4), "One rounded corner per end quadrant of the 2x1 wall.");
            Assert.That(straight, Is.EqualTo(4), "Straight walls along both long sides.");
        }

        private static void AssertPaintRefused(LevelData level, Vector2Int cell, string expected)
        {
            Assert.That(LevelEditCommands.TryPaintVoid(level, cell, out LevelData result, out string issue), Is.False,
                $"{cell}");
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo(expected));
        }

        private static int CountInsideCell(List<LevelWallGlyph> glyphs, Vector2Int cell, ref int corners, ref int straight)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in glyphs)
            {
                if (Mathf.Abs(glyph.Center.x - cell.x) < 0.5f && Mathf.Abs(glyph.Center.y - cell.y) < 0.5f)
                {
                    count++;
                    if (glyph.IsCorner)
                    {
                        corners++;
                    }
                    else
                    {
                        straight++;
                    }
                }
            }

            return count;
        }

        private static Vector2Int[] GetVoidCells(LevelData level)
        {
            Vector2Int[] cells = new Vector2Int[level.VoidCellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = level.GetVoidCellAt(index);
            }

            return cells;
        }

        // A 5 x 5 board: block b1 at (1,3), gate d1 on the top edge at 1 opening onto (1,4), board object o1 (a
        // closed barrier) at (3,3).
        private static LevelData CreateLevel()
        {
            return new LevelData("walls", "Walls", 5, 5, 60, new Vector2Int[0],
                new[] { new LevelDoorData("d1", BoardSide.Top, 1, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 3), s_single) },
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                    new Vector2Int(3, 3)) });
        }

        // A 3 x 3 board with (1,1) and (2,1) void, so the floor is a U: (2,0)-(1,0)-(0,0)-(0,1)-(0,2)-(1,2)-(2,2), with
        // block b1 at the far end (2,2). (0,1) is the bridge between its two arms.
        private static LevelData CreateU()
        {
            return new LevelData("u", "U", 3, 3, 60, s_level32Wall, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) },
                new LevelCellObjectData[0]);
        }

        // A 5 x 5 board whose middle 3 x 3 cells are one wide wall.
        private static LevelData CreateWideWall()
        {
            var voidCells = new List<Vector2Int>();
            for (int y = 1; y <= 3; y++)
            {
                for (int x = 1; x <= 3; x++)
                {
                    voidCells.Add(new Vector2Int(x, y));
                }
            }

            return new LevelData("wide", "Wide", 5, 5, 60, voidCells.ToArray(), new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_single) }, new LevelCellObjectData[0]);
        }

        // Reference 17, Level 32: a 4 x 6 board whose grooved 2x1 wall sits at (1,1) and (2,1). Built here only; it is
        // never added to the active level database (decision 5(a)).
        internal static LevelData CreateLevel32()
        {
            return new LevelData("level-32", "Level 32", 4, 6, 60,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 1, 2, "Blue"),
                    new LevelDoorData("d2", BoardSide.Bottom, 0, 1, "Red")
                },
                new[]
                {
                    new LevelBlockData("b1", "Blue", new Vector2Int(1, 3),
                        new[] { Vector2Int.zero, Vector2Int.right }),
                    new LevelBlockData("b2", "Red", new Vector2Int(0, 4), s_single),
                    new LevelBlockData("b3", "Red", new Vector2Int(3, 0), s_single)
                });
        }
    }
}
