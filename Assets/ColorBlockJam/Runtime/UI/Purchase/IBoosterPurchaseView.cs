using System;
using UnityEngine;

namespace ColorBlockJam.UI.Purchase
{
    /// <summary>
    /// What a presenter may do to the shared offer popup, without any VisualElement, so EditMode tests can use a fake
    /// view (Hub-And-Player-Data-Design 9.2, DEC-136). Two presenters drive it: <see cref="BoosterPurchasePresenter"/>
    /// (boosters, Hub and Game, DEC-151) and the Hub's life refill presenter (DEC-174).
    /// </summary>
    public interface IBoosterPurchaseView
    {
        /// <summary>Raised when Buy is released as a click.</summary>
        event Action BuyPressed;

        /// <summary>Raised when the close button is released as a click.</summary>
        event Action CloseRequested;

        /// <summary>Shows or hides the whole popup, scrim included.</summary>
        void SetVisible(bool visible);

        /// <summary>
        /// Shows one offer: a title, an icon, a bundle amount text (for example "x1"), a description, and on Buy the
        /// price currency's icon and the price text (reference 01).
        /// </summary>
        void SetOffer(string title, Sprite icon, string amount, string description, Sprite currencyIcon, string price);

        /// <summary>Shows one feedback line in the reserved message row; an empty text hides it without moving anything.</summary>
        void SetMessage(string text);

        void SetBuyInteractable(bool interactable);
    }
}
