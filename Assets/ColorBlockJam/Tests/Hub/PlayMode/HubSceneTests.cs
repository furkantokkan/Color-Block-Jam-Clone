using System.Collections;
using System.Reflection;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.Unity.Contexts;
using Onity.Unity.Installers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Proves the Hub scene composition and the bound Home screen (contract scenarios 1, 7 and 10): one Hub scene
    /// scope with the Hub installer next to the hidden Game scene it prepared (DEC-199), the UIDocument tree, the one
    /// persistent Input System EventSystem, Home bound once with the saved level, and the shared press feedback on every
    /// Home button.
    /// </summary>
    public sealed class HubSceneTests
    {
        private static readonly string[] s_namedElements =
        {
            "home-screen", "home-background", "safe-area", "top-bar", "profile-button", "currency-holders",
            "settings-button", "level-route", "route-rails", "level-button", "tab-bar", "tab-row", "tab-leaderboard",
            "tab-shop", "tab-home", "tab-collection", "tab-support", "overlay-layer", "settings-screen",
            "pre-level-screen", "booster-purchase-screen"
        };

        private static readonly string[] s_tabOrder = { "tab-leaderboard", "tab-shop", "tab-home", "tab-collection", "tab-support" };

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Every test here loads Hub (directly or through Init); leave no Hub scene or its EventSystem for a
            // later PlayMode suite.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator HubOpenedDirectly_ComposesTheSceneAndBindsHome()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");

            AssertComposition();
            AssertHomeBound();
        }

        [UnityTest]
        public IEnumerator InitOpensHub_HomeBindsOnceWithTheSavedLevel()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Init");

            AssertComposition();
            AssertHomeBound();
        }

        [UnityTest]
        public IEnumerator EveryHomeButton_ScalesAndDarkensWhilePressed_AndOnlySettingsNavigates()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            VisualElement root = HubTestSupport.Root();
            IHubNavigator navigator = HubTestSupport.HubScope().Container.Resolve<IHubNavigator>();

            // Level, settings, profile, both plus badges and all five tabs (AC-1.5, AC-1.6); Settings has its own test.
            var buttons = root.Q("home-screen").Query<Button>(className: "cbj-pressable").ToList();
            Assert.That(buttons.Count, Is.EqualTo(10), "level, settings, profile, 2 plus badges, 5 tabs");
            var opened = new System.Collections.Generic.List<string>();

            // The editor's real mouse would move the captured button's pointer outside it and clear :active, so the
            // UI input module is paused while the test sends its own presses.
            InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
            module.enabled = false;
            try
            {
                foreach (Button button in buttons)
                {
                    VisualElement art = button.resolvedStyle.backgroundImage.sprite != null
                                        || button.resolvedStyle.backgroundImage.texture != null
                        ? button
                        : button.Q(className: "cbj-press-tint");
                    Assert.That(art, Is.Not.Null, button.name + " has nothing to darken");
                    float restTint = art.resolvedStyle.unityBackgroundImageTintColor.r;

                    HubTestSupport.Press(button);
                    VisualElement pressedArt = art;
                    yield return HubTestSupport.WaitUntil(() => IsSettled(button, pressedArt, 0.92f, restTint, true));

                    Assert.That(button.resolvedStyle.scale.value.x, Is.EqualTo(0.92f).Within(0.005f), button.name + " scale while pressed");
                    Assert.That(art.resolvedStyle.unityBackgroundImageTintColor.r, Is.LessThan(restTint - 0.05f), button.name + " darkens while pressed");

                    HubTestSupport.Release(button);
                    yield return HubTestSupport.WaitUntil(() => IsSettled(button, pressedArt, 1f, restTint, false));

                    Assert.That(button.resolvedStyle.scale.value.x, Is.EqualTo(1f).Within(0.005f), button.name + " scale after release");
                    Assert.That(art.resolvedStyle.unityBackgroundImageTintColor.r, Is.EqualTo(restTint).Within(0.01f), button.name + " tint after release");

                    // A released click may open an overlay: record which button did, then close it so the next button
                    // is pressed on a bare Home.
                    if (navigator.IsOpen(HubOverlayId.PreLevel))
                    {
                        opened.Add(button.name + ":" + HubOverlayId.PreLevel);
                    }

                    if (navigator.IsOpen(HubOverlayId.Settings))
                    {
                        opened.Add(button.name + ":" + HubOverlayId.Settings);
                    }

                    while (navigator.CloseTop())
                    {
                    }
                }
            }
            finally
            {
                module.enabled = true;
            }

            // Only the level button (pre-level popup, task 4) and the settings button (task 3) navigate.
            Assert.That(opened, Is.EquivalentTo(new[] { "level-button:PreLevel", "settings-button:Settings" }));
            Assert.That(navigator.OpenCount, Is.Zero);
            Assert.That(root.Q<Button>("tab-home").ClassListContains("tab--active"), Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        // The editor throttles frames when unfocused, so the 0.08 s transition is awaited by value, not by time.
        private static bool IsSettled(VisualElement button, VisualElement art, float scale, float restTint, bool pressed)
        {
            float tint = art.resolvedStyle.unityBackgroundImageTintColor.r;
            bool tintSettled = pressed ? tint < restTint - 0.05f : Mathf.Abs(tint - restTint) < 0.01f;
            return Mathf.Abs(button.resolvedStyle.scale.value.x - scale) < 0.005f && tintSettled;
        }

        private static void AssertComposition()
        {
            // Hub's own scope; the only other one belongs to the Game scene Hub prepared behind Home (DEC-199).
            SceneContext scope = HubTestSupport.HubScope();
            Assert.That(scope, Is.Not.Null, "Hub has its SceneContext");
            foreach (SceneContext other in Object.FindObjectsOfType<SceneContext>())
            {
                Assert.That(other == scope || other.gameObject.scene.name == "Game", Is.True,
                    "no SceneContext besides Hub's and its prepared level's");
            }

            HubTestSupport.AssertOnlyHubAndItsPreparedLevel();

            // OnityContext keeps its installer list private; the scene file is the authority being checked.
            FieldInfo field = typeof(OnityContext).GetField("m_installers", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Onity renamed OnityContext.m_installers");
            var installers = (MonoInstaller[])field.GetValue(scope);
            Assert.That(installers, Has.Length.EqualTo(1));
            Assert.That(installers[0], Is.InstanceOf<HubSceneInstaller>());

            HubScreen screen = HubTestSupport.FindScreen();
            Assert.That(screen.gameObject.name, Is.EqualTo("Hub"));
            Assert.That(screen.transform.parent, Is.SameAs(scope.transform));
            Assert.That(screen.GetComponent<HubSceneInstaller>(), Is.Not.Null);
            UIDocument document = screen.GetComponent<UIDocument>();
            Assert.That(document.panelSettings, Is.Not.Null);
            Assert.That(document.panelSettings.name, Is.EqualTo("HubPanelSettings"));
            Assert.That(document.visualTreeAsset, Is.Not.Null);
            Assert.That(document.visualTreeAsset.name, Is.EqualTo("Hub"));

            foreach (string name in s_namedElements)
            {
                Assert.That(document.rootVisualElement.Q(name), Is.Not.Null, "missing element " + name);
            }

            VisualElement tabRow = document.rootVisualElement.Q("tab-row");
            Assert.That(tabRow.childCount, Is.EqualTo(s_tabOrder.Length));
            for (int i = 0; i < s_tabOrder.Length; i++)
            {
                Assert.That(tabRow[i].name, Is.EqualTo(s_tabOrder[i]), "tab order (reference 06)");
            }

            Assert.That(document.rootVisualElement.Q("tab-shop").Q("tab-lock"), Is.Not.Null);
            Assert.That(document.rootVisualElement.Q("tab-collection").Q("tab-lock"), Is.Not.Null);

            // One persistent EventSystem on the ProjectContext serves every scene, so a scene that unloads never takes
            // another scene's UI Toolkit panel handlers with it (DEC-188, DEC-199).
            EventSystem[] eventSystems = Object.FindObjectsOfType<EventSystem>();
            Assert.That(eventSystems, Has.Length.EqualTo(1), "exactly one EventSystem");
            Assert.That(eventSystems[0].transform.parent, Is.SameAs(ProjectContext.Instance.transform));
            InputSystemUIInputModule module = eventSystems[0].GetComponent<InputSystemUIInputModule>();
            Assert.That(module, Is.Not.Null);
            Assert.That(module.actionsAsset, Is.Not.Null);
        }

        private static void AssertHomeBound()
        {
            VisualElement root = HubTestSupport.Root();
            var container = HubTestSupport.HubScope().Container;
            Assert.That(container.TryResolve(out LevelCatalog levels), Is.True);
            Assert.That(container.TryResolve(out IPlayerProgress progress), Is.True);
            Assert.That(container.TryResolve(out CurrencyCatalog catalog), Is.True);

            int index = PlayerProgress.GetPlayableLevelIndex(progress.SavedLevelIndex.Value, levels.Count);
            Button levelButton = root.Q<Button>("level-button");
            // D-010: the level label is a "level-button-label" child (a two-layer heavy-text pair), not the
            // Button's own text.
            Label levelButtonLabel = levelButton.Q<Label>("level-button-label");
            Assert.That(levelButtonLabel.text, Is.EqualTo(index < 0 ? "Level -" : "Level " + (index + 1)));
            Assert.That(levelButton.enabledSelf, Is.EqualTo(index >= 0));

            // DEC-143: the holders are authored in Home.uxml (life, coin), not instantiated per bind, so the
            // count never grows; a second bind would only show/hide them again, never add or duplicate one.
            VisualElement holders = root.Q("currency-holders");
            Assert.That(holders.childCount, Is.EqualTo(2), "Home.uxml authors exactly the life and coin holders");
            for (int i = 0; i < holders.childCount; i++)
            {
                VisualElement holder = holders[i];
                bool claimed = false;
                for (int c = 0; c < catalog.Count; c++)
                {
                    CurrencyDefinition definition = catalog.GetAt(c);
                    if (definition.ShowOnHome && holder.name == "currency-holder-" + definition.Id)
                    {
                        claimed = true;
                        break;
                    }
                }

                DisplayStyle expected = claimed ? DisplayStyle.Flex : DisplayStyle.None;
                Assert.That(holder.resolvedStyle.display, Is.EqualTo(expected),
                    holder.name + " should be shown only when a Show On Home currency claims it");
            }

            Assert.That(HubTestSupport.FindScreen().IsBound, Is.True);
        }
    }
}
