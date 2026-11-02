using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// Verifies that each player scene starts with its own Onity scope, that Init is the app's only loading screen
    /// with no other route through a Loading scene (DEC-140), and that the project scope loads the active
    /// Addressable level JSON.
    /// </summary>
    public sealed class SceneFlowSmokeTests
    {
        [UnityTest]
        public IEnumerator GameRunsWhenOpenedDirectly()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;

            AssertSceneScopes("Game", OnitySceneFlowStateId.Gameplay);
        }

        [UnityTest]
        public IEnumerator HubRunsWhenOpenedDirectly()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;

            AssertSceneScopes("Hub", OnitySceneFlowStateId.Hub);
            yield return WaitForHubBuild();
        }

        /// <summary>
        /// Init is the only loading screen (DEC-140): it stays loaded and drawn above Hub until Hub's scope is ready,
        /// then unloads, leaving Hub and the Game scene Hub prepared behind Home (DEC-199).
        /// </summary>
        [UnityTest]
        public IEnumerator InitKeepsTheLoadingScreenUntilHubIsReady()
        {
            yield return SceneManager.LoadSceneAsync("Init", LoadSceneMode.Single);
            Scene init = SceneManager.GetSceneByName("Init");
            bool sawHubBehindLoading = false;
            float deadline = Time.realtimeSinceStartup + 10f;

            while (init.isLoaded && Time.realtimeSinceStartup < deadline)
            {
                Scene hub = SceneManager.GetSceneByName("Hub");
                if (hub.isLoaded)
                {
                    sawHubBehindLoading = true;
                    Assert.That(SortingOrderOf(init), Is.GreaterThan(SortingOrderOf(hub)),
                        "the loading screen must draw above Hub while Hub builds");
                }

                yield return null;
                if (!init.isLoaded)
                {
                    SceneContext hubScope = SceneScopes.Find(SceneManager.GetSceneByName("Hub"));
                    Assert.That(hubScope != null && hubScope.IsReady, Is.True, "Init unloaded before Hub was ready");
                }
            }

            Assert.That(init.isLoaded, Is.False, "Init never unloaded");
            Assert.That(sawHubBehindLoading, Is.True, "Hub never loaded behind the loading screen");

            // A scene stops reporting isLoaded when its unload begins and leaves the scene list when it ends; Init's
            // unload now starts behind the screen fade (DEC-198), so it can still be listed here for a frame.
            while (SceneManager.GetSceneByName("Init").IsValid() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetSceneByName("Init").IsValid(), Is.False, "Init left the scene list");
            AssertSceneScopes("Hub", OnitySceneFlowStateId.Hub);
        }

        [UnityTest]
        public IEnumerator HubOpensGameAndDisposesItsSceneScope()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;

            ProjectContext projectScope = ProjectContext.Instance;
            SceneContext hubScope = SceneScopes.Find(SceneManager.GetSceneByName("Hub"));
            var hubContainer = hubScope.Container;

            // Hub's build ends with the level it prepares behind Home (DEC-199); the Single load below replaces both.
            yield return WaitForTask(hubScope.ReadyTask);
            OnitySceneFlowProfile profile = AssertSceneScopes(
                "Hub",
                OnitySceneFlowStateId.Hub);

            Task transition = OnitySceneFlow.TransitionAsync(
                profile,
                OnitySceneFlowStateId.Gameplay);
            yield return WaitForScene("Game");
            yield return WaitForTask(transition);
            yield return null;

            Assert.That(transition.IsFaulted, Is.False, transition.Exception?.ToString());
            Assert.That(transition.IsCanceled, Is.False);
            Assert.That(ProjectContext.Instance, Is.SameAs(projectScope));
            Assert.That(hubScope == null, Is.True);
            SceneContext gameScope = SceneScopes.Find(SceneManager.GetActiveScene());
            Assert.That(gameScope.Container, Is.Not.SameAs(hubContainer));
            Assert.That(gameScope.Container, Is.Not.SameAs(projectScope.Container));
            AssertSceneScopes("Game", OnitySceneFlowStateId.Gameplay);
        }

        /// <summary>
        /// Level and Home transitions load their scene directly: the profile names no Loading scene and the build has
        /// none, so no route can show a second loading screen (DEC-140).
        /// </summary>
        [UnityTest]
        public IEnumerator NoTransitionRoutesThroughALoadingScene()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return null;
            OnitySceneFlowProfile profile = AssertSceneScopes("Hub", OnitySceneFlowStateId.Hub);

            foreach (OnitySceneFlowStateId target in new[] { OnitySceneFlowStateId.Hub, OnitySceneFlowStateId.Gameplay })
            {
                OnitySceneFlowTransitionPlan plan = OnitySceneFlow.BuildTransitionPlan(profile, target);
                Assert.That(plan.RouteViaLoadingScene, Is.False, target.ToString());
                Assert.That(plan.EntrySceneName, Is.EqualTo(plan.TargetSceneName), target.ToString());
            }

            Assert.That(SceneUtility.GetBuildIndexByScenePath("Assets/ColorBlockJam/Scenes/Loading.unity"), Is.EqualTo(-1));
            yield return WaitForHubBuild();
        }

        [UnityTest]
        public IEnumerator ProjectScopeLoadsActiveAddressableLevelJson()
        {
            yield return SceneManager.LoadSceneAsync("Hub", LoadSceneMode.Single);
            yield return WaitForTask(ProjectContext.Instance.ReadyTask);

            Assert.That(ProjectContext.Instance.ReadyTask.IsFaulted, Is.False,
                ProjectContext.Instance.ReadyTask.Exception?.ToString());
            Assert.That(ProjectContext.Instance.Container.TryResolve<LevelCatalog>(
                out LevelCatalog levels), Is.True);
            Assert.That(levels.JsonGuid, Is.Not.Empty);
            Assert.That(levels.Count, Is.GreaterThan(0));
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + 10f;

            while (SceneManager.GetActiveScene().name != sceneName
                && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }

        // Hub's build ends with the level it prepares behind Home (DEC-199); a test ends after it, so no half-loaded
        // hidden scene reaches the next test.
        private static IEnumerator WaitForHubBuild()
        {
            SceneContext hub = SceneScopes.Find(SceneManager.GetSceneByName("Hub"));
            if (hub != null)
            {
                yield return WaitForTask(hub.ReadyTask);
            }
        }

        private static IEnumerator WaitForTask(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 10f;

            while (task.IsCompleted == false && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True);
        }

        private static float SortingOrderOf(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                UIDocument document = roots[index].GetComponentInChildren<UIDocument>(true);
                if (document != null && document.panelSettings != null)
                {
                    return document.panelSettings.sortingOrder;
                }
            }

            Assert.Fail($"scene '{scene.name}' has no UI document");
            return 0;
        }

        private static OnitySceneFlowProfile AssertSceneScopes(
            string sceneName,
            OnitySceneFlowStateId stateId)
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
            Assert.That(ProjectContext.Instance, Is.Not.Null);

            // The active scene's own scope; any other one belongs to a hidden Game scene Hub prepared (DEC-199).
            SceneContext scope = SceneScopes.Find(SceneManager.GetActiveScene());
            Assert.That(scope, Is.Not.Null, sceneName + " has its SceneContext");
            foreach (SceneContext other in Object.FindObjectsOfType<SceneContext>())
            {
                Assert.That(other == scope || (sceneName == "Hub" && other.gameObject.scene.name == "Game"), Is.True,
                    "no SceneContext besides the active scene's and the level Hub prepared");
            }

            Assert.That(
                scope.Container.TryResolve<OnitySceneFlowProfile>(out var profile),
                Is.True);
            Assert.That(profile.RouteTransitionsThroughLoadingScene, Is.False);
            Assert.That(profile.TryGetSceneName(OnitySceneFlowStateId.Loading, out _), Is.False);
            Assert.That(profile.GetSceneName(OnitySceneFlowStateId.Hub), Is.EqualTo("Hub"));
            Assert.That(
                profile.GetSceneName(OnitySceneFlowStateId.Gameplay),
                Is.EqualTo("Game"));
            Assert.That(profile.TryGetStateId(sceneName, out var actualStateId), Is.True);
            Assert.That(actualStateId, Is.EqualTo(stateId));
            return profile;
        }
    }
}
