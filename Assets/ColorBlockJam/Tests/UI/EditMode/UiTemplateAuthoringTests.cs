using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ColorBlockJam.Runtime.SceneFlow;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.SharedUI
{
    public sealed class UiTemplateAuthoringTests
    {
        private const string k_uiRoot = "Assets/ColorBlockJam/UI/";
        private static readonly string[] s_documents =
        {
            "Hub/Home.uxml", "Hub/Hub.uxml", "Hub/CurrencyHolder.uxml", "Hub/PreLevel.uxml",
            "Hub/BoosterSelector.uxml", "Settings/SettingsPopup.uxml", "Purchase/BoosterPurchasePopup.uxml",
            "Game/GameplayHud.uxml", "Game/TopBar/GameTopBar.uxml", "Game/Overlays/GameOverlays.uxml",
            "Game/Overlays/RestartConfirmPopup.uxml", "Game/Overlays/LevelCompletePopup.uxml",
            "Startup/StartupLoading.uxml", "Common/ScreenFade.uxml"
        };
        private static readonly string[] s_screenNames =
        {
            "home-screen", "home-screen", "currency-holder", "pre-level-screen", "booster-selector",
            "settings-screen", "booster-purchase-screen", "hud-root", "game-top-bar", "settings-screen",
            "restart-confirm-screen", "level-complete-screen", "startup-loading", "screen-fade"
        };
        private EditorWindow m_window;

        [TearDown]
        public void TearDown()
        {
            if (m_window != null)
            {
                m_window.Close();
                m_window = null;
            }
        }

        [Test]
        public void AllDocuments_ImportEveryStyleAndTemplateReference()
        {
            foreach (string document in s_documents)
            {
                string path = k_uiRoot + document;
                Assert.That(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path), Is.Not.Null, path);
                var xml = new XmlDocument();
                xml.Load(path);
                foreach (XmlNode child in xml.DocumentElement.ChildNodes)
                {
                    if (child.LocalName != "Style" && child.LocalName != "Template")
                    {
                        continue;
                    }
                    Assert.That(child.Prefix, Is.EqualTo("ui"), path + " qualified " + child.LocalName);
                    string reference = child.Attributes["src"].Value.Replace("project://database/", string.Empty);
                    Assert.That(File.Exists(reference), Is.True, path + " -> " + reference);
                    Assert.That(AssetDatabase.LoadMainAssetAtPath(reference), Is.Not.Null, reference);
                }
            }
        }

        [UnityTest]
        public IEnumerator StandaloneDocuments_AreVisibleAndStyledWithoutRuntimeBinding()
        {
            OpenPanel();
            for (int index = 0; index < s_documents.Length; index++)
            {
                string path = k_uiRoot + s_documents[index];
                TemplateContainer tree = Clone(path);
                m_window.rootVisualElement.Clear();
                m_window.rootVisualElement.Add(tree);
                yield return null;
                yield return null;
                VisualElement screen = tree.Q(s_screenNames[index]);
                Assert.That(screen, Is.Not.Null, path);
                Assert.That(IsShown(screen), Is.True, path + " standalone root");
                Assert.That(screen.layout.width, Is.GreaterThan(0f), path + " width");
                Assert.That(screen.layout.height, Is.GreaterThan(0f), path + " height");
                foreach (Label label in tree.Query<Label>().ToList())
                {
                    if (!IsShown(label))
                    {
                        continue;
                    }
                    Assert.That(label.resolvedStyle.unityFontDefinition.fontAsset, Is.Not.Null, path + " " + label.name);
                    Assert.That(label.resolvedStyle.unityFontDefinition.fontAsset.name, Is.EqualTo("LiberationSans-UITK"), path);
                    if (!label.ClassListContains("cbj-heavy-text__front"))
                    {
                        continue;
                    }
                    Label back = label.parent.Q<Label>(className: "cbj-heavy-text__back");
                    Assert.That(back, Is.Not.Null, path + " paired back");
                    Assert.That(back.text, Is.EqualTo(label.text), path + " paired text");
                    Assert.That(Vector2.Distance(back.worldBound.position, label.worldBound.position), Is.LessThan(1f), path + " paired position");
                    Assert.That(Vector2.Distance(back.worldBound.size, label.worldBound.size), Is.LessThan(1f), path + " paired size");
                }
                foreach (Button button in tree.Query<Button>(className: "cbj-pressable").ToList())
                {
                    if (!IsShown(button))
                    {
                        continue;
                    }
                    Assert.That(button.resolvedStyle.backgroundColor.a, Is.Zero, path + " " + button.name + " default fill");
                    Assert.That(button.resolvedStyle.borderLeftWidth, Is.Zero, path + " " + button.name + " default border");
                }
                if (s_documents[index] == "Hub/Home.uxml")
                {
                    Background icon = tree.Q("currency-holder-life").Q("holder-icon").resolvedStyle.backgroundImage;
                    Object source = icon.sprite != null ? (Object)icon.sprite : icon.texture;
                    Assert.That(AssetDatabase.GetAssetPath(source),
                        Does.EndWith("ic_heart.png"), "life keeps its variant instead of the template's coin sample");
                    Assert.That(tree.Q<Label>("level-button-label").text, Is.EqualTo("Level 1"));
                }
                if (s_documents[index] == "Common/ScreenFade.uxml")
                {
                    Assert.That(screen.resolvedStyle.opacity, Is.EqualTo(1f));
                    Assert.That(screen.resolvedStyle.backgroundColor, Is.EqualTo(Color.black));
                }
            }
        }

        [UnityTest]
        public IEnumerator UnboundHosts_KeepEmbeddedOverlaysClosedAfterReattachment_AndViewsCanOpenThem()
        {
            OpenPanel();
            foreach (string path in new[] { "Hub/Hub.uxml", "Game/GameplayHud.uxml" })
            {
                TemplateContainer tree = Clone(k_uiRoot + path);
                m_window.rootVisualElement.Clear();
                m_window.rootVisualElement.Add(tree);
                yield return null;
                yield return null;
                AssertOverlaysClosed(tree, path);
                tree.RemoveFromHierarchy();
                m_window.rootVisualElement.Add(tree);
                yield return null;
                yield return null;
                AssertOverlaysClosed(tree, path + " reattached");
                VisualElement settings = tree.Q(SettingsPopupView.ScreenName);
                using (var view = new SettingsPopupView(settings))
                {
                    view.SetVisible(true);
                    yield return null;
                    Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), path + " inline runtime Show wins");
                    view.SetVisible(false);
                    yield return null;
                    Assert.That(settings.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), path + " runtime Close");
                }
                Assert.That(settings.style.display.value, Is.EqualTo(DisplayStyle.None), "dispose leaves host closed");
            }
        }

        [Test]
        public void ScreenFade_EnableAndDocumentReenable_CloseTheOpaqueAuthoringSampleSynchronously()
        {
            var gameObject = new GameObject("ScreenFade authoring verification");
            gameObject.SetActive(false);
            try
            {
                UIDocument document = gameObject.AddComponent<UIDocument>();
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_uiRoot + "Common/ScreenFade.uxml");
                ScreenFadeView view = gameObject.AddComponent<ScreenFadeView>();
                MethodInfo enable = typeof(ScreenFadeView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo disable = typeof(ScreenFadeView).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
                // EditMode does not run this non-ExecuteAlways component's callbacks; invoke after UIDocument builds its tree.
                gameObject.SetActive(true);
                enable.Invoke(view, null);
                VisualElement fade = document.rootVisualElement.Q("screen-fade");
                Assert.That(fade.style.display.value, Is.EqualTo(DisplayStyle.None));
                Assert.That(fade.style.opacity.value, Is.Zero);
                fade.style.display = DisplayStyle.Flex;
                fade.style.opacity = 1f;
                disable.Invoke(view, null);
                document.enabled = false;
                document.enabled = true;
                enable.Invoke(view, null);
                fade = document.rootVisualElement.Q("screen-fade");
                Assert.That(fade.style.display.value, Is.EqualTo(DisplayStyle.None), "new document tree hidden synchronously");
                Assert.That(fade.style.opacity.value, Is.Zero);
                Assert.That(view.IsVisible, Is.False);
                var order = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(typeof(ScreenFadeView), typeof(DefaultExecutionOrder));
                Assert.That(order, Is.Not.Null);
                Assert.That(order.order, Is.GreaterThan(0), "runs after the default UIDocument order");
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private void OpenPanel()
        {
            m_window = ScriptableObject.CreateInstance<UiTemplatePreviewWindow>();
            m_window.titleContent = new GUIContent("UI authoring verification");
            m_window.Show();
            m_window.rootVisualElement.style.width = 1080f;
            m_window.rootVisualElement.style.height = 1920f;
        }

        private static TemplateContainer Clone(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
            Assert.That(asset, Is.Not.Null, path);
            TemplateContainer tree = asset.CloneTree();
            tree.style.position = Position.Absolute;
            tree.style.width = 1080f;
            tree.style.height = 1920f;
            return tree;
        }

        private static bool IsShown(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }
            return true;
        }

        private static void AssertOverlaysClosed(VisualElement tree, string context)
        {
            int count = 0;
            foreach (string name in new[] { "settings-screen", "pre-level-screen", "booster-purchase-screen", "restart-confirm-screen", "level-complete-screen" })
            {
                foreach (VisualElement overlay in tree.Query(name: name).ToList())
                {
                    count++;
                    Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None), context + " " + name);
                }
            }
            Assert.That(count, Is.EqualTo(4), context + " all embedded overlays checked");
        }
    }

    internal sealed class UiTemplatePreviewWindow : EditorWindow { }
}
