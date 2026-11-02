using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that the Blocks tiles and the Colors swatches highlight their frame while hovered, keep the armed or
    /// selected white outline, and restore the right frame on leave.
    /// </summary>
    public sealed class LevelPaletteHoverTests
    {
        private EditorWindow m_host;

        [SetUp]
        public void OpenHost()
        {
            // A shown utility window gives the controls a panel, so they receive pointer events.
            m_host = ScriptableObject.CreateInstance<EditorWindow>();
            m_host.ShowUtility();
        }

        [TearDown]
        public void CloseHost()
        {
            m_host.Close();
        }

        [Test]
        public void HoveredTileFrameHighlightsAndLeaveRestoresIt()
        {
            LevelShapePalette palette = CreatePalette();
            VisualElement single = palette.GetTile(0);

            Send<PointerEnterEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_hoverBorder));
            Assert.That(GetBorder(palette.GetTile(1)), Is.EqualTo(LevelShapePalette.s_idleBorder), "Only the hovered tile.");

            Send<PointerLeaveEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_idleBorder));
        }

        [Test]
        public void ArmedTileStaysWhiteWhileHoveredAndArmingDuringHoverLeavesNoStaleFrame()
        {
            LevelShapePalette palette = CreatePalette();
            VisualElement single = palette.GetTile(0);
            palette.SetArmed(LevelTestShapes.Key(new[] { Vector2Int.zero }));

            Send<PointerEnterEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_armedBorder), "Hover never hides the armed outline.");

            palette.SetArmed(default);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_hoverBorder), "Disarming under the pointer shows hover.");

            Send<PointerLeaveEvent>(single);
            Assert.That(GetBorder(single), Is.EqualTo(LevelShapePalette.s_idleBorder));
        }

        [Test]
        public void DeleteBadgeDoesNotHighlightTheTile()
        {
            LevelShapePalette palette = CreatePalette();

            Send<PointerEnterEvent>(palette.GetDeleteButton(0));
            Assert.That(GetBorder(palette.GetTile(0)), Is.EqualTo(LevelShapePalette.s_idleBorder), "The badge is a sibling.");
        }

        [Test]
        public void HoveredSwatchFrameHighlightsAndSelectedStaysWhite()
        {
            LevelColorSwatches swatches = new LevelColorSwatches();
            m_host.rootVisualElement.Add(swatches);
            swatches.SetSelected(PaletteColor.Red);
            VisualElement blue = swatches.GetSwatch(PaletteColor.Blue);
            VisualElement red = swatches.GetSwatch(PaletteColor.Red);

            Send<PointerEnterEvent>(blue);
            Assert.That(GetBorder(blue), Is.EqualTo(LevelColorSwatches.s_hoverBorder));
            Assert.That(GetBorder(red), Is.EqualTo(LevelColorSwatches.s_selectedBorder));

            swatches.SetSelected(PaletteColor.Blue);
            Assert.That(GetBorder(blue), Is.EqualTo(LevelColorSwatches.s_selectedBorder), "Picking under the pointer shows it.");
            Assert.That(GetBorder(red), Is.EqualTo(LevelColorSwatches.s_idleBorder));

            Send<PointerLeaveEvent>(blue);
            Assert.That(GetBorder(blue), Is.EqualTo(LevelColorSwatches.s_selectedBorder));

            Send<PointerEnterEvent>(red);
            Send<PointerLeaveEvent>(red);
            Assert.That(GetBorder(red), Is.EqualTo(LevelColorSwatches.s_idleBorder));
        }

        private LevelShapePalette CreatePalette()
        {
            LevelShapeChoice single = LevelTestShapes.Template("One Cell", new[] { Vector2Int.zero });
            LevelShapeChoice domino = LevelTestShapes.Template("Domino", new[] { Vector2Int.zero, Vector2Int.right });
            LevelShapePalette palette = new LevelShapePalette();
            palette.SetCatalog(LevelShapeCatalog.Create(new[] { single, domino }));
            m_host.rootVisualElement.Add(palette);
            return palette;
        }

        private static Color GetBorder(VisualElement element)
        {
            return element.style.borderTopColor.value;
        }

        private static void Send<T>(VisualElement target) where T : EventBase<T>, new()
        {
            using (T pointerEvent = EventBase<T>.GetPooled())
            {
                pointerEvent.target = target;
                target.SendEvent(pointerEvent);
            }
        }
    }
}
