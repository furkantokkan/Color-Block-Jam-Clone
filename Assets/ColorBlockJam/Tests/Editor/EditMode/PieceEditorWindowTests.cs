using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Piece Editor window's built layout (grid colour, preview sizing, hidden type controls, one grid
    /// size slider) and its grid-shortcut Undo/Redo behaviour, including focus scoping and rotate/clear support.
    /// </summary>
    public sealed class PieceEditorWindowTests
    {
        [Test]
        public void PieceEditorGridColorMatchesPreviewAndCenterCrossIsIndependentOfCells()
        {
            var window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            try
            {
                // UI Toolkit dispatches BaseField change events on an attached panel.
                window.ShowUtility();
                window.CreateGUI();
                var root = window.rootVisualElement;
                var intro = root.Q<HelpBox>("piece-info");
                Assert.That(intro.messageType, Is.EqualTo(HelpBoxMessageType.Info));
                Assert.That(intro.style.marginTop.value.value, Is.GreaterThan(0));
                Assert.That(intro.parent.IndexOf(intro), Is.Zero);
                Assert.That(intro.text, Does.Contain("around the center").And.Contain("Ctrl+Shift+Z"));
                Assert.That(intro.text.Split('\n'), Has.Length.EqualTo(3), "The help stays three short lines.");
                Assert.That(root.Q<Button>("rotate-grid"), Is.Null, "Rotation comes from the Level Editor Tools.");
                Assert.That(root.Q("undo-help"), Is.Null);
                // One fixed piece library JSON and no database choice (DEC-195).
                var source = root.Q<ObjectField>("json-source");
                Assert.That(source.value, Is.SameAs(AssetDatabase.LoadAssetAtPath<TextAsset>(BoardKitAssets.ShapesJsonPath)));
                Assert.That(root.Q("piece-database"), Is.Null);
                Assert.That(root.Q("create-database"), Is.Null);
                Assert.That(root.Q("delete-database"), Is.Null);
                root.Q<EnumField>("preview-color").value = PaletteColor.DarkGreen;
                var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
                var grid = root.Q("piece-grid");
                var gridViewport = root.Q("grid-viewport");
                var preview = root.Q<Image>("piece-preview");
                Assert.That(gridViewport.style.width.value.value, Is.EqualTo(preview.style.width.value.value));
                Assert.That(gridViewport.style.height.value.value, Is.EqualTo(preview.style.height.value.value));
                var cell = grid.Q("cell-0-0");
                Assert.That(cell.style.backgroundColor.value, Is.EqualTo(palette.GetColor(PaletteColor.DarkGreen)));
                Assert.That(cell.style.borderLeftWidth.value, Is.Zero);
                Assert.That(cell.style.borderBottomWidth.value, Is.Zero);
                var cross = grid.Q("grid-center");
                Assert.That(cross.parent, Is.SameAs(grid));
                Assert.That(cross.style.left.value, Is.EqualTo(Length.Percent(50)));
                Assert.That(cross.style.top.value, Is.EqualTo(Length.Percent(50)));
                Assert.That(cross.pickingMode, Is.EqualTo(PickingMode.Ignore));
                var libraryRow = root.Q<DropdownField>("piece-library").parent;
                Assert.That(root.Q<Button>("new-piece").parent, Is.SameAs(libraryRow));
                Assert.That(root.Q<Button>("delete-piece").parent, Is.SameAs(libraryRow));
                Assert.That(root.Q("library-actions"), Is.Null);
                Assert.That(root.Q<Button>("save-piece").parent.name, Is.EqualTo("editing-actions"));
                Assert.That(root.Q("database-actions"), Is.Null);
                Assert.That(root.Q<TextField>("database-name"), Is.Null);
                Assert.That(root.Q<Button>("use-database"), Is.Null);
                Assert.That(root.Q("json-actions"), Is.Null);
                Assert.That(grid.style.width.value.value, Is.EqualTo(320));
                Assert.That(grid.style.height.value.value, Is.EqualTo(320));
            }
            finally { window.Close(); }
        }

        [Test]
        public void PieceEditorListsNumberedNamesAndHidesTypeControlsAndShowsOneGridSizeSlider()
        {
            var window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            try
            {
                window.ShowUtility(); window.CreateGUI();
                var root = window.rootVisualElement;
                var library = BlockShapeLibrary.LoadProject();
                var choices = root.Q<DropdownField>("piece-library").choices;
                Assert.That(choices.Count, Is.EqualTo(library.Shapes.Count + 1));
                Assert.That(choices[0], Is.EqualTo("New piece"));
                for (int i = 0; i < library.Shapes.Count; i++)
                    Assert.That(choices[i + 1], Is.EqualTo($"{i + 1}. {library.Shapes[i].DisplayName}"),
                        "The list shows piece names only; enum type labels stay internal.");
                Assert.That(root.Q<TextField>("piece-name").label, Is.EqualTo("Name"));
                Assert.That(root.Query<TextField>().Where(field => field.label == "Piece type").ToList(), Is.Empty);
                var sliders = root.Query<SliderInt>().ToList();
                Assert.That(sliders.Count, Is.EqualTo(1), "One slider sets a square grid.");
                var size = root.Q<SliderInt>("grid-size");
                // Every piece fits 5 x 5 (DEC-193), so the grid opens at its largest size.
                Assert.That(size.value, Is.EqualTo(PieceGridDraft.MaxSize));
                Assert.That(size.highValue, Is.EqualTo(5));
                var grid = root.Q("piece-grid");
                Assert.That(grid.Q("cell--2--2"), Is.Not.Null);
                Assert.That(grid.Q("cell-2-2"), Is.Not.Null);
                Assert.That(grid.Q("cell-3-0"), Is.Null, "The default area is 5×5 around (0, 0).");

                window.SetGridSize(3);
                Assert.That(root.Q("piece-grid").Q("cell-1-1"), Is.Not.Null, "3 means a 3×3 area.");
                Assert.That(root.Q("piece-grid").Q("cell--1--1"), Is.Not.Null);
                Assert.That(root.Q("piece-grid").Q("cell-2-2"), Is.Null);
                Assert.That(size.value, Is.EqualTo(3));
                window.SetGridSize(5);
                GetDraft(window).Set(2, 0, true);
                window.SetGridSize(3);
                Assert.That(GetDraft(window).Size, Is.EqualTo(5), "A size that would cut off painted cells is refused.");
                Assert.That(size.value, Is.EqualTo(5));
            }
            finally { window.Close(); }
        }

        [Test]
        public void GridShortcutsUndoWholeStrokesAndPreserveNameColorAndAuthoredData()
        {
            var window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            string savedJson = System.IO.File.ReadAllText(BoardKitAssets.ShapesJsonPath);
            try
            {
                window.ShowUtility(); window.CreateGUI();
                var root = window.rootVisualElement;
                var help = root.Q<HelpBox>("piece-info");
                Assert.That(help.text, Does.Contain("Ctrl+Z").And.Contain("Ctrl+Shift+Z"));
                Assert.That(help.parent.IndexOf(help), Is.Zero);
                PaintStroke(window, true, new Vector2Int(1, 0), new Vector2Int(2, 0));
                Assert.That(help.text, Does.Contain("3 cells").And.Contain("Ctrl+Shift+Z"));
                SendUndoShortcut(window, false);
                Assert.That(GetDraft(window).Get(1, 0), Is.False);
                Assert.That(GetDraft(window).Get(2, 0), Is.False);
                Assert.That(new SerializedObject(window).FindProperty("m_dirty").boolValue, Is.False);
                Assert.That(root.Q<Image>("piece-preview").image, Is.Not.Null, "Returning to the clean draft must redraw its preview.");
                SendUndoShortcut(window, true);
                Assert.That(GetDraft(window).Get(1, 0), Is.True);
                Assert.That(GetDraft(window).Get(2, 0), Is.True);
                PaintStroke(window, true, new Vector2Int(0, 1));
                PaintStroke(window, true, new Vector2Int(0, 1)); // No-op must not add a history step.
                root.Q<TextField>("piece-name").value = "Named draft";
                root.Q<EnumField>("preview-color").value = PaletteColor.Yellow;
                SendUndoShortcut(window, false);
                Assert.That(GetDraft(window).Get(0, 1), Is.False);
                Assert.That(GetDraft(window).Get(2, 0), Is.True);
                Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo("Named draft"));
                Assert.That(root.Q<EnumField>("preview-color").value, Is.EqualTo(PaletteColor.Yellow));
                var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
                Assert.That(root.Q("cell-1-0").style.backgroundColor.value, Is.EqualTo(palette.GetColor(PaletteColor.Yellow)));
                SendUndoShortcut(window, true);
                Assert.That(GetDraft(window).Get(0, 1), Is.True);
                Assert.That(System.IO.File.ReadAllText(BoardKitAssets.ShapesJsonPath), Is.EqualTo(savedJson));
            }
            finally { window.Close(); }
        }

        [Test]
        public void GridShortcutsOnlyRunWhilePieceEditorIsFocused()
        {
            var window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            var other = ScriptableObject.CreateInstance<PieceEditorWindow>();
            try
            {
                window.ShowUtility(); window.CreateGUI();
                PaintStroke(window, true, new Vector2Int(1, 0));
                other.ShowUtility(); other.CreateGUI(); other.Focus();
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(other));
                InvokeUndoShortcut(window);
                Assert.That(GetDraft(window).Get(1, 0), Is.True);
                window.Focus();
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(window));
                InvokeUndoShortcut(window);
                Assert.That(GetDraft(window).Get(1, 0), Is.False);
            }
            finally { other.Close(); window.Close(); }
        }

        [Test]
        public void GridManipulationUndoRestoresRotatedAndClearedCells()
        {
            var window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            try
            {
                window.ShowUtility(); window.CreateGUI();
                PaintStroke(window, true, new Vector2Int(1, 0), new Vector2Int(1, 1));
                var root = window.rootVisualElement;
                string original = JsonUtility.ToJson(GetDraft(window));
                window.TransformPiece(LevelEditorTool.RotateClockwise);
                string rotated = JsonUtility.ToJson(GetDraft(window));
                Assert.That(rotated, Is.Not.EqualTo(original));
                SendUndoShortcut(window, false);
                Assert.That(JsonUtility.ToJson(GetDraft(window)), Is.EqualTo(original));
                SendUndoShortcut(window, true);
                Assert.That(JsonUtility.ToJson(GetDraft(window)), Is.EqualTo(rotated));
                Click(root.Q<Button>("clear-grid"));
                var cells = new System.Collections.Generic.List<Vector2Int>(); GetDraft(window).ReadCells(cells);
                Assert.That(cells, Is.Empty);
                SendUndoShortcut(window, false);
                Assert.That(JsonUtility.ToJson(GetDraft(window)), Is.EqualTo(rotated));
            }
            finally { window.Close(); }
        }

        private static PieceGridDraft GetDraft(PieceEditorWindow window) =>
            PieceEditorTestAccess.Get<PieceGridDraft>(window, "m_draft");

        private static void PaintStroke(PieceEditorWindow window, bool filled, params Vector2Int[] cells)
        {
            var grid = PieceEditorTestAccess.Get<PieceGridView>(window, "m_grid");
            var root = window.rootVisualElement;
            grid.BeginPaint(cells[0].x, cells[0].y, filled, root.Q("cell-" + cells[0].x + "-" + cells[0].y));
            for (int i = 1; i < cells.Length; i++)
                grid.Paint(cells[i].x, cells[i].y, root.Q("cell-" + cells[i].x + "-" + cells[i].y));
            grid.EndPaint();
        }

        private static void SendUndoShortcut(PieceEditorWindow window, bool redo)
        {
            window.Focus();
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(window));
            var input = new Event
            {
                type = EventType.KeyDown,
                keyCode = KeyCode.Z,
                modifiers = EventModifiers.Control | (redo ? EventModifiers.Shift : EventModifiers.None)
            };
            using (var evt = KeyDownEvent.GetPooled(input)) window.rootVisualElement.Q("piece-grid").SendEvent(evt);
        }

        private static void InvokeUndoShortcut(PieceEditorWindow window)
        {
            var input = new Event { type = EventType.KeyDown, keyCode = KeyCode.Z, modifiers = EventModifiers.Control };
            using (var evt = KeyDownEvent.GetPooled(input)) PieceEditorTestAccess.Invoke(window, "OnKeyDown", evt);
        }

        private static void Click(Button button)
        {
            // Exercise the registered click action without relying on OS keyboard focus.
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(Clickable).GetMethod("SimulateSingleClick", flags).Invoke(button.clickable, new object[] { null, 0 });
        }
    }
}
