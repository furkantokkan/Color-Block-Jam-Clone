using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
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
    /// Writes the Home and Settings verification captures to docs/verification/hub-home and hub-settings (Home
    /// contract scenarios 3, 4 and 7; Settings contract scenarios 2 and 10) by rendering the Hub panel into a
    /// texture of the target size. Explicit, and ignored unless the Editor process has CBJ_HUB_CAPTURE=1, so an
    /// assembly-wide run never rewrites the committed captures.
    /// </summary>
    [Explicit("Writes verification captures into docs/verification; run by name.")]
    [Category("Capture")]
    public sealed class HubCaptureTests
    {
        private const float k_settleSeconds = 0.6f;
        private const string k_captureVariable = "CBJ_HUB_CAPTURE";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // A skipped (Ignored) run never loads Hub, so this is a no-op then; a real capture run leaves no Hub
            // scene or its EventSystem for a later PlayMode suite.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator CaptureHome()
        {
            if (System.Environment.GetEnvironmentVariable(k_captureVariable) != "1")
            {
                Assert.Ignore("Set the environment variable " + k_captureVariable + "=1 in the Editor process to write captures.");
            }

            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            HubScreen screen = HubTestSupport.FindScreen();
            UIDocument document = screen.GetComponent<UIDocument>();
            PanelSettings original = document.panelSettings;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "verification", "hub-home"));
            Directory.CreateDirectory(folder);

            // Real editor mouse input would clear a synthetic press; the UI input module pauses while capturing.
            InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
            module.enabled = false;
            try
            {
                yield return Capture(document, screen, 1080, 1920, 0, 0, null, Path.Combine(folder, "home-1080x1920.png"));
                yield return Capture(document, screen, 1080, 2400, 110, 70, null, Path.Combine(folder, "home-1080x2400-notch.png"));
                yield return Capture(document, screen, 1080, 1920, 0, 0, "level-button", Path.Combine(folder, "home-1080x1920-level-pressed.png"));
                yield return Capture(document, screen, 1080, 1920, 0, 0, "tab-shop", Path.Combine(folder, "home-1080x1920-shop-pressed.png"));
            }
            finally
            {
                module.enabled = true;
                RestorePanel(document, original);
            }
        }

        [UnityTest]
        public IEnumerator CaptureSettings()
        {
            if (System.Environment.GetEnvironmentVariable(k_captureVariable) != "1")
            {
                Assert.Ignore("Set the environment variable " + k_captureVariable + "=1 in the Editor process to write captures.");
            }

            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            HubScreen screen = HubTestSupport.FindScreen();
            UIDocument document = screen.GetComponent<UIDocument>();
            PanelSettings original = document.panelSettings;
            VisualElement root = document.rootVisualElement;
            OnityContainer container = HubTestSupport.HubScope().Container;
            IPlayerSettings settings = container.Resolve<IPlayerSettings>();
            SettingsPopupPresenter presenter = container.Resolve<SettingsPopupPresenter>();
            bool[] originals = new bool[s_captureStates.Length];
            for (int i = 0; i < s_captureStates.Length; i++)
            {
                originals[i] = settings.Get(s_captureStates[i]).Value;
            }

            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "verification", "hub-settings"));
            Directory.CreateDirectory(folder);

            InputSystemUIInputModule module = Object.FindObjectOfType<InputSystemUIInputModule>();
            module.enabled = false;
            try
            {
                // As reference 19: Vibration OFF, Sound and Music ON, so one image shows both toggle states.
                yield return SetAll(settings, false, true, true);

                Click(root.Q<Button>("settings-button"));
                yield return HubTestSupport.WaitUntil(() => root.Q(SettingsPopupView.ScreenName).resolvedStyle.display == DisplayStyle.Flex);

                yield return Capture(document, screen, 1080, 1920, 0, 0, null, Path.Combine(folder, "settings-1080x1920.png"));
                yield return Capture(document, screen, 1080, 2400, 110, 70, null, Path.Combine(folder, "settings-1080x2400-notch.png"));
                yield return Capture(document, screen, 1080, 1920, 0, 0, SettingsPopupView.LegalTermsButtonName,
                    Path.Combine(folder, "settings-1080x1920-legal-pressed.png"));

                // Component preview only: the shared popup with HOME shown, as the Game pause menu will host it
                // (reference 20). The Hub itself keeps HOME hidden; it is hidden again below.
                presenter.SetHomeVisible(true);
                yield return Capture(document, screen, 1080, 1920, 0, 0, null, Path.Combine(folder, "settings-1080x1920-home-shown-preview.png"));
            }
            finally
            {
                presenter.SetHomeVisible(false);
                module.enabled = true;
                RestorePanel(document, original);
            }

            // Owner: this test, which waits for each restoring save. The project-scoped service outlives Hub.
            yield return SetAll(settings, originals[0], originals[1], originals[2]);
        }

        // Vibration, Sound, Music: the row order of reference 19.
        private static readonly PlayerSettingId[] s_captureStates =
        {
            PlayerSettingId.Vibration, PlayerSettingId.Sound, PlayerSettingId.Music
        };

        private static IEnumerator SetAll(IPlayerSettings settings, bool vibration, bool sound, bool music)
        {
            bool[] values = { vibration, sound, music };
            for (int i = 0; i < s_captureStates.Length; i++)
            {
                if (settings.Get(s_captureStates[i]).Value == values[i])
                {
                    continue;
                }

                Task<PlayerSaveResult> save = settings.SetAsync(s_captureStates[i], values[i], CancellationToken.None);
                yield return HubTestSupport.WaitUntil(() => save.IsCompleted);
                Assert.That(save.IsCompleted && save.Result == PlayerSaveResult.Saved, Is.True, s_captureStates[i] + " save");
            }
        }

        private static void Click(VisualElement element)
        {
            HubTestSupport.Press(element);
            HubTestSupport.Release(element);
        }

        private static void RestorePanel(UIDocument document, PanelSettings original)
        {
            PanelSettings capturePanel = document.panelSettings;
            document.panelSettings = original;
            if (capturePanel != original)
            {
                RenderTexture captureTarget = capturePanel.targetTexture;
                Object.Destroy(capturePanel);
                if (captureTarget != null)
                {
                    captureTarget.Release();
                    Object.Destroy(captureTarget);
                }
            }
        }

        private static IEnumerator Capture(UIDocument document, HubScreen screen, int width, int height,
            int topInset, int bottomInset, string pressedName, string path)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            PanelSettings previous = document.panelSettings;
            PanelSettings capturePanel = Object.Instantiate(previous);
            capturePanel.name = "HubPanelSettings (capture)";
            capturePanel.targetTexture = target;
            capturePanel.clearColor = true;
            capturePanel.colorClearValue = Color.black;
            document.panelSettings = capturePanel;
            if (previous.targetTexture != null)
            {
                RenderTexture previousTarget = previous.targetTexture;
                Object.Destroy(previous);
                previousTarget.Release();
                Object.Destroy(previousTarget);
            }

            screen.SetSafeAreaSource(new Rect(0f, bottomInset, width, height - topInset - bottomInset), new Vector2(width, height));
            yield return HubTestSupport.WaitSeconds(k_settleSeconds);

            VisualElement pressed = pressedName != null ? document.rootVisualElement.Q(pressedName) : null;
            if (pressed != null)
            {
                HubTestSupport.Press(pressed);
                yield return HubTestSupport.WaitUntil(() => Mathf.Abs(pressed.resolvedStyle.scale.value.x - 0.92f) < 0.005f);
            }

            yield return new WaitForEndOfFrame();
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            Object.Destroy(pixels);
            Debug.Log("Hub capture written: " + path);

            if (pressed != null)
            {
                HubTestSupport.Release(pressed);
                yield return HubTestSupport.WaitUntil(() => Mathf.Abs(pressed.resolvedStyle.scale.value.x - 1f) < 0.005f);

                // The release is a click: the level button opens the pre-level popup, which later Home captures must
                // not show. Settings captures open their popup after this, so only a pre-level popup is closed here.
                IHubNavigator navigator = HubTestSupport.HubScope().Container.Resolve<IHubNavigator>();
                if (navigator.IsOpen(HubOverlayId.PreLevel))
                {
                    navigator.Close(HubOverlayId.PreLevel);
                }
            }
        }
    }
}
