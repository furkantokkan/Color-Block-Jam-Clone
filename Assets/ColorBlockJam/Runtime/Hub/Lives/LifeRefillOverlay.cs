using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI.Purchase;
using Cysharp.Text;

namespace ColorBlockJam.Hub.Lives
{
    /// <summary>
    /// The Hub's life refill popup (DEC-174): it shows the life count, the time to the next life and a Buy that refills
    /// every life for the life currency's refill price, on the shared purchase popup's layout (a second instance of
    /// BoosterPurchasePopup.uxml). It opens as the "life-refill" menu on the Hub overlay stack, from the Level button
    /// at 0 lives and when Hub shows with none left.
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle, bound to itself only, so IHubOverlay keeps one container binding).
    /// <c>HubScreen</c> binds it with its lifetime token, registers it with the navigator, calls <see cref="Tick"/> from
    /// its view-owned timer and unbinds it in <c>OnDisable</c>. Each refill is one owned task in
    /// <see cref="PendingRefill"/>, linked to the bind token; a result after the popup closed shows nothing. Text changes
    /// only when the shown count or second changes.
    /// </remarks>
    public sealed class LifeRefillOverlay : IHubOverlay, IDisposable
    {
        public const string MenuId = "life-refill";
        public const string Title = "Lives";
        public const string NextLifePrefix = "Next life in ";
        public const string FullText = "Your lives are full!";
        public const string NotEnoughPrefix = "Not enough ";
        public const string InvalidOfferMessage = "This offer is not available.";
        public const string SaveFailedMessage = "Couldn't save. Try again.";

        private const int k_notShown = -1;

        private readonly ILives m_lives;
        private readonly IHubNavigator m_navigator;
        private readonly IHubMenuNavigator m_menus;
        private readonly Action m_onBuyPressed;
        private readonly Action m_onCloseRequested;

        private IBoosterPurchaseView m_view;
        private CancellationTokenSource m_bindCancellation;
        private int m_shownCount = k_notShown;
        private long m_shownSeconds = k_notShown;
        private bool m_isShown;
        private bool m_isRefilling;
        private bool m_disposed;

        public LifeRefillOverlay(ILives lives, IHubNavigator navigator, IHubMenuNavigator menus)
        {
            m_lives = lives ?? throw new ArgumentNullException(nameof(lives));
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            m_menus = menus ?? throw new ArgumentNullException(nameof(menus));
            m_onBuyPressed = OnBuyPressed;
            m_onCloseRequested = OnCloseRequested;
        }

        public bool IsBound => m_view != null;

        /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
        public bool IsShown => m_isShown;

        /// <summary>The last refill started by Buy; a completed task when none ran. It never faults.</summary>
        public Task PendingRefill { get; private set; } = Task.CompletedTask;

        /// <summary>Binds the popup view; it starts hidden.</summary>
        /// <param name="hostToken">The Hub screen's token; every refill started by this bind is linked to it.</param>
        public void Bind(IBoosterPurchaseView view, CancellationToken hostToken)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(LifeRefillOverlay));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Life refill overlay: already bound; unbind first.");
            }

            m_view = view ?? throw new ArgumentNullException(nameof(view));
            m_bindCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
            m_view.BuyPressed += m_onBuyPressed;
            m_view.CloseRequested += m_onCloseRequested;
            m_view.SetVisible(false);
        }

        /// <summary>Detaches the view and cancels a refill's wait; safe to call more than once.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.BuyPressed -= m_onBuyPressed;
            m_view.CloseRequested -= m_onCloseRequested;
            m_view = null;
            m_isShown = false;
            m_isRefilling = false;
            m_bindCancellation.Cancel();
            m_bindCancellation.Dispose();
            m_bindCancellation = null;
        }

        /// <summary>Opens the popup on top of the Hub stack; false when unbound or already open.</summary>
        public bool Open()
        {
            return m_view != null && !m_isShown && m_menus.OpenMenu(MenuId);
        }

        /// <summary>Called by the navigator when the popup opens.</summary>
        public void Show()
        {
            if (m_view == null)
            {
                return;
            }

            m_isShown = true;
            m_shownCount = k_notShown;
            m_shownSeconds = k_notShown;
            m_view.SetMessage(string.Empty);
            Refresh();
            m_view.SetVisible(true);
        }

        /// <summary>Called by the navigator when the popup closes (close button, Back or unregistering).</summary>
        public void Hide()
        {
            m_isShown = false;
            m_view?.SetVisible(false);
        }

        /// <summary>The Hub screen's timer: updates the count and the countdown while the popup shows.</summary>
        public void Tick()
        {
            if (m_isShown)
            {
                Refresh();
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_disposed = true;
        }

        private void Refresh()
        {
            int count = m_lives.GetAvailable();
            long seconds = m_lives.GetSecondsUntilNext();
            if (count == m_shownCount && seconds == m_shownSeconds)
            {
                return;
            }

            m_shownCount = count;
            m_shownSeconds = seconds;
            CurrencyDefinition life = m_lives.Currency;
            CurrencyDefinition price = life.RefillPriceCurrency;
            bool full = count >= life.MaxAmount;
            string description = full ? FullText : ZString.Concat(NextLifePrefix, LifeCountdownText.Format(seconds));
            m_view.SetOffer(Title, life.Icon, ZString.Concat(count), description,
                price != null ? price.Icon : null, ZString.Concat(life.RefillPrice));
            m_view.SetBuyInteractable(!m_isRefilling && !full && life.HasRefillOffer);
        }

        private void OnBuyPressed()
        {
            if (!m_isShown || m_isRefilling || m_view == null)
            {
                return;
            }

            m_isRefilling = true;
            m_view.SetBuyInteractable(false);
            m_view.SetMessage(string.Empty);
            PendingRefill = RefillAsync(m_bindCancellation.Token);
        }

        // Owner: this overlay, which keeps the task in PendingRefill. Stops: when the refill returns; the bind token
        // cancels only the wait for the save lock, a started write completes under the save service's lifetime.
        private async Task RefillAsync(CancellationToken cancellationToken)
        {
            CurrencyRefillResult result = await m_lives.RefillAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested || m_view == null)
            {
                return;
            }

            m_isRefilling = false;
            if (!m_isShown)
            {
                return;
            }

            switch (result)
            {
                case CurrencyRefillResult.Refilled:
                case CurrencyRefillResult.AlreadyFull:
                    m_navigator.CloseTop();
                    return;
                case CurrencyRefillResult.InsufficientFunds:
                    CurrencyDefinition price = m_lives.Currency.RefillPriceCurrency;
                    m_view.SetMessage(price != null
                        ? ZString.Concat(NotEnoughPrefix, price.DisplayName.ToLowerInvariant())
                        : InvalidOfferMessage);
                    break;
                case CurrencyRefillResult.InvalidOffer:
                    m_view.SetMessage(InvalidOfferMessage);
                    break;
                case CurrencyRefillResult.SaveFailed:
                    m_view.SetMessage(SaveFailedMessage);
                    break;
            }

            m_shownCount = k_notShown;
            Refresh();
        }

        private void OnCloseRequested()
        {
            if (m_isShown)
            {
                m_navigator.CloseTop();
            }
        }
    }
}
