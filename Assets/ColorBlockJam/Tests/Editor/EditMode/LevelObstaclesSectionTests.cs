using System;
using System.Collections;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Obstacles section: the official Barrier tile (user decision of 2026-09-25), which arms the Pen's
    /// Barrier brush, and the Void tile (DEC-200), which cuts board cells into void; the Eraser or a right drag undoes
    /// either as one undo step each. Fixture levels live only in the draft; nothing is saved.
    /// </summary>
    public sealed class LevelObstaclesSectionTests
    {
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
        public void ObstaclesHoldsTheBarrierAndVoidAndNoBoundaryWallBlockOrToggle()
        {
            VisualElement root = OpenWindow();
            VisualElement tools = root.Q<ScrollView>("tools-pane").contentContainer;

            Foldout obstacles = tools.Q<Foldout>("obstacles-group");
            Assert.That(obstacles, Is.Not.Null);
            Assert.That(obstacles.text, Is.EqualTo("Obstacles"));
            Assert.That(obstacles.value, Is.True, "Obstacles starts open.");
            Assert.That(tools.IndexOf(obstacles), Is.EqualTo(tools.IndexOf(tools.Q<Foldout>("blocks-group")) + 1));
            Assert.That(tools.IndexOf(obstacles), Is.EqualTo(tools.childCount - 1),
                "Edit moved up under Tools and Raw data is gone (2026-09-26), so Obstacles is the last group.");

            foreach (string removed in new[] { "boundary-group", "obstacle-wall", "obstacle-block", "barrier-starts-open" })
            {
                Assert.That(root.Q(removed), Is.Null, $"{removed} is gone (user decisions of 2026-09-25).");
            }

            Assert.That(obstacles.Query<Button>().ToList().ConvertAll(button => button.name),
                Is.EqualTo(new[] { "obstacle-barrier", "obstacle-void" }));
            Assert.That(obstacles.Q<Button>("obstacle-void").tooltip, Is.EqualTo(LevelDatabaseWindow.k_voidTooltip));
            Assert.That(obstacles.contentContainer.Query<Toggle>().ToList(), Is.Empty,
                "Starts open is an Effect Editor param; only the foldout header is a toggle.");
            Button barrier = obstacles.Q<Button>("obstacle-barrier");
            Assert.That(barrier.tooltip, Is.EqualTo(LevelDatabaseWindow.k_barrierTooltip));
            Assert.That(barrier.tooltip, Does.Contain("closed cells are walls and open cells are floor")
                .And.Contain("flips after every cleared block"));
            Color wall = root.Q<LevelCanvasElement>().GetPaletteColor(PaletteColor.Wall);
            Color swatch = barrier.Q("obstacle-barrier-swatch").style.backgroundColor.value;
            Assert.That(new Color(swatch.r, swatch.g, swatch.b), Is.EqualTo(new Color(wall.r, wall.g, wall.b)),
                "The swatch shows the Wall colour.");
        }

        [Test]
        public void BarrierTileArmsThePenAndAPieceSelectEraserOrTransformDisarmsIt()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Button barrier = root.Q<Button>("obstacle-barrier");
            Assert.That(Enum.GetValues(typeof(LevelEditorTool)), Has.Length.EqualTo(7), "No tool value is added.");

            Press(barrier);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen), "Barrier arms the Pen.");
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Barrier));
            Assert.That(GetToolState().IsBarrierArmed, Is.True);
            Assert.That(GetToolState().HasArmedShape, Is.False, "Barrier replaces an armed piece.");
            Assert.That(barrier.style.backgroundColor.keyword, Is.EqualTo(StyleKeyword.Undefined),
                "The tile shows its armed state with the tool buttons' fill.");
            Assert.That(barrier.style.borderTopColor.value, Is.EqualTo(Color.white), "And their white frame.");
            Assert.That(root.Q<Button>("tool-pen").style.borderTopColor.value, Is.EqualTo(Color.white));

            // The brush keeps the Pen, where 1, 2 and 3 pick colours (TASK-20260926-180414067-b923c8a9), so the tool
            // buttons switch.
            Press(root.Q<Button>("tool-pen"));
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Barrier), "The Pen button keeps the brush.");

            Press(root.Q<Button>("tool-select"));
            AssertDisarmed(root, canvas, "Select");
            Press(barrier);
            Press(root.Q<Button>("tool-eraser"));
            AssertDisarmed(root, canvas, "Eraser");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Eraser));
            Press(barrier);
            Press(root.Q<Button>("rotate-block-cw"));
            AssertDisarmed(root, canvas, "a transform tool");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.RotateClockwise));

            LevelShapeChoice piece = FindTemplate();
            if (piece != null)
            {
                Press(barrier);
                LevelEditorTestAccess.Invoke(m_window, "ArmShape", new object[] { piece });
                AssertDisarmed(root, canvas, "a piece");
                Assert.That(GetToolState().ArmedKey, Is.EqualTo(piece.Key));
                Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "A piece is placed with Select (2026-09-26).");
            }
        }

        [UnityTest]
        public IEnumerator EraserAndRightDragEraseBarrierCellsAsOneUndoStepEach()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LevelData fixture = LevelObstacleWallCommandsTests.CreateLevel32();
            LoadFixture(root, fixture);
            Press(root.Q<Button>("obstacle-barrier"));
            Drag(root, canvas, new Vector2Int(1, 1), new Vector2Int(2, 1), 0);
            string painted = JsonUtility.ToJson(GetDraft());
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(1));
            Assert.That(GetDraft().GetCellObjectAt(0).CellCount, Is.EqualTo(2));
            Assert.That(GetDraft().VoidCellCount, Is.Zero, "A barrier is a board object, not a wall.");

            // The brush keeps the Pen, where 3 picks a colour (TASK-20260926-180414067-b923c8a9).
            Press(root.Q<Button>("tool-eraser"));
            Drag(root, canvas, new Vector2Int(1, 1), new Vector2Int(2, 1), 0);
            Assert.That(GetDraft().CellObjectCount, Is.Zero, "The Eraser drag removes the barrier.");
            string erased = JsonUtility.ToJson(GetDraft());
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(painted), "One undo brings the whole barrier back.");
            Undo.PerformRedo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(erased));
            Undo.PerformUndo();

            Press(root.Q<Button>("obstacle-barrier"));
            Drag(root, canvas, new Vector2Int(2, 1), new Vector2Int(1, 1), 1);
            Assert.That(GetDraft().CellObjectCount, Is.Zero, "A right drag with the Barrier brush erases it.");
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Barrier), "The brush stays armed.");
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(painted));

            // The one right-click rule: a right press on a block still removes it with the Barrier brush armed.
            Drag(root, canvas, new Vector2Int(3, 0), new Vector2Int(3, 0), 1);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(fixture.BlockCount - 1));
            Assert.That(GetDraft().CellObjectCount, Is.EqualTo(1), "The barrier stays.");
        }

        [UnityTest]
        public IEnumerator VoidBrushCutsFreeFloorAndARightDragRestoresItAsOneUndoStepEach()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LevelData fixture = LevelObstacleWallCommandsTests.CreateLevel32();
            LoadFixture(root, fixture);
            string before = JsonUtility.ToJson(GetDraft());
            Press(root.Q<Button>("obstacle-void"));
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen), "Void arms the Pen.");
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Void));
            Assert.That(GetToolState().IsVoidArmed && !GetToolState().IsBarrierArmed, Is.True);

            Drag(root, canvas, new Vector2Int(1, 1), new Vector2Int(2, 1), 0);
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(2), "The stroke cuts both free cells.");
            Assert.That(GetDraft().CellObjectCount, Is.Zero, "A void is not a board object.");
            Assert.That(GetDraft().BlockCount, Is.EqualTo(fixture.BlockCount));
            string cut = JsonUtility.ToJson(GetDraft());
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(before), "One undo restores the whole stroke.");
            Undo.PerformRedo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(cut));

            Drag(root, canvas, new Vector2Int(2, 1), new Vector2Int(1, 1), 1);
            Assert.That(GetDraft().VoidCellCount, Is.Zero, "A right drag with the Void brush restores floor.");
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.Void), "The brush stays armed.");
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(cut));

            // Block b1 covers (1,3), so a stroke over it changes nothing there.
            Drag(root, canvas, new Vector2Int(1, 3), new Vector2Int(1, 3), 0);
            Assert.That(GetDraft().BlockCount, Is.EqualTo(fixture.BlockCount), "A block cell is never cut.");
            Assert.That(GetDraft().VoidCellCount, Is.EqualTo(2));
        }

        private static void AssertDisarmed(VisualElement root, LevelCanvasElement canvas, string by)
        {
            Assert.That(canvas.ObstacleBrush, Is.EqualTo(LevelObstacleBrush.None), $"{by} disarms the Barrier brush.");
            StyleKeyword fill = root.Q<Button>("obstacle-barrier").style.backgroundColor.keyword;
            Assert.That(fill, Is.Not.EqualTo(StyleKeyword.Undefined), $"The Barrier tile is not highlighted after {by}.");
        }

        // Any template can be armed (DEC-193: templates carry no prefab).
        private LevelShapeChoice FindTemplate()
        {
            LevelShapeCatalog catalog = (LevelShapeCatalog)LevelEditorTestAccess.Get(m_window, "m_shapeCatalog");
            return catalog.Count > 0 ? catalog.Get(0) : null;
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        private static void Drag(VisualElement root, LevelCanvasElement canvas, Vector2Int from, Vector2Int to, int button)
        {
            Vector2 start = canvas.LocalToWorld(canvas.Geometry.GetCellRect(from).center);
            Vector2 end = canvas.LocalToWorld(canvas.Geometry.GetCellRect(to).center);
            UiTestInput.SendPointer(root, EventType.MouseDown, start, button);
            UiTestInput.SendPointer(root, EventType.MouseDrag, end, button);
            UiTestInput.SendPointer(root, EventType.MouseUp, end, button);
        }

        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // Replaces the window's draft with a known level, as a Reload would, and refreshes the canvas.
        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private LevelData GetDraft()
        {
            return m_fixture.GetDraft();
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open();
            m_window = m_fixture.Window;
            return root;
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

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }
    }
}
