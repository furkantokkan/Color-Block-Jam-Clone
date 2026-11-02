using System;
using System.Collections;
using ColorBlockJam.Gameplay.Composition;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Hud
{
    /// <summary>
    /// Loads the real Game scene additively for the HUD top bar and menu tests (direct Play, as the Booster composition
    /// tests do) and exposes its scene singletons, the HUD document root and small UI helpers. The caller unloads it.
    /// </summary>
    internal sealed class GameHudScene
    {
        public const string SceneName = "Game";
        public const string OverlayBackActionName = "GameOverlayBack";

        private const float k_buildTimeoutSeconds = 20f;
        private const float k_waitSeconds = 5f;

        private GameHudScene(SceneContext scope)
        {
            Scope = scope;
            Session = scope.Container.Resolve<LevelSession>();
            Flow = scope.Container.Resolve<GameMenuFlow>();
            TopBar = scope.Container.Resolve<GameTopBar>();
            Hud = scope.Container.Resolve<GameplayHud>();
            Document = scope.Container.Resolve<GameplayHudContext>().Document;
            PauseRelay = FindInGame<ApplicationPauseRelay>();
        }

        /// <summary>The scene's real OS-backgrounding relay (integration hunk 6).</summary>
        public ApplicationPauseRelay PauseRelay { get; }

        public SceneContext Scope { get; }

        public LevelSession Session { get; }

        public GameMenuFlow Flow { get; }

        public GameTopBar TopBar { get; }

        public GameplayHud Hud { get; }

        public UIDocument Document { get; }

        public VisualElement Root => Document.rootVisualElement;

        public T Resolve<T>()
        {
            return Scope.Container.Resolve<T>();
        }

        /// <summary>
        /// Unity's OnApplicationPause message on the scene's relay component, as the OS sends it when the app goes to
        /// the background (true) or returns (false).
        /// </summary>
        public void SendApplicationPause(bool paused)
        {
            Assert.That(PauseRelay, Is.Not.Null, "the Game scene has an ApplicationPauseRelay");
            PauseRelay.SendMessage("OnApplicationPause", paused, SendMessageOptions.RequireReceiver);
        }

        public static IEnumerator Load(Action<GameHudScene> loaded)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
            SceneContext scope = FindInGame<SceneContext>();
            Assert.That(scope, Is.Not.Null, "the Game scene has a SceneContext");
            float deadline = Time.realtimeSinceStartup + k_buildTimeoutSeconds;
            while (!scope.IsReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(scope.IsReady, Is.True, "the Game scene container finished its async build");

            // Two frames so the HUD panel lays out the bar before any geometry is read.
            yield return null;
            yield return null;
            GameHudScene scene = new GameHudScene(scope);
            Assert.That(scene.TopBar.IsBound, Is.True, "the loader showed the top bar on Home");
            loaded(scene);
        }

        public static IEnumerator Unload()
        {
            yield return SceneManager.UnloadSceneAsync(SceneName);
        }

        public static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + k_waitSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        /// <summary>A submit on the button runs its click handler as a tap does, without a pointer device.</summary>
        public static void Click(Button button)
        {
            Assert.That(button, Is.Not.Null);
            Assert.That(button.enabledInHierarchy, Is.True, button.name + " is enabled");
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }

        /// <summary>The m:ss text of Booster's prebuilt timer table (RULE-21).</summary>
        public static string FormatTime(int seconds)
        {
            return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
        }

        /// <summary>
        /// Inverts RuntimePanelUtils.ScreenToPanel and flips back to screen space (origin bottom left), as the Booster
        /// composition tests do.
        /// </summary>
        public static Vector2 ToScreen(IPanel panel, Vector2 panelPoint)
        {
            Vector2 origin = RuntimePanelUtils.ScreenToPanel(panel, Vector2.zero);
            Vector2 unit = RuntimePanelUtils.ScreenToPanel(panel, Vector2.one) - origin;
            Vector2 flipped = new Vector2((panelPoint.x - origin.x) / unit.x, (panelPoint.y - origin.y) / unit.y);
            return new Vector2(flipped.x, Screen.height - flipped.y);
        }

        /// <summary>True while an enabled InputAction named like the overlay Back action exists anywhere.</summary>
        public static bool IsOverlayBackEnabled()
        {
            foreach (InputAction action in InputSystem.ListEnabledActions())
            {
                if (action.name == OverlayBackActionName)
                {
                    return true;
                }
            }

            return false;
        }

        private static T FindInGame<T>() where T : Component
        {
            Scene game = SceneManager.GetSceneByName(SceneName);
            GameObject[] roots = game.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                T component = roots[i].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }
    }
}
