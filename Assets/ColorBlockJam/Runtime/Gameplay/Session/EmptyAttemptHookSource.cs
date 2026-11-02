using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// The hook source while no effect is registered: every attempt gets empty hook arrays, so the
    /// plain colour-door rules run unchanged (AC-EXT-10).
    /// </summary>
    public sealed class EmptyAttemptHookSource : IAttemptHookSource
    {
        public AttemptHooks Create(AttemptLevel level)
        {
            return AttemptHooks.CreateEmpty();
        }
    }
}
