using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's Test Level (PRD 11.5, DEC-127) without entering Play Mode: the session request and
    /// its lookup, the start and end of a test play through replaced Editor steps, the Save Level and Test Level
    /// buttons, whose writes and Play Mode start go to fakes, and the button's Stop Test while Play Mode runs, whose
    /// Play Mode end and state go to fakes too.
    /// </summary>
    public sealed class LevelTestPlayTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string k_guid = "test-play-fixture-json";
        private const string k_initScenePath = "Assets/ColorBlockJam/Scenes/Init.unity";

        private LevelDatabaseWindow m_window;
        private ProjectScopeInstaller m_projectScope;
        private LevelData m_publishedBefore;
        private readonly List<string> m_calls = new List<string>();
        private readonly List<LevelTestPlayRequest> m_requests = new List<LevelTestPlayRequest>();
        private bool m_writeFails;

        [SetUp]
        public void RequireNoTestPlay()
        {
            Assume.That(LevelTestPlay.IsActive, Is.False, "A test play of this Editor session is still running.");
            m_publishedBefore = LevelDatabaseWindow.LastPublishedLevel;
            // NUnit shares one fixture instance between the tests.
            m_calls.Clear();
            m_requests.Clear();
            m_writeFails = false;
        }

        [TearDown]
        public void CloseWindowAndClearTheRequest()
        {
            // Clears the session keys without touching the Editor's real Play Mode start scene.
            LevelTestPlay.Finish(new LevelTestPlay.Steps { LoadScene = _ => null, SetStartScene = _ => { } });
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
        public void RequestIsReadFromSessionStorageUntilCleared()
        {
            Assert.That(LevelTestPlayRequest.TryRead(out _), Is.False, "No Test Level request is pending.");

            new LevelTestPlayRequest(k_guid, "t2").Write();

            Assert.That(LevelTestPlayRequest.TryRead(out LevelTestPlayRequest read), Is.True);
            Assert.That(read.DatabaseGuid, Is.EqualTo(k_guid));
            Assert.That(read.LevelId, Is.EqualTo("t2"));

            LevelTestPlayRequest.Clear();

            Assert.That(LevelTestPlayRequest.TryRead(out read), Is.False);
            Assert.That(read, Is.Null);
        }

        [Test]
        public void RequestNeedsAJsonAndALevel()
        {
            Assert.That(() => new LevelTestPlayRequest(" ", "t1"), Throws.ArgumentException);
            Assert.That(() => new LevelTestPlayRequest(k_guid, ""), Throws.ArgumentException);
        }

        [Test]
        public void RequestFindsItsLevelByIdInTheLoadedJson()
        {
            LevelCatalog levels = CreateCatalog(Level("t1", "First"), Level("t2", "Second"));

            Assert.That(new LevelTestPlayRequest(k_guid, "t2").TryFindIndex(levels, out int index, out string error),
                Is.True, error);

            Assert.That(index, Is.EqualTo(1), "The stable ID chooses the level, not its position.");
            Assert.That(levels.GetAt(index).Name, Is.EqualTo("Second"));
        }

        [Test]
        public void RequestFindsNothingInAnotherJsonOrForAMissingLevel()
        {
            LevelCatalog levels = CreateCatalog(Level("t1", "First"));

            Assert.That(new LevelTestPlayRequest("another-json", "t1").TryFindIndex(levels, out int index,
                out string error), Is.False);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(error, Does.Contain("another-json").And.Contain(k_guid));

            Assert.That(new LevelTestPlayRequest(k_guid, "t9").TryFindIndex(levels, out index, out error), Is.False);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(error, Is.EqualTo("level t9 is not in the loaded level database."));
        }

        [Test]
        public void StartPointsPlayModeAtTheGameSceneAndFinishPutsTheOldStartSceneBack()
        {
            SceneAsset game = LoadScene(LevelTestPlay.k_gameScenePath);
            SceneAsset init = LoadScene(k_initScenePath);
            SceneAsset startScene = init;
            int entered = 0;
            LevelTestPlay.Steps steps = CreateSteps();
            steps.GetStartScene = () => startScene;
            steps.SetStartScene = scene => startScene = scene;
            steps.EnterPlayMode = () => entered++;

            Assert.That(LevelTestPlay.Start(new LevelTestPlayRequest(k_guid, "t2"), steps), Is.Null);

            Assert.That(LevelTestPlay.IsActive, Is.True);
            Assert.That(startScene, Is.SameAs(game), "The Game scene runs instead of the open scenes.");
            Assert.That(entered, Is.EqualTo(1));
            Assert.That(LevelTestPlayRequest.TryRead(out LevelTestPlayRequest read), Is.True);
            Assert.That(read.LevelId, Is.EqualTo("t2"));

            LevelTestPlay.Finish(steps);

            Assert.That(startScene, Is.SameAs(init), "A start scene chosen elsewhere comes back.");
            Assert.That(LevelTestPlay.IsActive, Is.False);
            Assert.That(LevelTestPlayRequest.TryRead(out _), Is.False, "Leaving Play Mode clears the request (RISK-12).");
        }

        [Test]
        public void FinishWithoutAnEarlierStartSceneLeavesNone()
        {
            SceneAsset startScene = null;
            LevelTestPlay.Steps steps = CreateSteps();
            steps.GetStartScene = () => startScene;
            steps.SetStartScene = scene => startScene = scene;

            Assert.That(LevelTestPlay.Start(new LevelTestPlayRequest(k_guid, "t1"), steps), Is.Null);
            Assume.That(startScene, Is.Not.Null);
            LevelTestPlay.Finish(steps);

            Assert.That(startScene, Is.Null, "Play keeps running the open scenes again.");
        }

        [TestCase("playing", "Stop Play Mode before testing a level.")]
        [TestCase("compiling", "Scripts are compiling. Press Test Level again when they finish.")]
        [TestCase("compile errors", "Fix the script compile errors in the Console before testing a level.")]
        [TestCase("no Game scene", "Test Level needs the Game scene at Assets/ColorBlockJam/Scenes/Game.unity.")]
        public void StartRefusesAndChangesNothing(string state, string expected)
        {
            bool changed = false;
            LevelTestPlay.Steps steps = CreateSteps();
            steps.IsPlaying = () => state == "playing";
            steps.IsCompiling = () => state == "compiling";
            steps.HasCompileErrors = () => state == "compile errors";
            if (state == "no Game scene")
            {
                steps.LoadScene = _ => null;
            }

            steps.SetStartScene = _ => changed = true;
            steps.EnterPlayMode = () => changed = true;

            Assert.That(LevelTestPlay.Start(new LevelTestPlayRequest(k_guid, "t1"), steps), Is.EqualTo(expected));

            Assert.That(changed, Is.False);
            Assert.That(LevelTestPlay.IsActive, Is.False);
            Assert.That(LevelTestPlayRequest.TryRead(out _), Is.False);
        }

        [Test]
        public void SaveLevelAndTestLevelAreLargerButtonsSideBySideAtTheBottomLeft()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));

            // The buttons moved from the bottom bar to the validation panel's last row (TASK-20260928-091128541-cb48c84a).
            VisualElement bar = root.Q("validation-panel").Q("validation-buttons");
            Button save = bar.Q<Button>("save-level");
            Button validate = bar.Q<Button>("validate-level");
            Button test = bar.Q<Button>("test-level");
            Assert.That(bar.IndexOf(save), Is.Zero, "Save Level comes first, at the left.");
            Assert.That(bar.IndexOf(validate), Is.EqualTo(1), "Validate Level sits beside it (DEC-144).");
            Assert.That(bar.IndexOf(test), Is.EqualTo(2), "Test Level follows Validate Level.");
            Assert.That(root.Q("bottom-bar").IndexOf(root.Q<Label>("status")), Is.Zero,
                "The status line stays in the bottom bar.");
            Assert.That(test.text, Is.EqualTo("Test Level"));
            foreach (Button button in new[] { save, validate, test })
            {
                Assert.That(button.style.height.value.value, Is.EqualTo(LevelDatabaseWindow.k_bottomButtonHeight));
                Assert.That(button.style.minWidth.value.value, Is.GreaterThanOrEqualTo(120f));
                Assert.That(button.style.fontSize.value.value, Is.EqualTo(13f));
            }

            ValidateLayout(root);
            Assert.That(save.layout.height, Is.EqualTo(LevelDatabaseWindow.k_bottomButtonHeight).Within(0.5f));
            Assert.That(test.layout.height, Is.EqualTo(save.layout.height).Within(0.5f));
            Assert.That(test.layout.width, Is.EqualTo(save.layout.width).Within(0.5f), "Both buttons share one size.");
            Assert.That(validate.layout.height, Is.EqualTo(save.layout.height).Within(0.5f));
            Assert.That(save.enabledSelf, Is.True);
            Assert.That(test.enabledSelf, Is.True);
        }

        [Test]
        public void TestLevelIsOffWhereSaveLevelIsOff()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            Button save = root.Q<Button>("save-level");
            Button test = root.Q<Button>("test-level");

            // The issues list reports an error, as it does for blocks a re-stamp left overlapping.
            LevelEditorTestAccess.Set(m_window, "m_draftHasErrors", true);
            LevelEditorTestAccess.Invoke(m_window, "UpdateSaveButton");

            Assert.That(save.enabledSelf, Is.False);
            Assert.That(test.enabledSelf, Is.False);
            Assert.That(test.tooltip, Is.EqualTo(LevelDatabaseWindow.k_testBlockedTooltip));

            LevelEditorTestAccess.Set(m_window, "m_draftHasErrors", false);
            LevelEditorTestAccess.Set(m_window, "m_selectedIndex", -1);
            LevelEditorTestAccess.Invoke(m_window, "UpdateSaveButton");

            Assert.That(test.enabledSelf, Is.False, "No level is open.");
        }

        [Test]
        public void TestLevelPlaysTheSavedLevelWithoutWritingAndComesBackToIt()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            // A queued publish would bring the preview back before Play Mode.
            LevelEditorTestAccess.Invoke(m_window, "SchedulePublish");

            Click(root.Q<Button>("test-level"));

            Assert.That(m_calls, Is.EqualTo(new[] { "start" }), "No Save Level write: nothing changed.");
            Assert.That(m_requests[0].DatabaseGuid, Is.EqualTo(k_guid));
            Assert.That(m_requests[0].LevelId, Is.EqualTo("t1"));
            Assert.That(root.Q<Label>("status").text, Does.StartWith("Testing First in the Game scene."));
            Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.Null, "The scene preview leaves before Play Mode.");
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.False);

            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.EnteredEditMode);

            Assert.That(root.Q<Label>("status").text, Does.StartWith("Back from testing First."));
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_testedLevelName"), Is.Null.Or.Empty);
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.True, "The preview comes back.");
            LevelEditorTestAccess.Invoke(m_window, "PublishDraft");
            Assert.That(LevelDatabaseWindow.LastPublishedLevel.Id, Is.EqualTo("t1"), "The same level stays open.");
        }

        [Test]
        public void TestLevelSavesUnsavedChangesFirst()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            root.Q<IntegerField>("timer-seconds").value = 75;
            Assume.That(m_window.hasUnsavedChanges, Is.True);

            Click(root.Q<Button>("test-level"));

            Assert.That(m_calls, Is.EqualTo(new[] { "write", "start" }));
            Assert.That(LevelEditorTestAccess.GetLevels(m_window).GetAt(0).TimerSeconds, Is.EqualTo(75),
                "The Game scene reads the saved JSON.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void TestLevelStartsNothingWhenSaveLevelFails()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            root.Q<IntegerField>("timer-seconds").value = 75;
            m_writeFails = true;

            Click(root.Q<Button>("test-level"));

            Assert.That(m_calls, Is.EqualTo(new[] { "write" }), "A level that is not saved is not tested.");
            Assert.That(m_window.hasUnsavedChanges, Is.True, "The edit stays.");
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_testedLevelName"), Is.Null.Or.Empty);
        }

        [Test]
        public void WhilePlayModeRunsTestLevelReadsStopTestAndEndsIt()
        {
            // Contract Revision 1 of TASK-20260928-091128541-cb48c84a; the Play Mode state comes from the window's seams.
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            Button test = root.Q<Button>("test-level");
            bool inPlayMode = false;
            bool playingOrChanging = false;
            m_window.IsInPlayMode = () => inPlayMode;
            m_window.ValidationFlow.IsPlaying = () => playingOrChanging;
            m_window.StopTestPlay = () => m_calls.Add("stop");

            Click(test);
            Assume.That(m_calls, Is.EqualTo(new[] { "start" }));
            Assert.That(test.text, Is.EqualTo("Test Level"));

            // Entering Play Mode: the button waits with the Play Mode tooltip.
            playingOrChanging = true;
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.ExitingEditMode);
            Assert.That(test.enabledSelf, Is.False, "It waits while Play Mode starts.");
            Assert.That(test.tooltip, Is.EqualTo(LevelDatabaseWindow.k_testPlayingTooltip));

            // Play Mode runs: Stop Test, enabled.
            inPlayMode = true;
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.EnteredPlayMode);
            Assert.That(test.text, Is.EqualTo("Stop Test"));
            Assert.That(test.enabledSelf, Is.True);
            Assert.That(test.tooltip, Is.EqualTo(LevelDatabaseWindow.k_stopTestTooltip));

            Click(test);

            Assert.That(m_calls, Is.EqualTo(new[] { "start", "stop" }),
                "Stop Test ends Play Mode through its step; it saves and starts nothing.");

            // Leaving Play Mode: the button waits again.
            playingOrChanging = false;
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.ExitingPlayMode);
            Assert.That(test.enabledSelf, Is.False, "It waits while Play Mode stops.");
            Assert.That(test.tooltip, Is.EqualTo(LevelDatabaseWindow.k_testPlayingTooltip));

            // Back in Edit Mode: Test Level again, and DEC-127's return to the tested level runs as before.
            inPlayMode = false;
            LevelEditorTestAccess.Invoke(m_window, "OnPlayModeStateChanged", PlayModeStateChange.EnteredEditMode);
            Assert.That(test.text, Is.EqualTo("Test Level"));
            Assert.That(test.enabledSelf, Is.True);
            Assert.That(root.Q<Label>("status").text, Does.StartWith("Back from testing First."));
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_publishScheduled"), Is.True, "The preview comes back.");
        }

        [Test]
        public void TestLevelShowsWhyPlayModeDidNotStart()
        {
            VisualElement root = OpenWindow();
            UseDatabase(Level("t1", "First"));
            m_window.StartTestPlay = request =>
            {
                m_calls.Add("start");
                return "Stop Play Mode before testing a level.";
            };

            Click(root.Q<Button>("test-level"));

            Assert.That(root.Q<Label>("status").text, Is.EqualTo("Stop Play Mode before testing a level."));
            Assert.That(LevelEditorTestAccess.Get(m_window, "m_testedLevelName"), Is.Null.Or.Empty);
            Assert.That(LevelDatabaseWindow.LastPublishedLevel, Is.Not.Null, "The preview stays.");
        }

        private static LevelData Level(string id, string name)
        {
            return new LevelData(id, name, 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }

        // The fixture JSON's levels as the project scope's catalog holds them.
        private static LevelCatalog CreateCatalog(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            LevelCatalog catalog = new LevelCatalog(k_guid);
            Assert.That(catalog.LoadJson(json, out error), Is.True, error);
            return catalog;
        }

        private static SceneAsset LoadScene(string path)
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Assume.That(scene, Is.Not.Null, path);
            return scene;
        }

        // Real checks and scene loads; the start scene and Play Mode calls are replaced per test.
        private static LevelTestPlay.Steps CreateSteps()
        {
            return new LevelTestPlay.Steps
            {
                IsPlaying = () => false,
                IsCompiling = () => false,
                HasCompileErrors = () => false,
                GetStartScene = () => null,
                SetStartScene = _ => { },
                EnterPlayMode = () => { }
            };
        }

        // Every seam is a fake: nothing reaches the level JSON, the piece library or Play Mode.
        private VisualElement OpenWindow()
        {
            m_window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            m_window.IsPieceEditorOpen = () => false;
            m_window.ShowLevelPieceInEditor = _ => { };
            m_window.ShowLibraryPieceInEditor = _ => { };
            m_window.CommitTemplatesToLibrary = _ => "No template is committed in this test.";
            m_window.WriteLevels = levels =>
            {
                m_calls.Add("write");
                if (m_writeFails || !LevelDatabaseJson.TryWrite(levels, out string json, out _))
                {
                    return false;
                }

                LevelEditorTestAccess.LoadLevels(m_window, json);
                return true;
            };
            m_window.StartTestPlay = request =>
            {
                m_calls.Add("start");
                m_requests.Add(request);
                return null;
            };
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
            // The fixture JSON as the active one; the stand-in installer only names it (DEC-194).
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

        private static void ValidateLayout(VisualElement root)
        {
            object panel = root.panel;
            panel.GetType().GetMethod("ValidateLayout", k_members).Invoke(panel, null);
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
