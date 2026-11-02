namespace ColorBlockJam.Player
{
    /// <summary>
    /// The expected outcomes of a booster stock debit. Only Consumed changes anything; every other result leaves
    /// the stock as it was (AC-6.10, Booster-Gameplay B-14).
    /// </summary>
    public enum BoosterConsumeResult
    {
        Consumed = 0,
        InsufficientStock = 1,
        InvalidRequest = 2,
        SaveFailed = 3,
        Cancelled = 4
    }
}
