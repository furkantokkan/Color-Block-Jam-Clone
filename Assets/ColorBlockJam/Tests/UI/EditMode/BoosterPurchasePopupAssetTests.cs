using System.Collections;
using System.Reflection;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Proves the shared purchase popup's authored assets and boundary (contract scenarios 3, 11 and 12): the UXML holds
    /// reference 01's parts with heavy two-layer text, the view writes both text layers, the icons, the message and
    /// Buy's state, the USS slices equal the measured sprite borders, and ColorBlockJam.UI still references no Hub,
    /// Gameplay or scene assembly.
    /// </summary>
    public sealed class BoosterPurchasePopupAssetTests
    {
        private const string k_uxmlPath = "Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uxml";
        private const string k_ussPath = "Assets/ColorBlockJam/UI/Purchase/BoosterPurchasePopup.uss";
        private const string k_hubPath = "Assets/ColorBlockJam/UI/Hub/Hub.uxml";
        private const string k_gamePath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";
        private const string k_fontPath = "Assets/ColorBlockJam/Data/UI/Fonts/LiberationSans-UITK.asset";

        // Sprite paths paired with UiAssetAssert's measured borders (docs/UI-Asset-Import.md).
        private static readonly object[] s_borders =
        {
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup.png", UiAssetAssert.BgPopupBorder },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_header.png", UiAssetAssert.BgPopupHeaderBorder },
            new object[]
            {
                "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_info_holder.png", UiAssetAssert.BgPopupInfoHolderBorder
            },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_green.png", UiAssetAssert.BtnPopupGreenBorder }
        };

        [Test]
        public void Uxml_HasReference01Parts_WithHeavyTitleAndButtonText()
        {
            VisualElement screen = CloneScreen();

            string[] names =
            {
                BoosterPurchaseView.ScrimName, BoosterPurchaseView.SafeAreaName, BoosterPurchaseView.PopupName,
                BoosterPurchaseView.TitleName, BoosterPurchaseView.CloseButtonName, BoosterPurchaseView.IconName,
                BoosterPurchaseView.AmountName, BoosterPurchaseView.DescriptionName, BoosterPurchaseView.MessageName,
                BoosterPurchaseView.BuyButtonName, BoosterPurchaseView.PriceIconName, BoosterPurchaseView.PriceName
            };
            foreach (string name in names)
            {
                Assert.That(screen.Q(name), Is.Not.Null, "missing " + name);
            }

            Assert.That(screen.Q(BoosterPurchaseView.ScrimName).pickingMode, Is.EqualTo(PickingMode.Position));
            Assert.That(screen.Q(BoosterPurchaseView.PopupName).pickingMode, Is.EqualTo(PickingMode.Position));
            foreach (string name in new[] { BoosterPurchaseView.TitleName, BoosterPurchaseView.PriceName, "booster-purchase-buy-label" })
            {
                Label front = screen.Q<Label>(name);
                Assert.That(front.ClassListContains("cbj-heavy-text__front"), Is.True, name + " is a heavy front layer");
                Assert.That(front.parent.ClassListContains("cbj-heavy-text"), Is.True, name + " sits in a heavy pair");
                Assert.That(front.parent.Q<Label>(className: "cbj-heavy-text__back"), Is.Not.Null, name + " has a back layer");
            }

            Assert.That(screen.Q<Label>("booster-purchase-buy-label").text, Is.EqualTo("Buy"));
        }

        [Test]
        public void View_WritesBothTextLayers_IconsMessageAndBuyState()
        {
            VisualElement screen = CloneScreen();
            var view = new BoosterPurchaseView(screen);
            var texture = new Texture2D(2, 2);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            try
            {
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.None), "Binding starts closed.");
                view.SetOffer("Runtime offer", icon, "x7", "Runtime description.", icon, "57");

                AssertPair(screen, BoosterPurchaseView.TitleName, "Runtime offer");
                AssertPair(screen, BoosterPurchaseView.AmountName, "x7");
                AssertPair(screen, BoosterPurchaseView.DescriptionName, "Runtime description.");
                AssertPair(screen, BoosterPurchaseView.PriceName, "57");
                Assert.That(screen.Q(BoosterPurchaseView.IconName).style.backgroundImage.value.sprite, Is.SameAs(icon));
                Assert.That(screen.Q(BoosterPurchaseView.PriceIconName).style.backgroundImage.value.sprite, Is.SameAs(icon));

                Label message = screen.Q<Label>(BoosterPurchaseView.MessageName);
                view.SetMessage("Not enough coins");
                Assert.That(message.text, Is.EqualTo("Not enough coins"));
                Assert.That(message.style.visibility.value, Is.EqualTo(Visibility.Visible));
                view.SetMessage(string.Empty);
                Assert.That(message.style.visibility.value, Is.EqualTo(Visibility.Hidden));

                Button buy = screen.Q<Button>(BoosterPurchaseView.BuyButtonName);
                view.SetBuyInteractable(false);
                Assert.That(buy.enabledSelf, Is.False);
                view.SetBuyInteractable(true);
                Assert.That(buy.enabledSelf, Is.True);

                view.SetVisible(true);
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                view.SetVisible(false);
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                view.Dispose();
                Object.DestroyImmediate(icon);
                Object.DestroyImmediate(texture);
            }
        }

        [UnityTest]
        public IEnumerator StandalonePanel_ShowsStyledSampleWithoutRuntimeBinding()
        {
            VisualElement root = CloneDocument(k_uxmlPath);
            VisualElement screen = root.Q(BoosterPurchaseView.ScreenName);
            EditorWindow window = ShowPanel(root);
            try
            {
                yield return WaitForStyles(window, screen);
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                VisualElement popup = screen.Q(BoosterPurchaseView.PopupName);
                Assert.That(popup.layout.width, Is.EqualTo(866f).Within(0.1f));
                Assert.That(popup.layout.height, Is.GreaterThan(0f));
                Assert.That(screen.layout.width, Is.EqualTo(1080f).Within(0.1f));
                Assert.That(screen.layout.height, Is.EqualTo(1920f).Within(0.1f));

                Label title = screen.Q<Label>(BoosterPurchaseView.TitleName);
                Label back = title.parent.Q<Label>(className: "cbj-heavy-text__back");
                Assert.That(back.resolvedStyle.position, Is.EqualTo(Position.Absolute), "Common heavy-text style resolves.");
                Assert.That(back.resolvedStyle.color, Is.EqualTo((Color)new Color32(23, 35, 91, 255)));
                Assert.That(title.resolvedStyle.unityFontDefinition.fontAsset,
                    Is.SameAs(AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(k_fontPath)));
                Assert.That(title.resolvedStyle.unityFontDefinition.fontAsset, Is.Not.Null);
                AssertPair(screen, BoosterPurchaseView.TitleName, "Rocket");
                AssertPair(screen, BoosterPurchaseView.AmountName, "x1");
                AssertPair(screen, BoosterPurchaseView.DescriptionName, "Remove one cell from the selected block.");
                AssertPair(screen, BoosterPurchaseView.PriceName, "10");
                AssertBackground(screen.Q(BoosterPurchaseView.IconName),
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_rocket_purple.png");
                AssertBackground(screen.Q(BoosterPurchaseView.PriceIconName),
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Currency/ic_coin_single.png");
            }
            finally
            {
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator UnboundHosts_KeepEveryPurchaseInstanceHiddenWhenAttachedAgain()
        {
            foreach (string path in new[] { k_hubPath, k_gamePath })
            {
                VisualElement root = CloneDocument(path);
                EditorWindow window = ShowPanel(root);
                try
                {
                    VisualElement booster = root.Q("booster-purchase-instance").Q(BoosterPurchaseView.ScreenName);
                    yield return WaitForStyles(window, booster);
                    AssertHiddenPurchases(root, path == k_hubPath ? 2 : 1);
                    root.RemoveFromHierarchy();
                    window.rootVisualElement.Add(root);
                    yield return null;
                    window.Repaint();
                    yield return null;
                    AssertHiddenPurchases(root, path == k_hubPath ? 2 : 1);
                }
                finally
                {
                    window.Close();
                }
            }
        }

        [UnityTest]
        public IEnumerator RuntimeBinding_HidesVisibleStandalonePanel()
        {
            VisualElement root = CloneDocument(k_uxmlPath);
            VisualElement screen = root.Q(BoosterPurchaseView.ScreenName);
            EditorWindow window = ShowPanel(root);
            BoosterPurchaseView view = null;
            try
            {
                yield return WaitForStyles(window, screen);
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                view = new BoosterPurchaseView(screen);
                yield return null;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                view?.Dispose();
                window.Close();
            }
        }

        [UnityTest]
        public IEnumerator RuntimeOffer_OverridesHostVisibilityAndClearsSampleIcons()
        {
            VisualElement root = CloneDocument(k_gamePath);
            VisualElement screen = root.Q("booster-purchase-instance").Q(BoosterPurchaseView.ScreenName);
            EditorWindow window = ShowPanel(root);
            BoosterPurchaseView view = null;
            try
            {
                yield return WaitForStyles(window, screen);
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                view = new BoosterPurchaseView(screen);
                view.SetOffer("Life refill", null, "x5", "Refill lives.", null, "250");
                view.SetVisible(true);
                yield return null;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "Inline opening overrides the host rule.");
                AssertPair(screen, BoosterPurchaseView.TitleName, "Life refill");
                AssertPair(screen, BoosterPurchaseView.AmountName, "x5");
                AssertPair(screen, BoosterPurchaseView.DescriptionName, "Refill lives.");
                AssertPair(screen, BoosterPurchaseView.PriceName, "250");
                Assert.That(screen.Q(BoosterPurchaseView.IconName).resolvedStyle.backgroundImage, Is.EqualTo(default(Background)));
                Assert.That(screen.Q(BoosterPurchaseView.PriceIconName).resolvedStyle.backgroundImage, Is.EqualTo(default(Background)));
                view.SetVisible(false);
                yield return null;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                view.SetVisible(true);
                yield return null;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
                view.Dispose();
                yield return null;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "Disposal closes an open popup.");
            }
            finally
            {
                view?.Dispose();
                window.Close();
            }
        }

        [TestCaseSource(nameof(s_borders))]
        public void StretchedSprite_UssSlicesEqualTheMeasuredBorder(string path, Vector4 expected)
        {
            UiAssetAssert.AssertUssSlicesMatchBorder(path, k_ussPath, expected);
        }

        [Test]
        public void PurchasePopup_LivesInTheSharedAssembly_WithNoHubGameplayOrSceneReference()
        {
            Assembly shared = typeof(BoosterPurchasePresenter).Assembly;
            Assert.That(shared.GetName().Name, Is.EqualTo("ColorBlockJam.UI"));
            UiAssetAssert.AssertNoSceneOrCompositionReference(shared);
        }

        private static void AssertPair(VisualElement screen, string frontName, string text)
        {
            Label front = screen.Q<Label>(frontName);
            Label back = front.parent.Q<Label>(className: "cbj-heavy-text__back");
            Assert.That(front.text, Is.EqualTo(text), frontName + " front");
            Assert.That(back.text, Is.EqualTo(text), frontName + " back");
        }

        private static VisualElement CloneScreen()
        {
            VisualElement root = CloneDocument(k_uxmlPath);
            VisualElement screen = root.Q(BoosterPurchaseView.ScreenName);
            Assert.That(screen, Is.Not.Null);
            return screen;
        }

        private static VisualElement CloneDocument(string path)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(tree, Is.Not.Null, path);
            return tree.Instantiate();
        }

        private static EditorWindow ShowPanel(VisualElement root)
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            window.titleContent = new GUIContent("Purchase popup verification");
            window.Show();
            root.style.width = 1080f;
            root.style.height = 1920f;
            root.style.flexShrink = 0f;
            window.rootVisualElement.Add(root);
            return window;
        }

        private static IEnumerator WaitForStyles(EditorWindow window, VisualElement screen)
        {
            Label title = screen.Q<Label>(BoosterPurchaseView.TitleName);
            for (int frame = 0; frame < 300 && (screen.panel == null || title.resolvedStyle.fontSize != 76f); frame++)
            {
                window.Repaint();
                yield return null;
            }

            Assert.That(screen.panel, Is.Not.Null);
            Assert.That(title.resolvedStyle.fontSize, Is.EqualTo(76f), "Purchase stylesheet resolves on the panel.");
            yield return null;
        }

        private static void AssertHiddenPurchases(VisualElement root, int expectedCount)
        {
            int count = 0;
            root.Query<VisualElement>(BoosterPurchaseView.ScreenName).ForEach(screen =>
            {
                count++;
                Assert.That(screen.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), screen.parent.name);
                Assert.That(screen.style.display.keyword, Is.EqualTo(StyleKeyword.Null), "Hidden before a runtime inline assignment.");
            });
            Assert.That(count, Is.EqualTo(expectedCount), "All host instances are checked, including life refill.");
        }

        private static void AssertBackground(VisualElement element, string path)
        {
            Background background = element.resolvedStyle.backgroundImage;
            Object image = background.sprite != null ? (Object)background.sprite : background.texture;
            Assert.That(image, Is.Not.Null, element.name);
            Assert.That(AssetDatabase.GetAssetPath(image), Is.EqualTo(path), element.name);
        }
    }
}
