using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Composition;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.HubScene
{
    /// <summary>
    /// Proves the shared Settings popup through the real Hub scene (contract scenarios 1, 2, 4, 5, 6 and 9): the
    /// settings button opens reference 19's popup above Home with HOME hidden, the scrim blocks Home, close, outside
    /// tap and Back return to Home, a toggle tap commits through the save service and survives reopening Hub, and the
    /// four placeholders only give press feedback.
    /// </summary>
    /// <remarks>
    /// The UI input module is paused while the test sends its own presses: the editor's real mouse would otherwise
    /// move the captured pointer and clear :active (same pattern as HubSceneTests).
    /// </remarks>
    public sealed class HubSettingsTests
    {
        private const float k_knobTravel = 108f;

        private static readonly string[] s_placeholderNames =
        {
            SettingsPopupView.LegalTermsButtonName, SettingsPopupView.RestorePurchasesButtonName,
            SettingsPopupView.SupportButtonName, SettingsPopupView.LanguageButtonName
        };

        private InputSystemUIInputModule m_module;
        private Task m_restore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
            PauseInputModule();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (m_module != null)
            {
                m_module.enabled = true;
            }

            // A test that changed a toggle restores the shared in-memory save before the next test starts.
            if (m_restore != null)
            {
                Task restore = m_restore;
                m_restore = null;
                yield return HubTestSupport.WaitUntil(() => restore.IsCompleted);
                Assert.That(restore.IsCompleted, Is.True, "the restoring save never finished");
            }

            // Every test here loads Hub; leave no Hub scene or its EventSystem for a later PlayMode suite.
            yield return HubTestSupport.ReturnToCleanScene();
        }

        [UnityTest]
        public IEnumerator SettingsButton_OpensThePopupAboveHome_WithEveryElement_AndHomeHidden()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();
            VisualElement settings = root.Q(SettingsPopupView.ScreenName);

            Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(Navigator().IsOpen(HubOverlayId.Settings), Is.True);
            Assert.That(settings.Q<Label>(SettingsPopupView.TitleName).text, Is.EqualTo("SETTINGS"));
            Assert.That(settings.Q<Button>(SettingsPopupView.CloseButtonName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            foreach (string row in new[] { "vibration", "sound", "music" })
            {
                VisualElement icon = settings.Q(row + "-icon");
                Assert.That(icon.resolvedStyle.backgroundImage.texture != null || icon.resolvedStyle.backgroundImage.sprite != null,
                    Is.True, row + " icon");
                Assert.That(settings.Q<Button>(row + "-toggle"), Is.Not.Null, row + " toggle");
            }

            // Rows top to bottom: Vibration, Sound, Music; then the four placeholders in reference 19's order.
            Assert.That(settings.Q("vibration-row").worldBound.y, Is.LessThan(settings.Q("sound-row").worldBound.y));
            Assert.That(settings.Q("sound-row").worldBound.y, Is.LessThan(settings.Q("music-row").worldBound.y));
            float previous = settings.Q("music-row").worldBound.y;
            foreach (string name in s_placeholderNames)
            {
                Button button = settings.Q<Button>(name);
                Assert.That(button.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), name);
                Assert.That(button.worldBound.y, Is.GreaterThan(previous), name + " order");
                previous = button.worldBound.y;
            }

            Assert.That(settings.Q<Button>(SettingsPopupView.HomeButtonName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None),
                "the Hub keeps HOME hidden");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator WhileOpen_EveryHomeButtonIsUnderTheScrim()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();
            VisualElement settings = root.Q(SettingsPopupView.ScreenName);
            var homeButtons = root.Q(HubScreen.HomeScreenName).Query<Button>().ToList();
            Assert.That(homeButtons.Count, Is.GreaterThan(0));

            // AC-2.6: a press anywhere on Home lands on Settings (scrim or popup), never on a Home button.
            foreach (Button button in homeButtons)
            {
                VisualElement picked = root.panel.Pick(button.worldBound.center);
                Assert.That(picked, Is.Not.Null, button.name);
                Assert.That(IsInside(picked, settings), Is.True, button.name + " is covered while Settings is open");
            }
        }

        [UnityTest]
        public IEnumerator Toggles_ShowTheCommittedValues_WithDistinctOnAndOffStates()
        {
            yield return OpenSettings();
            VisualElement settings = HubTestSupport.Root().Q(SettingsPopupView.ScreenName);
            IPlayerSettings playerSettings = Resolve<IPlayerSettings>();

            foreach (PlayerSettingId id in new[] { PlayerSettingId.Vibration, PlayerSettingId.Sound, PlayerSettingId.Music })
            {
                Button toggle = settings.Q<Button>(ToggleName(id));
                bool isOn = playerSettings.Get(id).Value;
                yield return HubTestSupport.WaitUntil(() => ToggleSettled(toggle, isOn));
                AssertToggle(toggle, isOn, id.ToString());
            }
        }

        [UnityTest]
        public IEnumerator ToggleTap_FlipsAtOnce_CommitsAndSurvivesReopeningHub()
        {
            yield return OpenSettings();
            IPlayerSettings playerSettings = Resolve<IPlayerSettings>();
            bool original = playerSettings.Get(PlayerSettingId.Music).Value;
            Button toggle = HubTestSupport.Root().Q<Button>(SettingsPopupView.MusicToggleName);

            try
            {
                Click(toggle);
                Assert.That(toggle.ClassListContains(SettingsPopupView.ToggleOnClass), Is.EqualTo(!original), "flips at once");
                yield return HubTestSupport.WaitUntil(() => ToggleSettled(toggle, !original));
                AssertToggle(toggle, !original, "Music after the tap");

                yield return HubTestSupport.WaitUntil(() => playerSettings.Get(PlayerSettingId.Music).Value == !original);
                Assert.That(playerSettings.Get(PlayerSettingId.Music).Value, Is.EqualTo(!original), "committed");

                // Leave and reopen Hub on the same app scope and store (AC-2.5, DEC-15).
                yield return HubTestSupport.LoadHubAndWaitForHome("Hub");
                PauseInputModule();
                yield return OpenSettings();
                Button reopened = HubTestSupport.Root().Q<Button>(SettingsPopupView.MusicToggleName);
                yield return HubTestSupport.WaitUntil(() => ToggleSettled(reopened, !original));
                AssertToggle(reopened, !original, "Music after reopening Hub");
            }
            finally
            {
                // Owner: this test; TearDown waits for it. The project-scoped service outlives the Hub scene.
                m_restore = playerSettings.SetAsync(PlayerSettingId.Music, original, CancellationToken.None);
            }
        }

        [UnityTest]
        public IEnumerator CloseButton_ClosesSettings_AndHomeIsInteractiveAgain()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();

            Click(root.Q<Button>(SettingsPopupView.CloseButtonName));
            yield return WaitUntilClosed(root);

            AssertClosed(root);
        }

        [UnityTest]
        public IEnumerator OutsideTap_ClosesSettings_ButATapOnThePopupDoesNot()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();
            VisualElement scrim = root.Q(SettingsPopupView.ScrimName);
            VisualElement popup = root.Q(SettingsPopupView.PopupName);

            // A tap on the shell between the controls is not an outside tap: Settings stays open.
            Rect popupBounds = popup.worldBound;
            Vector2 popupPoint = new Vector2(popupBounds.xMin + 70f, popupBounds.center.y);
            Assert.That(root.panel.Pick(popupPoint), Is.SameAs(popup), "the popup point hits no control");
            HubTestSupport.ClickAt(root.panel, popupPoint);
            yield return null;
            Assert.That(root.Q(SettingsPopupView.ScreenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "popup tap");
            Assert.That(Navigator().IsOpen(HubOverlayId.Settings), Is.True);

            // A tap on the scrim outside the popup closes it (AC-2.3a). The panel picks the element under the point.
            Rect scrimBounds = scrim.worldBound;
            Vector2 scrimPoint = new Vector2(scrimBounds.xMin + 20f, scrimBounds.yMax - 20f);
            Assert.That(root.panel.Pick(scrimPoint), Is.SameAs(scrim), "the scrim is on top outside the popup");
            HubTestSupport.ClickAt(root.panel, scrimPoint);
            yield return WaitUntilClosed(root);

            AssertClosed(root);
        }

        [UnityTest]
        public IEnumerator Back_ClosesSettings_ThenDoesNothingOnHome()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();
            HubBackInput back = Resolve<HubBackInput>();
            Assert.That(back.IsEnabled, Is.True);

            Assert.That(back.Back(), Is.True);
            yield return WaitUntilClosed(root);

            AssertClosed(root);
            Assert.That(back.Back(), Is.False, "Back on Home does nothing");
        }

        [UnityTest]
        public IEnumerator Placeholders_GiveFeedbackOnly()
        {
            yield return OpenSettings();
            VisualElement root = HubTestSupport.Root();
            IPlayerSettings playerSettings = Resolve<IPlayerSettings>();
            bool vibration = playerSettings.Get(PlayerSettingId.Vibration).Value;
            bool sound = playerSettings.Get(PlayerSettingId.Sound).Value;
            bool music = playerSettings.Get(PlayerSettingId.Music).Value;

            foreach (string name in s_placeholderNames)
            {
                Button button = root.Q<Button>(name);
                float restTint = button.resolvedStyle.unityBackgroundImageTintColor.r;

                HubTestSupport.Press(button);
                yield return HubTestSupport.WaitUntil(() => Mathf.Abs(button.resolvedStyle.scale.value.x - 0.92f) < 0.005f
                                                            && button.resolvedStyle.unityBackgroundImageTintColor.r < restTint - 0.05f);
                Assert.That(button.resolvedStyle.scale.value.x, Is.EqualTo(0.92f).Within(0.005f), name + " scale while pressed");
                Assert.That(button.resolvedStyle.unityBackgroundImageTintColor.r, Is.LessThan(restTint - 0.05f), name + " darkens");

                HubTestSupport.Release(button);
                yield return HubTestSupport.WaitUntil(() => Mathf.Abs(button.resolvedStyle.scale.value.x - 1f) < 0.005f);
                Assert.That(button.resolvedStyle.scale.value.x, Is.EqualTo(1f).Within(0.005f), name + " scale after release");

                Assert.That(Navigator().OpenCount, Is.EqualTo(1), name + ": Settings stays the only overlay");
                Assert.That(Navigator().IsOpen(HubOverlayId.Settings), Is.True, name);
            }

            Assert.That(playerSettings.Get(PlayerSettingId.Vibration).Value, Is.EqualTo(vibration));
            Assert.That(playerSettings.Get(PlayerSettingId.Sound).Value, Is.EqualTo(sound));
            Assert.That(playerSettings.Get(PlayerSettingId.Music).Value, Is.EqualTo(music));
            LogAssert.NoUnexpectedReceived();
        }

        private IEnumerator OpenSettings()
        {
            VisualElement root = HubTestSupport.Root();
            Click(root.Q<Button>("settings-button"));
            yield return HubTestSupport.WaitUntil(() => root.Q(SettingsPopupView.ScreenName).resolvedStyle.display == DisplayStyle.Flex);
            Assert.That(root.Q(SettingsPopupView.ScreenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "Settings never opened");

            // One more frame for the first layout pass of the popup.
            yield return null;
        }

        private static IEnumerator WaitUntilClosed(VisualElement root)
        {
            yield return HubTestSupport.WaitUntil(() => root.Q(SettingsPopupView.ScreenName).resolvedStyle.display == DisplayStyle.None);
        }

        private static void AssertClosed(VisualElement root)
        {
            Assert.That(root.Q(SettingsPopupView.ScreenName).resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(Navigator().OpenCount, Is.Zero);
            Button levelButton = root.Q<Button>("level-button");
            Assert.That(root.panel.Pick(levelButton.worldBound.center), Is.SameAs(levelButton), "Home receives presses again");
        }

        private static void AssertToggle(Button toggle, bool isOn, string what)
        {
            Assert.That(toggle.ClassListContains(SettingsPopupView.ToggleOnClass), Is.EqualTo(isOn), what + " state class");
            Assert.That(toggle.Q(SettingsPopupView.KnobName).resolvedStyle.translate.x, Is.EqualTo(isOn ? k_knobTravel : 0f).Within(0.5f),
                what + " knob side");
            Assert.That(toggle.Q(className: "settings-toggle__knob-on").resolvedStyle.opacity, Is.EqualTo(isOn ? 1f : 0f).Within(0.01f),
                what + " btn_toggle_on knob");
            Assert.That(toggle.Q(className: "settings-toggle__knob-off").resolvedStyle.opacity, Is.EqualTo(isOn ? 0f : 1f).Within(0.01f),
                what + " grey knob");
            Color track = toggle.Q(className: "settings-toggle__track").resolvedStyle.backgroundColor;
            Assert.That(isOn ? track.g > track.b : track.b > track.g, Is.True, what + " track colour " + track);
        }

        private static bool ToggleSettled(Button toggle, bool isOn)
        {
            float knob = toggle.Q(SettingsPopupView.KnobName).resolvedStyle.translate.x;
            float onOpacity = toggle.Q(className: "settings-toggle__knob-on").resolvedStyle.opacity;
            return Mathf.Abs(knob - (isOn ? k_knobTravel : 0f)) < 0.5f && Mathf.Abs(onOpacity - (isOn ? 1f : 0f)) < 0.01f;
        }

        private static string ToggleName(PlayerSettingId id)
        {
            switch (id)
            {
                case PlayerSettingId.Music:
                    return SettingsPopupView.MusicToggleName;
                case PlayerSettingId.Sound:
                    return SettingsPopupView.SoundToggleName;
                default:
                    return SettingsPopupView.VibrationToggleName;
            }
        }

        private static bool IsInside(VisualElement element, VisualElement ancestor)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private static void Click(VisualElement element)
        {
            HubTestSupport.Press(element);
            HubTestSupport.Release(element);
        }

        private static IHubNavigator Navigator()
        {
            return Resolve<IHubNavigator>();
        }

        private static T Resolve<T>()
        {
            return HubTestSupport.HubScope().Container.Resolve<T>();
        }

        private void PauseInputModule()
        {
            m_module = Object.FindObjectOfType<InputSystemUIInputModule>();
            m_module.enabled = false;
        }
    }
}
