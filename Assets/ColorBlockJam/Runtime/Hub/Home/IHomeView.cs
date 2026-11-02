using System;
using UnityEngine;

namespace ColorBlockJam.Hub.Home
{
    /// <summary>
    /// What <see cref="HomePresenter"/> may do to the Home screen, without any VisualElement, so EditMode
    /// tests can use a fake view (design 6.3).
    /// </summary>
    public interface IHomeView
    {
        /// <summary>Raised when the large level button is released as a click.</summary>
        event Action LevelPressed;

        /// <summary>Raised when the settings button is released as a click.</summary>
        event Action SettingsPressed;

        /// <summary>The number of decorative route nodes; slot 0 is the current level.</summary>
        int RouteNodeCount { get; }

        /// <summary>Sets the level button text and whether it accepts presses.</summary>
        void SetLevel(string label, bool interactable);

        /// <summary>Shows or hides one route node with its number and lock state.</summary>
        void SetRouteNode(int slot, string label, bool visible, bool locked);

        /// <summary>
        /// Hides every currency holder authored in Home.uxml (DEC-143). The presenter calls this once per bind,
        /// before <see cref="TryGetCurrencyHolder"/>, so a holder no Home currency claims stays hidden.
        /// </summary>
        void HideCurrencyHolders();

        /// <summary>
        /// Finds the holder authored in Home.uxml for <paramref name="currencyId"/> (named
        /// "currency-holder-&lt;currencyId&gt;"), sets its icon and count-on-icon mode and shows it. Nothing is
        /// instantiated or removed: a missing holder returns false so the presenter can log and skip it.
        /// </summary>
        /// <param name="icon">The currency icon; null leaves Home.uxml's design-time icon.</param>
        /// <param name="countOnIcon">True shows a count on the icon (Lives); false shows the amount only.</param>
        bool TryGetCurrencyHolder(string currencyId, Sprite icon, bool countOnIcon, out ICurrencyHolderView holder);
    }

    /// <summary>
    /// One currency holder in the Home top bar: holder text and, for CountOnIcon currencies, the icon count.
    /// </summary>
    public interface ICurrencyHolderView
    {
        void SetText(string text);

        void SetIconCount(string text);
    }
}
