using ColorBlockJam.Presentation.UI;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves that SafeAreaPadding.Calculate converts screen-pixel insets into panel units (contract scenario 11).
    /// </summary>
    public sealed class SafeAreaPaddingTests
    {
        private const float k_tolerance = 0.001f;

        [Test]
        public void Calculate_FullScreenSafeArea_ReturnsZero()
        {
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(0f, 0f, 1080f, 1920f), new Vector2(1080f, 1920f), new Vector2(1080f, 1920f));

            AssertInsets(insets, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void Calculate_TopNotchAndBottomInset_ReturnsInsetsAtScaleOne()
        {
            // 1080 x 2400 with a 110 px notch and a 70 px home indicator; Screen.safeArea origin is bottom left.
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(0f, 70f, 1080f, 2400f - 110f - 70f), new Vector2(1080f, 2400f), new Vector2(1080f, 2400f));

            AssertInsets(insets, 0f, 110f, 0f, 70f);
        }

        [Test]
        public void Calculate_BottomInsetOnly_ReturnsBottomOnly()
        {
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(0f, 48f, 1080f, 1872f), new Vector2(1080f, 1920f), new Vector2(1080f, 1920f));

            AssertInsets(insets, 0f, 0f, 0f, 48f);
        }

        [Test]
        public void Calculate_PanelScaleNotOne_ConvertsToPanelUnits()
        {
            // A 1440 x 3200 screen shown in a 1080 x 2400 panel: every pixel is 0.75 panel units.
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(40f, 100f, 1440f - 40f - 20f, 3200f - 160f - 100f), new Vector2(1440f, 3200f), new Vector2(1080f, 2400f));

            AssertInsets(insets, 30f, 120f, 15f, 75f);
        }

        [Test]
        public void Calculate_ZeroScreenSize_ReturnsZero()
        {
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(0f, 10f, 100f, 100f), Vector2.zero, new Vector2(1080f, 1920f));

            AssertInsets(insets, 0f, 0f, 0f, 0f);
        }

        [Test]
        public void Mask_KeepsOnlyTheChosenEdges()
        {
            var insets = new SafeAreaInsets(10f, 110f, 20f, 70f);

            AssertInsets(SafeAreaPadding.Mask(insets, SafeAreaEdges.Left | SafeAreaEdges.Top | SafeAreaEdges.Right),
                10f, 110f, 20f, 0f);
            AssertInsets(SafeAreaPadding.Mask(insets, SafeAreaEdges.Left | SafeAreaEdges.Right | SafeAreaEdges.Bottom),
                10f, 0f, 20f, 70f);
            AssertInsets(SafeAreaPadding.Mask(insets, SafeAreaEdges.All), 10f, 110f, 20f, 70f);
        }

        [Test]
        public void Calculate_PanelNotLaidOut_ReturnsZero()
        {
            SafeAreaInsets insets = SafeAreaPadding.Calculate(
                new Rect(0f, 70f, 1080f, 2220f), new Vector2(1080f, 2400f), new Vector2(float.NaN, float.NaN));

            AssertInsets(insets, 0f, 0f, 0f, 0f);
        }

        private static void AssertInsets(SafeAreaInsets insets, float left, float top, float right, float bottom)
        {
            Assert.That(insets.Left, Is.EqualTo(left).Within(k_tolerance), "left");
            Assert.That(insets.Top, Is.EqualTo(top).Within(k_tolerance), "top");
            Assert.That(insets.Right, Is.EqualTo(right).Within(k_tolerance), "right");
            Assert.That(insets.Bottom, Is.EqualTo(bottom).Within(k_tolerance), "bottom");
        }
    }
}
