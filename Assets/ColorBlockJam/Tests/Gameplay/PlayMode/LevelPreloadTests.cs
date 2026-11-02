using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Proves the hot level between two Game scenes with the real project scope (DEC-199): a prepared level builds
    /// hidden next to the playing scene (camera, lights and UI off, board far away, session on Home), a matching start
    /// swaps the scenes in one step with no fade and plays it, and a start for another level returns false and leaves
    /// the prepared level waiting, so the caller loads behind the fade.
    /// </summary>
    public sealed class LevelPreloadTests
    {
        private const string k_gameScene = "Game";
        private const float k_timeoutSeconds = 20f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return PreparedLevelScenes.UnloadAllGameScenes();
        }

        [UnityTest]
        public IEnumerator APreparedLevelBuildsHiddenAndItsStartSwapsTheScenesAtOnceWithNoFade()
        {
            Scene playing = default;
            yield return LoadPlayingGame(scene => playing = scene);
            LevelPreloader preloader = Resolve<LevelPreloader>();
            IScreenFade fade = Resolve<IScreenFade>();
            LevelCatalog levels = Resolve<LevelCatalog>();
            Assume.That(levels.Count, Is.GreaterThan(1), "needs a second level");

            yield return WaitForTask(preloader.PrepareAsync(1, CancellationToken.None));
            Assert.That(preloader.IsPrepared(1), Is.True, "level index 1 waits prepared");
            Scene prepared = FindOtherGameScene(playing);
            Assert.That(prepared.IsValid() && prepared.isLoaded, Is.True, "a second Game scene is loaded");
            Assert.That(SceneManager.GetActiveScene() == playing, Is.True, "the playing scene stays active");

            SceneContext scope = SceneScopes.Find(prepared);
            LevelSession session = scope.Container.Resolve<LevelSession>();
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "the prepared level waits on Home");
            Assert.That(session.Attempt.Level.LevelId, Is.EqualTo(levels.Manifest.GetLevelIdAt(1)));
            AssertHidden(prepared, scope);

            // With no starting boosters the swap happens within the call; only the replaced scene's unload is awaited.
            Task<bool> start = preloader.TryStartAsync(new LevelLaunchRequest(1, null), CancellationToken.None);
            Assert.That(SceneManager.GetActiveScene() == prepared, Is.True, "the prepared scene is active at once");
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "it plays at once");
            AssertShown(prepared, scope);

            bool fadeShown = fade.IsVisible;
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!start.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                fadeShown |= fade.IsVisible;
                yield return null;
            }

            Assert.That(start.IsCompleted, Is.True, "the start never ended");
            Assert.That(start.Result, Is.True);
            Assert.That(playing.isLoaded, Is.False, "the replaced scene unloaded");
            Assert.That(fadeShown, Is.False, "no fade: the level opened at once");
            Assert.That(preloader.IsPrepared(1), Is.False, "a started level no longer waits");
        }

        [UnityTest]
        public IEnumerator AStartForAnotherLevelReturnsFalseAndLeavesThePreparedLevelWaiting()
        {
            Scene playing = default;
            yield return LoadPlayingGame(scene => playing = scene);
            LevelPreloader preloader = Resolve<LevelPreloader>();

            yield return WaitForTask(preloader.PrepareAsync(1, CancellationToken.None));
            Assert.That(preloader.IsPrepared(1), Is.True);

            Task<bool> start = preloader.TryStartAsync(new LevelLaunchRequest(5, null), CancellationToken.None);
            yield return WaitForTask(start);

            Assert.That(start.Result, Is.False, "the caller falls back to the fade");
            Assert.That(SceneManager.GetActiveScene() == playing, Is.True, "nothing swapped");
            Assert.That(preloader.IsPrepared(1), Is.True, "the prepared level still waits");
            AssertHidden(FindOtherGameScene(playing), SceneScopes.Find(FindOtherGameScene(playing)));
        }

        /// <summary>
        /// Back to Home from the win popup runs a Single load that unloads the next level's scene while it still builds;
        /// Hub then asks for that same level. The request must not join the lost preparation: the level is prepared again.
        /// </summary>
        [UnityTest]
        public IEnumerator ARequestAfterTheRunningPreparationLostItsSceneAsksForTheLevelAgain()
        {
            Scene playing = default;
            yield return LoadPlayingGame(scene => playing = scene);
            LevelPreloader preloader = Resolve<LevelPreloader>();

            Task first = preloader.PrepareAsync(1, CancellationToken.None);
            yield return WaitUntil(() => FindOtherGameScene(playing).IsValid());
            Assert.That(preloader.IsPreparing, Is.True, "the first preparation still builds its scene");

            SceneManager.UnloadSceneAsync(FindOtherGameScene(playing));
            Task second = preloader.PrepareAsync(1, CancellationToken.None);
            yield return WaitForTask(second);

            Assert.That(first.IsCompleted, Is.True);
            Assert.That(preloader.IsPrepared(1), Is.True, "level index 1 waits prepared again");
            Scene prepared = FindOtherGameScene(playing);
            Assert.That(prepared.IsValid() && prepared.isLoaded, Is.True, "a new hidden Game scene is loaded");
            AssertHidden(prepared, SceneScopes.Find(prepared));
        }

        // Loads the Game scene additively as the playing scene: active, built, and playing its serialized level.
        private static IEnumerator LoadPlayingGame(Action<Scene> loaded)
        {
            Scene scene = default;

            void OnLoaded(Scene candidate, LoadSceneMode mode)
            {
                if (candidate.name == k_gameScene)
                {
                    scene = candidate;
                }
            }

            SceneManager.sceneLoaded += OnLoaded;
            yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
            SceneManager.sceneLoaded -= OnLoaded;
            SceneManager.SetActiveScene(scene);
            SceneContext scope = SceneScopes.Find(scene);
            yield return WaitUntil(() => scope.IsReady);
            Assert.That(scope.Container.Resolve<LevelSession>().State.Value, Is.EqualTo(SessionState.Playing));
            loaded(scene);
        }

        private static void AssertHidden(Scene scene, SceneContext scope)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    Assert.That(camera.enabled, Is.False, $"camera '{camera.name}' draws nothing while hidden");
                }

                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    Assert.That(light.enabled, Is.False, $"light '{light.name}' is off while hidden");
                }

                foreach (UIDocument document in root.GetComponentsInChildren<UIDocument>(true))
                {
                    Assert.That(document.rootVisualElement == null
                                || document.rootVisualElement.resolvedStyle.display == DisplayStyle.None
                                || document.rootVisualElement.style.display.value == DisplayStyle.None, Is.True,
                        $"document '{document.name}' shows nothing while hidden");
                }
            }

            Vector3 playArea = scope.Container.Resolve<EnvironmentView>().transform.position;
            Assert.That(playArea.x, Is.GreaterThanOrEqualTo(GameSceneVisibility.HiddenOffset.x),
                "the hidden board sits beyond every camera's reach");
        }

        private static void AssertShown(Scene scene, SceneContext scope)
        {
            Camera camera = FindBoardCamera(scene);
            Assert.That(camera != null && camera.enabled, Is.True, "the gameplay camera draws again");
            Vector3 playArea = scope.Container.Resolve<EnvironmentView>().transform.position;
            Assert.That(playArea.x, Is.LessThan(GameSceneVisibility.HiddenOffset.x), "the board came back");
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (UIDocument document in root.GetComponentsInChildren<UIDocument>(true))
                {
                    if (document.rootVisualElement != null)
                    {
                        Assert.That(document.rootVisualElement.style.display.value, Is.Not.EqualTo(DisplayStyle.None),
                            $"document '{document.name}' shows again");
                    }
                }
            }
        }

        private static Camera FindBoardCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                BoardCamera boardCamera = root.GetComponentInChildren<BoardCamera>(true);
                if (boardCamera != null)
                {
                    return boardCamera.GetComponent<Camera>();
                }
            }

            return null;
        }

        private static Scene FindOtherGameScene(Scene playing)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.name == k_gameScene && scene != playing && scene.isLoaded)
                {
                    return scene;
                }
            }

            return default;
        }

        private static T Resolve<T>()
        {
            Assert.That(ProjectContext.Instance, Is.Not.Null);
            Assert.That(ProjectContext.Instance.Container.TryResolve(out T service), Is.True, typeof(T).Name);
            return service;
        }

        private static IEnumerator WaitForTask(Task task)
        {
            yield return WaitUntil(() => task.IsCompleted);
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "timed out");
        }
    }
}
