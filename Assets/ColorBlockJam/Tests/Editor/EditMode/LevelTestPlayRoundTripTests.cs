using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies Test Level's whole round trip in real Play Mode (PRD 11.5, DEC-127): from the LevelEditor scene the
    /// Game scene plays the chosen level by its ID, with no scene preview inside it, and Edit Mode comes back on the
    /// LevelEditor scene with the start scene restored, the request cleared and the preview shown again. Explicit,
    /// because it enters Play Mode: run it by name.
    /// </summary>
    public sealed class LevelTestPlayRoundTripTests
    {
        private const BindingFlags k_members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const string k_previewRootName = "Level Preview (not saved)";
        private const double k_attemptSeconds = 30.0;
        private const double k_publishSeconds = 10.0;

        [UnityTest, Explicit("Enters Play Mode in the Game scene; run it by name.")]
        public IEnumerator TestLevelPlaysTheLevelInTheGameSceneAndComesBackToTheLevelEditor()
        {
            Assume.That(LevelTestPlay.IsActive, Is.False, "A test play of this Editor session is still running.");
            SceneSetup[] originalScenes = EditorSceneManager.GetSceneManagerSetup();
            SceneAsset originalStartScene = EditorSceneManager.playModeStartScene;
            List<string> logs = new List<string>();
            Application.LogCallback capture = (message, stackTrace, type) => logs.Add(message);
            LevelDatabaseWindow window = null;
            // Owner: this test. Stop point: the finally block.
            Application.logMessageReceived += capture;
            try
            {
                EditorSceneManager.OpenScene(LevelEditorWorkspace.k_scenePath, OpenSceneMode.Single);
                window = OpenWindow();
                Assume.That(LevelEditorTestAccess.Get<bool>(window, "m_sourceValid"), Is.True,
                    "The project has an active level JSON.");
                LevelCatalog levels = LevelEditorTestAccess.GetLevels(window);
                // The second level, when there is one, so the Game scene's own index 0 cannot pass for it.
                int index = levels.Count > 1 ? 1 : 0;
                LevelEditorTestAccess.Set(window, "m_selectedIndex", index);
                LevelEditorTestAccess.Invoke(window, "LoadSelectedDraft");
                LevelData level = levels.GetAt(index);
                string expectedLog = $"Test play: {level.Name} ({level.Id}) from the Level Editor.";
                double deadline = EditorApplication.timeSinceStartup + k_publishSeconds;
                while (FindPreviewRoot() == null && EditorApplication.timeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(FindPreviewRoot(), Is.Not.Null, "The LevelEditor scene shows the level before the test.");

                Click(window.rootVisualElement.Q<Button>("test-level"));

                Assert.That(LevelTestPlay.IsActive, Is.True, window.rootVisualElement.Q<Label>("status").text);
                Assert.That(AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene),
                    Is.EqualTo(LevelTestPlay.k_gameScenePath));
                Assert.That(FindPreviewRoot(), Is.Null, "The scene preview leaves before Play Mode.");
                yield return new EnterPlayMode();

                deadline = EditorApplication.timeSinceStartup + k_attemptSeconds;
                while (!logs.Contains(expectedLog) && EditorApplication.timeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(logs, Has.Member(expectedLog), "The Game scene plays the tested level by its ID.");
                Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(LevelTestPlay.k_gameScenePath));
                Assert.That(IsLoaded(LevelEditorWorkspace.k_scenePath), Is.False, "The LevelEditor scene does not run.");
                Assert.That(FindPreviewRoot(), Is.Null, "No scene preview joins the Game scene.");

                yield return new ExitPlayMode();

                Assert.That(IsLoaded(LevelEditorWorkspace.k_scenePath), Is.True, "Unity reopens the LevelEditor scene.");
                Assert.That(EditorSceneManager.playModeStartScene, Is.SameAs(originalStartScene));
                Assert.That(LevelTestPlay.IsActive, Is.False);
                Assert.That(LevelTestPlayRequest.TryRead(out _), Is.False, "Leaving Play Mode clears the request.");
                Assert.That(window.rootVisualElement.Q<Label>("status").text,
                    Does.StartWith($"Back from testing {level.Name}."));
                Assert.That(LevelEditorTestAccess.Get(window, "m_selectedIndex"), Is.EqualTo(index),
                    "The same level stays open.");

                deadline = EditorApplication.timeSinceStartup + k_publishSeconds;
                while (FindPreviewRoot() == null && EditorApplication.timeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(FindPreviewRoot(), Is.Not.Null, "The scene preview shows the level again.");
            }
            finally
            {
                Application.logMessageReceived -= capture;
                if (LevelTestPlay.IsActive)
                {
                    LevelTestPlay.Finish(new LevelTestPlay.Steps());
                }

                if (window != null)
                {
                    window.DiscardChanges();
                    window.Close();
                }

                EditorSceneManager.playModeStartScene = originalStartScene;
                if (!EditorApplication.isPlaying && HasActiveLoadedScene(originalScenes))
                {
                    EditorSceneManager.RestoreSceneManagerSetup(originalScenes);
                }
            }
        }

        // A real Level Editor on the project's active level JSON. Nothing is saved; Play Mode is entered by the test.
        private static LevelDatabaseWindow OpenWindow()
        {
            LevelDatabaseWindow window = ScriptableObject.CreateInstance<LevelDatabaseWindow>();
            window.WriteLevels = _ =>
            {
                Assert.Fail("Test Level writes nothing when the level has no unsaved changes.");
                return false;
            };
            window.StartTestPlay = request =>
                LevelTestPlay.Start(request, new LevelTestPlay.Steps { EnterPlayMode = () => { } });
            window.ShowUtility();
            window.position = new Rect(40f, 40f, 960f, 640f);
            LevelEditorTestAccess.Invoke(window, "CreateGUI");
            return window;
        }

        private static GameObject FindPreviewRoot()
        {
            foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (candidate.name == k_previewRootName && candidate.transform.parent == null
                    && !EditorUtility.IsPersistent(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static bool IsLoaded(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            return scene.IsValid() && scene.isLoaded;
        }

        private static bool HasActiveLoadedScene(SceneSetup[] scenes)
        {
            foreach (SceneSetup scene in scenes)
            {
                if (scene.isLoaded && scene.isActive)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Click(Button button)
        {
            // Runs the registered click action without relying on OS pointer input.
            typeof(Clickable).GetMethod("SimulateSingleClick", k_members)
                .Invoke(button.clickable, new object[] { null, 0 });
        }
    }
}
