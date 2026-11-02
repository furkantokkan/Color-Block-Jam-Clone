using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A hook source that hands every attempt the same prebuilt hooks and counts the requests.</summary>
    public sealed class FixedHookSource : IAttemptHookSource
    {
        private readonly AttemptHooks m_hooks;

        public FixedHookSource(AttemptHooks hooks)
        {
            m_hooks = hooks;
        }

        public int CreateCount { get; private set; }

        public AttemptHooks Create(AttemptLevel level)
        {
            CreateCount++;
            return m_hooks;
        }
    }
}
