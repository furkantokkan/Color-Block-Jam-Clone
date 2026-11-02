namespace ColorBlockJam.Domain.Session
{
    /// <summary>The one level-session state; it alone decides whether gameplay runs (AR-2, PROJECT.md Game States).</summary>
    public enum SessionState
    {
        Home = 0,
        Playing = 1,
        Paused = 2,
        Won = 3,
        Failed = 4
    }
}
