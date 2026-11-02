using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Piece Editor side of the Level Editor link: library and level-piece requests, live level-piece
    /// changes and the library commit notice. No test writes the project's piece library. Library pieces are found by
    /// their key and the batch TryAddPieces/AddRecords enum commit is gone (DEC-193), so its cases were removed.
    /// </summary>
    public sealed class PieceEditorLinkTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_lPiece = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };
        private static readonly Vector2Int[] s_square =
            { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };
        // LoadLevelPiece centres the L piece on (-1, -1), (0, -1) and (-1, 0); painting this cell makes a square.
        private static readonly Vector2Int s_squareCorner = new Vector2Int(0, 0);
        private PieceEditorWindow m_window;

        [TearDown]
        public void CloseWindow()
        {
            if (m_window != null) m_window.Close();
            m_window = null;
        }

        [Test]
        public void LibraryPieceRequestLoadsThatPiece()
        {
            var library = BlockShapeLibrary.LoadProject();
            BlockShapeLibrary.Shape shape = library.Shapes[1];
            VisualElement root = OpenWindow();

            m_window.LoadLibraryPiece(shape.Key);

            Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo(shape.DisplayName));
            Assert.That(root.Q<DropdownField>("piece-library").value, Is.EqualTo($"2. {shape.DisplayName}"));
            Assert.That(FilledCellCount(root), Is.EqualTo(shape.Cells.Count));
            Assert.That(root.Q<Button>("save-piece").text, Is.EqualTo("Save piece"));
        }

        [Test]
        public void UnknownLibraryPieceKeepsTheDraftAndSaysWhy()
        {
            var library = BlockShapeLibrary.LoadProject();
            VisualElement root = OpenWindow();
            m_window.LoadLibraryPiece(library.Shapes[1].Key);

            // A key no template has (the default key names no shape).
            m_window.LoadLibraryPiece(default);

            Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo(library.Shapes[1].DisplayName));
            Assert.That(root.Q<HelpBox>("piece-info").text, Does.Contain("No template"));
        }

        [Test]
        public void LevelPieceRequestOpensLevelPieceModeWithoutApplyToLevel()
        {
            VisualElement root = OpenWindow();

            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", Array.Empty<Vector2Int>()));

            Assert.That(FilledCellCount(root), Is.EqualTo(1), "An empty request starts from one cell, like New piece.");
            Assert.That(root.Q<Button>("save-piece").style.display.value, Is.EqualTo(DisplayStyle.None),
                "Edits reach the level as they happen, so there is no Apply to level.");
            Assert.That(root.Q<Button>("save-template").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("delete-piece").enabledSelf, Is.False);
            Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo("New Piece"), "A request without a name.");
            string info = root.Q<HelpBox>("piece-info").text;
            Assert.That(info, Does.Contain("Level piece").And.Contain("live"));
            Assert.That(info, Does.Not.Contain("Apply to level"));

            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece));

            Assert.That(FilledCellCount(root), Is.EqualTo(3));
        }

        [Test]
        public void ChoosingNewPieceLeavesLevelPieceMode()
        {
            VisualElement root = OpenWindow();
            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece));

            // A level-piece draft never asks the discard question.
            Click(root.Q<Button>("new-piece"));

            Assert.That(root.Q<Button>("save-piece").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(root.Q<Button>("save-piece").text, Is.EqualTo("Save piece"));
            Assert.That(FilledCellCount(root), Is.EqualTo(1));
            Assert.That(root.Q<Button>("save-template").style.display.value, Is.EqualTo(DisplayStyle.None));
        }

        [Test]
        public void LevelPieceModeSurvivesAGuiRebuildAndIsSerialized()
        {
            VisualElement root = OpenWindow();
            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));

            // A domain reload keeps the serialized fields and then calls CreateGUI again.
            m_window.CreateGUI();
            root = m_window.rootVisualElement;

            Assert.That(root.Q<Button>("save-piece").style.display.value, Is.EqualTo(DisplayStyle.None));
            Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo("Level 1 Piece 1"));
            Assert.That(FilledCellCount(root), Is.EqualTo(3), "The level draft is kept, not replaced by New piece.");
            string json = EditorJsonUtility.ToJson(m_window);
            Assert.That(json, Does.Contain("\"m_levelPieceMode\":true"));
            Assert.That(json, Does.Contain("Custom_ABC").And.Contain("level-001").And.Contain("Level 1 Piece 1"));
        }

        [Test]
        public void GridEditsRaiseLevelPieceChangedOnceAndWriteNoLibraryData()
        {
            string[] libraryFiles =
            {
                BoardKitAssets.ShapesJsonPath
            };
            string[] before = Hashes(libraryFiles);
            var changes = new List<LevelPieceRequest>();
            Action<LevelPieceRequest> listener = changes.Add;
            PieceEditorWindow.LevelPieceChanged += listener;
            try
            {
                VisualElement root = OpenWindow();
                m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
                Assert.That(changes, Is.Empty, "Showing a request sends nothing back.");

                Stroke(root, true, s_squareCorner);

                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].LevelId, Is.EqualTo("level-001"));
                Assert.That(changes[0].PieceId, Is.EqualTo("Custom_ABC"));
                Assert.That(changes[0].Name, Is.EqualTo("Level 1 Piece 1"));
                Assert.That(changes[0].Cells, Is.EquivalentTo(s_square), "Cells are normalized.");

                Stroke(root, true, s_squareCorner);
                Assert.That(changes, Has.Count.EqualTo(1), "A stroke that changes nothing sends nothing.");

                m_window.TransformPiece(LevelEditorTool.RotateClockwise);
                Assert.That(changes, Has.Count.EqualTo(2));

                Click(root.Q<Button>("clear-grid"));
                Assert.That(changes, Has.Count.EqualTo(2), "An empty grid is not sent.");
                Assert.That(root.Q<HelpBox>("piece-info").text, Does.Contain("The level keeps its last footprint"));

                // Grid undo restores the cleared cells and sends them.
                Undo.PerformUndo();
                Assert.That(changes, Has.Count.EqualTo(3));
                Assert.That(changes[2].Cells, Is.EquivalentTo(s_square));
                Assert.That(Hashes(libraryFiles), Is.EqualTo(before), "Level-piece edits write no library data.");
            }
            finally
            {
                PieceEditorWindow.LevelPieceChanged -= listener;
            }
        }

        [Test]
        public void LibraryModeEditsRaiseNothing()
        {
            var changes = new List<LevelPieceRequest>();
            Action<LevelPieceRequest> listener = changes.Add;
            PieceEditorWindow.LevelPieceChanged += listener;
            try
            {
                VisualElement root = OpenWindow();

                Stroke(root, true, new Vector2Int(1, 0));
                m_window.TransformPiece(LevelEditorTool.MirrorHorizontal);

                Assert.That(changes, Is.Empty);
            }
            finally
            {
                PieceEditorWindow.LevelPieceChanged -= listener;
            }
        }

        [Test]
        public void ListenerUndoJoinsTheStrokeUndoStep()
        {
            GameObject probe = EditorUtility.CreateGameObjectWithHideFlags("level-piece-undo-probe", HideFlags.HideAndDontSave);
            var changes = new List<LevelPieceRequest>();
            Action<LevelPieceRequest> listener = request =>
            {
                changes.Add(request);
                // Only the stroke records, like a level that already matches after the undo.
                if (changes.Count > 1) return;
                Undo.RecordObject(probe.transform, "Level edit");
                probe.transform.position = Vector3.one;
            };
            PieceEditorWindow.LevelPieceChanged += listener;
            try
            {
                VisualElement root = OpenWindow();
                m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
                Stroke(root, true, s_squareCorner);
                Assert.That(probe.transform.position, Is.EqualTo(Vector3.one));

                Undo.PerformUndo();

                Assert.That(probe.transform.position, Is.EqualTo(Vector3.zero), "One Undo restores the listener's object.");
                Assert.That(FilledCellCount(root), Is.EqualTo(3), "The same Undo restores the grid.");
                Assert.That(changes, Has.Count.EqualTo(2));
                Assert.That(changes[1].Cells, Is.EquivalentTo(s_lPiece), "The restored cells are sent.");
            }
            finally
            {
                PieceEditorWindow.LevelPieceChanged -= listener;
                Undo.ClearUndo(probe.transform);
                Object.DestroyImmediate(probe);
            }
        }

        [Test]
        public void ThrowingListenerDoesNotStopOtherListeners()
        {
            var changes = new List<LevelPieceRequest>();
            Action<LevelPieceRequest> failing = _ => throw new InvalidOperationException("Test listener failure");
            Action<LevelPieceRequest> listener = changes.Add;
            PieceEditorWindow.LevelPieceChanged += failing;
            PieceEditorWindow.LevelPieceChanged += listener;
            try
            {
                VisualElement root = OpenWindow();
                m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
                UnityEngine.TestTools.LogAssert.Expect(LogType.Exception, "InvalidOperationException: Test listener failure");

                Stroke(root, true, s_squareCorner);

                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(FilledCellCount(root), Is.EqualTo(4));
            }
            finally
            {
                PieceEditorWindow.LevelPieceChanged -= failing;
                PieceEditorWindow.LevelPieceChanged -= listener;
            }
        }

        [Test]
        public void CommittedNameEditSendsTheNewName()
        {
            var changes = new List<LevelPieceRequest>();
            Action<LevelPieceRequest> listener = changes.Add;
            PieceEditorWindow.LevelPieceChanged += listener;
            try
            {
                VisualElement root = OpenWindow();
                m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
                var name = root.Q<TextField>("piece-name");
                // The designer types in a focused field; the panel routes key events to the focused element.
                name.Focus();

                name.value = "Hook";
                Assert.That(changes, Is.Empty, "Typing sends nothing until the edit is committed.");

                SendKey(name, KeyCode.Return);
                Assert.That(changes, Has.Count.EqualTo(1));
                Assert.That(changes[0].Name, Is.EqualTo("Hook"));
                Assert.That(changes[0].Cells, Is.EquivalentTo(s_lPiece));

                SendKey(name, KeyCode.Return);
                Assert.That(changes, Has.Count.EqualTo(1), "A committed name is not sent again.");

                name.value = "Long Hook";
                name.Blur();
                Assert.That(changes, Has.Count.EqualTo(2), "Leaving the field commits the edit.");
                Assert.That(changes[1].Name, Is.EqualTo("Long Hook"));

                name.Focus();
                name.value = "  ";
                SendKey(name, KeyCode.Return);
                Assert.That(changes, Has.Count.EqualTo(2), "A blank name is not sent.");
                Assert.That(root.Q<HelpBox>("piece-info").text, Does.Contain("Enter a piece name"));
            }
            finally
            {
                PieceEditorWindow.LevelPieceChanged -= listener;
            }
        }

        [Test]
        public void EqualRequestKeepsTheDraftAndItsUndoHistory()
        {
            VisualElement root = OpenWindow();
            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
            Stroke(root, true, s_squareCorner);

            // The Level Editor mirrors the stroke back with the same footprint at its own offset.
            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", Offset(s_square, 3, 5), "Level 1 Piece 1"));
            Assert.That(FilledCellCount(root), Is.EqualTo(4));

            Undo.PerformUndo();
            Assert.That(FilledCellCount(root), Is.EqualTo(3), "The stroke is still undoable.");

            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_single, "Level 1 Piece 1"));
            Assert.That(FilledCellCount(root), Is.EqualTo(1), "A different request replaces the draft.");
        }

        [Test]
        public void LevelPieceModeNeverAsksToDiscard()
        {
            VisualElement root = OpenWindow();
            m_window.LoadLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"));
            Click(root.Q<Button>("clear-grid"));

            // Checked first: a discard dialog would block this test instead of failing it.
            Assert.That(Invoke("CanDiscardDraft"), Is.True);

            m_window.LoadLevelPiece(new LevelPieceRequest("level-002", "Custom_XYZ", s_single));
            Assert.That(FilledCellCount(root), Is.EqualTo(1));
            Stroke(root, true, new Vector2Int(1, 0));
            Assert.That(Invoke("CanDiscardDraft"), Is.True);
            Click(root.Q<Button>("new-piece"));
            Assert.That(root.Q<Button>("save-piece").style.display.value, Is.EqualTo(DisplayStyle.Flex));
        }

        [Test]
        public void ShowLevelPieceWithoutFocusUpdatesTheOpenWindow()
        {
            // The one-argument form still converts to the Level Editor's Action<LevelPieceRequest> property.
            Action<LevelPieceRequest> show = PieceEditorWindow.ShowLevelPiece;
            Assert.That(show, Is.Not.Null);

            VisualElement root = OpenWindow();
            if (Resources.FindObjectsOfTypeAll<PieceEditorWindow>().Length != 1)
                Assert.Ignore("Another Piece Editor is open, and this check would change it.");

            PieceEditorWindow.ShowLevelPiece(new LevelPieceRequest("level-001", "Custom_ABC", s_lPiece, "Level 1 Piece 1"), false);

            Assert.That(Resources.FindObjectsOfTypeAll<PieceEditorWindow>(), Has.Length.EqualTo(1), "No second window opens.");
            Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo("Level 1 Piece 1"));
            Assert.That(FilledCellCount(root), Is.EqualTo(3));
        }

        [Test]
        public void LibraryCommittedIsRaisedOncePerSuccessfulCommitIncludingUndo()
        {
            var library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            int commits = 0;
            Action listener = () => commits++;
            ShapeLibraryAuthoring.LibraryCommitted += listener;
            try
            {
                ShapeLibraryAuthoring.SavePiece(library, default, "Single", s_single, null);
                Assert.That(commits, Is.EqualTo(1));

                // A second template of the same shape fails validation, so this commit changes nothing.
                var sameShape = new BlockShapeLibrary.Shape("Same shape", s_single);
                Assert.Throws<ArgumentException>(() => ShapeLibraryAuthoring.Commit(
                    library, new[] { library.Shapes[0], sameShape }, "Add a second single"));
                Assert.That(commits, Is.EqualTo(1), "A failed commit raises nothing.");

                // Library undo replays through Commit.
                Undo.PerformUndo();
                Assert.That(library.Shapes, Is.Empty);
                Assert.That(commits, Is.EqualTo(2));
            }
            finally
            {
                ShapeLibraryAuthoring.LibraryCommitted -= listener;
                ShapeLibraryAuthoring.ClearHistory(library);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void CommitNoticeRefreshesTheCachedLibraryJsonAndKeepsTheDraft()
        {
            var library = BlockShapeLibrary.LoadProject();
            ShapeLibraryAuthoring.Load(library);
            string original = library.LoadedJson;
            VisualElement root = OpenWindow();
            Assert.That(Subscribers(), Does.Contain(m_window), "The window listens for library commits.");
            m_window.LoadLibraryPiece(library.Shapes[0].Key);
            var shapes = new List<BlockShapeLibrary.Shape>(library.Shapes);
            shapes.RemoveAt(shapes.Count - 1);
            string changed = BlockShapeLibraryJson.Write(shapes);
            try
            {
                // An in-memory library change stands in for another window's commit, so no project file is written.
                // Only this window is told, so other open windows keep their state.
                library.Load(changed);
                typeof(PieceEditorWindow).GetMethod("OnLibraryCommitted", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(m_window, null);

                Assert.That(typeof(PieceEditorWindow).GetField("m_loadedJson", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(m_window), Is.EqualTo(changed), "The next Save compares the disk with this JSON.");
                Assert.That(root.Q<DropdownField>("piece-library").choices, Has.Count.EqualTo(shapes.Count + 1));
                Assert.That(root.Q<DropdownField>("piece-library").value, Is.EqualTo($"1. {shapes[0].DisplayName}"),
                    "The selected piece stays selected.");
                Assert.That(root.Q<TextField>("piece-name").value, Is.EqualTo(shapes[0].DisplayName));
            }
            finally
            {
                library.Load(original);
            }
        }

        private VisualElement OpenWindow()
        {
            m_window = ScriptableObject.CreateInstance<PieceEditorWindow>();
            // A shown utility window gives the fields a panel, so they send change events.
            m_window.ShowUtility();
            m_window.CreateGUI();
            return m_window.rootVisualElement;
        }

        private static Vector2Int[] Offset(IReadOnlyList<Vector2Int> cells, int x, int y)
        {
            var result = new Vector2Int[cells.Count];
            for (int i = 0; i < result.Length; i++) result[i] = cells[i] + new Vector2Int(x, y);
            return result;
        }

        // Paints or erases the given grid cells as one pointer stroke, without relying on OS pointer input.
        private void Stroke(VisualElement root, bool paint, params Vector2Int[] cells)
        {
            var grid = (PieceGridView)typeof(PieceEditorWindow).GetField("m_grid", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(m_window);
            grid.BeginPaint(cells[0].x, cells[0].y, paint, GridCell(root, cells[0]));
            for (int i = 1; i < cells.Length; i++)
                grid.Paint(cells[i].x, cells[i].y, GridCell(root, cells[i]));
            grid.EndPaint();
        }

        private static VisualElement GridCell(VisualElement root, Vector2Int cell) => root.Q($"cell-{cell.x}-{cell.y}");

        private object Invoke(string method, params object[] arguments) =>
            typeof(PieceEditorWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(m_window, arguments);

        // The panel delivers key events to its focused element, so the target must have the focus.
        private static void SendKey(VisualElement target, KeyCode key)
        {
            using (KeyDownEvent keyDown = KeyDownEvent.GetPooled('\0', key, EventModifiers.None))
                target.SendEvent(keyDown);
        }

        private static List<object> Subscribers()
        {
            var targets = new List<object>();
            var listeners = (Action)typeof(ShapeLibraryAuthoring)
                .GetField("LibraryCommitted", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (listeners != null)
                foreach (Delegate listener in listeners.GetInvocationList())
                    targets.Add(listener.Target);
            return targets;
        }

        private static int FilledCellCount(VisualElement root)
        {
            int filled = 0;
            root.Q("piece-grid").Query<VisualElement>().ForEach(element =>
            {
                if (element.name != null && element.name.StartsWith("cell-", StringComparison.Ordinal) &&
                    element.style.backgroundColor.value != PieceGridView.EmptyCellColor)
                    filled++;
            });
            return filled;
        }

        private static string[] Hashes(string[] paths)
        {
            var hashes = new string[paths.Length];
            using (SHA256 sha = SHA256.Create())
            {
                for (int i = 0; i < paths.Length; i++)
                    hashes[i] = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(paths[i])));
            }
            return hashes;
        }

        private static void Click(Button button)
        {
            // Runs the registered click action without relying on OS pointer input.
            typeof(Clickable).GetMethod("SimulateSingleClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(button.clickable, new object[] { null, 0 });
        }
    }
}
