using ColorBlockJam.Gameplay.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The opening layer of GameplayHud.uxml (user request of 2026-09-28): any number of starting boosters gets one icon
    /// each in opening-row, elements are reused when the count changes, and the layer is drawn above the booster bar so
    /// the dim covers the whole HUD.
    /// </summary>
    public sealed class OpeningLayerViewTests
    {
        private const string k_hudPath = "Assets/ColorBlockJam/UI/Game/GameplayHud.uxml";

        private VisualElement m_root;
        private GameplayHudView m_view;
        private Texture2D m_texture;
        private Sprite[] m_icons;

        [SetUp]
        public void SetUp()
        {
            VisualTreeAsset hud = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_hudPath);
            Assert.That(hud, Is.Not.Null, k_hudPath);
            m_root = hud.CloneTree().Q(GameplayHudView.RootName);
            m_view = new GameplayHudView(m_root);
            m_texture = new Texture2D(4, 4);
            m_icons = new Sprite[4];
            for (int index = 0; index < m_icons.Length; index++)
            {
                m_icons[index] = Sprite.Create(m_texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
            }
        }

        [TearDown]
        public void TearDown()
        {
            m_view.Dispose();
            for (int index = 0; index < m_icons.Length; index++)
            {
                Object.DestroyImmediate(m_icons[index]);
            }

            Object.DestroyImmediate(m_texture);
        }

        [Test]
        public void EveryIconGetsOneElementAndElementsAreReused()
        {
            VisualElement row = m_root.Q(GameplayHudView.OpeningRowName);

            m_view.SetOpeningIcons(m_icons);
            Assert.That(m_view.OpeningIconCount, Is.EqualTo(4));
            Assert.That(row.childCount, Is.EqualTo(4));
            VisualElement first = row[0];
            Assert.That(first.ClassListContains(GameplayHudView.OpeningIconClass), Is.True);
            Assert.That(first.pickingMode, Is.EqualTo(PickingMode.Ignore));

            m_view.SetOpeningIcons(new[] { m_icons[1] });
            Assert.That(m_view.OpeningIconCount, Is.EqualTo(1));
            Assert.That(row.childCount, Is.EqualTo(4), "no element is removed or created again");
            Assert.That(row[0], Is.SameAs(first));
            Assert.That(row[0].style.backgroundImage.value.sprite, Is.SameAs(m_icons[1]));

            m_view.SetOpeningIcons(new Sprite[0]);
            Assert.That(m_view.OpeningIconCount, Is.Zero);
        }

        [Test]
        public void TheDimIsDrawnAboveTheBoosterBar()
        {
            VisualElement layer = m_root.Q(GameplayHudView.OpeningLayerName);
            VisualElement bar = m_root.Q(GameplayHudView.BoosterBarName);

            Assert.That(layer.parent, Is.SameAs(bar.parent));
            Assert.That(layer.parent.IndexOf(layer), Is.GreaterThan(bar.parent.IndexOf(bar)));
            Assert.That(layer.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }
    }
}
