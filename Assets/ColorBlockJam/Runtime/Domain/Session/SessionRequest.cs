namespace ColorBlockJam.Domain.Session
{
    /// <summary>Every request of the PRD 7.5 transition table.</summary>
    public enum SessionRequest
    {
        OpenPreLevel = 0,
        ClosePreLevel = 1,
        Play = 2,
        Pause = 3,
        Background = 4,
        BoosterCommit = 5,
        ZeroStockTap = 6,
        Resume = 7,
        CommitSucceeded = 8,
        CommitFailed = 9,
        PurchaseClosed = 10,
        Restart = 11,
        Home = 12,
        AllRequiredCleared = 13,
        TimerExpired = 14,
        NextLevel = 15
    }
}
