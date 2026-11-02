using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// Builds the hook arrays (XP-1..XP-5) for one attempt on Home. It is a port so that a later
    /// effect registry (Ice, Arrow, gate effects) binds its own source without changing the session
    /// (PRD 8.3, AC-EXT-1, DEC-72).
    /// </summary>
    public interface IAttemptHookSource
    {
        AttemptHooks Create(AttemptLevel level);
    }
}
