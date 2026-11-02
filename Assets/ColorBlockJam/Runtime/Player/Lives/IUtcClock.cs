namespace ColorBlockJam.Player.Lives
{
    /// <summary>The wall clock that timed refills read, so tests can move time (DEC-174).</summary>
    public interface IUtcClock
    {
        /// <summary>Current UTC time in whole Unix seconds.</summary>
        long UtcNowSeconds { get; }
    }
}
