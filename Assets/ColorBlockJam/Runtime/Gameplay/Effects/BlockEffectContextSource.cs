using System;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Scene-owned level metadata and the last fully built attempt, never a partial attempt.</summary>
    public sealed class BlockEffectContextSource
    {
        private readonly IBlockEffectModule[] m_modules;
        private int m_generation;

        public BlockEffectContextSource(IBlockEffectModule[] modules)
        {
            if (modules == null) throw new ArgumentNullException(nameof(modules));
            m_modules = (IBlockEffectModule[])modules.Clone();
            for (int index = 0; index < m_modules.Length; index++)
            {
                if (m_modules[index] == null || string.IsNullOrEmpty(m_modules[index].TypeId))
                    throw new ArgumentException("Every effect module needs a stable type ID.", nameof(modules));
                for (int previous = 0; previous < index; previous++)
                    if (m_modules[index].TypeId == m_modules[previous].TypeId)
                        throw new ArgumentException("An effect module may be registered only once.", nameof(modules));
            }
        }

        public int Count => m_modules.Length;
        public BlockEffectContext Context { get; private set; }
        public BlockEffectAttempt CurrentAttempt { get; private set; }
        public IBlockEffectModule GetModule(int index) => m_modules[index];

        public void Prepare(AttemptLevel level, LevelData authored)
        {
            CurrentAttempt = null;
            Context = null;
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            if (level.LevelId != authored.Id || level.BlockCount != authored.BlockCount)
                throw new ArgumentException("Effect metadata must match the converted level.", nameof(authored));
            for (int block = 0; block < level.BlockCount; block++)
                if (level.GetBlockId(block) != authored.GetBlockAt(block).Id)
                    throw new ArgumentException("Effect metadata must match converted block order.", nameof(authored));

            IBlockEffectLevel[] levels = new IBlockEffectLevel[m_modules.Length];
            for (int index = 0; index < m_modules.Length; index++)
            {
                IBlockEffectLevel metadata = m_modules[index].Read(authored);
                if (metadata == null || metadata.TypeId != m_modules[index].TypeId || metadata.BlockCount != level.BlockCount)
                    throw new InvalidOperationException("An effect module returned mismatched level metadata.");
                levels[index] = metadata;
            }
            Context = new BlockEffectContext(level, levels);
        }

        internal int BeginAttempt(AttemptLevel level)
        {
            CurrentAttempt = null;
            if (Context == null || !ReferenceEquals(Context.Level, level))
                throw new InvalidOperationException("Prepare effect metadata for this exact level before creating an attempt.");
            return ++m_generation;
        }

        internal void Publish(BlockEffectAttempt attempt)
        {
            if (!ReferenceEquals(attempt.Context, Context) || attempt.Generation != m_generation)
                throw new InvalidOperationException("Cannot publish stale effect state.");
            CurrentAttempt = attempt;
        }
    }
}
