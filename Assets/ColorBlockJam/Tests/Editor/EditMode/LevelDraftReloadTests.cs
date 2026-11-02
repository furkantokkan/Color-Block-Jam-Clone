using System.IO;
using System.Text.RegularExpressions;
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
    /// Verifies that the Level Editor keeps an unsaved draft through a script or domain reload, restores it only onto
    /// the same level of an unchanged JSON source, and never writes the level JSON
    /// (TASK-20260925-172236747-9ac18e21-level-editor-draft-survives-reload).
    /// </summary>
    public sealed class LevelDraftReloadTests
    {
        private const string k_unsavedStatus = "Unsaved level changes. Press Save Level to write the JSON source.";
        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;

        [TearDown]
        public void CloseWindow()
        {
            if (m_window == null)
            {
                return;
            }

            int windowId = m_window.GetInstanceID();
            m_fixture.Dispose();
            m_fixture = null;
            m_window = null;
            LevelDraft.TryTakeReloadCopy(windowId, out _);
        }

        [Test]
        public void UnsavedEditsSurviveAReloadWithoutWritingTheJson()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            string sourcePath = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            string sourceText = File.ReadAllText(sourcePath);
            LevelData saved = GetDraft().Snapshot();
            string editedName = saved.Name + " (reload)";
            int editedTimer = saved.TimerSeconds == 60 ? 61 : 60;
            root.Q<TextField>("level-name").value = editedName;
            root.Q<IntegerField>("timer-seconds").value = editedTimer;
            // The reported loss: freshly placed level-piece blocks before Save as template reloads the domain.
            AddLevelPieceBlock();
            string edited = JsonUtility.ToJson(GetDraft().Snapshot());
            Assume.That(m_window.hasUnsavedChanges, Is.True);

            Reload();

            VisualElement rebuilt = m_window.rootVisualElement;
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(edited), "The whole draft comes back.");
            Assert.That(rebuilt.Q<TextField>("level-name").value, Is.EqualTo(editedName));
            Assert.That(rebuilt.Q<IntegerField>("timer-seconds").value, Is.EqualTo(editedTimer));
            Assert.That(m_window.hasUnsavedChanges, Is.True, "The dirty marker stays.");
            Assert.That(rebuilt.Q<Label>("status").text, Is.EqualTo(k_unsavedStatus));
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(sourceText), "Only Save Level writes the JSON.");
            Assert.That(JsonUtility.ToJson(LevelEditorTestAccess.GetLevels(m_window).GetAt(0)),
                Is.EqualTo(JsonUtility.ToJson(saved)));

            LevelData previousPublished = LevelDatabaseWindow.LastPublishedLevel;
            try
            {
                RunScheduledPublishNow();
                Assert.That(JsonUtility.ToJson(LevelDatabaseWindow.LastPublishedLevel), Is.EqualTo(edited),
                    "The preview shows the restored draft.");
            }
            finally
            {
                RestorePublishedLevel(previousPublished);
            }
        }

        [Test]
        public void APendingTemplateFootprintSurvivesAReloadWithItsDraft()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            string sourcePath = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            string sourceText = File.ReadAllText(sourcePath);
            root.Q<IntegerField>("timer-seconds").value = GetDraft().Snapshot().TimerSeconds == 60 ? 61 : 60;
            // A Pen template edit's pending footprint (DEC-125) rides the draft; the template key needs no library
            // record.
            Vector2Int[] single = { Vector2Int.zero };
            Vector2Int[] domino = { Vector2Int.zero, Vector2Int.right };
            GetDraft().SetTemplateEdit(new LevelTemplateEdit(LevelTestShapes.Key(single), "Reload fixture piece",
                single, domino));
            string edited = JsonUtility.ToJson(GetDraft().Snapshot());

            Reload();

            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(edited));
            Assert.That(GetDraft().TemplateEdits, Has.Count.EqualTo(1), "The pending footprint comes back with it.");
            LevelTemplateEdit edit = GetDraft().TemplateEdits[0];
            Assert.That(edit.TemplateKey, Is.EqualTo(LevelTestShapes.Key(single)));
            Assert.That(edit.DisplayName, Is.EqualTo("Reload fixture piece"));
            Assert.That(edit.BaseCells, Is.EqualTo(single));
            Assert.That(edit.Cells, Is.EqualTo(domino));
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(sourceText), "Only Save Level writes the JSON.");

            // A discarded draft keeps nothing, the pending footprint included.
            m_window.DiscardChanges();
            Reload();
            Assert.That(GetDraft().TemplateEdits, Is.Empty);
        }

        [Test]
        public void UndoWorksOnTheRestoredDraft()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            string sourcePath = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            string sourceText = File.ReadAllText(sourcePath);
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int restored = timer.value == 60 ? 61 : 60;
            timer.value = restored;

            Reload();

            timer = m_window.rootVisualElement.Q<IntegerField>("timer-seconds");
            Assert.That(timer.value, Is.EqualTo(restored));
            Undo.IncrementCurrentGroup();
            timer.value = restored == 90 ? 91 : 90;
            // Guards the global undo: the newest undo group must be this edit of the restored draft.
            Assert.That(Undo.GetCurrentGroupName(), Is.EqualTo("Set Level Timer"));
            Undo.PerformUndo();

            Assert.That(timer.value, Is.EqualTo(restored), "Undo returns to the restored draft.");
            Assert.That(m_window.hasUnsavedChanges, Is.True, "The restored draft is still unsaved.");
            Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(sourceText), "Only Save Level writes the JSON.");
        }

        [TestCase("database", "Another level database is active after the editor reload")]
        [TestCase("level", "Another level is selected after the editor reload")]
        [TestCase("source", "The level JSON changed on disk during the editor reload")]
        public void AChangedDatabaseLevelOrSourceDiscardsTheKeptDraft(string change, string reason)
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            LevelData saved = GetDraft().Snapshot();
            root.Q<IntegerField>("timer-seconds").value = saved.TimerSeconds == 60 ? 61 : 60;
            int windowId = m_window.GetInstanceID();

            Invoke("OnDisable");
            Assert.That(LevelDraft.TryTakeReloadCopy(windowId, out LevelDraft.ReloadCopy copy), Is.True);
            // Stands in for the change during the reload, without writing the level JSON or the database asset.
            LevelDraft.KeepForReload(windowId, new LevelDraft.ReloadCopy(
                change == "database" ? "another-database-guid" : copy.DatabaseGuid,
                copy.LevelIndex,
                change == "level" ? "another-level-id" : copy.LevelId,
                change == "source" ? "another-source-hash" : copy.SourceHash,
                copy.SavedJson,
                copy.DraftJson));
            LogAssert.Expect(LogType.Warning, new Regex(Regex.Escape(reason)));
            Invoke("OnEnable");
            Invoke("CreateGUI");

            VisualElement rebuilt = m_window.rootVisualElement;
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(JsonUtility.ToJson(saved)),
                "The saved level is loaded, as before.");
            Assert.That(rebuilt.Q<IntegerField>("timer-seconds").value, Is.EqualTo(saved.TimerSeconds));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            string status = rebuilt.Q<Label>("status").text;
            Assert.That(status, Does.StartWith(reason));
            Assert.That(status, Does.EndWith("were discarded."));
            Assert.That(LevelDraft.TryTakeReloadCopy(windowId, out _), Is.False, "The kept draft is used once.");
        }

        [Test]
        public void ADiscardedDraftIsNotKept()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            LevelData saved = GetDraft().Snapshot();
            root.Q<IntegerField>("timer-seconds").value = saved.TimerSeconds == 60 ? 61 : 60;
            // The close prompt's Discard clears the flag before Unity disables the window.
            m_window.DiscardChanges();

            Reload();

            Assert.That(m_window.rootVisualElement.Q<IntegerField>("timer-seconds").value, Is.EqualTo(saved.TimerSeconds));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(m_window.rootVisualElement.Q<Label>("status").text, Does.StartWith("Editing "));
        }

        [Test]
        public void ACleanWindowKeepsNothing()
        {
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");

            Invoke("OnDisable");

            Assert.That(LevelDraft.TryTakeReloadCopy(m_window.GetInstanceID(), out _), Is.False);
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open();
            m_window = m_fixture.Window;
            return root;
        }

        // A domain reload disables the window, enables it again and rebuilds its GUI.
        private void Reload()
        {
            Invoke("OnDisable");
            Invoke("OnEnable");
            Invoke("CreateGUI");
        }

        // Adds a well-formed level-piece block (an L of three cells) to the draft, as the "+" level-piece flow places one.
        private void AddLevelPieceBlock()
        {
            LevelDraft draft = GetDraft();
            LevelData level = draft.Snapshot();
            Vector2Int[] cells = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
            LevelBlockData[] existing = LevelRecordCopy.GetBlocks(level);
            LevelBlockData[] blocks = new LevelBlockData[existing.Length + 1];
            existing.CopyTo(blocks, 0);
            blocks[existing.Length] = new LevelBlockData("b-reload-test", "Red", Vector2Int.zero, cells);

            draft.Replace(LevelRecordCopy.WithBlocks(level, blocks));
            Invoke("OnDraftChanged");
        }

        private LevelDraft GetDraft()
        {
            return (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
        }

        // Runs the window's pending publish at once instead of on the next editor update; PublishDraft removes
        // its own scheduled call, so it does not run again.
        private void RunScheduledPublishNow()
        {
            LevelEditorTestAccess.Invoke(m_window, "PublishDraft", null);
        }

        // Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level again.
        private static void RestorePublishedLevel(LevelData previous)
        {
            LevelEditorWindowFixture.RestorePublishedLevel(previous);
        }

        private void Invoke(string method)
        {
            LevelEditorTestAccess.Invoke(m_window, method, null);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }
    }
}
