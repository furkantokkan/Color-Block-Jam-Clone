using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using NUnit.Framework;
using Onity.DI;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Proves the Hub-to-Game launch end to end (contract scenarios 1, 5, 6, 7, 8 and 9): Level opens the pre-level
    /// popup with the Home level, close and Back return to Home, Play starts the level Hub prepared behind Home at once,
    /// with no fade, carrying the selected boosters into its opening (DEC-199), and a launch request for another level
    /// loads Game behind the fade and plays that level instead of its serialized index. Scenario 8 (narrowed 2026-09-27): Hub and the launcher spend
    /// nothing up to the moment the one transition starts; any debit belongs to the Game-scene opening
    /// (booster-gameplay-flow), so this suite asserts nothing about stock once the Game scene is ready.
    /// </summary>
    public sealed class HubLaunchTests
    {
        private const float k_timeoutSeconds = 20f;

        private InputSystemUIInputModule m_module;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_module != null)
            {
                m_module.enabled = true;
            }

            m_module = null;

            // No Hub (or Game, for LaunchRequest_PlaysTheRequestedIndex_InsteadOfTheSerializedOne) is left loaded
            // for a later PlayMode suite that loads its own scene and would otherwise find two EventSystems.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator LevelOpensThePreLevelPopup_CloseAndBackReturnHome_AndSpendNothing()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            OnityContainer hub = HubTestSupport.HubScope().Container;
            IHubNavigator navigator = hub.Resolve<IHubNavigator>();
            LevelCatalog levels = hub.Resolve<LevelCatalog>();
            IPlayerProgress progress = hub.Resolve<IPlayerProgress>();
            IBoosterInventory inventory = hub.Resolve<IBoosterInventory>();
            PlayerSaveService saveService = hub.Resolve<PlayerSaveService>();

            // Arranged explicitly: an earlier test in the run may have spent the shared stock, and a zero-stock
            // selector opens the purchase popup instead of selecting (DEC-85), which this test does not exercise.
            HubTestSupport.ArrangeStock(saveService, BoosterId.ExtraTime, 2);
            int expected = PlayerProgress.GetPlayableLevelIndex(progress.SavedLevelIndex.Value, levels.Count);
            int extraTime = inventory.GetStock(BoosterId.ExtraTime).Value;
            VisualElement root = HubTestSupport.Root();
            PauseInputModule();

            Click(root.Q<Button>("level-button"));
            VisualElement screen = root.Q(PreLevelView.ScreenName);
            yield return HubTestSupport.WaitUntil(() => screen.resolvedStyle.display == DisplayStyle.Flex);

            Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "Level opens the pre-level popup");
            Assert.That(root.Q<Label>(PreLevelView.TitleName).text, Is.EqualTo("LEVEL " + (expected + 1)));
            Assert.That(root.Q<Label>("level-button-label").text, Is.EqualTo("Level " + (expected + 1)), "same N as Home");
            Assert.That(root.Q<Label>(PreLevelView.SubtitleName).text, Is.EqualTo("Select Boosters:"));
            Assert.That(root.Q<Button>(PreLevelView.CloseButtonName), Is.Not.Null);
            Assert.That(root.Q<Button>(PreLevelView.PlayButtonName).enabledSelf, Is.True);
            List<VisualElement> selectors = screen.Query(className: PreLevelView.SelectorClass).ToList();
            Assert.That(selectors.Count, Is.EqualTo(2), "Extra Time and Rocket");
            Assert.That(selectors[0].Q<Label>(className: PreLevelView.SelectorLabelClass).text, Is.EqualTo("Extra Time"));
            Assert.That(selectors[1].Q<Label>(className: PreLevelView.SelectorLabelClass).text, Is.EqualTo("Rocket"));
            foreach (VisualElement selector in selectors)
            {
                Assert.That(selector.ClassListContains(PreLevelView.SelectedClass), Is.False, "both start unselected");
            }

            // A selection is cleared by the close button, and again by Back.
            Click(selectors[0].Q<Button>(className: PreLevelView.SelectorButtonClass));
            Click(root.Q<Button>(PreLevelView.CloseButtonName));
            Assert.That(navigator.OpenCount, Is.Zero, "close returns to Home");
            Assert.That(screen.resolvedStyle.display == DisplayStyle.None || screen.style.display.value == DisplayStyle.None, Is.True);

            Assert.That(navigator.OpenPreLevel(), Is.True);
            Assert.That(selectors[0].ClassListContains(PreLevelView.SelectedClass), Is.False, "the selection was cleared");
            Click(selectors[0].Q<Button>(className: PreLevelView.SelectorButtonClass));
            Assert.That(navigator.CloseTop(), Is.True, "Back closes the pre-level popup");
            Assert.That(navigator.OpenCount, Is.Zero);
            Assert.That(inventory.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(extraTime), "opening and selecting spend nothing");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PlayInHub_OpensThePreparedLevelAtOnce_WithTheShownLevel_AndCarriesTheSelection()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            OnityContainer hub = HubTestSupport.HubScope().Container;
            LevelCatalog levels = hub.Resolve<LevelCatalog>();
            IPlayerProgress progress = hub.Resolve<IPlayerProgress>();
            IBoosterInventory inventory = hub.Resolve<IBoosterInventory>();
            PreLevelPresenter preLevel = hub.Resolve<PreLevelPresenter>();
            PlayerSaveService saveService = hub.Resolve<PlayerSaveService>();
            LevelPreloader preloader = hub.Resolve<LevelPreloader>();
            IScreenFade fade = hub.Resolve<IScreenFade>();

            // Arranged explicitly so both selectors select regardless of what an earlier test in the run left
            // behind (DEC-78 seeds each stock only once per run); Editor play seeds no coins (DEC-129), so a
            // real purchase cannot arrange this instead.
            HubTestSupport.ArrangeStock(saveService, BoosterId.ExtraTime, 2);
            HubTestSupport.ArrangeStock(saveService, BoosterId.StartingRocket, 2);
            int expected = PlayerProgress.GetPlayableLevelIndex(progress.SavedLevelIndex.Value, levels.Count);
            Assume.That(expected, Is.GreaterThanOrEqualTo(0), "the active level JSON has no level");
            int extraTime = inventory.GetStock(BoosterId.ExtraTime).Value;
            int rocket = inventory.GetStock(BoosterId.StartingRocket).Value;
            string expectedId = levels.Manifest.GetLevelIdAt(expected);

            // Hub prepared the level its Level button offers in a hidden Game scene behind Home (DEC-199).
            Assert.That(preloader.IsPrepared(expected), Is.True, "Hub prepared the offered level behind Home");
            Scene prepared = SceneManager.GetSceneByName("Game");
            Assert.That(prepared.isLoaded, Is.True, "the prepared Game scene is loaded next to Hub");
            VisualElement root = HubTestSupport.Root();
            PauseInputModule();

            Click(root.Q<Button>("level-button"));
            VisualElement screen = root.Q(PreLevelView.ScreenName);
            yield return HubTestSupport.WaitUntil(() => screen.resolvedStyle.display == DisplayStyle.Flex);

            List<VisualElement> selectors = screen.Query(className: PreLevelView.SelectorClass).ToList();
            foreach (VisualElement selector in selectors)
            {
                Click(selector.Q<Button>(className: PreLevelView.SelectorButtonClass));
            }

            Assert.That(preLevel.IsSelected(0) && preLevel.IsSelected(1), Is.True, "both selectors selected (reference 10)");
            Assert.That(selectors[0].ClassListContains(PreLevelView.SelectedClass), Is.True);
            Assert.That(selectors[1].ClassListContains(PreLevelView.SelectedClass), Is.True);

            // Selecting boosters while Hub is loaded spends nothing (AC-8, narrowed 2026-09-27).
            Assert.That(inventory.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(extraTime), "selecting spends nothing while Hub is loaded");
            Assert.That(inventory.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(rocket), "selecting spends nothing while Hub is loaded");

            Button play = root.Q<Button>(PreLevelView.PlayButtonName);
            Click(play);
            Click(play);
            Assert.That(play.enabledSelf, Is.False, "Play is disabled once pressed; a second press does nothing");

            // The prepared level starts at once: it becomes the active scene and plays while Hub hides and unloads, with
            // no fade and no scene load (DEC-199).
            bool fadeShown = fade.IsVisible;
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline
                   && (SceneManager.GetActiveScene() != prepared || SceneManager.GetSceneByName("Hub").IsValid()))
            {
                fadeShown |= fade.IsVisible;
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene() == prepared, Is.True, "the prepared Game scene is the active scene");
            Assert.That(SceneManager.GetSceneByName("Hub").IsValid(), Is.False, "Hub unloaded behind it");
            Assert.That(fadeShown, Is.False, "the prepared level opened with no fade");
            LevelSession session = null;
            yield return WaitForAttempt(found => session = found);

            // The Game-scene opening (booster-gameplay-flow) owns any debit from here on (AC-8); this suite makes
            // no stock assertion once the Game scene is ready.
            Assert.That(session.Attempt.Level.LevelId, Is.EqualTo(expectedId), "Game opens once with the level Hub launched");
            Assert.That(session.State.Value, Is.EqualTo(SessionState.Playing));
            Assert.That(session.IsOpening.Value, Is.True, "the selected starting boosters play their opening (B-16)");
            Assert.That(OnitySceneTransitionStore.TryConsumeActiveEnterData(out LevelLaunchRequest leftover), Is.False,
                "the request went to the prepared level, not through the scene store");
            Assert.That(leftover, Is.Null);
            Assert.That(Object.FindObjectsOfType<SceneContext>(), Has.Length.EqualTo(1), "Hub unloaded; one Game scope");
        }

        [UnityTest]
        public IEnumerator LaunchRequest_PlaysTheRequestedIndex_InsteadOfTheSerializedOne()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            OnityContainer hub = HubTestSupport.HubScope().Container;
            LevelCatalog levels = hub.Resolve<LevelCatalog>();
            Assume.That(levels.Count, Is.GreaterThan(1), "needs a level other than the serialized index 0");
            int target = levels.Count - 1;
            string expectedId = levels.Manifest.GetLevelIdAt(target);
            ILevelLauncher launcher = hub.Resolve<ILevelLauncher>();

            // No starting boosters: this test only proves the requested index plays instead of the serialized one
            // (AC-1.1), so it never spends the shared stock other Hub PlayMode tests depend on.
            Task<LevelLaunchResult> launch = launcher.LaunchAsync(
                new LevelLaunchRequest(target, null), CancellationToken.None);
            Task<LevelLaunchResult> second = launcher.LaunchAsync(new LevelLaunchRequest(0, null), CancellationToken.None);
            Assert.That(second.IsCompleted && second.Result == LevelLaunchResult.Ignored, Is.True, "one transition in flight");

            yield return WaitForScene("Game");
            LevelSession session = null;
            yield return WaitForAttempt(found => session = found);

            Assert.That(session.Attempt.Level.LevelId, Is.EqualTo(expectedId));
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!launch.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(launch.IsCompleted, Is.True);
            Assert.That(launch.Result, Is.EqualTo(LevelLaunchResult.Completed).Or.EqualTo(LevelLaunchResult.Cancelled),
                "Hub unload may cancel the wait after Game has loaded");
        }

        private void PauseInputModule()
        {
            // The editor's real mouse would interfere with the synthetic clicks.
            m_module = Object.FindObjectOfType<InputSystemUIInputModule>();
            m_module.enabled = false;
        }

        private static void Click(VisualElement element)
        {
            HubTestSupport.Press(element);
            HubTestSupport.Release(element);
        }

        private static IEnumerator WaitForScene(string sceneName)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (SceneManager.GetActiveScene().name != sceneName && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(sceneName));
        }

        private static IEnumerator WaitForAttempt(Action<LevelSession> found)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            LevelSession session = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                // The active Game scene's scope: Hub, or a Game scene prepared next to it, may still be loaded.
                SceneContext scope = SceneManager.GetActiveScene().name == "Game"
                    ? SceneScopes.Find(SceneManager.GetActiveScene())
                    : null;
                if (scope != null && scope.Container != null && scope.Container.TryResolve(out session)
                    && session.Attempt != null)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(session, Is.Not.Null, "the Game scene has no LevelSession");
            Assert.That(session.Attempt, Is.Not.Null, "the Game attempt never built");
            found(session);
        }
    }
}
