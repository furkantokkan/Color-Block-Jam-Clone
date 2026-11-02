using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.UI.Purchase
{
    /// <summary>
    /// Wraps the shared offer popup's VisualElement subtree (BoosterPurchasePopup.uxml, one instance per host: booster
    /// purchase and life refill, DEC-174): it shows or hides the popup, writes the offer text and icons into the
    /// two-layer heavy labels, the message and Buy's state, and forwards Buy and close clicks; it reads no service and
    /// decides nothing (design 9.2, DEC-136).
    /// </summary>
    /// <remarks>
    /// Owner: the host screen, which creates it on bind and disposes it on unbind. <see cref="Dispose"/> removes the
    /// click handlers and hides the popup. Press feedback is USS only (.cbj-pressable). The scrim blocks the host
    /// underneath (AC-2.6) but does not close the popup. The host pads <see cref="SafeAreaName"/> from its safe area.
    /// </remarks>
    public sealed class BoosterPurchaseView : IBoosterPurchaseView, IDisposable
    {
        private const float k_popupIntroStartScale = 0.96f;
        private const float k_popupIntroSeconds = 0.15f;

        public const string ScreenName = "booster-purchase-screen";
        public const string ScrimName = "booster-purchase-scrim";
        public const string SafeAreaName = "booster-purchase-safe-area";
        public const string PopupName = "booster-purchase-popup";
        public const string TitleName = "booster-purchase-title";
        public const string CloseButtonName = "booster-purchase-close";
        public const string IconName = "booster-purchase-icon";
        public const string AmountName = "booster-purchase-amount";
        public const string DescriptionName = "booster-purchase-description";
        public const string MessageName = "booster-purchase-message";
        public const string BuyButtonName = "booster-purchase-buy";
        public const string PriceIconName = "booster-purchase-price-icon";
        public const string PriceName = "booster-purchase-price";

        private const string k_heavyBackClass = "cbj-heavy-text__back";

        private readonly VisualElement m_screen;
        private readonly VisualElement m_popup;
        private readonly Label m_title;
        private readonly Label m_titleBack;
        private readonly Button m_closeButton;
        private readonly VisualElement m_icon;
        private readonly Label m_amount;
        private readonly Label m_amountBack;
        private readonly Label m_description;
        private readonly Label m_descriptionBack;
        private readonly Label m_message;
        private readonly Button m_buyButton;
        private readonly VisualElement m_priceIcon;
        private readonly Label m_price;
        private readonly Label m_priceBack;
        private readonly Action m_onBuyClicked;
        private readonly Action m_onCloseClicked;
        private Tween m_popupIntro;
        private bool m_disposed;

        /// <param name="screen">The <c>booster-purchase-screen</c> element of BoosterPurchasePopup.uxml.</param>
        public BoosterPurchaseView(VisualElement screen)
        {
            m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
            m_popup = Require<VisualElement>(screen, PopupName);
            m_title = Require<Label>(screen, TitleName);
            m_titleBack = RequireBack(m_title);
            m_closeButton = Require<Button>(screen, CloseButtonName);
            m_icon = Require<VisualElement>(screen, IconName);
            m_amount = Require<Label>(screen, AmountName);
            m_amountBack = RequireBack(m_amount);
            m_description = Require<Label>(screen, DescriptionName);
            m_descriptionBack = RequireBack(m_description);
            m_message = Require<Label>(screen, MessageName);
            m_buyButton = Require<Button>(screen, BuyButtonName);
            m_priceIcon = Require<VisualElement>(screen, PriceIconName);
            m_price = Require<Label>(screen, PriceName);
            m_priceBack = RequireBack(m_price);

            m_onBuyClicked = OnBuyClicked;
            m_onCloseClicked = OnCloseClicked;
            m_buyButton.clicked += m_onBuyClicked;
            m_closeButton.clicked += m_onCloseClicked;
            // The reusable UXML is visible for artists; constructing its runtime view starts it closed.
            SetVisible(false);
        }

        public event Action BuyPressed;

        public event Action CloseRequested;

        public void SetVisible(bool visible)
        {
            if (m_popupIntro.isAlive)
            {
                m_popupIntro.Stop();
            }

            m_screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                m_popupIntro = Tween.Scale(m_popup.transform, Vector3.one * k_popupIntroStartScale, Vector3.one,
                    k_popupIntroSeconds, Ease.OutQuad, useUnscaledTime: true);
            }
        }

        public void SetOffer(string title, Sprite icon, string amount, string description, Sprite currencyIcon, string price)
        {
            SetHeavy(m_title, m_titleBack, title);
            m_icon.style.backgroundImage = ToBackground(icon);
            SetHeavy(m_amount, m_amountBack, amount);
            SetHeavy(m_description, m_descriptionBack, description);
            m_priceIcon.style.backgroundImage = ToBackground(currencyIcon);
            SetHeavy(m_price, m_priceBack, price);
        }

        public void SetMessage(string text)
        {
            bool hasText = !string.IsNullOrEmpty(text);
            m_message.text = hasText ? text : string.Empty;
            m_message.style.visibility = hasText ? Visibility.Visible : Visibility.Hidden;
        }

        public void SetBuyInteractable(bool interactable)
        {
            m_buyButton.SetEnabled(interactable);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_buyButton.clicked -= m_onBuyClicked;
            m_closeButton.clicked -= m_onCloseClicked;
            SetVisible(false);
            BuyPressed = null;
            CloseRequested = null;
        }

        private void OnBuyClicked()
        {
            BuyPressed?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        // Both layers of a ".cbj-heavy-text" pair must always carry the same text (Common.uss).
        private static void SetHeavy(Label front, Label back, string text)
        {
            string value = text ?? string.Empty;
            front.text = value;
            back.text = value;
        }

        private static StyleBackground ToBackground(Sprite sprite)
        {
            return sprite != null ? new StyleBackground(sprite) : new StyleBackground(StyleKeyword.None);
        }

        // Runs once per bind, outside any gameplay tick; the message concatenation is a setup error path only.
        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Booster purchase view: BoosterPurchasePopup.uxml has no "
                                                    + typeof(T).Name + " named '" + name + "'.");
            }

            return element;
        }

        private static Label RequireBack(Label front)
        {
            Label back = front.parent?.Q<Label>(className: k_heavyBackClass);
            if (back == null)
            {
                throw new InvalidOperationException("Booster purchase view: '" + front.name
                                                    + "' has no '.cbj-heavy-text__back' sibling label.");
            }

            return back;
        }
    }
}
