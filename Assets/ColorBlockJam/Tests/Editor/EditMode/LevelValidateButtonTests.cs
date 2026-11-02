using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's Validate Level button and Save Level's solvability warning (DEC-144): the button's
    /// enable rules and tooltips, a check that changes nothing, the warning's Cancel and Save Anyway paths through Save
    /// Level, Test Level and save-on-close, reuse of an unchanged result, and the progress bar's delay and Cancel. Every
    /// write, dialog, progress bar and Play Mode start goes to a fake.
    /// </summary>
    public sealed class LevelValidateButtonTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string k_guid = "validate-level-fixture-json";

        private LevelDatabaseWindow m_window;
        private ProjectScopeInstaller m_projectScope;
        private LevelData m_publishedBefore;
        private readonly List<string> m_calls = new List<string>();
        private readonly List<string> m_titles = new List<string>();
        private readonly List<string> m_messages = new List<string>();
        private readonly List<string> m_progress = new List<string>();
        private bool m_saveAnyway;
        private bool m_cancelProgress;
        private int m_clearedProgress;

        [SetUp]
        public void ClearRecords()
        {
            Assume.That(LevelTestPlay.IsActive, Is.False, "A test play of this Editor session is still running.");
            m_publishedBefore = LevelDatabaseWindow.LastPublishedLevel;
            // NUnit shares one fixture instance between the tests.
            m_calls.Clear();
            m_titles.Clear();
            m_messages.Clear();
            m_progress.Clear();
            m_saveAnyway = false;
            m_cancelProgress = false;
            m_clearedProgress = 0;
        }

        [TearDown]
        public void CloseWindow()
        {
            if (m_window != null)
            {
                m_window.DiscardChanges();
                m_window.Close();
                m_window = null;
                RestorePublishedLevel(m_publishedBefore);
            }

            LevelEditorTestAccess.DestroyProjectScope(m_projectScope);
            m_projectScope = null;
        }

        [Test]
        public void ValidateLevelIsOnWhereSaveLevelIsOnAndOffInPlayMode()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());
            Button save = root.Q<Button>("save-level");
            Button validate = root.Q<Button>("validate-level");

            Assert.That(validate.text, Is.EqualTo("Validate Level"));
            Assert.That(validate.enabledSelf, Is.True);
            Assert.That(validate.tooltip, Is.EqualTo("Check whether this level can be solved without boosters."));

            // The issues list reports an error, as it does for overlapping blocks.
            LevelEditorTestAccess.Set(m_window, "m_draftHasErrors", true);
            LevelEditorTestAccess.Invoke(m_window, "UpdateSaveButton");
            Assert.That(save.enabledSelf, Is.False);
            Assert.That(validate.enabledSelf, Is.False);
            Assert.That(validate.tooltip, Is.EqualTo(LevelDatabaseWindow.k_validateBlockedTooltip));

            LevelEditorTestAccess.Set(m_window, "m_draftHasErrors", false);
            m_window.ValidationFlow.IsPlaying = () => true;
            LevelEditorTestAccess.Invoke(m_window, "UpdateSaveButton");
            Assert.That(validate.enabledSelf, Is.False, "The check refuses to start in Play Mode.");
            Assert.That(validate.tooltip, Is.EqualTo(LevelDatabaseWindow.k_validatePlayingTooltip));

            LevelEditorTestAccess.Invoke(m_window, "ValidateLevel");
            Assert.That(Status(root), Is.EqualTo("Not checked: the Editor is in Play Mode."));
            Assert.That(m_window.ValidationFlow.RunCount, Is.Zero, "Play Mode runs no search.");

            m_window.ValidationFlow.IsPlaying = () => false;
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", -1);
            LevelEditorTestAccess.Invoke(m_window, "UpdateSaveButton");
            Assert.That(validate.enabledSelf, Is.False, "No level is open.");
        }

        [Test]
        public void ValidateLevelShowsTheResultAndChangesNothing()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());
            string draftBefore = JsonUtility.ToJson(m_window.GetDraftLevel());
            string fileBefore = DatabaseJson();

            Click(root.Q<Button>("validate-level"));

            Assert.That(Status(root), Is.EqualTo("Solvable in 1 move."));
            Assert.That(JsonUtility.ToJson(m_window.GetDraftLevel()), Is.EqualTo(draftBefore));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
            Assert.That(m_calls, Is.Empty, "Validate Level never saves.");
            Assert.That(m_titles, Is.Empty);
            Assert.That(m_clearedProgress, Is.EqualTo(1), "The progress bar is always cleared.");
            Assert.That(m_progress, Is.Empty, "A check shorter than 0.25 s shows no progress bar.");
        }

        [Test]
        public void ValidateLevelNamesWhyALevelCannotBeSolved()
        {
            VisualElement root = OpenWindow();
            UseDatabase(UnsolvableLevel());

            Click(root.Q<Button>("validate-level"));

            Assert.That(Status(root), Does.StartWith("Cannot be solved: ").And.Contain("Red"));
        }

        [Test]
        public void CancelOrKeepEditingWritesNothingAndKeepsTheEdit()
        {
            VisualElement root = OpenWindow();
            UseDatabase(UnsolvableLevel());
            string fileBefore = DatabaseJson();
            EditTimer(root);

            Click(root.Q<Button>("save-level"));

            Assert.That(m_titles, Is.EqualTo(new[] { "Level Cannot Be Solved" }));
            Assert.That(m_messages[0], Does.StartWith("Cannot be solved: "));
            Assert.That(m_calls, Is.Empty, "Nothing is written.");
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
            Assert.That(m_window.hasUnsavedChanges, Is.True, "The edit stays.");
            Assert.That(Status(root), Does.StartWith("Save cancelled: Cannot be solved: "));

            Click(root.Q<Button>("test-level"));

            Assert.That(m_titles.Count, Is.EqualTo(2), "Test Level saves first, so it asks too.");
            Assert.That(m_calls, Is.Empty, "A declined save starts no test play.");

            m_window.SaveChanges();

            Assert.That(m_titles.Count, Is.EqualTo(3), "Save-on-close asks too.");
            Assert.That(m_window.hasUnsavedChanges, Is.True, "A declined save keeps the window's changes.");
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
        }

        [Test]
        public void SaveAnywaySavesAsBefore()
        {
            VisualElement root = OpenWindow();
            UseDatabase(UnsolvableLevel());
            EditTimer(root);
            m_saveAnyway = true;

            Click(root.Q<Button>("save-level"));

            Assert.That(m_titles, Is.EqualTo(new[] { "Level Cannot Be Solved" }));
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(LevelEditorTestAccess.GetLevels(m_window).GetAt(0).TimerSeconds, Is.EqualTo(75));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void ASolvableLevelSavesWithoutAQuestion()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());
            EditTimer(root);

            Click(root.Q<Button>("save-level"));

            Assert.That(m_titles, Is.Empty);
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(LevelEditorTestAccess.GetLevels(m_window).GetAt(0).TimerSeconds, Is.EqualTo(75));
        }

        [Test]
        public void ALevelTheCheckCannotConfirmAsksWithItsOwnTitle()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());
            // The block needs a slide before it can leave, so one position is not enough.
            m_window.ValidationFlow.Options = new LevelCheckOptions { MaxPositions = 1 };
            EditTimer(root);

            Click(root.Q<Button>("save-level"));

            Assert.That(m_titles, Is.EqualTo(new[] { "Level Not Confirmed" }));
            Assert.That(m_messages[0], Does.StartWith("Not confirmed: the check stopped after "));
            Assert.That(m_calls, Is.Empty);
        }

        [Test]
        public void ANotCheckedLevelSavesAndKeepsTheReasonAfterTheSaveMessage()
        {
            VisualElement root = OpenWindow();
            UseDatabase(UnsolvableLevel());
            EditTimer(root);
            m_window.ValidationFlow.IsPlaying = () => true;

            Click(root.Q<Button>("save-level"));

            Assert.That(m_titles, Is.Empty, "Not checked never asks.");
            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(Status(root), Does.EndWith("Not checked: the Editor is in Play Mode."));
        }

        [Test]
        public void SaveReusesAnUnchangedResultAndChecksAnEditedDraftAgain()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());

            Click(root.Q<Button>("validate-level"));
            Assume.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1));
            Click(root.Q<Button>("save-level"));

            Assert.That(m_calls, Is.EqualTo(new[] { "write" }));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(1), "No change since Validate Level: no new search.");

            Click(root.Q<Button>("validate-level"));
            int runs = m_window.ValidationFlow.RunCount;
            EditTimer(root);
            Click(root.Q<Button>("save-level"));

            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(runs + 1), "An edit makes the kept result stale.");
        }

        [Test]
        public void CancelOnTheProgressBarCancelsTheSave()
        {
            VisualElement root = OpenWindow();
            UseDatabase(SolvableLevel());
            string fileBefore = DatabaseJson();
            EditTimer(root);
            // The bar appears at the search's first report, and its Cancel is pressed.
            m_window.ValidationFlow.ProgressDelaySeconds = 0;
            m_cancelProgress = true;

            Click(root.Q<Button>("save-level"));

            Assert.That(m_progress, Has.Count.EqualTo(1));
            Assert.That(m_progress[0], Does.Contain("positions"));
            Assert.That(m_clearedProgress, Is.EqualTo(1), "The progress bar is always cleared.");
            Assert.That(m_titles, Is.Empty);
            Assert.That(m_calls, Is.Empty, "A cancelled check cancels the save.");
            Assert.That(DatabaseJson(), Is.EqualTo(fileBefore));
            Assert.That(m_window.hasUnsavedChanges, Is.True);
            Assert.That(Status(root), Is.EqualTo("Save cancelled: Not checked: the check was cancelled."));

            Click(root.Q<Button>("validate-level"));
            Assert.That(Status(root), Is.EqualTo("Not checked: the check was cancelled."));
            Assert.That(m_window.ValidationFlow.RunCount, Is.EqualTo(2), "A cancelled result is never reused.");
        }

        // One Red block one cell from the left edge's Red gate: a slide and an exit, one player move.
        private static LevelData SolvableLevel()
        {
            return new LevelData("v1", "First", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Left, 1, 1, "Red") },
                new[] { new LevelBlockData("b1", "Red", new Vector2Int(1, 1), new[] { Vector2Int.zero }) });
        }

        // A Red block on a board with no gate (PRE-1).
        private static LevelData UnsolvableLevel()
        {
            return new LevelData("v1", "First", 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }

        private static void EditTimer(VisualElement root)
        {
            root.Q<IntegerField>("timer-seconds").value = 75;
            Assume.That(root.Q<Button>("save-level").enabledSelf, Is.True);
        }

        private static string Status(VisualElement root)
        {
            return root.Q<Label>("status").text;
        }

        // The fixture database's levels as the level JSON they would be written as; equal text means an unchanged file.
        private string DatabaseJson()
        {
            Assert.That(LevelDatabaseJson.TryWrite(LevelSaveController.CopyLevels(LevelEditorTestAccess.GetLevels(m_window)), out string json,
                out string error), Is.True, error);
            return json;
        }

        // Every seam is a fake. ChecksSolvabilityOnSave stands in for Open, the designer entry point that turns on
        // Save Level's solvability check; a window a test creates otherwise saves without it.
        private VisualElement OpenWindow()
        {
            m_window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            m_window.ChecksSolvabilityOnSave = true;
            m_window.ShowEffectEditor = _ => { };
            m_window.IsPieceEditorOpen = () => false;
            m_window.ShowLevelPieceInEditor = _ => { };
            m_window.ShowLibraryPieceInEditor = _ => { };
            m_window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            m_window.WriteLevels = levels =>
            {
                m_calls.Add("write");
                if (!LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(m_window, json);
                return true;
            };
            m_window.StartTestPlay = request =>
            {
                m_calls.Add("start");
                return null;
            };
            m_window.ConfirmUnsolvableSave = (title, message) =>
            {
                m_titles.Add(title);
                m_messages.Add(message);
                return m_saveAnyway;
            };
            m_window.ValidationFlow.IsPlaying = () => false;
            m_window.ValidationFlow.ShowProgress = (info, fraction) =>
            {
                m_progress.Add(info);
                return m_cancelProgress;
            };
            m_window.ValidationFlow.ClearProgress = () => m_clearedProgress++;
            m_window.ShowUtility();
            m_window.position = new Rect(40f, 40f, 960f, 640f);
            // Tests call CreateGUI directly, as the other editor window tests do.
            LevelEditorTestAccess.Invoke(m_window, "CreateGUI");
            return m_window.rootVisualElement;
        }

        // An in-memory level JSON under a fixture GUID stands in for the project's, with its first level open as saved.
        private void UseDatabase(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            // The stand-in installer names the fixture JSON; the window's own catalog holds its levels (DEC-192, DEC-194).
            m_projectScope = LevelEditorTestAccess.CreateProjectScope(k_guid);
            LevelEditorTestAccess.Set(m_window, "m_projectScope", m_projectScope);
            LevelEditorTestAccess.LoadLevels(m_window, json);
            LevelEditorTestAccess.Set(m_window, "m_loadedGuid", k_guid);
            LevelEditorTestAccess.Set(m_window, "m_sourceValid", true);
            LevelEditorTestAccess.SetLoadedSource(m_window, json);
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", 0);
            LevelDraft draft = LevelEditorTestAccess.Get<LevelDraft>(m_window, "m_draft");
            draft.Replace(levels[0]);
            LevelEditorTestAccess.Set(m_window, "m_savedDraftJson", JsonUtility.ToJson(draft.Snapshot()));
            LevelEditorTestAccess.Invoke(m_window, "OnDraftChanged");
            LevelEditorTestAccess.Invoke(m_window, "PublishDraft");
            Assume.That(m_window.hasUnsavedChanges, Is.False, "The fixture opens as saved.");
        }

        private static void Click(Button button)
        {
            // Runs the registered click action without relying on OS pointer input.
            typeof(Clickable).GetMethod("SimulateSingleClick", k_members)
                .Invoke(button.clickable, new object[] { null, 0 });
        }

        // Puts back the snapshot a test window replaced, so an open LevelEditor scene preview shows the user's level again.
        private static void RestorePublishedLevel(LevelData previous)
        {
            typeof(LevelDatabaseWindow).GetProperty(nameof(LevelDatabaseWindow.LastPublishedLevel)).SetValue(null, previous);
            System.Action<LevelData> listeners = (System.Action<LevelData>)typeof(LevelDatabaseWindow)
                .GetField(nameof(LevelDatabaseWindow.DraftLevelPublished), BindingFlags.Static | BindingFlags.NonPublic)
                .GetValue(null);
            listeners?.Invoke(previous);
        }
    }
}
