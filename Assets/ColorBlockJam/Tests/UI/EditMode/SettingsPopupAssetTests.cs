using System.IO;
using System.Text.RegularExpressions;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Proves the shared Settings popup's authored assets and boundary (contract scenarios 1, 7 and 8): the UXML
    /// holds reference 19's rows and buttons in order with HOME hidden, the view writes toggle and HOME state, the five
    /// stretched sprites carry the measured borders that the USS slices repeat, and ColorBlockJam.UI references no
    /// Hub, Gameplay, Presentation or scene assembly.
    /// </summary>
    public sealed class SettingsPopupAssetTests
    {
        private const string k_uxmlPath = "Assets/ColorBlockJam/UI/Settings/SettingsPopup.uxml";
        private const string k_ussPath = "Assets/ColorBlockJam/UI/Settings/SettingsPopup.uss";
        private const string k_asmdefPath = "Assets/ColorBlockJam/Runtime/UI/ColorBlockJam.UI.asmdef";

        // Sprite paths paired with UiAssetAssert's measured borders (docs/UI-Asset-Import.md).
        private static readonly object[] s_borders =
        {
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup.png", UiAssetAssert.BgPopupBorder },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_header.png", UiAssetAssert.BgPopupHeaderBorder },
            new object[]
            {
                "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_info_holder.png", UiAssetAssert.BgPopupInfoHolderBorder
            },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_blue.png", UiAssetAssert.BtnPopupBlueBorder },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_green.png", UiAssetAssert.BtnPopupGreenBorder }
        };

        [Test]
        public void Uxml_HasReference19RowsAndButtonsInOrder_WithHomeLast()
        {
            VisualElement screen = CloneScreen();
            VisualElement holder = screen.Q("settings-holder");
            Assert.That(holder, Is.Not.Null);

            string[] expected =
            {
                "vibration-row", "sound-row", "music-row", SettingsPopupView.LegalTermsButtonName,
                SettingsPopupView.RestorePurchasesButtonName, SettingsPopupView.SupportButtonName,
                SettingsPopupView.LanguageButtonName, SettingsPopupView.HomeButtonName
            };
            Assert.That(holder.childCount, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(holder[i].name, Is.EqualTo(expected[i]), "child " + i);
            }

            Assert.That(screen.Q<Label>(SettingsPopupView.TitleName).text, Is.EqualTo("SETTINGS"));
            Assert.That(screen.Q<Button>(SettingsPopupView.CloseButtonName), Is.Not.Null);
            Assert.That(screen.Q<Button>(SettingsPopupView.SupportButtonName).Q(className: "settings-button__support-icon"), Is.Not.Null);
            Assert.That(screen.Q<Button>(SettingsPopupView.HomeButtonName).Q<Label>().text, Is.EqualTo("HOME"));
            Assert.That(screen.Q(SettingsPopupView.ScrimName).pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(screen.Q(SettingsPopupView.PopupName).pickingMode, Is.EqualTo(PickingMode.Position));
        }

        [Test]
        public void View_WritesToggleHomeAndMessageState()
        {
            VisualElement screen = CloneScreen();
            var view = new SettingsPopupView(screen);
            try
            {
                Button music = screen.Q<Button>(SettingsPopupView.MusicToggleName);
                view.SetToggle(PlayerSettingId.Music, true);
                Assert.That(music.ClassListContains(SettingsPopupView.ToggleOnClass), Is.True);
                view.SetToggle(PlayerSettingId.Music, false);
                Assert.That(music.ClassListContains(SettingsPopupView.ToggleOnClass), Is.False);

                Button home = screen.Q<Button>(SettingsPopupView.HomeButtonName);
                view.SetHomeVisible(false);
                Assert.That(home.style.display.value, Is.EqualTo(DisplayStyle.None));
                view.SetHomeVisible(true);
                Assert.That(home.style.display.value, Is.EqualTo(DisplayStyle.Flex));

                Label message = screen.Q<Label>(SettingsPopupView.MessageName);
                view.SetMessage(SettingsPopupPresenter.SaveFailedMessage);
                Assert.That(message.text, Is.EqualTo(SettingsPopupPresenter.SaveFailedMessage));
                Assert.That(message.style.visibility.value, Is.EqualTo(Visibility.Visible));
                view.SetMessage(string.Empty);
                Assert.That(message.style.visibility.value, Is.EqualTo(Visibility.Hidden));

                view.SetVisible(true);
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                view.SetVisible(false);
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                view.Dispose();
            }
        }

        [Test]
        public void Toggles_AnimateOnlyAfterATap_SoTheSavedStateShowsAtOnceWhenThePopupOpens()
        {
            // Every toggle transition sits under the animated modifier; the plain toggle parts have none.
            string uss = File.ReadAllText(k_ussPath);
            MatchCollection rules = Regex.Matches(uss, @"([^{}]+)\{([^}]*)\}");
            int animatedRules = 0;
            foreach (Match rule in rules)
            {
                string selector = rule.Groups[1].Value;
                if (!selector.Contains("settings-toggle") || !rule.Groups[2].Value.Contains("transition"))
                {
                    continue;
                }

                Assert.That(selector, Does.Contain("." + SettingsPopupView.ToggleAnimatedClass), selector.Trim());
                animatedRules++;
            }

            Assert.That(animatedRules, Is.GreaterThan(0));

            VisualElement screen = CloneScreen();
            var view = new SettingsPopupView(screen);
            try
            {
                Button music = screen.Q<Button>(SettingsPopupView.MusicToggleName);
                music.AddToClassList(SettingsPopupView.ToggleAnimatedClass);
                view.SetVisible(true);
                Assert.That(music.ClassListContains(SettingsPopupView.ToggleAnimatedClass), Is.False);
            }
            finally
            {
                view.Dispose();
            }
        }

        [TestCaseSource(nameof(s_borders))]
        public void StretchedSprite_BorderEqualsTheMeasuredValue_AndTheUssSlices(string path, Vector4 expected)
        {
            UiAssetAssert.AssertUssSlicesMatchBorder(path, k_ussPath, expected);
        }

        [Test]
        public void Popup_DoesNotUseBgMenu()
        {
            Assert.That(File.ReadAllText(k_ussPath), Does.Not.Contain("bg_menu"));
            Assert.That(File.ReadAllText(k_uxmlPath), Does.Not.Contain("bg_menu"));
        }

        [Test]
        public void SharedAssembly_ReferencesOnlyApprovedRuntimeDependencies()
        {
            string asmdef = File.ReadAllText(k_asmdefPath);
            Match references = Regex.Match(asmdef, "\"references\"\\s*:\\s*\\[([^\\]]*)\\]");
            Assert.That(references.Success, Is.True);
            MatchCollection names = Regex.Matches(references.Groups[1].Value, "\"([^\"]+)\"");
            var actual = new string[names.Count];
            for (int index = 0; index < names.Count; index++)
            {
                actual[index] = names[index].Groups[1].Value;
            }
            Assert.That(actual, Is.EquivalentTo(new[]
            {
                "ColorBlockJam.Runtime", "ColorBlockJam.Player", "ColorBlockJam.Haptics", "Onity.Reactive", "PrimeTween.Runtime"
            }));

            // The compiled assembly agrees: no Hub, Gameplay, Presentation, Environment or scene-flow dependency.
            UiAssetAssert.AssertNoSceneOrCompositionReference(typeof(SettingsPopupPresenter).Assembly);
        }

        private static VisualElement CloneScreen()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_uxmlPath);
            Assert.That(tree, Is.Not.Null, k_uxmlPath);
            VisualElement root = tree.Instantiate();
            VisualElement screen = root.Q(SettingsPopupView.ScreenName);
            Assert.That(screen, Is.Not.Null);
            return screen;
        }
    }
}
