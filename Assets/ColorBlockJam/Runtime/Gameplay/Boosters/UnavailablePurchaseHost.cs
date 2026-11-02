using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The purchase host of a HUD-less Game scene (DEC-151): it never shows, so a zero-stock tap plays the
    /// button's blocked feedback and nothing pauses.
    /// </summary>
    public sealed class UnavailablePurchaseHost : IInGamePurchaseHost
    {
        public bool IsShowing => false;

        public bool TryShow(BoosterDefinition definition) => false;

        public void Hide()
        {
        }
    }
}
