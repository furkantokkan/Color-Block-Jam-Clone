using System.Collections;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// HOME from the in-game pause menu through the real scene flow (contract AC-8, DEC-135, DEC-140): the press tears
    /// the attempt down to Home once, the project's scene-flow profile loads Hub with no loading scene on the way, and
    /// the played Game unloads with its scene scope; Hub then prepares the next level in a new hidden Game scene
    /// (DEC-199). Game opens in Single mode, as a fade launch leaves it, and the test returns to one empty scene
    /// afterwards.
    /// </summary>
    public sealed class GameHudHomeTests
    {
        private const float k_timeoutSeconds = 20f;
        private static int s_cleanupSceneCount;

        [UnityTearDown]
        public IEnumerator ReturnToCleanScene()
        {
            Scene cleanup = SceneManager.CreateScene("GameHudHomeCleanup" + s_cleanupSceneCount++);
            SceneManager.SetActiveScene(cleanup);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != cleanup && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }
        }

        [UnityTest]
        public IEnumerator HomeFromThePauseMenuTearsDownOnceAndOpensHubWithNoLoadingScene()
        {
            yield return SceneManager.LoadSceneAsync(GameHudScene.SceneName, LoadSceneMode.Single);
            SceneContext game = null;
            yield return WaitUntil(() => (game = FindScope(GameHudScene.SceneName)) != null && game.IsReady);
            Assert.That(game != null && game.IsReady, Is.True, "the Game scene container finished its async build");
            LevelSession session = game.Container.Resolve<LevelSession>();
            yield return WaitUntil(() => session.State.Value == SessionState.Playing);
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing), "direct Play auto-plays (DEC-116)");
            GameMenuFlow flow = game.Container.Resolve<GameMenuFlow>();
            IGameExitNavigator exit = game.Container.Resolve<IGameExitNavigator>();
            VisualElement root = game.Container.Resolve<GameplayHudContext>().Document.rootVisualElement;

            GameHudScene.Click(game.Container.Resolve<GameTopBar>().View.PauseButton);
            yield return WaitUntil(() => flow.Current == GameOverlay.PauseMenu);
            Assert.That(flow.Current, Is.EqualTo(GameOverlay.PauseMenu));
            yield return null;

            Button home = root.Q<Button>("home-button");
            GameHudScene.Click(home);

            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "HOME tore the attempt down");
            Assert.That(session.Attempt, Is.Null, "nothing of the attempt is left");
            Assert.That(exit.IsExiting, Is.True, "one Hub transition runs");
            Assert.That(flow.Current, Is.EqualTo(GameOverlay.PauseMenu), "the menu stays up while the exit runs");
            flow.RequestHome();
            flow.RequestClose();
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Home), "a second press changes nothing");

            bool sawLoadingScene = false;
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline && !(game == null && IsHubReady()))
            {
                sawLoadingScene |= IsLoaded("Loading") || IsLoaded("Init");
                yield return null;
            }

            Assert.That(sawLoadingScene, Is.False, "no loading screen between Game and Hub (DEC-140)");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
            Assert.That(game == null, Is.True, "the played Game scene unloaded with its scope");

            // Hub's build ends with the next level prepared behind Home, so the only other scene is that Game scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string name = SceneManager.GetSceneAt(i).name;
                Assert.That(name == "Hub" || name == GameHudScene.SceneName, Is.True, $"unexpected scene '{name}'");
            }
        }

        // Hub is active and finished its build, which includes the level it prepares behind Home (DEC-199).
        private static bool IsHubReady()
        {
            SceneContext hub = FindScope("Hub");
            return hub != null && hub.IsReady && SceneManager.GetActiveScene().name == "Hub";
        }

        private static bool IsLoaded(string sceneName)
        {
            return SceneManager.GetSceneByName(sceneName).isLoaded;
        }

        private static SceneContext FindScope(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.isLoaded)
            {
                return null;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                SceneContext scope = roots[i].GetComponentInChildren<SceneContext>(true);
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }

        private static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }
    }
}
