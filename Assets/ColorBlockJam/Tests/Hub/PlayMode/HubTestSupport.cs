using System.Collections;
using System.Reflection;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Shared PlayMode steps for the Hub tests: open Hub, wait until Home is bound, press or release a UI Toolkit
    /// element through the panel's own mouse events, arrange a specific booster stock on the shared project-scoped
    /// save service, and return to a clean empty scene afterwards.
    /// </summary>
    internal static class HubTestSupport
    {
        private const float k_timeoutSeconds = 15f;

        private static readonly MethodInfo s_publishMethod =
            typeof(PlayerSaveService).GetMethod("Publish", BindingFlags.NonPublic | BindingFlags.Instance);

        private static int s_cleanupSceneCount;

        public static IEnumerator LoadHubAndWaitForHome(string entrySceneName)
        {
            yield return SceneManager.LoadSceneAsync(entrySceneName, LoadSceneMode.Single);
            yield return WaitForHome();

            // Init builds Hub behind its loading screen and unloads itself after Hub's build (DEC-140). That build also
            // prepares the offered level in a hidden Game scene (DEC-199), so Hub and that scene remain.
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline
                   && ((entrySceneName != "Hub" && SceneManager.GetSceneByName(entrySceneName).IsValid()) || !IsHubReady()))
            {
                yield return null;
            }

            Assert.That(entrySceneName == "Hub" || !SceneManager.GetSceneByName(entrySceneName).IsValid(), Is.True,
                "the entry scene never unloaded");
            Assert.That(IsHubReady(), Is.True, "Hub never finished its build");
            AssertOnlyHubAndItsPreparedLevel();
        }

        /// <summary>
        /// The Hub scene's SceneContext. A scene-wide search is not enough: Hub prepares the offered level in a hidden
        /// Game scene with a SceneContext of its own (DEC-199).
        /// </summary>
        public static SceneContext HubScope()
        {
            Scene hub = SceneManager.GetSceneByName("Hub");
            if (!hub.IsValid() || !hub.isLoaded)
            {
                return null;
            }

            foreach (GameObject root in hub.GetRootGameObjects())
            {
                SceneContext scope = root.GetComponentInChildren<SceneContext>(true);
                if (scope != null)
                {
                    return scope;
                }
            }

            return null;
        }

        /// <summary>Hub is the active scene, and any other loaded scene is the hidden Game scene it prepared.</summary>
        public static void AssertOnlyHubAndItsPreparedLevel()
        {
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"), "Hub is the active scene");
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string name = SceneManager.GetSceneAt(i).name;
                Assert.That(name == "Hub" || name == "Game", Is.True, $"unexpected scene '{name}' next to Hub");
            }
        }

        private static bool IsHubReady()
        {
            SceneContext scope = HubScope();
            return scope != null && scope.IsReady;
        }

        public static IEnumerator WaitForHome()
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline
                   && (SceneManager.GetActiveScene().name != "Hub" || FindScreen() == null || !FindScreen().IsBound))
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Hub"));
            Assert.That(ProjectContext.Instance, Is.Not.Null);
            Assert.That(ProjectContext.Instance.ReadyTask.IsCompleted, Is.True, "the project scope never became ready");
            Assert.That(FindScreen(), Is.Not.Null, "Hub has no HubScreen");
            Assert.That(FindScreen().IsBound, Is.True, "Home never bound");

            // One more frame so the first layout pass and the safe area have run.
            yield return null;
        }

        public static HubScreen FindScreen()
        {
            return Object.FindObjectOfType<HubScreen>();
        }

        public static VisualElement Root()
        {
            return FindScreen().GetComponent<UIDocument>().rootVisualElement;
        }

        public static void Press(VisualElement element)
        {
            Send(element, EventType.MouseDown, element.worldBound.center, true);
        }

        public static void Release(VisualElement element)
        {
            Send(element, EventType.MouseUp, element.worldBound.center, true);
        }

        /// <summary>
        /// Presses and releases at a panel position with no target, so the panel picks the element under that
        /// position the way a real tap does.
        /// </summary>
        public static void ClickAt(IPanel panel, Vector2 panelPosition)
        {
            // A pointer move refreshes the panel's cached element under the mouse pointer; synthetic mouse events
            // alone keep the element of an earlier synthetic click, and the press would land there instead.
            var moveEvent = new Event { type = EventType.MouseMove, mousePosition = panelPosition };
            using (PointerMoveEvent pointerMove = PointerMoveEvent.GetPooled(moveEvent))
            {
                panel.visualTree.SendEvent(pointerMove);
            }

            Send(panel.visualTree, EventType.MouseMove, panelPosition, false);
            Send(panel.visualTree, EventType.MouseDown, panelPosition, false);
            Send(panel.visualTree, EventType.MouseUp, panelPosition, false);
        }

        /// <summary>Yields frames until the condition holds or two realtime seconds pass; the caller asserts after.</summary>
        public static IEnumerator WaitUntil(System.Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        public static IEnumerator WaitSeconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end)
            {
                yield return null;
            }
        }

        /// <summary>
        /// Arranges one booster's committed stock directly on the shared project-scoped save service, through the
        /// same commit-and-republish step a purchase uses (the private Publish method), so a PlayMode test can set
        /// up a specific stock value (at least 1, or exactly 0) without depending on whatever an earlier test in the
        /// run left behind. Editor play seeds no coins (DEC-129), so a real purchase cannot arrange this instead.
        /// </summary>
        public static void ArrangeStock(PlayerSaveService service, BoosterId id, int stock)
        {
            Assert.That(s_publishMethod, Is.Not.Null, "PlayerSaveService renamed or removed its private Publish method");
            PlayerSnapshot next = service.Committed.WithBoosterStock(id, stock).WithRevision(service.Committed.Revision + 1);
            s_publishMethod.Invoke(service, new object[] { next });
        }

        /// <summary>
        /// Leaves one new empty scene active and unloads Hub, Init or Game if any of them is still loaded, so a
        /// later PlayMode suite that loads its own scene never finds a leftover SceneContext or EventSystem.
        /// </summary>
        public static IEnumerator ReturnToCleanScene()
        {
            bool needsCleanup = false;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                string name = SceneManager.GetSceneAt(i).name;
                if (name == "Hub" || name == "Init" || name == "Game")
                {
                    needsCleanup = true;
                    break;
                }
            }

            if (!needsCleanup)
            {
                yield break;
            }

            Scene cleanup = SceneManager.CreateScene("HubTestCleanup" + s_cleanupSceneCount++);
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

        private static void Send(VisualElement element, EventType type, Vector2 panelPosition, bool targetElement)
        {
            var systemEvent = new Event
            {
                type = type,
                mousePosition = panelPosition,
                button = 0,
                clickCount = 1
            };

            // A mouse press reaches Button's Clickable as a mouse event (a mouse PointerDownEvent only waits for it).
            using (EventBase mouseEvent = type == EventType.MouseDown ? MouseDownEvent.GetPooled(systemEvent)
                       : type == EventType.MouseUp ? MouseUpEvent.GetPooled(systemEvent)
                       : (EventBase)MouseMoveEvent.GetPooled(systemEvent))
            {
                if (targetElement)
                {
                    mouseEvent.target = element;
                }

                element.SendEvent(mouseEvent);
            }
        }
    }
}
