namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The time-booster port of the attempt clock (B-8): Time Freeze holds the main countdown for a
    /// span of Playing time with its own clock, and Extra Time adds whole seconds. They are clock
    /// operations, not board commands, so Suspend, Resume and Teardown cover them through ICountdown.
    /// </summary>
    public interface IFreezeCountdown
    {
        /// <summary>True while a freeze holds the main countdown.</summary>
        bool IsFrozen { get; }

        /// <summary>The freeze time left in seconds; 0 when no freeze runs.</summary>
        float FreezeRemaining { get; }

        /// <summary>
        /// Starts a freeze of seconds; false, changing nothing, while a freeze runs or before the
        /// countdown has started (a second Time Freeze is refused before any spend).
        /// </summary>
        bool TryFreeze(float seconds);

        /// <summary>Adds whole seconds to the main countdown of a started, unexpired countdown (Extra Time).</summary>
        void AddSeconds(int seconds);
    }
}
