namespace ColorBlockJam.Hub.Purchase
{
    /// <summary>
    /// An open Hub overlay that the purchase popup covers: it hides while covered and shows again, with its state
    /// intact, when the purchase popup closes, so two popups never overlap (user feedback 2026-09-27, reference 8).
    /// </summary>
    /// <remarks>
    /// Covering is not closing: the overlay stays on the navigator's stack and keeps its selection. Only
    /// <see cref="BoosterPurchaseOverlay"/> calls it, from its own Show and Hide.
    /// </remarks>
    public interface ICoverableOverlay
    {
        /// <summary>Hides the overlay while <paramref name="covered"/> is true; false shows it again if still open.</summary>
        void SetCovered(bool covered);
    }
}
