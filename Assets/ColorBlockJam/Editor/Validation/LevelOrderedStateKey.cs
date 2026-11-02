using System;
using System.Collections.Generic;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The solution listing's search position: the check's LevelStateKey plus the order in which its exited blocks
    /// left, 4 bits per exit (bits 4 * k hold the k-th exit's block index; 16 blocks fill 64 bits). The number of
    /// exits is the number of exited bits in the position, so a packed order is never ambiguous.
    /// </summary>
    internal readonly struct LevelOrderedStateKey : IEquatable<LevelOrderedStateKey>
    {
        // 1 << OrderBits must equal LevelStateKey.MaxBlocks, so every block index packs into the 64-bit order.
        public const int OrderBits = 4;

        public LevelOrderedStateKey(LevelStateKey position, ulong exitOrder)
        {
            Position = position;
            ExitOrder = exitOrder;
        }

        public LevelStateKey Position { get; }

        public ulong ExitOrder { get; }

        public LevelOrderedStateKey WithSlot(int block, int slot)
        {
            return new LevelOrderedStateKey(Position.WithSlot(block, slot), ExitOrder);
        }

        /// <summary>The block leaves as the exit after exitedCount earlier ones.</summary>
        public LevelOrderedStateKey WithExited(int block, int exitedCount)
        {
            return new LevelOrderedStateKey(Position.WithExited(block),
                ExitOrder | ((ulong)block << (OrderBits * exitedCount)));
        }

        /// <summary>Packs block indices in exit order, the first in the lowest 4 bits.</summary>
        public static ulong PackOrder(IReadOnlyList<int> order)
        {
            ulong packed = 0UL;
            for (int i = 0; i < order.Count; i++)
            {
                packed |= (ulong)order[i] << (OrderBits * i);
            }

            return packed;
        }

        public bool Equals(LevelOrderedStateKey other) => Position.Equals(other.Position) && ExitOrder == other.ExitOrder;

        public override bool Equals(object obj) => obj is LevelOrderedStateKey other && Equals(other);

        // The position hash is already mixed; the order is spread by a golden-ratio multiply and folded into it.
        public override int GetHashCode()
        {
            ulong hash = (uint)Position.GetHashCode() ^ (ExitOrder * 0x9E3779B97F4A7C15UL);
            return (int)hash ^ (int)(hash >> 32);
        }
    }
}
