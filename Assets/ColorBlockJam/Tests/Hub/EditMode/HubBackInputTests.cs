using ColorBlockJam.Hub.Navigation;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Drives the Hub Back action with a simulated keyboard (contract scenario 9); InputTestFixture isolates
    /// the Input System per test.
    /// </summary>
    public sealed class HubBackInputTests : InputTestFixture
    {
        [Test]
        public void Escape_ClosesOnlyTheTopOverlay_AndDoesNothingOnHome()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var navigator = new HubNavigator();
            var settings = new CountingOverlay();
            var preLevel = new CountingOverlay();
            navigator.Register(HubOverlayId.Settings, settings);
            navigator.Register(HubOverlayId.PreLevel, preLevel);
            navigator.OpenSettings();
            navigator.OpenPreLevel();
            var back = new HubBackInput(navigator);
            back.Enable();

            PressAndRelease(keyboard.escapeKey);
            Assert.That(preLevel.HideCount, Is.EqualTo(1));
            Assert.That(settings.HideCount, Is.Zero);
            Assert.That(navigator.IsOpen(HubOverlayId.Settings), Is.True);

            PressAndRelease(keyboard.escapeKey);
            Assert.That(settings.HideCount, Is.EqualTo(1));
            Assert.That(navigator.OpenCount, Is.Zero);

            PressAndRelease(keyboard.escapeKey);
            Assert.That(settings.HideCount + preLevel.HideCount, Is.EqualTo(2), "Back on Home does nothing");

            back.Dispose();
            navigator.Dispose();
        }

        [Test]
        public void Escape_AfterDisable_ReachesNoOverlay()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            var navigator = new HubNavigator();
            var settings = new CountingOverlay();
            navigator.Register(HubOverlayId.Settings, settings);
            navigator.OpenSettings();
            var back = new HubBackInput(navigator);
            back.Enable();
            back.Disable();

            PressAndRelease(keyboard.escapeKey);

            Assert.That(back.HasAction, Is.False, "disabling Hub disposes the InputAction");
            Assert.That(settings.HideCount, Is.Zero);
            Assert.That(navigator.IsOpen(HubOverlayId.Settings), Is.True);
            navigator.Dispose();
        }

        private void PressAndRelease(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            Press(key);
            Release(key);
        }

        private sealed class CountingOverlay : IHubOverlay
        {
            public int HideCount { get; private set; }

            public void Show()
            {
            }

            public void Hide()
            {
                HideCount++;
            }
        }
    }
}
