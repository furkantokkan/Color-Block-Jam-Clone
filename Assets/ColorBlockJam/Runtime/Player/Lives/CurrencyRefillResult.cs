namespace ColorBlockJam.Player.Lives
{
    /// <summary>
    /// Outcome of one paid refill of a currency to its maximum (Lives: 1 coin, DEC-174); every result except Refilled
    /// changes nothing.
    /// </summary>
    public enum CurrencyRefillResult
    {
        Refilled = 0,
        AlreadyFull = 1,
        InsufficientFunds = 2,
        InvalidOffer = 3,
        SaveFailed = 4,
        Cancelled = 5
    }
}
