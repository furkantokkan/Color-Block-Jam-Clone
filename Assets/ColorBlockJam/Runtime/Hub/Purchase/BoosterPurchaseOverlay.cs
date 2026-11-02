using System;
using System.Threading;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.UI.Purchase;

namespace ColorBlockJam.Hub.Purchase
{
    /// <summary>
    /// Adapts the shared booster purchase popup to the Hub's overlay stack: <see cref="Open"/> pushes it above the
    /// pre-level popup for one booster, the navigator shows and hides it, the overlay below is covered (hidden) while
    /// it shows, and its close request (close button) closes it through the navigator (design 9.2, DEC-136).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle, bound to itself only, so <see cref="IHubOverlay"/> keeps one container
    /// binding). <c>HubScreen</c> binds it with its lifetime token, registers it with <see cref="HubNavigator"/> and
    /// unbinds it in <c>OnDisable</c>; the container disposes it at Hub unload. Back reaches it through
    /// <see cref="HubBackInput"/> and the navigator, never directly.
    /// </remarks>
    public sealed class BoosterPurchaseOverlay : IHubOverlay, IDisposable
    {
        private readonly BoosterPurchasePresenter m_presenter;
        private readonly IHubNavigator m_navigator;
        private readonly Action m_onCloseRequested;
        private BoosterDefinition m_pending;
        private ICoverableOverlay m_covered;
        private bool m_isBound;

        public BoosterPurchaseOverlay(BoosterPurchasePresenter presenter, IHubNavigator navigator)
        {
            m_presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            m_onCloseRequested = OnCloseRequested;
        }

        public bool IsBound => m_isBound;

        /// <summary>Binds the shared presenter to the Hub's popup view.</summary>
        /// <param name="screenToken">The Hub screen's token; every purchase started by this bind is linked to it.</param>
        public void Bind(IBoosterPurchaseView view, CancellationToken screenToken)
        {
            if (m_isBound)
            {
                throw new InvalidOperationException("Booster purchase overlay: already bound; unbind first.");
            }

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
            m_pending = null;
            m_covered = null;
            m_presenter.CloseRequested -= m_onCloseRequested;
            m_presenter.Unbind();
        }

        /// <summary>
        /// Opens the purchase popup for <paramref name="booster"/> on top of the stack, covering
        /// <paramref name="below"/> (the popup that asked, or null) until it closes, so the purchase popup shows alone
        /// over the dimmed Home (reference 8); false when unbound, already open or <paramref name="booster"/> is null.
        /// </summary>
        public bool Open(BoosterDefinition booster, ICoverableOverlay below)
        {
            if (!m_isBound || booster == null || m_navigator.IsOpen(HubOverlayId.BoosterPurchase))
            {
                return false;
            }

            m_pending = booster;
            m_covered = below;
            if (!m_navigator.Open(HubOverlayId.BoosterPurchase))
            {
                m_pending = null;
                m_covered = null;
                return false;
            }

            return true;
        }

        /// <summary>Called by the navigator when the popup opens: hides the covered overlay, then shows the popup.</summary>
        public void Show()
        {
            m_covered?.SetCovered(true);
            m_presenter.Show(m_pending);
        }

        /// <summary>
        /// Called by the navigator when the popup closes (close button, Back or unregistering): hides the popup, then
        /// shows the covered overlay again with its state intact.
        /// </summary>
        public void Hide()
        {
            ICoverableOverlay covered = m_covered;
            m_pending = null;
            m_covered = null;
            m_presenter.Hide();
            covered?.SetCovered(false);
        }

        public void Dispose()
        {
            Unbind();
        }

        private void OnCloseRequested()
        {
            m_navigator.Close(HubOverlayId.BoosterPurchase);
        }
    }
}
