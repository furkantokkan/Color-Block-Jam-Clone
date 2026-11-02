using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using NUnit.Framework;
using Onity.DI;
using Onity.Reactive;
using Onity.Unity.Async;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// The Game scene container binds one BoardArt from the scene installer and releases it with the scene,
    /// leaving no running task or live subscription behind (AC-5, DEC-123). The scene installer binds no shape
    /// library any more (DEC-193), so its shape definition case was removed.
    /// </summary>
    public sealed class BoardArtContainerTests
    {
        private const string k_gameScene = "Game";
        private const float k_buildTimeoutSeconds = 20f;

        private bool m_sceneLoaded;

        [UnityTearDown]
        public IEnumerator UnloadGameScene()
        {
            if (m_sceneLoaded)
            {
                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
            }
        }

        [UnityTest]
        public IEnumerator GameSceneResolvesOneBoardArtAndReleasesItWithTheScene()
        {
            var runningBefore = new HashSet<int>();
            CollectRunningTaskIds(runningBefore);
            bool wasTracking = OnityObservableTracker.EnableTracking;
            OnityObservableTracker.EnableTracking = true;
            var subscriptionsBefore = new HashSet<int>();
            CollectSubscriptionIds(subscriptionsBefore, includeDisposed: true);
            try
            {
                yield return SceneManager.LoadSceneAsync(k_gameScene, LoadSceneMode.Additive);
                m_sceneLoaded = true;

                SceneContext scope = FindGameScope();
                Assert.That(scope, Is.Not.Null, "the Game scene has a SceneContext");
                float deadline = Time.realtimeSinceStartup + k_buildTimeoutSeconds;
                while (!scope.IsReady && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(scope.IsReady, Is.True, "the Game scene container finished its build");
                OnityContainer container = scope.Container;
                BoardArt art = container.Resolve<BoardArt>();
                Assert.That(container.Resolve<BoardArt>(), Is.SameAs(art), "one BoardArt instance");
                Assert.That(art.Validate(out string error), Is.True, error);

                m_sceneLoaded = false;
                yield return SceneManager.UnloadSceneAsync(k_gameScene);
                yield return null;

                bool resolved;
                try
                {
                    resolved = container.TryResolve(out BoardArt _);
                }
                catch (OnityResolveException)
                {
                    // A disposed container refuses every resolve.
                    resolved = false;
                }

                Assert.That(resolved, Is.False, "the scene container released its binding with the scene");

                var runningAfter = new HashSet<int>();
                CollectRunningTaskIds(runningAfter);
                runningAfter.ExceptWith(runningBefore);
                Assert.That(runningAfter, Is.Empty, "no task started by the Game scene is still running");

                var liveAfter = new HashSet<int>();
                CollectSubscriptionIds(liveAfter, includeDisposed: false);
                liveAfter.ExceptWith(subscriptionsBefore);
                Assert.That(liveAfter, Is.Empty, "no subscription made while the Game scene was loaded is still live");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                OnityObservableTracker.EnableTracking = wasTracking;
            }
        }

        private static void CollectRunningTaskIds(HashSet<int> ids)
        {
            var tasks = new List<OnityTrackedTaskInfo>();
            OnityTaskTracker.GetSnapshot(tasks, includeCompleted: false);
            for (int i = 0; i < tasks.Count; i++)
            {
                ids.Add(tasks[i].TaskId);
            }
        }

        private static void CollectSubscriptionIds(HashSet<int> ids, bool includeDisposed)
        {
            var subscriptions = new List<OnityTrackedObservableInfo>();
            OnityObservableTracker.GetSnapshot(subscriptions, includeDisposed);
            for (int i = 0; i < subscriptions.Count; i++)
            {
                ids.Add(subscriptions[i].TrackingId);
            }
        }

        private static SceneContext FindGameScope()
        {
            Scene game = SceneManager.GetSceneByName(k_gameScene);
            GameObject[] roots = game.GetRootGameObjects();
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
    }
}
