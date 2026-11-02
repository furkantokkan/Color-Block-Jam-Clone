using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the pre-level popup's authored assets and the booster catalog order (contract scenarios 1, 2, 3 and 11):
    /// PreLevel.uxml holds reference 09's parts with heavy two-layer text, the view builds selectors whose state
    /// classes and badges follow references 09 and 10, the USS slices equal the measured sprite borders, and
    /// BoosterCatalog.asset lists Extra Time first so both the pre-level and the in-game order follow the catalog.
    /// </summary>
    public sealed class PreLevelAssetTests
    {
        private const string k_uxmlPath = "Assets/ColorBlockJam/UI/Hub/PreLevel.uxml";
        private const string k_ussPath = "Assets/ColorBlockJam/UI/Hub/PreLevel.uss";
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Boosters/BoosterCatalog.asset";

        // Measured borders from docs/UI-Asset-Import.md, Unity order L, B, R, T (Sprite.border / spriteBorder).
        private static readonly object[] s_borders =
        {
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup.png", new Vector4(106, 104, 107, 74) },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_header.png", new Vector4(79, 90, 79, 90) },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Panels/bg_popup_info_holder.png", new Vector4(43, 35, 42, 26) },
            new object[] { "Assets/ColorBlockJam/Art/UI/Supplied/Buttons/btn_popup_green.png", new Vector4(69, 128, 70, 129) }
        };

        [Test]
        public void Uxml_HasReference09Parts_WithHeavyText()
        {
            VisualElement screen = CloneScreen();

            string[] names =
            {
                PreLevelView.ScrimName, PreLevelView.SafeAreaName, PreLevelView.PopupName, PreLevelView.TitleName,
                PreLevelView.SubtitleName, PreLevelView.CloseButtonName, PreLevelView.SelectorsName, PreLevelView.PlayButtonName
            };
            foreach (string name in names)
            {
                Assert.That(screen.Q(name), Is.Not.Null, "missing " + name);
            }

            Assert.That(screen.Q<Label>(PreLevelView.SubtitleName).text, Is.EqualTo("Select Boosters:"));
            Assert.That(screen.Q<Button>(PreLevelView.PlayButtonName).Q<Label>("pre-level-play-label").text, Is.EqualTo("Play"));
            Assert.That(screen.Q(PreLevelView.ScrimName).pickingMode, Is.EqualTo(PickingMode.Position));
            foreach (string name in new[] { PreLevelView.TitleName, PreLevelView.SubtitleName, "pre-level-play-label" })
            {
                Label front = screen.Q<Label>(name);
                Assert.That(front.ClassListContains("cbj-heavy-text__front"), Is.True, name + " is a heavy front layer");
                Assert.That(front.parent.Q<Label>(className: "cbj-heavy-text__back").text, Is.EqualTo(front.text), name + " back layer");
            }
        }

        [Test]
        public void View_BuildsSelectors_WithStateClasses_AndHeavyTitle()
        {
            VisualElement screen = CloneScreen();
            var view = new PreLevelView(screen);
            try
            {
                view.SetTitle("LEVEL 7");
                Assert.That(screen.Q<Label>(PreLevelView.TitleName).text, Is.EqualTo("LEVEL 7"));
                Assert.That(screen.Q<Label>(PreLevelView.TitleName).parent.Q<Label>(className: "cbj-heavy-text__back").text,
                    Is.EqualTo("LEVEL 7"));

                Assert.That(view.AddSelector(null, "Extra Time"), Is.EqualTo(0));
                Assert.That(view.AddSelector(null, "Rocket"), Is.EqualTo(1));
                List<VisualElement> selectors = screen.Q(PreLevelView.SelectorsName).Query(className: PreLevelView.SelectorClass).ToList();
                Assert.That(selectors.Count, Is.EqualTo(2));
                Assert.That(Texts(selectors[0], PreLevelView.SelectorLabelClass), Is.EqualTo(new[] { "Extra Time", "Extra Time" }));

                view.SetSelector(0, PreLevelSelectorState.Available, "2");
                Assert.That(Texts(selectors[0], PreLevelView.SelectorCountClass), Is.EqualTo(new[] { "2", "2" }));
                Assert.That(selectors[0].ClassListContains(PreLevelView.SelectedClass), Is.False);
                Assert.That(selectors[0].ClassListContains(PreLevelView.OutOfStockClass), Is.False);

                view.SetSelector(0, PreLevelSelectorState.Selected, "2");
                Assert.That(selectors[0].ClassListContains(PreLevelView.SelectedClass), Is.True);

                view.SetSelector(1, PreLevelSelectorState.OutOfStock, "0");
                Assert.That(selectors[1].ClassListContains(PreLevelView.OutOfStockClass), Is.True);
                Assert.That(selectors[1].Q(className: PreLevelView.SelectorPlusIconClass), Is.Not.Null);

                view.ClearSelectors();
                Assert.That(screen.Q(PreLevelView.SelectorsName).childCount, Is.Zero);

                view.SetVisible(true);
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                view.SetPlayInteractable(false);
                Assert.That(screen.Q<Button>(PreLevelView.PlayButtonName).enabledSelf, Is.False);
            }
            finally
            {
                view.Dispose();
            }

            Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.None), "Dispose hides the popup");
        }

        [Test]
        public void Binding_ReplacesSamples_AndRebindKeepsTheAuthoredTemplate()
        {
            VisualElement screen = CloneScreen();
            VisualElement row = screen.Q(PreLevelView.SelectorsName);
            var sample = row.Q<TemplateContainer>("selector-preview-time");
            Assert.That(sample, Is.Not.Null);
            VisualTreeAsset template = sample.templateSource;
            Assert.That(AssetDatabase.GetAssetPath(template), Is.EqualTo("Assets/ColorBlockJam/UI/Hub/BoosterSelector.uxml"));
            Assert.That(row.Query(className: PreLevelView.SelectorClass).ToList().Count, Is.EqualTo(2));

            var pressed = new List<int>();
            var view = new PreLevelView(screen);
            try
            {
                Assert.That(row.childCount, Is.Zero, "binding removes authoring samples before runtime data arrives");
                Assert.That(screen.style.display.value, Is.EqualTo(DisplayStyle.None));
                view.SelectorPressed += pressed.Add;
                view.AddSelector(null, "Runtime Time");
                view.AddSelector(null, "Runtime Rocket");
                VisualElement first = row[0];
                VisualElement second = row[1];
                Background icon = first.Q(className: PreLevelView.SelectorIconClass).style.backgroundImage.value;
                Assert.That(icon.sprite, Is.Null);
                Assert.That(icon.texture, Is.Null);
                Assert.That(Texts(first, PreLevelView.SelectorCountClass), Is.EqualTo(new[] { "", "" }));
                Assert.That(Texts(first, PreLevelView.SelectorLabelClass), Is.EqualTo(new[] { "Runtime Time", "Runtime Time" }));
                view.SetSelector(1, PreLevelSelectorState.Available, "37");
                Assert.That(Texts(second, PreLevelView.SelectorCountClass), Is.EqualTo(new[] { "37", "37" }));
                Click(first.Q<Button>(className: PreLevelView.SelectorButtonClass));
                Click(second.Q<Button>(className: PreLevelView.SelectorButtonClass));
                Assert.That(pressed, Is.EqualTo(new[] { 0, 1 }));
                view.ClearSelectors();
                Click(first.Q<Button>(className: PreLevelView.SelectorButtonClass));
                Assert.That(pressed.Count, Is.EqualTo(2), "removed selector handlers are detached");
            }
            finally
            {
                view.Dispose();
            }

            Assert.That(row.userData, Is.SameAs(template));
            var rebound = new PreLevelView(screen);
            try
            {
                Assert.That(rebound.AddSelector(null, null), Is.Zero);
                Assert.That(Texts(row[0], PreLevelView.SelectorLabelClass), Is.EqualTo(new[] { "", "" }));
                Assert.That(Texts(row[0], PreLevelView.SelectorCountClass), Is.EqualTo(new[] { "", "" }));
            }
            finally
            {
                rebound.Dispose();
            }
            Assert.That(row.childCount, Is.Zero);
        }

        private static void Click(Button button)
        {
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        [UnityTest]
        public IEnumerator NullRuntimeIcon_DoesNotFallBackToTheAuthoredSampleOnAnAttachedPanel()
        {
            var window = ScriptableObject.CreateInstance<SelectorPreviewWindow>();
            VisualElement screen = CloneScreen();
            VisualElement document = screen.parent;
            document.style.width = 1080f;
            document.style.height = 1920f;
            window.Show();
            window.rootVisualElement.Add(document);
            var view = new PreLevelView(screen);
            try
            {
                view.AddSelector(null, "Runtime only");
                view.SetVisible(true);
                yield return null;
                yield return null;
                Background background = screen.Q(className: PreLevelView.SelectorIconClass).resolvedStyle.backgroundImage;
                Assert.That(background.sprite, Is.Null, "null runtime icon replaces the stylesheet sample");
                Assert.That(background.texture, Is.Null, "no sample Texture2D fallback");
            }
            finally
            {
                view.Dispose();
                window.Close();
            }
        }

        [TestCaseSource(nameof(s_borders))]
        public void StretchedSprite_UssSlicesEqualTheMeasuredBorder(string path, Vector4 expected)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.spriteBorder, Is.EqualTo(expected), path + " spriteBorder (L, B, R, T)");

            string uss = File.ReadAllText(k_ussPath);
            string fileName = Path.GetFileName(path);
            int rules = 0;
            foreach (Match rule in Regex.Matches(uss, @"\{([^{}]*)\}"))
            {
                string body = rule.Groups[1].Value;
                if (!body.Contains("/" + fileName + "\""))
                {
                    continue;
                }

                rules++;
                Assert.That(Slice(body, "left"), Is.EqualTo((int)expected.x), fileName + " slice left");
                Assert.That(Slice(body, "top"), Is.EqualTo((int)expected.w), fileName + " slice top");
                Assert.That(Slice(body, "right"), Is.EqualTo((int)expected.z), fileName + " slice right");
                Assert.That(Slice(body, "bottom"), Is.EqualTo((int)expected.y), fileName + " slice bottom");
            }

            Assert.That(rules, Is.GreaterThan(0), fileName + " is used by PreLevel.uss");
        }

        [Test]
        public void BoosterCatalog_ListsExtraTimeFirst_SoBothOrdersFollowTheCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoosterCatalog>(k_catalogPath);
            Assert.That(catalog, Is.Not.Null, k_catalogPath);
            Assert.That(catalog.Validate(out string error), Is.True, error);

            var all = new List<BoosterId>();
            var preLevel = new List<BoosterId>();
            var inGame = new List<BoosterId>();
            for (int i = 0; i < catalog.Count; i++)
            {
                BoosterDefinition definition = catalog.GetAt(i);
                all.Add(definition.Id);
                if ((definition.UseContexts & BoosterUseContext.PreLevel) != 0)
                {
                    preLevel.Add(definition.Id);
                }

                if ((definition.UseContexts & BoosterUseContext.InGame) != 0)
                {
                    inGame.Add(definition.Id);
                }
            }

            Assert.That(all, Is.EqualTo(new[]
            {
                BoosterId.ExtraTime, BoosterId.StartingRocket, BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer,
                BoosterId.Vacuum
            }));
            Assert.That(preLevel, Is.EqualTo(new[] { BoosterId.ExtraTime, BoosterId.StartingRocket }), "reference 09; own stock (DEC-167)");
            Assert.That(inGame, Is.EqualTo(new[] { BoosterId.TimeFreeze, BoosterId.Rocket, BoosterId.Hammer, BoosterId.Vacuum }));

            Assert.That(catalog.TryGet(BoosterId.ExtraTime, out BoosterDefinition extraTime), Is.True);
            Assert.That(extraTime.DisplayName, Is.EqualTo("Extra Time"));
            Assert.That(AssetDatabase.GetAssetPath(extraTime.Icon), Does.EndWith("Icons/Gameplay/ic_clock.png"), "DEC-47, AF-16");
            Assert.That(catalog.TryGet(BoosterId.Rocket, out BoosterDefinition rocket), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(rocket.Icon), Does.EndWith("Icons/Boosters/ic_rocket_purple.png"));
            Assert.That(catalog.TryGet(BoosterId.StartingRocket, out BoosterDefinition startingRocket), Is.True);
            Assert.That(startingRocket.DisplayName, Is.EqualTo("Rocket"), "the popup label of reference 09");
            Assert.That(startingRocket.Icon, Is.SameAs(rocket.Icon), "the shared purple Rocket art (DEC-47)");
            Assert.That(startingRocket.EffectPrefab, Is.SameAs(rocket.EffectPrefab), "one Rocket effect");
        }

        private static string[] Texts(VisualElement selector, string sizeClass)
        {
            List<Label> labels = selector.Query<Label>(className: sizeClass).ToList();
            var texts = new string[labels.Count];
            for (int i = 0; i < labels.Count; i++)
            {
                texts[i] = labels[i].text;
            }

            return texts;
        }

        private static int Slice(string body, string side)
        {
            Match match = Regex.Match(body, @"-unity-slice-" + side + @"\s*:\s*(\d+)");
            Assert.That(match.Success, Is.True, "-unity-slice-" + side);
            return int.Parse(match.Groups[1].Value);
        }

        private static VisualElement CloneScreen()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_uxmlPath);
            Assert.That(tree, Is.Not.Null, k_uxmlPath);
            VisualElement root = tree.Instantiate();
            VisualElement screen = root.Q(PreLevelView.ScreenName);
            Assert.That(screen, Is.Not.Null);
            return screen;
        }
    }

    internal sealed class SelectorPreviewWindow : EditorWindow { }
}
