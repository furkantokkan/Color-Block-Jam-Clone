using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.UI.Purchase
{
    /// <summary>
    /// Drives the shared booster purchase popup (reference 01): it shows one booster's offer, buys it through
    /// <see cref="IBoosterShop"/> with Buy disabled while any save is pending, and shows failure results as messages;
    /// a successful purchase requests the host to close the popup (design 9.2, DEC-85, DEC-136, AC-6.7 to AC-6.9).
    /// </summary>
    /// <remarks>
    /// Owner: the host scene's container (AsSingle); the host binds it with its own lifetime token and unbinds it when
    /// the view goes away. It knows no navigator or scene: the Hub's BoosterPurchaseOverlay maps
    /// <see cref="CloseRequested"/> to its overlay stack, and the Game's InGamePurchaseHost hides the popup and
    /// resumes from Paused(Purchase) (DEC-151). The IsSaving subscription
    /// goes into one CompositeDisposable cleared by <see cref="Unbind"/>. Each purchase is one owned task kept in
    /// <see cref="PendingPurchase"/>; its token is linked to the host token and cancelled by <see cref="Unbind"/>, and a
    /// result that arrives after the offer closed or changed shows nothing. It never selects or uses the bought booster.
    /// </remarks>
    public sealed class BoosterPurchasePresenter : IDisposable
    {
        public const string PurchasedPrefix = "Purchased! You have ";
        public const string NotEnoughPrefix = PopupMessages.NotEnoughPrefix;
        public const string InvalidOfferMessage = PopupMessages.InvalidOffer;
        public const string StockFullMessage = "You already have the maximum.";
        public const string SaveFailedMessage = PopupMessages.SaveFailed;

        private const string k_amountPrefix = "x";

        private readonly IBoosterShop m_shop;
        private readonly IBoosterInventory m_inventory;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Action m_onBuyPressed;
        private readonly Action m_onCloseRequested;
        private readonly Observer<bool> m_onSavingChanged;

        private IBoosterPurchaseView m_view;
        private CancellationTokenSource m_bindCancellation;
        private BoosterDefinition m_booster;
        private int m_offerGeneration;
        private bool m_isSaving;
        private bool m_isPurchasing;
        private bool m_disposed;

        public BoosterPurchasePresenter(IBoosterShop shop, IBoosterInventory inventory)
        {
            m_shop = shop ?? throw new ArgumentNullException(nameof(shop));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_onBuyPressed = OnBuyPressed;
            m_onCloseRequested = OnCloseRequested;
            m_onSavingChanged = OnSavingChanged;
        }

        /// <summary>Raised by the close button or a successful purchase; the host decides what closing means.</summary>
        public event Action CloseRequested;

        public bool IsBound => m_view != null;

        /// <summary>True between <see cref="Show"/> and <see cref="Hide"/>.</summary>
        public bool IsVisible => m_booster != null;

        /// <summary>The booster the popup offers while it is shown; null when hidden.</summary>
        public BoosterDefinition Booster => m_booster;

        /// <summary>The last purchase started by Buy; a completed task when none ran. It never faults.</summary>
        public Task PendingPurchase { get; private set; } = Task.CompletedTask;

        /// <summary>Binds the view; the popup starts hidden until the host shows an offer.</summary>
        /// <param name="hostToken">The host's lifetime token; every purchase started by this bind is linked to it.</param>
        public void Bind(IBoosterPurchaseView view, CancellationToken hostToken)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(BoosterPurchasePresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Booster purchase presenter: already bound; unbind first.");
            }

            m_view = view;
            m_bindCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
            m_booster = null;
            m_isPurchasing = false;
            m_view.BuyPressed += m_onBuyPressed;
            m_view.CloseRequested += m_onCloseRequested;
            m_view.SetMessage(string.Empty);
            m_view.SetVisible(false);

            // Emits the current value at once, then on every change.
            m_subscriptions.Add(m_shop.IsSaving.Subscribe(m_onSavingChanged));
        }

        /// <summary>Cancels a pending purchase wait, disposes the subscription and detaches the view.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.BuyPressed -= m_onBuyPressed;
            m_view.CloseRequested -= m_onCloseRequested;
            m_view = null;
            m_subscriptions.Clear();
            m_booster = null;
            m_offerGeneration++;
            m_isPurchasing = false;

            // Detached first: a purchase that completes inside Cancel() finds no view and changes nothing.
            CancellationTokenSource cancellation = m_bindCancellation;
            m_bindCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        /// <summary>Shows the offer of <paramref name="booster"/> with an empty message; false when unbound or null.</summary>
        public bool Show(BoosterDefinition booster)
        {
            if (m_view == null || booster == null)
            {
                return false;
            }

            m_booster = booster;
            m_offerGeneration++;
            Sprite currencyIcon = null;
            string price = string.Empty;
            if (TryGetPrice(booster, out CurrencyDefinition currency, out int amount))
            {
                currencyIcon = currency.Icon;
                price = CurrencyText.FormatCompact(amount);
            }

            m_view.SetOffer(booster.DisplayName, booster.Icon,
                string.Concat(k_amountPrefix, CurrencyText.FormatCompact(booster.PurchaseAmount)), booster.Description,
                currencyIcon, price);
            m_view.SetMessage(string.Empty);
            UpdateBuy();
            m_view.SetVisible(true);
            return true;
        }

        /// <summary>Hides the popup; a purchase the service already started still completes, silently.</summary>
        public void Hide()
        {
            m_booster = null;
            m_offerGeneration++;
            if (m_view == null)
            {
                return;
            }

            m_view.SetVisible(false);
            m_view.SetMessage(string.Empty);
            UpdateBuy();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            CloseRequested = null;
            m_disposed = true;
        }

        /// <summary>
        /// The one place this popup reads a booster's price (design 3.2); false when the offer names no currency.
        /// A queued change turns the two fields into one CurrencyAmount here.
        /// </summary>
        private static bool TryGetPrice(BoosterDefinition booster, out CurrencyDefinition currency, out int price)
        {
            currency = booster.PurchaseCurrency;
            price = booster.PurchasePrice;
            return currency != null;
        }

        private void OnBuyPressed()
        {
            if (m_view == null || m_booster == null || m_isPurchasing || m_isSaving)
            {
                return;
            }

            m_isPurchasing = true;
            m_view.SetMessage(string.Empty);
            UpdateBuy();
            Task purchase = PurchaseAsync(m_booster, m_offerGeneration, m_bindCancellation.Token);
            PendingPurchase = purchase;
        }

        /// <summary>
        /// Owner: this presenter, which keeps the task in <see cref="PendingPurchase"/>. Stops: when the shop returns,
        /// or when <paramref name="cancellationToken"/> (this bind, linked to the host token) is cancelled; the save
        /// service decides whether a started write completes (design 4.4).
        /// </summary>
        private async Task PurchaseAsync(BoosterDefinition booster, int generation, CancellationToken cancellationToken)
        {
            BoosterPurchaseResult result;
            try
            {
                result = await m_shop.PurchaseAsync(booster, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                result = BoosterPurchaseResult.Cancelled;
            }
            catch (Exception exception)
            {
                // The task is stored, never awaited by a caller, so an unexpected fault is logged here instead of lost.
                Debug.LogException(exception);
                result = BoosterPurchaseResult.SaveFailed;
            }

            if (cancellationToken.IsCancellationRequested || m_view == null)
            {
                return;
            }

            m_isPurchasing = false;
            if (generation == m_offerGeneration && result != BoosterPurchaseResult.Cancelled)
            {
                if (result == BoosterPurchaseResult.Purchased)
                {
                    UpdateBuy();
                    CloseRequested?.Invoke();
                    return;
                }

                m_view.SetMessage(GetMessage(booster, result));
            }

            UpdateBuy();
        }

        private string GetMessage(BoosterDefinition booster, BoosterPurchaseResult result)
        {
            switch (result)
            {
                case BoosterPurchaseResult.Purchased:
                    // The service publishes the committed stock before the purchase task completes (design 4.4).
                    return string.Concat(PurchasedPrefix, CurrencyText.FormatCompact(m_inventory.GetStock(booster.Id).Value), ".");
                case BoosterPurchaseResult.InsufficientFunds:
                    return TryGetPrice(booster, out CurrencyDefinition currency, out _)
                        ? string.Concat(NotEnoughPrefix, currency.DisplayName.ToLowerInvariant())
                        : InvalidOfferMessage;
                case BoosterPurchaseResult.StockFull:
                    return StockFullMessage;
                case BoosterPurchaseResult.SaveFailed:
                    return SaveFailedMessage;
                default:
                    return InvalidOfferMessage;
            }
        }

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke();
        }

        private void OnSavingChanged(bool isSaving)
        {
            m_isSaving = isSaving;
            UpdateBuy();
        }

        private void UpdateBuy()
        {
            m_view?.SetBuyInteractable(m_booster != null && !m_isSaving && !m_isPurchasing);
        }
    }
}
