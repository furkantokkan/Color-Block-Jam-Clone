using System;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Immutable effect metadata belonging to exactly one converted level.</summary>
    public sealed class BlockEffectContext
    {
        private readonly IBlockEffectLevel[] m_levels;

        internal BlockEffectContext(AttemptLevel level, IBlockEffectLevel[] levels)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            m_levels = (IBlockEffectLevel[])levels.Clone();
        }

        public AttemptLevel Level { get; }
        public int Count => m_levels.Length;
        public IBlockEffectLevel GetAt(int index) => m_levels[index];
    }
}
