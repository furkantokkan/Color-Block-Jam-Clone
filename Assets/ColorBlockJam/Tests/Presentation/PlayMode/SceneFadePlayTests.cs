using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.SceneFlow;
using NUnit.Framework;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using PrimeTween;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// Verifies the black screen fade over scene changes (DEC-198) with the real ProjectContext: its panel sorts above
    /// every other panel and is hidden when idle, it blocks presses only while it shows, Init unloads behind it, and a
    /// Game scene builds behind full black and is revealed only once its scope is ready, with no tween left behind.
    /// </summary>
    public sealed class SceneFadePlayTests
    {
        private const float k_timeoutSeconds = 20f;
        private const string k_fadeName = "screen-fade";

        [UnityTest]
        public IEnumerator TheFadeSortsAboveEveryPanelAndIsHiddenWhenIdle()
        {
            yield return LoadIdle("Hub");
            ScreenFadeView view = ResolveView();
            UIDocument document = view.GetComponent<UIDocument>();

            foreach (UIDocument other in Object.FindObjectsOfType<UIDocument>())
            {
                if (other != document && other.panelSettings != null)
                {
                    Assert.That(document.panelSettings.sortingOrder, Is.GreaterThan(other.panelSettings.sortingOrder),
                        $"the fade draws above '{other.name}'");
                }
            }

            Assert.That(view.IsVisible, Is.False);
            Assert.That(document.rootVisualElement.Q(k_fadeName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(PickAtCentre(document), Is.Null, "hidden, the fade lets presses through");
        }

        [UnityTest]
        public IEnumerator FadeOutCoversTheScreenAndBlocksPressesUntilFadeInHidesIt()
        {
            yield return LoadIdle("Hub");
            ScreenFadeView view = ResolveView();
            UIDocument document = view.GetComponent<UIDocument>();
            VisualElement fade = document.rootVisualElement.Q(k_fadeName);

            Task fadeOut = view.FadeOutAsync(CancellationToken.None);
            Assert.That(view.IsVisible, Is.True);
            yield return WaitForTask(fadeOut);
            yield return null;
            Assert.That(view.Opacity, Is.EqualTo(1f));
            Assert.That(PickAtCentre(document), Is.SameAs(fade), "the black takes every press");

            Task fadeIn = view.FadeInAsync(CancellationToken.None);
            yield return WaitForTask(fadeIn);
            yield return null;
            Assert.That(view.Opacity, Is.EqualTo(0f));
            Assert.That(view.IsVisible, Is.False);
            Assert.That(fade.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(PickAtCentre(document), Is.Null);
            Assert.That(Tween.GetTweensCount(view), Is.EqualTo(0), "the view's tween ended with the fade");
        }

        [UnityTest]
        public IEnumerator InitUnloadsBehindTheFadeAndHomeFadesIn()
        {
            yield return SceneManager.LoadSceneAsync("Init", LoadSceneMode.Single);
            SceneFadeTransition transition = Resolve<SceneFadeTransition>();
            ScreenFadeView view = ResolveView();
            Scene init = SceneManager.GetSceneByName("Init");
            float loadingSortingOrder = SortingOrderOf(init);
            Assert.That(view.GetComponent<UIDocument>().panelSettings.sortingOrder, Is.GreaterThan(loadingSortingOrder),
                "the fade draws above the loading screen");

            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (init.isLoaded && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(init.isLoaded, Is.False, "Init never unloaded");
            Assert.That(transition.IsRunning, Is.True, "Init unloads through the scene fade");
            Assert.That(view.IsVisible, Is.True, "Init unloaded behind the fade");
            Assert.That(view.Opacity, Is.GreaterThan(0.5f), "Home has not appeared yet");

            yield return WaitUntil(() => !transition.IsRunning);
            Assert.That(SceneManager.GetSceneByName("Init").IsValid(), Is.False, "Init left the scene list");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
            Assert.That(SceneScopes.Find(SceneManager.GetActiveScene()).IsReady, Is.True);
            Assert.That(view.IsVisible, Is.False, "Home fades in");
            Assert.That(Tween.GetTweensCount(view), Is.EqualTo(0));
        }

        /// <summary>
        /// With the level's pack already resident the whole Game build can finish in its activation frame, so no frame
        /// shows it in progress; the real readiness wait is observed instead, at the change's end and at readiness.
        /// </summary>
        [UnityTest]
        public IEnumerator HubOpensGameBehindTheFadeAndRevealsItOnlyOnceItsScopeIsReady()
        {
            yield return LoadIdle("Hub");
            ScreenFadeView view = ResolveView();
            OnitySceneFlowProfile profile = Resolve<OnitySceneFlowProfile>();
            var readiness = new ObservedReadiness(new ActiveSceneReadiness(), view);

            using (var transition = new SceneFadeTransition(view, readiness))
            {
                Task run = transition.RunAsync(
                    token => OnitySceneFlow.TransitionAsync(profile, OnitySceneFlowStateId.Gameplay, null, token),
                    CancellationToken.None);
                yield return WaitForTask(run);
            }

            Assert.That(readiness.OpacityAfterChange, Is.EqualTo(1f), "the load ran behind full black");
            Assert.That(readiness.OpacityWhenReady, Is.EqualTo(1f), "the Game scene built behind full black");
            Assert.That(readiness.WasSceneReady, Is.True, "the fade lifts only once the Game scope is ready");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Game"));
            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.Opacity, Is.EqualTo(0f));
            Assert.That(Tween.GetTweensCount(view), Is.EqualTo(0));
        }

        // Loads a scene and waits until its build (for Hub, with the level it prepares, DEC-199) is done and no earlier
        // test's scene change is still revealing.
        private static IEnumerator LoadIdle(string sceneName)
        {
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            SceneFadeTransition transition = Resolve<SceneFadeTransition>();
            SceneContext scope = SceneScopes.Find(SceneManager.GetSceneByName(sceneName));
            yield return WaitUntil(() => !transition.IsRunning && (scope == null || scope.ReadyTask.IsCompleted));
        }

        private static T Resolve<T>()
        {
            Assert.That(ProjectContext.Instance, Is.Not.Null);
            Assert.That(ProjectContext.Instance.Container.TryResolve(out T service), Is.True, typeof(T).Name);
            return service;
        }

        private static ScreenFadeView ResolveView()
        {
            ScreenFadeView view = Resolve<IScreenFade>() as ScreenFadeView;
            Assert.That(view != null, Is.True, "the project scope binds the ProjectContext's ScreenFadeView");
            return view;
        }

        private static VisualElement PickAtCentre(UIDocument document)
        {
            IPanel panel = document.rootVisualElement.panel;
            return panel.Pick(panel.visualTree.worldBound.center);
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
            return 0f;
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

        // Wraps the real readiness wait and records the fade when the change has ended and when the scene is ready.
        private sealed class ObservedReadiness : IActiveSceneReadiness
        {
            private readonly IActiveSceneReadiness m_inner;
            private readonly ScreenFadeView m_view;

            public ObservedReadiness(IActiveSceneReadiness inner, ScreenFadeView view)
            {
                m_inner = inner;
                m_view = view;
            }

            public float OpacityAfterChange { get; private set; } = -1f;

            public float OpacityWhenReady { get; private set; } = -1f;

            public bool WasSceneReady { get; private set; }

            public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
            {
                OpacityAfterChange = m_view.Opacity;
                await m_inner.WaitUntilReadyAsync(cancellationToken);
                OpacityWhenReady = m_view.Opacity;
                SceneContext scope = SceneScopes.Find(SceneManager.GetActiveScene());
                WasSceneReady = scope != null && scope.IsReady;
            }
        }
    }
}
