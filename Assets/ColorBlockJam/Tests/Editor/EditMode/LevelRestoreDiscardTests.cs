using System.Collections;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's JSON Source Restore button: with unsaved draft changes it is enabled and, after the
    /// confirmation, discards them back to the saved level without writing the level JSON; Cancel changes nothing; with
    /// none it runs Restore Previous Save (TASK-20260926-233610537-9279a211). No dialog opens and no real level JSON or
    /// backup is written: the confirmation and the database write are replaced, and a stand-in database under an unused
    /// GUID gives the no-backup and backup cases.
    /// </summary>
    public sealed class LevelRestoreDiscardTests
    {
        private const string k_restoreButtonName = "restore-backup";

        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;
        private ProjectScopeInstaller m_standInProjectScope;
        private string m_standInBackupPath;
        private string m_sourcePath;
        private byte[] m_sourceBytes;
        private string m_backupPath;
        private byte[] m_backupBytes;
        private readonly List<string> m_questions = new List<string>();
        private readonly List<List<LevelData>> m_writes = new List<List<LevelData>>();
        private bool m_discardAnswer;

        [SetUp]
        public void RememberLiveFiles()
        {
            m_questions.Clear();
            m_writes.Clear();
            m_discardAnswer = false;
            ProjectScopeInstaller projectScope = LevelDatabaseAddressables.LoadProjectScope();
            m_sourcePath = LevelDatabaseAddressables.GetActivePath(projectScope);
            m_sourceBytes = ReadBytes(m_sourcePath);
            // The backup is a folder; its manifest is what Restore looks for (DEC-196).
            m_backupPath = projectScope != null && !string.IsNullOrEmpty(projectScope.LevelJsonGuid)
                ? Path.Combine(LevelDatabaseWindow.GetBackupFolder(projectScope.LevelJsonGuid), LevelJsonFiles.ManifestFile)
                : null;
            m_backupBytes = ReadBytes(m_backupPath);
        }

        [TearDown]
        public void CloseWindow()
        {
            // The fixture's own Dispose discards any pending edit, closes the window, releases the fixture's loaded
            // database and restores the published-level snapshot it captured at construction.
            if (m_fixture != null)
            {
                m_fixture.Dispose();
                m_fixture = null;
            }

            m_window = null;

            LevelEditorTestAccess.DestroyProjectScope(m_standInProjectScope);
            m_standInProjectScope = null;

            if (m_standInBackupPath != null)
            {
                Directory.Delete(m_standInBackupPath, true);
                m_standInBackupPath = null;
            }

            Assert.That(ReadBytes(m_sourcePath), Is.EqualTo(m_sourceBytes), "No test writes the level JSON.");
            Assert.That(ReadBytes(m_backupPath), Is.EqualTo(m_backupBytes), "No test writes the level JSON backup.");
        }

        [Test]
        public void TheTooltipNamesBothJobs()
        {
            VisualElement root = OpenWindow();

            Assert.That(root.Q<Button>(k_restoreButtonName).tooltip, Is.EqualTo(
                "Restore: with unsaved changes, discard them and return the level to its last saved state; otherwise " +
                "bring back the JSON version that the last save replaced."));
        }

        // AC-1: the button follows the draft through an edit, its undo and redo, and a level load.
        [UnityTest]
        public IEnumerator UnsavedChangesEnableRestoreWithoutABackup()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            // A fixture database load guarantees a loaded level, whatever the real project's active database
            // holds (Tests-Editor-1-F3); UseStandInDatabase then swaps in its own in-memory stand-in from it.
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            UseStandInDatabase(false);
            Button restore = root.Q<Button>(k_restoreButtonName);
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int original = timer.value;
            int edited = original == 60 ? 61 : 60;
            Assert.That(restore.enabledSelf, Is.False, "No unsaved changes and no backup.");

            Undo.IncrementCurrentGroup();
            timer.value = edited;
            Assert.That(m_window.hasUnsavedChanges, Is.True);
            Assert.That(restore.enabledSelf, Is.True, "Unsaved changes enable Restore without a backup.");
            // Guards the global undo: it runs in the same frame, and the newest undo group must be this edit.
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("Set Level Timer"));

            Undo.PerformUndo();
            Assert.That(timer.value, Is.EqualTo(original));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(restore.enabledSelf, Is.False, "Undo back to the saved level disables it again.");

            Undo.PerformRedo();
            Assert.That(timer.value, Is.EqualTo(edited));
            Assert.That(restore.enabledSelf, Is.True, "Redo brings the unsaved change back.");

            // What a level switch does after its Save/Discard/Cancel answer.
            Invoke("LoadSelectedDraft");
            Assert.That(timer.value, Is.EqualTo(original));
            Assert.That(restore.enabledSelf, Is.False, "A level load leaves nothing to discard.");
        }

        // AC-1: Save Level makes the draft the saved level, so without a backup Restore has nothing to do.
        [Test]
        public void SaveLevelDisablesRestoreWithoutABackup()
        {
            VisualElement root = OpenWindow();
            // A fixture database load guarantees a loaded level, whatever the real project's active database
            // holds (Tests-Editor-1-F3); UseStandInDatabase then swaps in its own in-memory stand-in from it.
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            UseStandInDatabase(false);
            Button restore = root.Q<Button>(k_restoreButtonName);
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            timer.value = timer.value == 60 ? 61 : 60;
            Assert.That(restore.enabledSelf, Is.True);

            Assume.That((bool)LevelEditorTestAccess.Invoke(m_window, "SaveLevel", null), Is.True,
                "The first level of the active database saves.");

            Assert.That(m_writes, Has.Count.EqualTo(1), "Save Level wrote through the replaced write.");
            Assert.That(restore.enabledSelf, Is.False, "The saved draft has nothing to discard and there is no backup.");
        }

        // AC-2: Discard returns the draft to the saved level; the level JSON stays byte for byte.
        [Test]
        public void DiscardReturnsTheDraftToTheSavedLevelWithoutWritingTheJson()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            Assume.That(m_sourceBytes, Is.Not.Null, "The active level JSON is readable.");
            Button restore = root.Q<Button>(k_restoreButtonName);
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int original = timer.value;
            string saved = JsonUtility.ToJson(GetDraft().Snapshot());
            string name = GetSelectedLevelName();
            LevelDraft editedDraft = GetDraft();
            // A colour order, a pending Pen template footprint (DEC-125) and an edit; none survives the discard. The
            // template key needs no library record.
            editedDraft.SetColorOrder(LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Blue));
            editedDraft.SetTemplateEdit(new LevelTemplateEdit(LevelTestShapes.Key(new[] { Vector2Int.zero }),
                "Restore fixture piece", new[] { Vector2Int.zero }, new[] { Vector2Int.zero, Vector2Int.right }));
            timer.value = original == 60 ? 61 : 60;
            Assume.That(editedDraft.TemplateEdits, Has.Count.EqualTo(1), "The edited draft holds a template edit.");
            Assume.That(m_window.hasUnsavedChanges, Is.True);
            Assert.That(restore.enabledSelf, Is.True);
            m_discardAnswer = true;

            Press(restore);

            Assert.That(m_questions, Is.EqualTo(new[]
            {
                $"Discard your unsaved changes to {name} and return it to its last saved state?"
            }));
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(saved), "The draft is the saved level.");
            Assert.That(GetDraft(), Is.Not.SameAs(editedDraft), "The draft was rebuilt, which clears its undo history.");
            Assert.That(GetDraft().ColorOrder.IsDefault, Is.True, "The colour order is reset.");
            Assert.That(GetDraft().TemplateEdits, Is.Empty, "The pending template edit is dropped.");
            Assert.That(timer.value, Is.EqualTo(original));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(root.Q<Label>("status").text,
                Is.EqualTo($"Discarded unsaved changes: {name} is back to its saved state."));
            Assert.That(restore.enabledSelf, Is.EqualTo(File.Exists(m_backupPath)),
                "With nothing left to discard, Restore waits for a backup.");
            Assert.That(ReadBytes(m_sourcePath), Is.EqualTo(m_sourceBytes), "The level JSON is byte-identical.");
            Assert.That(m_writes, Is.Empty);
        }

        // AC-3: Cancel keeps the edit and its undo step.
        [UnityTest]
        public IEnumerator CancelKeepsTheDraftAndItsUndo()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            // A fixture database load guarantees a loaded level, whatever the real project's active database
            // holds (Tests-Editor-1-F3).
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            Button restore = root.Q<Button>(k_restoreButtonName);
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int original = timer.value;
            int edited = original == 60 ? 61 : 60;
            string saved = JsonUtility.ToJson(GetDraft().Snapshot());
            Undo.IncrementCurrentGroup();
            timer.value = edited;
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("Set Level Timer"));
            LevelDraft editedDraft = GetDraft();
            string editedJson = JsonUtility.ToJson(editedDraft.Snapshot());
            string status = root.Q<Label>("status").text;
            m_discardAnswer = false;

            Press(restore);

            Assert.That(m_questions, Has.Count.EqualTo(1), "Restore asked before discarding.");
            Assert.That(GetDraft(), Is.SameAs(editedDraft), "The draft is not rebuilt.");
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(editedJson));
            Assert.That(timer.value, Is.EqualTo(edited));
            Assert.That(m_window.hasUnsavedChanges, Is.True);
            Assert.That(restore.enabledSelf, Is.True);
            Assert.That(root.Q<Label>("status").text, Is.EqualTo(status));
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("Set Level Timer"), "The undo step is still there.");

            Undo.PerformUndo();

            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(saved), "Undo still reverts the edit.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        // AC-4: without unsaved changes Restore brings back the backup through the unchanged Restore Previous Save.
        [Test]
        public void WithoutUnsavedChangesRestoreBringsBackThePreviousSave()
        {
            VisualElement root = OpenWindow();
            // A fixture database load guarantees a loaded level, whatever the real project's active database
            // holds (Tests-Editor-1-F3); UseStandInDatabase then swaps in its own in-memory stand-in from it.
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            LevelData[] backup = UseStandInDatabase(true);
            Button restore = root.Q<Button>(k_restoreButtonName);
            Assume.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(restore.enabledSelf, Is.True, "A backup enables Restore.");
            m_discardAnswer = true;

            Press(restore);

            Assert.That(m_questions, Is.Empty, "Nothing to discard, so no discard question.");
            Assert.That(m_writes, Has.Count.EqualTo(1), "Restore Previous Save wrote the backup once.");
            Assert.That(Describe(m_writes[0]), Is.EqualTo(Describe(backup)), "The written levels are the backup's.");
            Assert.That(root.Q<Label>("status").text, Does.StartWith("Restored the previous saved level JSON."));
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open(CreateWindow());
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(CreateWindow());
            m_window = m_fixture.Window;
        }

        // Every seam is a fake: nothing opens a dialog, a Piece Editor or writes the level JSON or the piece library.
        private LevelDatabaseWindow CreateWindow()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.IsPieceEditorOpen = () => false;
            window.ShowLevelPieceInEditor = _ => { };
            window.ShowLibraryPieceInEditor = _ => { };
            window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            window.ConfirmDiscardChanges = message =>
            {
                m_questions.Add(message);
                return m_discardAnswer;
            };
            window.WriteLevels = levels =>
            {
                m_writes.Add(new List<LevelData>(levels));
                if (m_standInProjectScope == null || !LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(window, json);
                return true;
            };
            return window;
        }

        /// <summary>
        /// Puts the loaded levels under an unused GUID in the project database's place: a stand-in project scope
        /// installer names that GUID and the window's own catalog keeps the levels (DEC-192, DEC-194), so the backup the
        /// button checks is this test's own: none, or a copy of the levels with the first one renamed. Returns that
        /// backup's levels, or null.
        /// </summary>
        private LevelData[] UseStandInDatabase(bool withBackup)
        {
            IReadOnlyList<LevelData> loaded = LevelEditorTestAccess.GetLoadedSource(m_window).Levels;
            string guid = System.Guid.NewGuid().ToString("N");
            m_standInProjectScope = LevelEditorTestAccess.CreateProjectScope(guid);
            string backupFolder = LevelDatabaseWindow.GetBackupFolder(guid);
            Assert.That(Directory.Exists(backupFolder), Is.False, "The stand-in GUID has no backup yet.");

            LevelData[] backup = null;
            if (withBackup)
            {
                backup = new List<LevelData>(loaded).ToArray();
                // Only the name differs, so the test tells the backup's levels from the loaded ones.
                backup[0] = backup[0].Clone();
                JsonUtility.FromJsonOverwrite("{\"m_name\":\"Restore backup fixture\"}", backup[0]);
                Assert.That(LevelDatabaseJson.TryWrite(backup, out string backupJson, out string error), Is.True, error);
                // A backup folder as a save leaves it: the whole database, manifest and packs (DEC-196).
                LevelDatabaseSnapshot files = LevelEditorTestAccess.CreateUnsavedSource(backupJson);
                Directory.CreateDirectory(backupFolder);
                m_standInBackupPath = backupFolder;
                File.WriteAllText(Path.Combine(backupFolder, LevelJsonFiles.ManifestFile), files.ManifestJson);
                foreach (LevelPackFile pack in files.Packs)
                {
                    File.WriteAllText(Path.Combine(backupFolder, Path.GetFileName(pack.Path)), pack.Json);
                }
            }

            LevelEditorTestAccess.Set(m_window, "m_projectScope", m_standInProjectScope);
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", guid);
            Invoke("OnDraftChanged");
            return backup;
        }

        private static string Describe(IReadOnlyList<LevelData> levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            return json;
        }

        private string GetSelectedLevelName()
        {
            return LevelEditorTestAccess.GetLevels(m_window)
                .GetAt(LevelEditorTestAccess.Get<int>(m_window, "m_selectedIndex")).Name;
        }

        private LevelDraft GetDraft()
        {
            return LevelEditorTestAccess.Get<LevelDraft>(m_window, "m_draft");
        }

        private void Invoke(string method)
        {
            LevelEditorTestAccess.Invoke(m_window, method, null);
        }

        // Submit presses the button the way a click does; a disabled button ignores it.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        // A 4 x 4 level with one block, never the real project's active database (Tests-Editor-1-F3). Only
        // UnsavedChangesEnableRestoreWithoutABackup needs a guaranteed load before its own UseStandInDatabase swap;
        // every other test here already builds its content from UseStandInDatabase or the real file this class
        // guards in TearDown.
        private static LevelData CreateDefaultLevel()
        {
            return new LevelData("fixture", "Restore fixture", 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }

        private static byte[] ReadBytes(string path)
        {
            return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllBytes(path) : null;
        }
    }
}
