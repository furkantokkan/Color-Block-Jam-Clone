using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The in-game purchase presentation for a booster with no stock (B-18). It shows the shared purchase popup for
    /// one definition while the session is Paused(Purchase); buying goes through IBoosterShop and never casts.
    /// </summary>
    public interface IInGamePurchaseHost
    {
        /// <summary>True while the purchase presentation is on screen.</summary>
        bool IsShowing { get; }

        /// <summary>Shows the purchase for definition; false when no presentation is available, changing nothing.</summary>
        bool TryShow(BoosterDefinition definition);

        /// <summary>Hides the presentation; does nothing when it is not showing.</summary>
        void Hide();
    }
}
