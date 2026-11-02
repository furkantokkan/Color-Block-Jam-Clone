using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the canvas corner buttons on level JSON v2 void cells: cut turns a free board corner cell into a void
    /// and fill turns it back, each as one undo step; further presses grow and shrink a square notch one ring
    /// at a time (TASK-20260926-181022947-224a7aef, DEC-190); a corner under a block, a board object or a gate cannot be cut;
    /// and blocks and gates are refused on a void cell. Also verifies that a board resize keeps a cut corner and any
    /// edge-line void on the resized side's outermost line (TASK-20260925-144031187-141dd370-resize-keeps-edge-voids).
    /// </summary>
    public sealed class LevelCanvasCornerVoidsTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_corners =
        {
            new Vector2Int(0, 0), new Vector2Int(4, 0), new Vector2Int(0, 4), new Vector2Int(4, 4)
        };

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;
        }

        [Test]
        public void CutTurnsEachCornerCellIntoAVoidAndFillTurnsItBack()
        {
            LevelData level = CreateLevel();

            foreach (Vector2Int corner in s_corners)
            {
                Assert.That(LevelEditCommands.TryCutCorner(level, corner, out LevelData cut, out string issue), Is.True,
                    issue);
                Assert.That(cut.VoidCellCount, Is.EqualTo(1), $"{corner}");
                Assert.That(cut.GetVoidCellAt(0), Is.EqualTo(corner));
                Assert.That(cut.BlockCount, Is.EqualTo(level.BlockCount), "Every other field is kept.");
                Assert.That(cut.DoorCount, Is.EqualTo(level.DoorCount));
                if (!cut.Validate(out string error))
                {
                    Assert.That(error, Does.Not.Contain("void"), "A cut corner is a valid void cell.");
                }

                Assert.That(LevelEditCommands.TryFillCorner(cut, corner, out LevelData filled, out issue), Is.True, issue);
                Assert.That(filled.VoidCellCount, Is.Zero, $"{corner}");
            }
        }

        [Test]
        public void CutNamesWhyACornerCannotBeCut()
        {
            LevelData level = new LevelData("corners", "Corners", 5, 5, 60, new Vector2Int[0],
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, s_single) },
                new[] { LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                    new Vector2Int(4, 0)) });

            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(0, 0)),
                Is.EqualTo("Block b1 covers the corner cell (0,0)."));
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(4, 0)),
                Is.EqualTo("Board object o1 covers the corner cell (4,0)."));
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(0, 4)),
                Is.EqualTo("Gate d1 opens onto the corner cell (0,4)."));
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(4, 4)), Is.Null);
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(2, 2)),
                Is.EqualTo("Cell (2,2) is not a board corner."));
            Assert.That(LevelEditCommands.TryCutCorner(level, new Vector2Int(0, 0), out LevelData result, out _), Is.False);
            Assert.That(result, Is.Null);

            Assert.That(LevelEditCommands.TryCutCorner(level, new Vector2Int(4, 4), out LevelData cut, out _), Is.True);
            Assert.That(LevelEditCommands.GetCornerCutIssue(cut, new Vector2Int(4, 4)), Is.Null,
                "A cut corner can take its next step, (4,3), (3,3) and (3,4) (corner steps, DEC-190).");
            Assert.That(LevelEditCommands.TryFillCorner(level, new Vector2Int(4, 4), out _, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("The corner cell (4,4) is already floor."));
        }

        [Test]
        public void CutIsRefusedWhenItWouldSplitTheFloor()
        {
            // With (1,1) and (2,1) void the floor is a U: (2,0)-(1,0)-(0,0)-(0,1)-(0,2)-(1,2)-(2,2). The corners
            // (0,0) and (0,2) are bridges, so cutting either would split it; (2,0) is an end.
            LevelData level = new LevelData("split", "Split", 3, 3, 60,
                new[] { new Vector2Int(1, 1), new Vector2Int(2, 1) },
                new LevelDoorData[0], new LevelBlockData[0], new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(0, 0)),
                Is.EqualTo("Cutting the corner cell (0,0) would split the floor."));
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(0, 2)),
                Is.EqualTo("Cutting the corner cell (0,2) would split the floor."));
            Assert.That(LevelEditCommands.GetCornerCutIssue(level, new Vector2Int(2, 0)), Is.Null,
                "(2,0) is an end of the floor, not a bridge.");
        }

        [Test]
        public void BlocksAndGatesAreRefusedOnAVoidCell()
        {
            Assert.That(LevelEditCommands.TryCutCorner(CreateLevel(), Vector2Int.zero, out LevelData level, out _), Is.True);

            Assert.That(LevelEditCommands.TryPlaceBlock(level, s_single, "Red", Vector2Int.zero,
                out LevelData result, out string issue), Is.False);
            Assert.That(issue, Is.EqualTo("Cell (0,0) is a void."));
            Assert.That(result, Is.Null);
            Assert.That(LevelEditCommands.TryMoveBlock(level, 0, Vector2Int.zero, out _, out issue), Is.False);
            Assert.That(issue, Is.EqualTo("Cell (0,0) is a void."));
            Assert.That(LevelEditCommands.TryReplaceBlockCells(level, 0,
                new[] { Vector2Int.zero, Vector2Int.left }, out _, out issue), Is.False, "b1 at (1,0) would reach (0,0).");
            Assert.That(issue, Is.EqualTo("Cell (0,0) is a void."));

            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Bottom, 0, "Red", out _, out _, out issue),
                Is.False);
            Assert.That(issue, Is.EqualTo("Cell (0,0) behind the Bottom edge at 0 is a void."));
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Left, 0, "Red", out _, out _, out issue),
                Is.False);
            Assert.That(LevelEditCommands.TryPlaceOrExtendDoor(level, BoardSide.Bottom, 1, "Red", out _, out _, out issue),
                Is.True, issue);
        }

        [Test]
        public void RightGrowthMovesTheCutCornerAndShrinkRestoresIt()
        {
            LevelData level = new LevelData("resize-corner", "Resize Corner", 4, 5, 60, new Vector2Int[0],
                new LevelDoorData[0], new LevelBlockData[0], new LevelCellObjectData[0]);
            Assert.That(LevelEditCommands.TryCutCorner(level, new Vector2Int(3, 0), out LevelData cut, out string issue),
                Is.True, issue);

            Assert.That(LevelEditCommands.TryResizeBoard(cut, BoardSide.Right, 1, out LevelData grown,
                out LevelResizeReport report, out issue), Is.True, issue);
            Assert.That(grown.Width, Is.EqualTo(5));
            Assert.That(grown.VoidCellCount, Is.EqualTo(1));
            Assert.That(grown.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(4, 0)), "The cut follows the Right edge.");
            Assert.That(grown.IsFloor(new Vector2Int(3, 0)), Is.True, "The old corner is floor again.");

            Assert.That(LevelEditCommands.TryResizeBoard(grown, BoardSide.Right, -1, out LevelData shrunk,
                out report, out issue), Is.True, issue);
            Assert.That(shrunk.Width, Is.EqualTo(4));
            Assert.That(shrunk.VoidCellCount, Is.EqualTo(1));
            Assert.That(shrunk.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(3, 0)), "Shrinking gives back the cut corner.");
        }

        [Test]
        public void LeftAndBottomGrowthKeepTheEdgeVoidOnLineZero()
        {
            LevelData left = new LevelData("edge-left", "Edge Left", 5, 5, 60,
                new[] { new Vector2Int(0, 2), new Vector2Int(2, 3) }, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_single) },
                new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.TryResizeBoard(left, BoardSide.Left, 1, out LevelData grownLeft,
                out LevelResizeReport report, out string issue), Is.True, issue);
            Assert.That(grownLeft.Width, Is.EqualTo(6));
            Assert.That(grownLeft.VoidCellCount, Is.EqualTo(2));
            Assert.That(grownLeft.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(3, 3)), "The inner void shifts with the content.");
            Assert.That(grownLeft.GetVoidCellAt(1), Is.EqualTo(new Vector2Int(0, 2)), "The edge void stays on line 0.");
            Assert.That(grownLeft.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 1)), "The block shifts with the content.");

            LevelData bottom = new LevelData("edge-bottom", "Edge Bottom", 5, 5, 60,
                new[] { new Vector2Int(2, 0), new Vector2Int(3, 2) }, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 1), s_single) },
                new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.TryResizeBoard(bottom, BoardSide.Bottom, 1, out LevelData grownBottom,
                out report, out issue), Is.True, issue);
            Assert.That(grownBottom.Height, Is.EqualTo(6));
            Assert.That(grownBottom.VoidCellCount, Is.EqualTo(2));
            Assert.That(grownBottom.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(3, 3)), "The inner void shifts with the content.");
            Assert.That(grownBottom.GetVoidCellAt(1), Is.EqualTo(new Vector2Int(2, 0)), "The edge void stays on line 0.");
            Assert.That(grownBottom.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 2)), "The block shifts with the content.");
        }

        [Test]
        public void TopShrinkDropsTheEdgeVoidWhenBlockedAndMovesItWhenFree()
        {
            LevelData blocked = new LevelData("top-blocked", "Top Blocked", 5, 5, 60,
                new[] { new Vector2Int(2, 4) }, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 3), s_single) },
                new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.TryResizeBoard(blocked, BoardSide.Top, -1, out LevelData shrunkBlocked,
                out LevelResizeReport report, out string issue), Is.True, issue);
            Assert.That(shrunkBlocked.VoidCellCount, Is.Zero, "The new top cell is covered by a block, so the void is dropped.");
            Assert.That(shrunkBlocked.BlockCount, Is.EqualTo(1));
            Assert.That(shrunkBlocked.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(2, 3)), "The block stays.");

            LevelData free = new LevelData("top-free", "Top Free", 5, 5, 60,
                new[] { new Vector2Int(2, 4) }, new LevelDoorData[0], new LevelBlockData[0], new LevelCellObjectData[0]);

            Assert.That(LevelEditCommands.TryResizeBoard(free, BoardSide.Top, -1, out LevelData shrunkFree,
                out report, out issue), Is.True, issue);
            Assert.That(shrunkFree.VoidCellCount, Is.EqualTo(1));
            Assert.That(shrunkFree.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(2, 3)), "The void moves down onto the free cell.");
        }

        [Test]
        public void ModelMarksVoidCellsAndEachCornersCutState()
        {
            Assert.That(LevelEditCommands.TryCutCorner(CreateLevel(), new Vector2Int(4, 4), out LevelData level, out _),
                Is.True);

            LevelCanvasModel model = LevelCanvasModel.Create(level);

            Assert.That(model.IsVoid(new Vector2Int(4, 4)), Is.True);
            Assert.That(model.IsFree(new Vector2Int(4, 4), -1), Is.False, "A void takes no block.");
            Assert.That(model.GetCornerCell(true, true), Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(model.GetCornerCell(false, false), Is.EqualTo(Vector2Int.zero));
            Assert.That(model.GetCornerCutIssue(true, true), Is.Null, "Its next step, (4,3), (3,3) and (3,4), is free.");
            Assert.That(model.GetCornerCutIssue(false, false), Is.Null);
            Assert.That(LevelCanvasModel.Empty.GetCornerCutIssue(false, false), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator CornerButtonsCutAndFillTheDraftAsOneUndoStepEach()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(canvas != null && IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, CreateLevel());
            Button cut = canvas.Q<Button>("corner-bottom-left-cut");
            Button fill = canvas.Q<Button>("corner-bottom-left-fill");
            Assert.That(cut.enabledSelf, Is.True, cut.tooltip);
            Assert.That(fill.enabledSelf, Is.False, "A floor corner has nothing to fill.");
            Assert.That(canvas.Q<Button>("corner-top-right-cut").enabledSelf, Is.True);

            Press(cut);

            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(1));
            Assert.That(GetDraft().GetVoidCellAt(0), Is.EqualTo(Vector2Int.zero), "Bottom-left is cell (0, 0).");
            Assert.That(canvas.Model.IsVoid(Vector2Int.zero), Is.True);
            Assert.That(cut.enabledSelf, Is.False, "b1 stands on (1,0), a cell of the next step.");
            Assert.That(cut.tooltip, Is.EqualTo("Cannot cut: Block b1 covers the corner step cell (1,0)."));
            Assert.That(fill.enabledSelf, Is.True);

            Undo.PerformUndo();
            Assert.That(GetDraft().VoidCellCount, Is.Zero, "Undo restores the floor in one step.");
            Assert.That(canvas.Model.IsVoid(Vector2Int.zero), Is.False);

            Undo.PerformRedo();
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(1));
            Press(fill);
            Assert.That(GetDraft().VoidCellCount, Is.Zero);
            Assert.That(cut.enabledSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator CutAndFillPressesGrowAndShrinkAStaircaseOneUndoStepEach()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(canvas != null && IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            // Staircase corners (task 224a7aef): a 6 x 6 board whose gate and block are away from the Bottom-left.
            LoadFixture(root, new LevelData("staircase", "Staircase", 6, 6, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 2, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(4, 4), s_single) }));
            Button cut = canvas.Q<Button>("corner-bottom-left-cut");
            Button fill = canvas.Q<Button>("corner-bottom-left-fill");
            int[] voidCounts = { 1, 4, 9 };
            for (int press = 0; press < voidCounts.Length; press++)
            {
                Assert.That(cut.enabledSelf, Is.True, cut.tooltip);
                Press(cut);
                Assert.That(GetDraft().VoidCellCount, Is.EqualTo(voidCounts[press]), $"Press {press + 1}.");
            }

            // Three presses leave a 3 x 3 notch (DEC-190).
            List<Vector2Int> notch = new List<Vector2Int>();
            for (int x = 0; x < 3; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    notch.Add(new Vector2Int(x, y));
                }
            }

            // The window publishes the draft on the next editor update (SchedulePublish).
            for (int frame = 0; frame < 10 && LevelDatabaseWindow.LastPublishedLevel?.VoidCellCount != 9; frame++)
            {
                yield return null;
            }

            foreach (Vector2Int cell in notch)
            {
                Assert.That(canvas.Model.IsVoid(cell), Is.True, $"The canvas draws {cell} as void.");
                Assert.That(LevelDatabaseWindow.LastPublishedLevel.IsFloor(cell), Is.False, $"The preview gets {cell}.");
            }

            Assert.That(fill.tooltip, Is.EqualTo("Fill the last corner step (5 cells) back with floor."));

            Undo.PerformUndo();
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(4), "Undo takes back exactly the last step.");
            Undo.PerformUndo();
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(1));
            Undo.PerformRedo();
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(4));

            Press(fill);
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(1), "Fill takes back step 2.");
            Press(fill);
            Assert.That(GetDraft().VoidCellCount, Is.Zero);
            Assert.That(fill.enabledSelf, Is.False, "Nothing is left to fill.");
        }

        // A 5 x 5 board with a gate and two blocks away from every corner cell.
        private static LevelData CreateLevel()
        {
            return new LevelData("corners", "Corners", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 2, 1, "Red") },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(1, 0), s_single),
                    new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), s_single)
                });
        }

        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private LevelData GetDraft()
        {
            return m_fixture.GetDraft();
        }

        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // Unity builds the GUI itself, so a later automatic CreateGUI cannot clear the undo records under a test.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity();
            m_window = m_fixture.Window;
            // A fixture level, deterministic whatever the real project's active database holds (Tests-Editor-1-F3).
            m_fixture.LoadDatabase();
            // A fresh undo group after the fixture load, so the test's own first edit starts a clean group.
            Undo.IncrementCurrentGroup();
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }
    }
}
