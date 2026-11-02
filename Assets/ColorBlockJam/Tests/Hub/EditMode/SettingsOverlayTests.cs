using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.Settings;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Settings;
using NUnit.Framework;
using Onity.Reactive;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the Hub adapter of the shared Settings popup with the real navigator and Back input (contract
    /// scenarios 5 and 8): the navigator opens and closes it, close requests and Back go through the navigator, and
    /// the Hub keeps HOME hidden.
    /// </summary>
    public sealed class SettingsOverlayTests
    {
        private FakeSettings m_settings;
        private HubNavigator m_navigator;
        private FakeView m_view;
        private SettingsPopupPresenter m_presenter;
        private SettingsOverlay m_overlay;

        [SetUp]
        public void SetUp()
        {
            m_settings = new FakeSettings();
            m_navigator = new HubNavigator();
            m_view = new FakeView();
            m_presenter = new SettingsPopupPresenter(m_settings);
            m_overlay = new SettingsOverlay(m_presenter, m_navigator);
        }

        [TearDown]
        public void TearDown()
        {
            m_overlay.Dispose();
            m_presenter.Dispose();
            m_navigator.Dispose();
            m_settings.Dispose();
        }

        [Test]
        public void Bind_HidesHome_AndStartsClosed()
        {
            m_presenter.SetHomeVisible(true);

            m_overlay.Bind(m_view, CancellationToken.None);

            Assert.That(m_view.HomeVisible, Is.False, "the Hub never shows HOME");
            Assert.That(m_presenter.IsHomeVisible, Is.False);
            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_overlay.IsBound, Is.True);
        }

        [Test]
        public void Navigator_OpenSettings_ShowsThePopup()
        {
            Register();

            Assert.That(m_navigator.OpenSettings(), Is.True);

            Assert.That(m_view.Visible, Is.True);
            Assert.That(m_presenter.IsVisible, Is.True);
            Assert.That(m_navigator.IsOpen(HubOverlayId.Settings), Is.True);
        }

        [Test]
        public void CloseRequest_ClosesSettingsThroughTheNavigator()
        {
            Register();
            m_navigator.OpenSettings();

            // The close button and a tap outside the popup both raise the view's CloseRequested.
            m_view.RequestClose();

            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_navigator.OpenCount, Is.Zero);
        }

        [Test]
        public void Back_ClosesSettings_ThenDoesNothing()
        {
            Register();
            m_navigator.OpenSettings();
            using var back = new HubBackInput(m_navigator);

            Assert.That(back.Back(), Is.True);

            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_navigator.IsOpen(HubOverlayId.Settings), Is.False);
            Assert.That(back.Back(), Is.False);
        }

        [Test]
        public void Unregister_WhileOpen_HidesSettings()
        {
            Register();
            m_navigator.OpenSettings();

            m_navigator.Unregister(HubOverlayId.Settings);

            Assert.That(m_view.Visible, Is.False);
            Assert.That(m_presenter.IsVisible, Is.False);
            Assert.That(m_navigator.OpenSettings(), Is.False);
        }

        [Test]
        public void Unbind_StopsForwardingCloseRequests_AndUnbindsThePresenter()
        {
            Register();
            m_navigator.OpenSettings();

            m_overlay.Unbind();

            Assert.That(m_presenter.IsBound, Is.False);
            Assert.That(m_view.HasListeners, Is.False);
            Assert.That(m_navigator.IsOpen(HubOverlayId.Settings), Is.True, "only the navigator closes it");
            Assert.DoesNotThrow(() => m_overlay.Unbind());
        }

        [Test]
        public void Bind_Twice_Throws()
        {
            m_overlay.Bind(m_view, CancellationToken.None);

            Assert.Throws<InvalidOperationException>(() => m_overlay.Bind(new FakeView(), CancellationToken.None));
        }

        [Test]
        public void TogglePress_ThroughTheOverlay_SavesTheSetting()
        {
            Register();
            m_navigator.OpenSettings();

            m_view.PressToggle(PlayerSettingId.Vibration);

            Assert.That(m_settings.LastId, Is.EqualTo(PlayerSettingId.Vibration));
            Assert.That(m_settings.LastValue, Is.False);
        }

        private void Register()
        {
            m_overlay.Bind(m_view, CancellationToken.None);
            m_navigator.Register(HubOverlayId.Settings, m_overlay);
        }

        /// <summary>All settings ON; SetAsync records the call and completes only as Cancelled on unbind.</summary>
        private sealed class FakeSettings : IPlayerSettings, IDisposable
        {
            private readonly ReactiveProperty<bool>[] m_values =
            {
                new ReactiveProperty<bool>(true), new ReactiveProperty<bool>(true), new ReactiveProperty<bool>(true)
            };

            public PlayerSettingId LastId { get; private set; }

            public bool LastValue { get; private set; } = true;

            public IReadOnlyReactiveProperty<bool> Get(PlayerSettingId id)
            {
                return m_values[(int)id];
            }

            public Task<PlayerSaveResult> SetAsync(PlayerSettingId id, bool value, CancellationToken cancellationToken)
            {
                LastId = id;
                LastValue = value;
                var completion = new TaskCompletionSource<PlayerSaveResult>();
                cancellationToken.Register(() => completion.TrySetResult(PlayerSaveResult.Cancelled));
                return completion.Task;
            }

            public void Dispose()
            {
                for (int i = 0; i < m_values.Length; i++)
                {
                    m_values[i].Dispose();
                }
            }
        }

        private sealed class FakeView : ISettingsPopupView
        {
            public event Action<PlayerSettingId> TogglePressed;

            public event Action CloseRequested;

            public event Action HomePressed;

            public bool Visible { get; private set; }

            public bool HomeVisible { get; private set; } = true;

            public bool HasListeners => TogglePressed != null || CloseRequested != null || HomePressed != null;

            public void SetVisible(bool visible)
            {
                Visible = visible;
            }

            public void SetToggle(PlayerSettingId id, bool isOn)
            {
            }

            public void SetMessage(string text)
            {
            }

            public void SetHomeVisible(bool visible)
            {
                HomeVisible = visible;
            }

            public void PressToggle(PlayerSettingId id)
            {
                TogglePressed?.Invoke(id);
            }

            public void RequestClose()
            {
                CloseRequested?.Invoke();
            }
        }
    }
}
