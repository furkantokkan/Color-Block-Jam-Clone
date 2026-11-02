using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies staircase corners (TASK-20260926-181022947-224a7aef):
    /// - each cut turns the next square step (an L-shaped ring) of a corner into void cells, so k cuts leave a k x k
    ///   notch and never a diagonal staircase (DEC-190), and each fill turns the last step back;
    /// - a step with a block, a board object or a gate, or one that would split or empty the floor, is refused whole;
    /// - the depth comes from the void cells alone;
    /// - a board resize keeps a notch of two or more steps at its corner;
    /// - the walls wrap it as the composer does, and the corner buttons name the step sizes.
    /// </summary>
    public sealed class LevelCornerStepsTests
    {
        private const float k_tolerance = 0.0001f;
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        // The first three steps of the Bottom-left corner: 1, 3 and 5 cells, a 3 x 3 notch.
        private static readonly Vector2Int[] s_bottomLeftThreeSteps =
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, 0),
            new Vector2Int(0, 2), new Vector2Int(1, 2), new Vector2Int(2, 2), new Vector2Int(2, 1), new Vector2Int(2, 0)
        };

        [Test]
        public void ThreeCutsGrowASquareNotchAndEachFillTakesBackTheLastStep()
        {
            LevelData level = CreateLevel(6, 6, Block("b1", new Vector2Int(4, 4)));

            LevelData one = Cut(level, Vector2Int.zero);
            LevelData two = Cut(one, Vector2Int.zero);
            LevelData three = Cut(two, Vector2Int.zero);

            Assert.That(GetVoidCells(one), Is.EquivalentTo(Take(s_bottomLeftThreeSteps, 1)), "Step 1 is the corner cell.");
            Assert.That(GetVoidCells(two), Is.EquivalentTo(Take(s_bottomLeftThreeSteps, 4)),
                "Step 2 adds its two neighbours and the cell between them: a 2 x 2 notch, not a diagonal.");
            Assert.That(GetVoidCells(three), Is.EquivalentTo(s_bottomLeftThreeSteps), "Step 3 makes a 3 x 3 notch.");
            Assert.That(LevelCornerSteps.GetDepth(three, Vector2Int.zero), Is.EqualTo(3));
            Assert.That(three.BlockCount, Is.EqualTo(1), "Every other field is kept.");
            Assert.That(GetVoidCells(level), Is.Empty, "The given level never changes.");

            LevelData filled = Fill(three, Vector2Int.zero);
            Assert.That(GetVoidCells(filled), Is.EquivalentTo(Take(s_bottomLeftThreeSteps, 4)), "Fill takes back step 3 only.");
            filled = Fill(Fill(filled, Vector2Int.zero), Vector2Int.zero);
            Assert.That(GetVoidCells(filled), Is.Empty);
            Assert.That(LevelEditCommands.TryFillCorner(filled, Vector2Int.zero, out LevelData result, out string issue),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("The corner cell (0,0) is already floor."));
        }

        [Test]
        public void ANextStepWithABlockABoardObjectOrAGateIsRefusedWholeAndNamed()
        {
            AssertSecondStepRefused(CreateLevel(5, 5, Block("b1", new Vector2Int(1, 0))),
                "Block b1 covers the corner step cell (1,0).");

            LevelData withObject = new LevelData("objects", "Objects", 5, 5, 60, new Vector2Int[0], new LevelDoorData[0],
                new[] { Block("b1", new Vector2Int(4, 4)) },
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                    new Vector2Int(0, 1)) });
            AssertSecondStepRefused(withObject, "Board object o1 covers the corner step cell (0,1).");

            LevelData withGate = new LevelData("gates", "Gates", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Left, 1, 1, "Red") },
                new[] { Block("b1", new Vector2Int(4, 4)) });
            AssertSecondStepRefused(withGate, "Gate d1 opens onto the corner step cell (0,1).");
        }

        [Test]
        public void AStepThatWouldSplitOrEmptyTheFloorIsRefused()
        {
            // With (1,2) void, (0,2) reaches the rest only through (0,1), so step 2 would cut it off.
            LevelData split = new LevelData("split", "Split", 3, 3, 60, new[] { new Vector2Int(1, 2) },
                new LevelDoorData[0], new LevelBlockData[0], new LevelCellObjectData[0]);
            LevelData cut = Cut(split, Vector2Int.zero);

            Assert.That(LevelEditCommands.GetCornerCutIssue(cut, Vector2Int.zero),
                Is.EqualTo("Cutting the next corner step would split the floor."));

            // On an empty 3 x 3 board two steps leave a 2 x 2 notch, and a third would take the last floor cells.
            LevelData empty = new LevelData("empty", "Empty", 3, 3, 60, new Vector2Int[0], new LevelDoorData[0],
                new LevelBlockData[0], new LevelCellObjectData[0]);
            for (int step = 0; step < 2; step++)
            {
                empty = Cut(empty, Vector2Int.zero);
            }

            Assert.That(GetVoidCells(empty).Count, Is.EqualTo(4));
            Assert.That(LevelEditCommands.TryCutCorner(empty, Vector2Int.zero, out LevelData result, out string issue),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo("Cutting the next corner step would leave no floor."));
        }

        [Test]
        public void EveryCornerGrowsAlongBothOfItsEdgesAndNoStepLeavesTheBoard()
        {
            LevelData level = new LevelData("corners", "Corners", 5, 4, 60, new Vector2Int[0], new LevelDoorData[0],
                new[] { Block("b1", new Vector2Int(2, 2)) }, new LevelCellObjectData[0]);
            Vector2Int[] corners = { new Vector2Int(0, 0), new Vector2Int(4, 0), new Vector2Int(0, 3), new Vector2Int(4, 3) };

            foreach (Vector2Int corner in corners)
            {
                Vector2Int alongX = new Vector2Int(corner.x == 0 ? 1 : -1, 0);
                Vector2Int alongY = new Vector2Int(0, corner.y == 0 ? 1 : -1);
                LevelData two = Cut(Cut(level, corner), corner);
                Assert.That(GetVoidCells(two),
                    Is.EquivalentTo(new[] { corner, corner + alongX, corner + alongY, corner + alongX + alongY }),
                    $"{corner}");
                Assert.That(GetVoidCells(Fill(Fill(two, corner), corner)), Is.Empty, $"{corner}");
            }

            List<Vector2Int> cells = new List<Vector2Int>();
            LevelCornerSteps.GetStepCells(3, 5, Vector2Int.zero, 4, cells);
            Assert.That(cells, Is.EqualTo(new[] { new Vector2Int(0, 3), new Vector2Int(1, 3), new Vector2Int(2, 3) }),
                "The ring's column x = 3 is off the 3-wide board, so only its row stays.");
            LevelCornerSteps.GetStepCells(3, 5, new Vector2Int(2, 4), 3, cells);
            Assert.That(cells, Is.EqualTo(new[]
            {
                new Vector2Int(2, 2), new Vector2Int(1, 2), new Vector2Int(0, 2), new Vector2Int(0, 3),
                new Vector2Int(0, 4)
            }), "From the corner's column along the ring's row, then up its column to the corner's row.");
            LevelCornerSteps.GetStepCells(3, 5, Vector2Int.zero, LevelCornerSteps.GetStepCount(3, 5) + 1, cells);
            Assert.That(cells, Is.Empty, "A step past the far corner has no cell.");
            LevelCornerSteps.GetStepCells(3, 5, new Vector2Int(1, 0), 1, cells);
            Assert.That(cells, Is.Empty, "(1,0) is no corner.");
        }

        [Test]
        public void AResizeOnEitherOfItsSidesKeepsATwoStepStaircaseAtItsCorner()
        {
            Vector2Int[] bottomLeft = Take(s_bottomLeftThreeSteps, 4);
            LevelData level = Cut(Cut(CreateLevel(5, 5, Block("b1", new Vector2Int(2, 2))), Vector2Int.zero),
                Vector2Int.zero);

            AssertResized(level, BoardSide.Left, 1, bottomLeft, new Vector2Int(3, 2));
            AssertResized(level, BoardSide.Left, -1, bottomLeft, new Vector2Int(1, 2));
            AssertResized(level, BoardSide.Bottom, 1, bottomLeft, new Vector2Int(2, 3));
            AssertResized(level, BoardSide.Bottom, -1, bottomLeft, new Vector2Int(2, 1));

            LevelData right = Cut(Cut(CreateLevel(5, 5, Block("b1", new Vector2Int(2, 2))), new Vector2Int(4, 0)),
                new Vector2Int(4, 0));
            AssertResized(right, BoardSide.Right, 1,
                new[] { new Vector2Int(5, 0), new Vector2Int(5, 1), new Vector2Int(4, 1), new Vector2Int(4, 0) },
                new Vector2Int(2, 2));
            AssertResized(right, BoardSide.Right, -1,
                new[] { new Vector2Int(3, 0), new Vector2Int(3, 1), new Vector2Int(2, 1), new Vector2Int(2, 0) },
                new Vector2Int(2, 2));
        }

        [Test]
        public void AResizedStaircaseEndsAtTheFirstStepThatNoLongerFits()
        {
            // Removing the Left line moves b1 from (2,0) to (1,0), a cell of step 2 at the new corner.
            LevelData level = Cut(Cut(CreateLevel(5, 5, Block("b1", new Vector2Int(2, 0))), Vector2Int.zero),
                Vector2Int.zero);

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, -1, out LevelData narrow,
                out LevelResizeReport report, out string issue), Is.True, issue);

            Assert.That(narrow.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(GetVoidCells(narrow), Is.EquivalentTo(new[] { Vector2Int.zero }),
                "Step 1 still fits; step 2 is under b1, so the staircase ends there.");
        }

        [Test]
        public void StaircasesOnTheOtherSidesAndVoidsOutsideAStaircaseResizeAsBefore()
        {
            // A Top-right staircase and an inner wall cell shift with the content when a line is added on the Left.
            Vector2Int topRight = new Vector2Int(4, 4);
            LevelData level = Cut(Cut(CreateLevel(5, 5, Block("b1", new Vector2Int(1, 1))), topRight), topRight);
            Assert.That(LevelEditCommands.TryPaintVoid(level, new Vector2Int(2, 3), out level, out string issue), Is.True,
                issue);

            Assert.That(LevelEditCommands.TryResizeBoard(level, BoardSide.Left, 1, out LevelData grown,
                out LevelResizeReport report, out issue), Is.True, issue);

            Assert.That(GetVoidCells(grown), Is.EquivalentTo(new[]
            {
                new Vector2Int(5, 4), new Vector2Int(4, 4), new Vector2Int(5, 3), new Vector2Int(4, 3),
                new Vector2Int(3, 3)
            }));
            Assert.That(LevelCornerSteps.GetDepth(grown, new Vector2Int(5, 4)), Is.EqualTo(2), "Still two steps deep.");
        }

        [Test]
        public void TheDepthComesFromTheVoidCellsAloneThroughJsonAndTheReloadCopy()
        {
            // A Top-left notch three steps deep, listed out of order, plus an unrelated wall cell.
            Vector2Int[] voids =
            {
                new Vector2Int(1, 4), new Vector2Int(4, 1), new Vector2Int(0, 5), new Vector2Int(2, 5),
                new Vector2Int(0, 3), new Vector2Int(1, 5), new Vector2Int(0, 4), new Vector2Int(2, 3),
                new Vector2Int(1, 3), new Vector2Int(2, 4)
            };
            LevelData level = new LevelData("depth", "Depth", 6, 6, 60, voids, new LevelDoorData[0],
                new[] { Block("b1", new Vector2Int(4, 4)) }, new LevelCellObjectData[0]);
            Vector2Int topLeft = new Vector2Int(0, 5);

            Assert.That(LevelCornerSteps.GetDepth(level, topLeft), Is.EqualTo(3));
            Assert.That(LevelCornerSteps.GetDepth(level, Vector2Int.zero), Is.Zero);
            Assert.That(LevelCornerSteps.GetDepth(level, new Vector2Int(2, 2)), Is.Zero, "(2,2) is no corner.");

            // The window's reload copy keeps the draft snapshot as JsonUtility text.
            LevelData copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level));
            Assert.That(LevelCornerSteps.GetDepth(copy, topLeft), Is.EqualTo(3));

            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out string error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] read, out error), Is.True, error);
            Assert.That(LevelCornerSteps.GetDepth(read[0], topLeft), Is.EqualTo(3), "Save and Reload keep the staircase.");
            Assert.That(GetVoidCells(Fill(read[0], topLeft)).Count, Is.EqualTo(5), "Fill takes back step 3 only.");

            // The old diagonal staircase of two steps reads as one step deep, so the next cut adds only (1,4).
            LevelData diagonal = new LevelData("diagonal", "Diagonal", 6, 6, 60,
                new[] { new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(0, 4) }, new LevelDoorData[0],
                new[] { Block("b1", new Vector2Int(4, 4)) }, new LevelCellObjectData[0]);
            Assert.That(LevelCornerSteps.GetDepth(diagonal, topLeft), Is.EqualTo(1));
            Assert.That(GetVoidCells(Cut(diagonal, topLeft)), Is.EquivalentTo(new[]
            {
                new Vector2Int(0, 5), new Vector2Int(1, 5), new Vector2Int(0, 4), new Vector2Int(1, 4)
            }), "The user's Level 5 case: one more cut squares the notch.");
        }

        [Test]
        public void AStaircaseWrapsTheWallsExactlyAsTheComposerDoes()
        {
            EnvironmentDatabase database = LevelCanvasWalls.LoadDatabase();
            Assume.That(database, Is.Not.Null, LevelCanvasWalls.k_databasePath);
            LevelData level = CreateLevel(5, 5, Block("b1", new Vector2Int(4, 4)));
            for (int step = 0; step < 3; step++)
            {
                level = Cut(level, Vector2Int.zero);
            }

            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            List<EnvironmentPiecePlacement> placements = new List<EnvironmentPiecePlacement>();
            new EnvironmentComposer(database).Compose(layout, placements);
            List<LevelWallGlyph> glyphs = new List<LevelWallGlyph>();
            Assert.That(new LevelCanvasWalls().TryCompose(level, database, glyphs, out string issue), Is.True, issue);

            EnvironmentRuleSet rules = database.RuleSet;
            // Rule 1 is the straight Wall rule; the other rule outputs are corner pieces (docs/Environment-Layout.md).
            BoardPieceView wallPiece = rules.GetRule(1).Output;
            int next = 0;
            foreach (EnvironmentPiecePlacement placement in placements)
            {
                if (!IsRuleOutput(rules, placement.Piece))
                {
                    continue;
                }

                string label = $"glyph {next} ({placement.Piece.name} at {placement.GridPosition})";
                Assert.That(next, Is.LessThan(glyphs.Count), label);
                LevelWallGlyph glyph = glyphs[next++];
                Assert.That(glyph.Center.x * 2f, Is.EqualTo(placement.GridPosition.x).Within(k_tolerance), label);
                Assert.That(glyph.Center.y * 2f, Is.EqualTo(placement.GridPosition.y).Within(k_tolerance), label);
                Assert.That(glyph.Yaw, Is.EqualTo(placement.Yaw), label);
                Assert.That(glyph.IsCorner, Is.EqualTo(placement.Piece != wallPiece), label);
            }

            Assert.That(glyphs.Count, Is.EqualTo(next));
            Assert.That(CountGlyphsInside(glyphs, new Vector2Int(2, 2)), Is.GreaterThan(0),
                "The walls wrap into the notch's outer step.");
        }

        [Test]
        public void TheModelAndTheCornerButtonsNameTheStepSizes()
        {
            LevelData level = CreateLevel(6, 6, Block("b1", new Vector2Int(4, 4)));
            LevelCanvasModel plain = LevelCanvasModel.Create(level);
            Assert.That(plain.GetCornerCutCells(false, false), Is.EqualTo(1));
            Assert.That(plain.GetCornerFillCells(false, false), Is.Zero);

            LevelData two = Cut(Cut(level, Vector2Int.zero), Vector2Int.zero);
            LevelCanvasModel model = LevelCanvasModel.Create(two);
            Assert.That(model.GetCornerCutIssue(false, false), Is.Null);
            Assert.That(model.GetCornerCutCells(false, false), Is.EqualTo(5));
            Assert.That(model.GetCornerFillCells(false, false), Is.EqualTo(3));

            LevelCanvasElement canvas = new LevelCanvasElement();
            canvas.SetModel(model);
            Assert.That(canvas.Q<Button>("corner-bottom-left-cut").tooltip, Is.EqualTo("Cut the next corner step (5 cells)."));
            Assert.That(canvas.Q<Button>("corner-bottom-left-fill").tooltip,
                Is.EqualTo("Fill the last corner step (3 cells) back with floor."));
            Assert.That(canvas.Q<Button>("corner-top-right-cut").tooltip, Is.EqualTo("Cut the next corner step (1 cell)."));

            canvas.SetModel(LevelCanvasModel.Create(Cut(CreateLevel(5, 5, Block("b1", new Vector2Int(1, 0))),
                Vector2Int.zero)));
            Button cut = canvas.Q<Button>("corner-bottom-left-cut");
            Assert.That(cut.enabledSelf, Is.False);
            Assert.That(cut.tooltip, Is.EqualTo("Cannot cut: Block b1 covers the corner step cell (1,0)."));
        }

        // The level has its corner cut once; the second step must be refused with this issue and change nothing.
        private static void AssertSecondStepRefused(LevelData level, string expected)
        {
            LevelData cut = Cut(level, Vector2Int.zero);

            Assert.That(LevelEditCommands.GetCornerCutIssue(cut, Vector2Int.zero), Is.EqualTo(expected));
            Assert.That(LevelEditCommands.TryCutCorner(cut, Vector2Int.zero, out LevelData result, out string issue),
                Is.False);
            Assert.That(result, Is.Null);
            Assert.That(issue, Is.EqualTo(expected));
            Assert.That(GetVoidCells(cut), Is.EqualTo(new[] { Vector2Int.zero }), "Step 1 stays cut.");
        }

        private static void AssertResized(LevelData level, BoardSide side, int delta, Vector2Int[] voids,
            Vector2Int blockOrigin)
        {
            string label = $"{side} {delta:+#;-#}";
            Assert.That(LevelEditCommands.TryResizeBoard(level, side, delta, out LevelData result,
                out LevelResizeReport report, out string issue), Is.True, $"{label}: {issue}");
            Assert.That(GetVoidCells(result), Is.EquivalentTo(voids), label);
            Assert.That(result.GetBlockAt(0).Origin, Is.EqualTo(blockOrigin), label);
            if (!result.Validate(out string error))
            {
                Assert.That(error, Does.Not.Contain("void").And.Not.Contain("floor"), label);
            }
        }

        private static LevelData Cut(LevelData level, Vector2Int corner)
        {
            Assert.That(LevelEditCommands.TryCutCorner(level, corner, out LevelData result, out string issue), Is.True,
                issue);
            return result;
        }

        private static LevelData Fill(LevelData level, Vector2Int corner)
        {
            Assert.That(LevelEditCommands.TryFillCorner(level, corner, out LevelData result, out string issue), Is.True,
                issue);
            return result;
        }

        private static LevelData CreateLevel(int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData("staircase", "Staircase", width, height, 60, new Vector2Int[0], new LevelDoorData[0],
                blocks, new LevelCellObjectData[0]);
        }

        private static LevelBlockData Block(string id, Vector2Int origin)
        {
            return new LevelBlockData(id, "Red", origin, s_single);
        }

        private static List<Vector2Int> GetVoidCells(LevelData level)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int index = 0; index < level.VoidCellCount; index++)
            {
                cells.Add(level.GetVoidCellAt(index));
            }

            return cells;
        }

        private static Vector2Int[] Take(Vector2Int[] cells, int count)
        {
            Vector2Int[] first = new Vector2Int[count];
            System.Array.Copy(cells, first, count);
            return first;
        }

        private static bool IsRuleOutput(EnvironmentRuleSet rules, BoardPieceView piece)
        {
            for (int index = 0; index < rules.RuleCount; index++)
            {
                if (rules.GetRule(index).Output == piece)
                {
                    return true;
                }
            }

            return false;
        }

        private static int CountGlyphsInside(List<LevelWallGlyph> glyphs, Vector2Int cell)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in glyphs)
            {
                if (Mathf.Abs(glyph.Center.x - cell.x) < 0.5f && Mathf.Abs(glyph.Center.y - cell.y) < 0.5f)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
