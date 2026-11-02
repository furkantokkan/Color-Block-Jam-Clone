using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Hub.Settings;
using ColorBlockJam.Hub.Tabs;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.UI.Purchase;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using Onity.DI;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Writes the pre-level and purchase captures to docs/verification/hub-pre-level (contract scenario 10) at
    /// 1080 x 1920 and at 1080 x 2400 with a simulated notch (top 110, bottom 70). Explicit, and ignored unless the
    /// Editor process has CBJ_HUB_CAPTURE=1, so an assembly-wide run never rewrites them.
    /// </summary>
    /// <remarks>
    /// Editor play starts with 0 coins (in-memory store, DEC-129), so a real purchase could only fail. Each resolution
    /// therefore renders a clone of the Hub object bound to its own presenters over an isolated PlayerSaveService with
    /// a seeded in-memory snapshot (15 coins, Extra Time 2, starting Rocket 0): the same views, UXML, USS and presenters as the
    /// scene, with real purchases through the real save service. The clone renders into a capture PanelSettings, so
    /// the scene's own Hub is untouched; everything the harness makes is disposed at the end.
    /// </remarks>
    [Explicit("Writes verification captures into docs/verification/hub-pre-level; run by name.")]
    [Category("Capture")]
    public sealed class PreLevelCaptureTests
    {
        private const float k_settleSeconds = 0.6f;
        private const float k_timeoutSeconds = 5f;
        private const string k_captureVariable = "CBJ_HUB_CAPTURE";

        private const string k_seedJson = "{\"formatVersion\":1,\"revision\":1,\"currentLevelIndex\":0,"
                                          + "\"currencies\":{\"life\":5,\"coin\":15},"
                                          + "\"boosterStock\":{\"TimeFreeze\":1,\"Rocket\":1,\"StartingRocket\":0,\"Hammer\":1,\"Vacuum\":1,\"ExtraTime\":2},"
                                          + "\"settings\":{\"music\":true,\"sound\":true,\"vibration\":true}}";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A skipped (Ignored) run never loads Hub, so this is a no-op then; a real capture run leaves no Hub
            // scene or its EventSystem for a later PlayMode suite. The capture's own isolated PlayerSaveService,
            // clone and render textures are already disposed by the test's own finally block.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator CapturePreLevelAndPurchase()
        {
            if (System.Environment.GetEnvironmentVariable(k_captureVariable) != "1")
            {
                Assert.Ignore("Set the environment variable " + k_captureVariable + "=1 in the Editor process to write captures.");
            }

            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "verification", "hub-pre-level"));
            Directory.CreateDirectory(folder);

            InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
            module.enabled = false;
            HubScreen original = HubTestSupport.FindScreen();
            original.enabled = false;
            try
            {
                yield return CaptureSet(folder, 1080, 1920, 0, 0, "1080x1920");
                yield return CaptureSet(folder, 1080, 2400, 110, 70, "1080x2400-notch");
            }
            finally
            {
                original.enabled = true;
                module.enabled = true;
            }
        }

        private static IEnumerator CaptureSet(string folder, int width, int height, int topInset, int bottomInset, string suffix)
        {
            OnityContainer scene = HubTestSupport.HubScope().Container;
            CurrencyCatalog currencies = scene.Resolve<CurrencyCatalog>();
            BoosterCatalog boosters = scene.Resolve<BoosterCatalog>();
            HubNavigationCatalog tabs = scene.Resolve<HubNavigationCatalog>();
            int levelCount = scene.Resolve<LevelCatalog>().Count;

            var service = new PlayerSaveService(new MemoryPlayerSnapshotStore(k_seedJson), currencies, boosters);
            var harness = new List<System.IDisposable>();
            GameObject clone = Object.Instantiate(HubTestSupport.FindScreen().gameObject);
            RenderTexture target = null;
            PanelSettings capturePanel = null;
            try
            {
                Task load = service.LoadAsync(CancellationToken.None);
                yield return WaitFor(load);

                var navigator = new HubNavigator();
                var settingsPresenter = new SettingsPopupPresenter(service);
                var purchasePresenter = new BoosterPurchasePresenter(service, service);
                var purchaseOverlay = new BoosterPurchaseOverlay(purchasePresenter, navigator);
                var launcher = new LevelLauncher(new NoTransition());
                var preLevel = new PreLevelPresenter(service, service, boosters, navigator, launcher);
                var home = new HomePresenter(service, service, currencies, navigator);
                var tabPresenter = new HubTabPresenter(tabs, service, navigator);
                var settings = new SettingsOverlay(settingsPresenter, navigator);
                var back = new HubBackInput(navigator);
                harness.AddRange(new System.IDisposable[]
                {
                    back, preLevel, purchaseOverlay, purchasePresenter, settings, settingsPresenter, tabPresenter, home, launcher,
                    navigator
                });

                HubScreen screen = clone.GetComponent<HubScreen>();
                UIDocument document = clone.GetComponent<UIDocument>();
                target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                target.Create();
                capturePanel = Object.Instantiate(document.panelSettings);
                capturePanel.name = "HubPanelSettings (capture)";
                capturePanel.targetTexture = target;
                capturePanel.clearColor = true;
                capturePanel.colorClearValue = Color.black;
                document.panelSettings = capturePanel;
                // The capture needs no plus-button reward and no button haptics.
                screen.Construct(home, settings, navigator, back, preLevel, purchaseOverlay, null, tabPresenter, null);

                // The clone copied the original's disabled HubScreen; enabling it before Show lets Show bind.
                screen.enabled = true;
                screen.SetSafeAreaSource(new Rect(0f, bottomInset, width, height - topInset - bottomInset), new Vector2(width, height));
                screen.Show(levelCount);
                Assert.That(screen.IsBound, Is.True, "the capture clone bound");

                // Synthetic clicks aim at the element's laid-out bounds, so the first layout pass must run first.
                yield return HubTestSupport.WaitSeconds(k_settleSeconds);
                VisualElement root = document.rootVisualElement;
                Click(root.Q<Button>("level-button"));
                Assert.That(navigator.IsOpen(HubOverlayId.PreLevel), Is.True, "Level opens the pre-level popup");
                yield return Capture(target, Path.Combine(folder, "pre-level-" + suffix + "-empty-stock.png"));

                // Rocket has no stock: its selector opens the purchase popup (reference 01) with 15 coins.
                List<VisualElement> selectors = root.Q(PreLevelView.ScreenName).Query(className: PreLevelView.SelectorClass).ToList();
                Click(selectors[1].Q<Button>(className: PreLevelView.SelectorButtonClass));
                Assert.That(navigator.IsOpen(HubOverlayId.BoosterPurchase), Is.True, "the empty selector opens the purchase popup");
                Assert.That(root.Q(PreLevelView.ScreenName).style.display.value, Is.EqualTo(DisplayStyle.None),
                    "the purchase popup shows alone: the pre-level popup is covered (reference 8)");
                yield return Capture(target, Path.Combine(folder, "purchase-" + suffix + ".png"));

                Button buy = root.Q<Button>(BoosterPurchaseView.BuyButtonName);
                Click(buy);
                yield return WaitFor(purchasePresenter.PendingPurchase);
                Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1), "15 coins buy one Rocket");
                yield return Capture(target, Path.Combine(folder, "purchase-" + suffix + "-purchased.png"));

                Click(buy);
                yield return WaitFor(purchasePresenter.PendingPurchase);
                Assert.That(root.Q<Label>(BoosterPurchaseView.MessageName).text, Is.EqualTo("Not enough coins"));
                Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1), "the failed purchase changes nothing");
                yield return Capture(target, Path.Combine(folder, "purchase-" + suffix + "-not-enough-coins.png"));

                // Close returns to the pre-level popup, whose Rocket selector now shows its count, unselected.
                Click(root.Q<Button>(BoosterPurchaseView.CloseButtonName));
                Assert.That(navigator.IsOpen(HubOverlayId.BoosterPurchase), Is.False);
                Assert.That(root.Q(PreLevelView.ScreenName).style.display.value, Is.EqualTo(DisplayStyle.Flex),
                    "closing the purchase popup shows the pre-level popup again");
                Assert.That(selectors[1].ClassListContains(PreLevelView.SelectedClass), Is.False, "a purchase never auto-selects");
                yield return Capture(target, Path.Combine(folder, "pre-level-" + suffix + ".png"));

                Click(selectors[0].Q<Button>(className: PreLevelView.SelectorButtonClass));
                Click(selectors[1].Q<Button>(className: PreLevelView.SelectorButtonClass));
                Assert.That(preLevel.IsSelected(0) && preLevel.IsSelected(1), Is.True);
                yield return Capture(target, Path.Combine(folder, "pre-level-" + suffix + "-selected.png"));
            }
            finally
            {
                // Immediately, so the clone's OnDisable unbinds everything before the harness is disposed.
                Object.DestroyImmediate(clone);
                for (int i = 0; i < harness.Count; i++)
                {
                    harness[i].Dispose();
                }

                service.Dispose();
                if (capturePanel != null)
                {
                    Object.Destroy(capturePanel);
                }

                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
            }

            yield return null;
        }

        private static IEnumerator Capture(RenderTexture target, string path)
        {
            yield return HubTestSupport.WaitSeconds(k_settleSeconds);
            yield return new WaitForEndOfFrame();

            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.Destroy(pixels);
            Debug.Log("Hub capture written: " + path);
        }

        private static IEnumerator WaitFor(Task task)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task did not complete in time");
            Assert.That(task.IsFaulted, Is.False, task.Exception?.ToString());
        }

        private static void Click(VisualElement element)
        {
            HubTestSupport.Press(element);
            HubTestSupport.Release(element);
        }

        /// <summary>A scene transition that never runs: the capture clone must not leave Hub.</summary>
        private sealed class NoTransition : ILevelSceneTransition
        {
            public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task TransitionAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
    }
}
