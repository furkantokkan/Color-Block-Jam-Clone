namespace ColorBlockJam.Player
{
    /// <summary>
    /// The expected outcomes of a booster purchase. Only Purchased changes anything; every other result leaves the
    /// balance and stock as they were (AC-6.8).
    /// </summary>
    public enum BoosterPurchaseResult
    {
        Purchased = 0,
        InsufficientFunds = 1,
        InvalidOffer = 2,
        StockFull = 3,
        SaveFailed = 4,
        Cancelled = 5
    }
}
