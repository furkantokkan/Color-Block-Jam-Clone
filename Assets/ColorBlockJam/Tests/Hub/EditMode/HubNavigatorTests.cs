using ColorBlockJam.Hub.Navigation;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the Hub overlay stack and the Back path without real input (contract scenarios 8 and 9).
    /// </summary>
    public sealed class HubNavigatorTests
    {
        private HubNavigator m_navigator;
        private FakeOverlay m_settings;
        private FakeOverlay m_preLevel;

        [SetUp]
        public void SetUp()
        {
            m_navigator = new HubNavigator();
            m_settings = new FakeOverlay();
            m_preLevel = new FakeOverlay();
        }

        [TearDown]
        public void TearDown()
        {
            m_navigator.Dispose();
        }

        [Test]
        public void Open_Unregistered_ReturnsFalseAndLogsNothing()
        {
            Assert.That(m_navigator.OpenSettings(), Is.False);
            Assert.That(m_navigator.OpenPreLevel(), Is.False);
            Assert.That(m_navigator.OpenCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Open_Registered_ShowsOnce()
        {
            m_navigator.Register(HubOverlayId.Settings, m_settings);

            Assert.That(m_navigator.OpenSettings(), Is.True);
            Assert.That(m_navigator.OpenSettings(), Is.False, "an open overlay is not pushed twice");
            Assert.That(m_settings.ShowCount, Is.EqualTo(1));
            Assert.That(m_navigator.IsOpen(HubOverlayId.Settings), Is.True);
        }

        [Test]
        public void CloseTop_TwoOverlays_ClosesOnlyTheTop()
        {
            m_navigator.Register(HubOverlayId.Settings, m_settings);
            m_navigator.Register(HubOverlayId.PreLevel, m_preLevel);
            m_navigator.OpenSettings();
            m_navigator.OpenPreLevel();

            Assert.That(m_navigator.CloseTop(), Is.True);

            Assert.That(m_preLevel.HideCount, Is.EqualTo(1));
            Assert.That(m_settings.HideCount, Is.Zero);
            Assert.That(m_navigator.IsOpen(HubOverlayId.Settings), Is.True);
            Assert.That(m_navigator.OpenCount, Is.EqualTo(1));
        }

        [Test]
        public void Close_NotTheTop_IsRefused()
        {
            m_navigator.Register(HubOverlayId.Settings, m_settings);
            m_navigator.Register(HubOverlayId.PreLevel, m_preLevel);
            m_navigator.OpenSettings();
            m_navigator.OpenPreLevel();

            Assert.That(m_navigator.Close(HubOverlayId.Settings), Is.False);
            Assert.That(m_settings.HideCount, Is.Zero);
            Assert.That(m_navigator.Close(HubOverlayId.PreLevel), Is.True);
        }

        [Test]
        public void CloseTop_NoOverlay_DoesNothing()
        {
            Assert.That(m_navigator.CloseTop(), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Unregister_OpenOverlay_HidesAndRemovesIt()
        {
            m_navigator.Register(HubOverlayId.Settings, m_settings);
            m_navigator.OpenSettings();

            m_navigator.Unregister(HubOverlayId.Settings);

            Assert.That(m_settings.HideCount, Is.EqualTo(1));
            Assert.That(m_navigator.OpenCount, Is.Zero);
            Assert.That(m_navigator.OpenSettings(), Is.False);
        }

        [Test]
        public void BackInput_Back_ClosesOnlyTheTopOverlay()
        {
            m_navigator.Register(HubOverlayId.Settings, m_settings);
            m_navigator.Register(HubOverlayId.PreLevel, m_preLevel);
            m_navigator.OpenSettings();
            m_navigator.OpenPreLevel();
            var back = new HubBackInput(m_navigator);

            Assert.That(back.Back(), Is.True);
            Assert.That(m_preLevel.HideCount, Is.EqualTo(1));
            Assert.That(m_settings.HideCount, Is.Zero);

            Assert.That(back.Back(), Is.True);
            Assert.That(m_settings.HideCount, Is.EqualTo(1));

            Assert.That(back.Back(), Is.False, "Back on Home with no overlay does nothing");
            back.Dispose();
        }

        [Test]
        public void BackInput_Disable_DisposesTheAction()
        {
            var back = new HubBackInput(m_navigator);

            back.Enable();
            Assert.That(back.IsEnabled, Is.True);

            back.Disable();
            Assert.That(back.IsEnabled, Is.False);
            Assert.That(back.HasAction, Is.False);

            back.Disable();
            back.Enable();
            Assert.That(back.IsEnabled, Is.True, "a re-enabled Hub creates a fresh action");
            back.Dispose();
            Assert.That(back.HasAction, Is.False);
        }

        private sealed class FakeOverlay : IHubOverlay
        {
            public int ShowCount { get; private set; }

            public int HideCount { get; private set; }

            public void Show()
            {
                ShowCount++;
            }

            public void Hide()
            {
                HideCount++;
            }
        }
    }
}
