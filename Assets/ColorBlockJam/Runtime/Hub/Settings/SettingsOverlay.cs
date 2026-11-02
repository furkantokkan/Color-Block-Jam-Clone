using System;
using System.Threading;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.UI.Settings;

namespace ColorBlockJam.Hub.Settings
{
    /// <summary>
    /// Adapts the shared Settings popup to the Hub's overlay stack: the navigator shows and hides it, and its close
    /// requests (close button, outside tap) close it through the navigator; HOME stays hidden on Home (design 8,
    /// DEC-136).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). <c>HubScreen</c> binds it with its lifetime token, registers it
    /// with <see cref="HubNavigator"/> and unbinds it in <c>OnDisable</c>; the container disposes it at Hub unload.
    /// Back reaches it through <see cref="HubBackInput"/> and the navigator, never directly.
    /// </remarks>
    public sealed class SettingsOverlay : IHubOverlay, IDisposable
    {
        private readonly SettingsPopupPresenter m_presenter;
        private readonly IHubNavigator m_navigator;
        private readonly Action m_onCloseRequested;
        private bool m_isBound;

        public SettingsOverlay(SettingsPopupPresenter presenter, IHubNavigator navigator)
        {
            m_presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            m_onCloseRequested = OnCloseRequested;
        }

        public bool IsBound => m_isBound;

        /// <summary>Binds the shared presenter to the Hub's popup view with HOME hidden.</summary>
        /// <param name="screenToken">The Hub screen's token; every save started by this bind is linked to it.</param>
        public void Bind(ISettingsPopupView view, CancellationToken screenToken)
        {
            if (m_isBound)
            {
                throw new InvalidOperationException("Settings overlay: already bound; unbind first.");
            }

            m_presenter.SetHomeVisible(false);
            m_presenter.Bind(view, screenToken);
            m_presenter.CloseRequested += m_onCloseRequested;
            m_isBound = true;
        }

        /// <summary>Detaches the close request and unbinds the presenter; safe to call more than once.</summary>
        public void Unbind()
        {
            if (!m_isBound)
            {
                return;
            }

            m_isBound = false;
            m_presenter.CloseRequested -= m_onCloseRequested;
            m_presenter.Unbind();
        }

        /// <summary>Called by the navigator when Settings opens.</summary>
        public void Show()
        {
            m_presenter.Show();
        }

        /// <summary>Called by the navigator when Settings closes.</summary>
        public void Hide()
        {
            m_presenter.Hide();
        }

        public void Dispose()
        {
            Unbind();
        }

        private void OnCloseRequested()
        {
            m_navigator.Close(HubOverlayId.Settings);
        }
    }
}
