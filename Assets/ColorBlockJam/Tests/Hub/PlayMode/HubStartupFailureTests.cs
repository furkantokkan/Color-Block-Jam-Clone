using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Haptics;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Hub.Settings;
using ColorBlockJam.Hub.Tabs;
using NUnit.Framework;
using Onity.DI;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Runs HubSceneInstaller's startup wait with a failed project scope (faulted or cancelled ready task) and with
    /// a scope that has no LevelCatalog, and proves Home then shows "Level -" disabled without errors (AC-10).
    /// </summary>
    /// <remarks>
    /// The loaded Hub has already shown Home once, so each test clones the scene's Hub object (UIDocument, HubScreen,
    /// installer) and drives the clone's installer; the original screen is disabled first so the scene's presenter
    /// can bind to the clone. The clone is destroyed at the end, which unbinds it.
    /// </remarks>
    public sealed class HubStartupFailureTests
    {
        private const float k_timeoutSeconds = 5f;
        private static readonly Regex s_unavailableWarning = new Regex("project scope or its level database is unavailable");

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Every test here loads Hub; leave no Hub scene or its EventSystem for a later PlayMode suite.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator FaultedProjectScope_ShowsLevelDashDisabled()
        {
            Task faulted = Task.FromException(new InvalidOperationException("Simulated project scope failure."));
            yield return ShowWithFailedStartup(faulted, null);
        }

        [UnityTest]
        public IEnumerator CancelledProjectScope_ShowsLevelDashDisabled()
        {
            Task cancelled = Task.FromCanceled(new CancellationToken(true));
            yield return ShowWithFailedStartup(cancelled, null);
        }

        [UnityTest]
        public IEnumerator MissingLevelDatabase_ShowsLevelDashDisabled()
        {
            // An empty container stands for a ready scope that bound no LevelCatalog.
            using (var emptyScope = new OnityContainer())
            {
                yield return ShowWithFailedStartup(Task.CompletedTask, emptyScope);
            }
        }

        /// <param name="resolver">Null uses the Hub scene container (which resolves the level database).</param>
        private static IEnumerator ShowWithFailedStartup(Task projectReady, IResolver resolver)
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            OnityContainer scene = HubTestSupport.HubScope().Container;
            HubScreen original = HubTestSupport.FindScreen();

            GameObject clone = Object.Instantiate(original.gameObject);
            try
            {
                original.enabled = false;
                Assert.That(original.IsBound, Is.False, "the original screen released the presenter");

                HubScreen screen = clone.GetComponent<HubScreen>();
                HubSceneInstaller installer = clone.GetComponent<HubSceneInstaller>();
                Assert.That(screen.IsBound, Is.False, "the clone has not been shown yet");
                screen.Construct(scene.Resolve<HomePresenter>(), scene.Resolve<SettingsOverlay>(),
                    scene.Resolve<HubNavigator>(), scene.Resolve<HubBackInput>(), scene.Resolve<PreLevelPresenter>(),
                    scene.Resolve<BoosterPurchaseOverlay>(), scene.Resolve<DummyCurrencyReward>(),
                    scene.Resolve<HubTabPresenter>(), scene.Resolve<IHaptics>());

                LogAssert.Expect(LogType.Warning, s_unavailableWarning);
                Task show = installer.ShowWhenReadyAsync(projectReady, resolver ?? scene, CancellationToken.None);
                float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
                while (!show.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.That(show.Status, Is.EqualTo(TaskStatus.RanToCompletion), "the startup wait ends without an exception");
                Assert.That(screen.IsBound, Is.True, "Home binds once with the unavailable level count");

                VisualElement root = clone.GetComponent<UIDocument>().rootVisualElement;
                Button levelButton = root.Q<Button>(HomeView.LevelButtonName);
                // D-010: the level label is a "level-button-label" child (a two-layer heavy-text pair), not the
                // Button's own text.
                Label levelButtonLabel = levelButton.Q<Label>(HomeView.LevelButtonLabelName);
                Assert.That(levelButtonLabel.text, Is.EqualTo("Level -"));
                Assert.That(levelButton.enabledSelf, Is.False);
                root.Query(className: HomeView.RouteNodeClass).ForEach(node =>
                    Assert.That(node.style.visibility.value, Is.EqualTo(Visibility.Hidden), "no route without levels"));

                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.Destroy(clone);
            }
        }
    }
}
