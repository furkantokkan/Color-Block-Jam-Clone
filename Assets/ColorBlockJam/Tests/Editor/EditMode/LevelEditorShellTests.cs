using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's two-pane shell, its unbound Level Name and Timer fields and the draft's undo, dirty and
    /// publish behaviour, without writing the level JSON.
    /// </summary>
    public sealed class LevelEditorShellTests
    {
        private const BindingFlags k_internalMembers = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
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
        public void ShellSplitsToolsFromTheSelectionBarAndCanvas()
        {
            VisualElement root = OpenWindow();

            Assert.That(m_window.minSize, Is.EqualTo(new Vector2(960f, 640f)));
            TwoPaneSplitView split = root.Q<TwoPaneSplitView>();
            Assert.That(split, Is.Not.Null);
            Assert.That(split.viewDataKey, Is.EqualTo(LevelDatabaseWindow.k_splitViewDataKey));
            Assert.That(split.fixedPaneIndex, Is.Zero);
            Assert.That(split.fixedPaneInitialDimension, Is.EqualTo(
                Mathf.Max(240f, EditorPrefs.GetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, 280f))));

            VisualElement column = root.Q("tools-column");
            ScrollView tools = root.Q<ScrollView>("tools-pane");
            VisualElement panel = root.Q("validation-panel");
            VisualElement selection = root.Q("selection-pane");
            Assert.That(split.IndexOf(column), Is.Zero, "The tools column is the fixed left pane.");
            Assert.That(column.IndexOf(tools), Is.Zero, "The tool groups sit at the top of the column.");
            Assert.That(column.IndexOf(panel), Is.EqualTo(1), "The validation panel is pinned under them, outside the scroll view.");
            Assert.That(split.IndexOf(selection), Is.EqualTo(1));
            Assert.That(column.style.minWidth.value.value, Is.EqualTo(240f));
            Assert.That(tools.style.minWidth.value.value, Is.EqualTo(240f));
            Assert.That(selection.style.minWidth.value.value, Is.EqualTo(480f));

            VisualElement topBar = selection.Q("top-bar");
            VisualElement canvas = selection.Q("canvas-slot");
            Assert.That(selection.IndexOf(topBar), Is.LessThan(selection.IndexOf(canvas)), "The canvas sits below the selection.");

            VisualElement bottomBar = root.Q("bottom-bar");
            Assert.That(bottomBar.parent, Is.SameAs(root), "The bottom bar spans the whole window.");
            Assert.That(bottomBar.childCount, Is.EqualTo(1), "The bottom bar keeps only the status line.");
            Assert.That(bottomBar[0], Is.SameAs(bottomBar.Q<Label>("status")));
            VisualElement buttons = panel.Q("validation-buttons");
            Assert.That(buttons.IndexOf(buttons.Q<Button>("save-level")), Is.Zero,
                "Save Level sits first in the validation panel's button row.");
            Assert.That(root.Query<Button>("save-level").ToList(), Has.Count.EqualTo(1));
        }

        [Test]
        public void ReopenedWindowRestoresTheDraggedToolsWidth()
        {
            bool hadWidth = EditorPrefs.HasKey(LevelDatabaseWindow.k_toolsWidthPrefKey);
            float savedWidth = EditorPrefs.GetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, LevelDatabaseWindow.k_toolsWidth);
            try
            {
                EditorPrefs.SetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, 300f);
                VisualElement root = OpenWindow(1280f, 800f);
                ValidateLayout(root);
                Assert.That(root.Q("tools-pane").layout.width, Is.EqualTo(300f).Within(0.5f));

                DragSplitter(root, 40f);

                Assert.That(EditorPrefs.GetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey), Is.EqualTo(340f).Within(0.5f),
                    "The end of a splitter drag stores the new width.");

                CloseWindow();
                root = OpenWindow(1280f, 800f);
                ValidateLayout(root);
                Assert.That(root.Q<TwoPaneSplitView>().fixedPaneInitialDimension, Is.EqualTo(340f).Within(0.5f));
                Assert.That(root.Q("tools-pane").layout.width, Is.EqualTo(340f).Within(0.5f));
            }
            finally
            {
                RestoreToolsWidth(hadWidth, savedWidth);
            }
        }

        [Test]
        public void ForcedToolsWidthDoesNotOverwriteTheSavedWidth()
        {
            bool hadWidth = EditorPrefs.HasKey(LevelDatabaseWindow.k_toolsWidthPrefKey);
            float savedWidth = EditorPrefs.GetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, LevelDatabaseWindow.k_toolsWidth);
            try
            {
                EditorPrefs.SetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, 300f);
                VisualElement root = OpenWindow(1280f, 800f);
                ValidateLayout(root);

                // The split forces a width like this on its fixed pane when the window narrows; the user did not choose it.
                VisualElement tools = root.Q("tools-column");
                tools.style.width = 450f;
                ValidateLayout(root);

                Assert.That(tools.layout.width, Is.EqualTo(450f).Within(0.5f));
                Assert.That(EditorPrefs.GetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey), Is.EqualTo(300f),
                    "Only a splitter drag stores the width.");
            }
            finally
            {
                RestoreToolsWidth(hadWidth, savedWidth);
            }
        }

        [Test]
        public void TopBarRowsHoldTheDatabaseSourceAndLevelControls()
        {
            VisualElement root = OpenWindow();
            VisualElement topBar = root.Q("top-bar");

            VisualElement databaseRow = topBar.Q("database-row");
            ObjectField source = databaseRow.Q<ObjectField>();
            Assert.That(databaseRow.Q<DropdownField>().label, Is.EqualTo("Level Database"));
            Assert.That(source.label, Is.EqualTo("JSON Source"));
            Assert.That(databaseRow.IndexOf(databaseRow.Q<Button>("refresh-json")), Is.GreaterThan(databaseRow.IndexOf(source)));
            Assert.That(databaseRow.IndexOf(databaseRow.Q<Button>("restore-backup")), Is.GreaterThan(databaseRow.IndexOf(source)));
            VisualElement databaseField = databaseRow.Q<DropdownField>().parent;
            Button createDatabase = databaseRow.Q<Button>("create-database");
            Button deleteDatabase = databaseRow.Q<Button>("delete-database");
            Assert.That(createDatabase.parent, Is.SameAs(databaseField), "+ sits beside the Level Database dropdown.");
            Assert.That(deleteDatabase.parent, Is.SameAs(databaseField));
            Assert.That(databaseField.IndexOf(createDatabase), Is.LessThan(databaseField.IndexOf(deleteDatabase)),
                "+ comes before ×.");
            Assert.That(topBar.Q("database-actions"), Is.Null, "+ asks for the database name in a popup.");
            Assert.That(root.Query<Button>().Where(button => button.text == "Create Database").ToList(), Is.Empty);

            VisualElement levelRow = topBar.Q("level-row");
            ChoiceStepper stepper = levelRow.Q<ChoiceStepper>();
            TextField levelName = levelRow.Q<TextField>("level-name");
            Assert.That(levelRow.IndexOf(levelName), Is.GreaterThan(levelRow.IndexOf(stepper)), "Level Name follows the stepper.");
            Assert.That(levelName.isDelayed, Is.True);
            IntegerField timer = levelRow.Q<IntegerField>("timer-seconds");
            Assert.That(levelRow.IndexOf(timer), Is.EqualTo(levelRow.IndexOf(levelName) + 1), "The timer follows Level Name.");
            Assert.That(levelRow.IndexOf(levelRow.Q<Label>("timer-text")), Is.EqualTo(levelRow.IndexOf(timer) + 1),
                "Its m:ss reading follows it.");
            Assert.That(timer.isDelayed, Is.True);

            Assert.That(root.Query<Button>().Where(button => button.text == "Reload JSON").ToList(), Is.Empty);
            Assert.That(root.Query<Button>().Where(button => button.text == "Restore Previous Save").ToList(), Is.Empty);
        }

        [Test]
        public void ToolsColumnHoldsToolsEditColorsBlocksObstaclesAndRawData()
        {
            VisualElement root = OpenWindow();
            VisualElement tools = root.Q<ScrollView>("tools-pane");

            var groups = new List<string>();
            foreach (VisualElement child in tools.contentContainer.Children())
            {
                groups.Add(child.name);
            }

            Assert.That(groups, Is.EqualTo(new[]
            {
                "tools-group", "edit-group", "colors-group", "blocks-group", "obstacles-group"
            }), "Edit sits directly below Tools (user request of 2026-09-26) and Obstacles directly below Blocks " +
                "(TASK-20260925-145628329-306ba760); Raw data is gone (user request of 2026-09-26).");
            foreach (string removed in new[] { "boundary-group", "obstacle-wall", "doors-group", "board-group", "mechanics-group", "level-id", "board-width",
                         "board-height", "raw-data-group", "raw-data" })
            {
                Assert.That(root.Q(removed), Is.Null, $"{removed} is gone (user request of 2026-09-25).");
            }

            Foldout blocks = tools.Q<Foldout>("blocks-group");
            List<DropdownField> blockFields = blocks.Query<DropdownField>().ToList();
            Assert.That(blockFields, Has.Count.EqualTo(2), "Selected Block and Piece Type.");
            foreach (DropdownField field in blockFields)
            {
                Assert.That(field.style.display.value, Is.EqualTo(DisplayStyle.None),
                    $"{field.label} is hidden but kept for the selection sync (user request of 2026-09-26).");
            }

            Assert.That(tools.Q<Foldout>("tools-group").Q("block-actions").Query<Button>().ToList()
                    .ConvertAll(button => button.name),
                Is.EqualTo(new[] { "rotate-block-ccw", "rotate-block-cw", "flip-block-h", "flip-block-v" }),
                "Rotate counter-clockwise, rotate clockwise, mirror horizontally and mirror vertically.");
        }

        [Test]
        public void NameAndTimerShowTheDraftAfterTheGuiIsRebuilt()
        {
            // The one smoke test kept on the real project database (Tests-Editor-1-F3): it proves the window loads
            // the active database's first level, so it never substitutes a fixture one.
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            LevelData level = GetFirstLevel();

            AssertFieldsShow(root, level);

            // A domain reload disables the window, enables it again and rebuilds its GUI.
            Invoke("OnDisable");
            Invoke("OnEnable");
            Invoke("CreateGUI");

            AssertFieldsShow(m_window.rootVisualElement, level);
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void TimerCommitClampsAndShowsMinutes()
        {
            VisualElement root = OpenWindow();
            m_fixture.LoadDatabase();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            Label timerText = root.Q<Label>("timer-text");
            int original = timer.value;

            timer.value = 5;
            Assert.That(timer.value, Is.EqualTo(10));
            Assert.That(timerText.text, Is.EqualTo("0:10"));

            timer.value = 999;
            Assert.That(timer.value, Is.EqualTo(300));
            Assert.That(timerText.text, Is.EqualTo("5:00"));
            Assert.That(m_window.hasUnsavedChanges, Is.EqualTo(original != 300));

            timer.value = original;
            Assert.That(m_window.hasUnsavedChanges, Is.False, "The original value is the saved state again.");
            Assert.That(root.Q<Label>("status").text, Is.EqualTo(GetEditingStatus()));
        }

        [Test]
        public void UndoAfterAnOutsideDatabaseSwitchKeepsUnsavedEdits()
        {
            VisualElement root = OpenWindow();
            m_fixture.LoadDatabase();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int original = timer.value;
            int edited = original == 60 ? 61 : 60;
            timer.value = edited;

            string realGuid = (string)LevelEditorTestAccess.Get(m_window, "m_loadedGuid");

            // Stands in for an Inspector edit switching the installer's active JSON, without writing ProjectContext.prefab.
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", "switched-outside-the-window");
            Invoke("OnUndoRedo");

            Assert.That(timer.value, Is.EqualTo(edited), "The unsaved edit stays.");
            Assert.That(m_window.hasUnsavedChanges, Is.True);
            Assert.That(root.Q<Label>("status").text, Does.StartWith("The active level JSON changed outside the Level Editor."));

            // Once the draft is clean, the next undo loads the active database again.
            timer.value = original;
            Invoke("OnUndoRedo");

            Assert.That(LevelEditorTestAccess.Get(m_window, "m_loadedGuid"), Is.EqualTo(realGuid));
            Assert.That(root.Q<Label>("status").text, Is.EqualTo(GetEditingStatus()));
        }

        [Test]
        public void ClosingADirtyWindowGoesThroughUnitysSavePrompt()
        {
            VisualElement root = OpenWindow();
            m_fixture.LoadDatabase();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            timer.value = timer.value == 60 ? 61 : 60;
            // Unity's host window (ContainerWindow, internal) tracks unsaved windows, adds the title asterisk and
            // asks Save/Discard/Cancel from its close request before it closes a window.
            object host = GetHostContainer();

            Assert.That(GetHostValue(host, "hasUnsavedChanges"), Is.True);
            Assert.That((string)GetHostValue(host, "displayedTitle"), Does.EndWith("*"), "The title shows the unsaved marker.");
            if (Application.isBatchMode)
            {
                // Batch mode answers the prompt with Cancel and logs it. The interactive Editor would open the modal
                // prompt and block the test run, so there the test stops at the state Unity checks before it asks.
                LogAssert.Expect(LogType.Assert, new Regex(
                    "Unsaved Changes Detected The selected level has unsaved changes\\. Save them to the JSON source\\?"));
                Assert.That(RequestClose(host), Is.False, "Cancel keeps the window open.");
                Assert.That(m_window.hasUnsavedChanges, Is.True, "Cancel keeps the edit.");
            }

            m_window.DiscardChanges();

            Assert.That(GetHostValue(host, "hasUnsavedChanges"), Is.False);
            Assert.That((string)GetHostValue(host, "displayedTitle"), Does.Not.EndWith("*"));
            Assert.That(RequestClose(host), Is.True, "A clean window closes without a prompt.");
        }

        [UnityTest]
        public IEnumerator UndoAndRedoOfABoardEditUpdateFieldsDirtyMarkerAndPreview()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            m_fixture.LoadDatabase();
            Assert.That(root.Q("top-bar") != null && IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int original = timer.value;
            int edited = original == 60 ? 61 : 60;
            LevelData published = null;
            void OnPublished(LevelData level) => published = level;
            LevelDatabaseWindow.DraftLevelPublished += OnPublished;
            try
            {
                Undo.IncrementCurrentGroup();
                timer.value = edited;
                Assert.That(m_window.hasUnsavedChanges, Is.True);
                Assert.That(root.Q<Label>("status").text, Does.StartWith("Unsaved level changes."));
                // Guards the global undo: it runs in the same frame, and the newest undo group must be this edit.
                Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("Set Level Timer"));
                Undo.PerformUndo();

                Assert.That(timer.value, Is.EqualTo(original));
                Assert.That(m_window.hasUnsavedChanges, Is.False);
                Assert.That(root.Q<Label>("status").text, Is.EqualTo(GetEditingStatus()), "The saved level is back.");
                published = null;
                yield return WaitUntil(() => published != null);
                Assert.That(published?.TimerSeconds, Is.EqualTo(original), "Undo publishes the draft to the preview.");

                // Redo only replays this edit; if anything else was recorded meanwhile, the redo stack is empty.
                Undo.PerformRedo();
                Assert.That(timer.value, Is.EqualTo(edited));
                Assert.That(m_window.hasUnsavedChanges, Is.True);
                published = null;
                yield return WaitUntil(() => published != null);
                Assert.That(published?.TimerSeconds, Is.EqualTo(edited), "Redo publishes the draft to the preview.");
            }
            finally
            {
                LevelDatabaseWindow.DraftLevelPublished -= OnPublished;
                CloseWindow();
            }
        }

        [UnityTest]
        public IEnumerator OneFieldCommitPublishesTheDraftOnce()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            m_fixture.LoadDatabase();
            Assert.That(root.Q("top-bar") != null && IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int edited = timer.value == 60 ? 61 : 60;
            int publishCount = 0;
            void OnPublished(LevelData level) => publishCount++;
            LevelDatabaseWindow.DraftLevelPublished += OnPublished;
            try
            {
                // Lets the load publish run first.
                yield return WaitFor(1.0);
                publishCount = 0;

                timer.value = edited;
                // The publish runs before the next binding tick, the order that once published twice.
                RunScheduledPublishNow();
                // Covers several binding ticks (100 ms) and editor updates.
                yield return WaitFor(1.5);

                Assert.That(publishCount, Is.EqualTo(1), "One commit reaches the preview once.");
            }
            finally
            {
                LevelDatabaseWindow.DraftLevelPublished -= OnPublished;
                CloseWindow();
            }
        }

        [Test]
        public void TimerTextUsesMinutesAndTwoDigitSeconds()
        {
            Assert.That(LevelDatabaseWindow.FormatTimer(10), Is.EqualTo("0:10"));
            Assert.That(LevelDatabaseWindow.FormatTimer(90), Is.EqualTo("1:30"));
            Assert.That(LevelDatabaseWindow.FormatTimer(300), Is.EqualTo("5:00"));
        }

        private VisualElement OpenWindow(float width = 960f, float height = 640f)
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open(width, height);
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity();
            m_window = m_fixture.Window;
        }

        // Drags the splitter handle the way a mouse does: press, move, release.
        private static void DragSplitter(VisualElement root, float distance)
        {
            // The main split's own handle: the tools column holds a second, vertical one with the same name.
            VisualElement split = root.Q("level-editor-split");
            VisualElement handle = null;
            foreach (VisualElement child in split.hierarchy.Children())
            {
                if (child.name == "unity-dragline-anchor")
                {
                    handle = child;
                }
            }

            Vector2 start = handle.worldBound.center;
            SendPointer(root, EventType.MouseDown, start);
            SendPointer(root, EventType.MouseDrag, start + new Vector2(distance, 0f));
            ValidateLayout(root);
            SendPointer(root, EventType.MouseUp, start + new Vector2(distance, 0f));
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position)
        {
            UiTestInput.SendPointer(root, type, position);
        }

        // Runs the window's pending publish at once instead of on the next editor update; PublishDraft removes
        // its own scheduled call, so it does not run again.
        private void RunScheduledPublishNow()
        {
            LevelEditorTestAccess.Invoke(m_window, "PublishDraft", null);
        }

        private static void RestoreToolsWidth(bool hadWidth, float savedWidth)
        {
            if (hadWidth)
            {
                EditorPrefs.SetFloat(LevelDatabaseWindow.k_toolsWidthPrefKey, savedWidth);
            }
            else
            {
                EditorPrefs.DeleteKey(LevelDatabaseWindow.k_toolsWidthPrefKey);
            }
        }

        // The level the window has selected in the levels it holds now: the fixture levels after LoadDatabase, the
        // real project database after a reload of the active JSON.
        private string GetEditingStatus()
        {
            int selectedIndex = (int)LevelEditorTestAccess.Get(m_window, "m_selectedIndex");
            return $"Editing {LevelEditorTestAccess.GetLevels(m_window).GetAt(selectedIndex).Name}. " +
                   "Save Level writes the JSON source.";
        }

        private static IEnumerator WaitFor(double seconds)
        {
            double end = EditorApplication.timeSinceStartup + seconds;
            while (EditorApplication.timeSinceStartup < end)
            {
                yield return null;
            }
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        private object GetHostContainer()
        {
            object view = typeof(EditorWindow).GetField("m_Parent", k_internalMembers).GetValue(m_window);
            return view.GetType().GetProperty("window", k_internalMembers).GetValue(view);
        }

        private static object GetHostValue(object host, string property)
        {
            return host.GetType().GetProperty(property, k_internalMembers).GetValue(host);
        }

        // Unity's own close request for one window: it prompts when the window has unsaved changes.
        private bool RequestClose(object host)
        {
            MethodInfo request = host.GetType().GetMethod(
                "InternalRequestClose", k_internalMembers, null, new[] { typeof(EditorWindow) }, null);
            return (bool)request.Invoke(host, new object[] { m_window });
        }

        private void Invoke(string method)
        {
            LevelEditorTestAccess.Invoke(m_window, method, null);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        // A new window selects the first level of the active database, read here from its manifest and packs.
        private static LevelData GetFirstLevel()
        {
            Assert.That(ShippedLevels.TryReadActive(out LevelData[] levels, out string error), Is.True, error);
            return levels[0];
        }

        private static void AssertFieldsShow(VisualElement root, LevelData level)
        {
            Assert.That(root.Q<TextField>("level-name").value, Is.EqualTo(level.Name));
            Assert.That(root.Q<IntegerField>("timer-seconds").value, Is.EqualTo(level.TimerSeconds));
            Assert.That(root.Q<Label>("timer-text").text, Is.EqualTo(LevelDatabaseWindow.FormatTimer(level.TimerSeconds)));
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            return LevelEditorWindowFixture.WaitUntil(condition);
        }
    }
}
