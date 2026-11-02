using System;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Immutable starting Ice counts in converted block order, and the block IDs an attempt can issue.</summary>
    public sealed class IceEffectLevel : IBlockEffectLevel
    {
        private readonly int[] m_counts;

        /// <summary>Counts for a board whose blocks never split: the capacity is the authored block count.</summary>
        public IceEffectLevel(int[] counts)
            : this(counts, counts?.Length ?? 0)
        {
        }

        /// <summary>
        /// Counts with the level's block ID capacity, its total authored cell count (LevelModel.TotalCellCount), so
        /// Ice rules and views have a slot for every Rocket part (B-10).
        /// </summary>
        public IceEffectLevel(int[] counts, int capacity)
        {
            m_counts = counts != null ? (int[])counts.Clone() : throw new ArgumentNullException(nameof(counts));
            if (capacity < m_counts.Length)
                throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must hold every authored block.");
            Capacity = capacity;
        }

        public string TypeId => LevelEffectTypes.Ice;
        public int BlockCount => m_counts.Length;

        /// <summary>The block IDs an attempt of this level can issue: the authored blocks, then every Rocket part.</summary>
        public int Capacity { get; }

        public int GetCount(int block) => m_counts[block];
        internal int[] CopyCounts() => (int[])m_counts.Clone();
    }
}
