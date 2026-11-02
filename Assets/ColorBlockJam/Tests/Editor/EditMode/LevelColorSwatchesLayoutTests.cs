using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that the Colors swatches are 40 px squares in two rows of 6 and 5 at the Level Editor tools pane's
    /// working widths (user answer "40 px, iki satır" of 2026-09-26), and that their colour order badges stay hidden
    /// until the Level Editor sets an order and never change that layout (TASK-20260926-145804549-da16f02f).
    /// </summary>
    public sealed class LevelColorSwatchesLayoutTests
    {
        // The swatch row of a 330 px tools pane: 6 px padding on each side and the 15 px foldout indent leave 302 px;
        // a 13 px vertical scrollbar leaves 289 px.
        private const float k_rowWidth = 289f;
        private const float k_rowWidthWithoutScrollbar = 302f;
        private const float k_swatchSize = 40f;
        private const int k_firstRowCount = 6;
        private EditorWindow m_host;

        [SetUp]
        public void OpenHost()
        {
            // A shown utility window gives the swatches a panel, so they get a layout.
            m_host = ScriptableObject.CreateInstance<EditorWindow>();
            m_host.ShowUtility();
        }

        [TearDown]
        public void CloseHost()
        {
            m_host.Close();
        }

        [TestCase(k_rowWidth)]
        [TestCase(k_rowWidthWithoutScrollbar)]
        public void ElevenFortyPixelSwatchesWrapSixAndFiveInTheToolsPane(float rowWidth)
        {
            LevelColorSwatches swatches = AddSwatches(rowWidth);
            UiTestInput.ValidateLayout(m_host.rootVisualElement);

            AssertTwoRows(swatches, LevelColorOrder.Default);
        }

        [Test]
        public void BadgesStayHiddenUntilAnOrderIsSetAndKeepTheRows()
        {
            LevelColorSwatches swatches = AddSwatches(k_rowWidth);

            for (int index = 0; index < LevelEditorToolState.k_colorCount; index++)
            {
                Assert.That(swatches.GetBadge((PaletteColor)index).style.display.value, Is.EqualTo(DisplayStyle.None));
            }

            LevelColorOrder order = LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Pink);
            swatches.SetOrder(order);
            UiTestInput.ValidateLayout(m_host.rootVisualElement);

            Assert.That(swatches.GetBadge(PaletteColor.Red).text, Is.EqualTo("11"));
            Assert.That(swatches.GetBadge(PaletteColor.Pink).text, Is.EqualTo("1"));
            Assert.That(swatches.GetBadge(PaletteColor.Blue).text, Is.EqualTo("9"));
            AssertTwoRows(swatches, order);
            for (int index = 0; index < LevelEditorToolState.k_colorCount; index++)
            {
                PaletteColor color = (PaletteColor)index;
                Rect swatch = swatches.GetSwatch(color).layout;
                Rect badge = swatches.GetBadge(color).layout;
                Assert.That(badge.center.x, Is.GreaterThan(swatch.width / 2f), $"{color} badge sits on the right.");
                Assert.That(badge.center.y, Is.LessThan(swatch.height / 2f), $"{color} badge sits at the top.");
            }
        }

        private LevelColorSwatches AddSwatches(float rowWidth)
        {
            VisualElement row = new VisualElement();
            row.style.width = rowWidth;
            LevelColorSwatches swatches = new LevelColorSwatches();
            row.Add(swatches);
            m_host.rootVisualElement.Add(row);
            return swatches;
        }

        // In order position, the first six swatches share the top row left to right and the last five the next one,
        // each 40 px square, so the grid reads 1 to 11 whatever the order.
        private static void AssertTwoRows(LevelColorSwatches swatches, LevelColorOrder order)
        {
            float firstTop = swatches.GetSwatch(order.GetColorAt(0)).layout.y;
            float secondTop = swatches.GetSwatch(order.GetColorAt(k_firstRowCount)).layout.y;
            Assert.That(secondTop, Is.GreaterThanOrEqualTo(firstTop + k_swatchSize), "The seventh swatch starts row two.");
            float previousX = float.NegativeInfinity;
            for (int position = 0; position < LevelEditorToolState.k_colorCount; position++)
            {
                PaletteColor color = order.GetColorAt(position);
                Rect swatch = swatches.GetSwatch(color).layout;
                Assert.That(swatch.width, Is.EqualTo(k_swatchSize).Within(0.5f), color.ToString());
                Assert.That(swatch.height, Is.EqualTo(k_swatchSize).Within(0.5f), color.ToString());
                float expectedTop = position < k_firstRowCount ? firstTop : secondTop;
                Assert.That(swatch.y, Is.EqualTo(expectedTop).Within(0.5f), $"{color} row");
                if (position == k_firstRowCount)
                {
                    previousX = float.NegativeInfinity;
                }

                Assert.That(swatch.x, Is.GreaterThan(previousX), $"{color} sits right of position {position}.");
                previousX = swatch.x;
            }
        }
    }
}
