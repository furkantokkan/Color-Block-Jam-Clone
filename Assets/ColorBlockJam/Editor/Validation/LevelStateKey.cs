using System;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// One search position in 128 bits: a 7-bit origin slot per block (bits 7 * block, at most 16 blocks
    /// and 80 slots) plus one exited bit per block (bits 112 + block). Ice needs no extra state, because a
    /// frozen block's count follows from the exited set. An exited block's slot field stays 0.
    /// </summary>
    internal readonly struct LevelStateKey : IEquatable<LevelStateKey>
    {
        public const int SlotBits = 7;
        public const int MaxSlot = (1 << SlotBits) - 1;

        /// <summary>The most blocks a key holds; LevelCheckOptions.MaxBlocks and LevelOrderedStateKey.OrderBits derive from it.</summary>
        public const int MaxBlocks = 16;

        private const ulong k_slotMask = MaxSlot;
        private const int k_exitedOffset = MaxBlocks * SlotBits;

        private readonly ulong m_low;
        private readonly ulong m_high;

        private LevelStateKey(ulong low, ulong high)
        {
            m_low = low;
            m_high = high;
        }

        public int GetSlot(int block)
        {
            int offset = block * SlotBits;
            ulong value;
            if (offset >= 64)
            {
                value = m_high >> (offset - 64);
            }
            else if (offset + SlotBits <= 64)
            {
                value = m_low >> offset;
            }
            else
            {
                value = (m_low >> offset) | (m_high << (64 - offset));
            }

            return (int)(value & k_slotMask);
        }

        public LevelStateKey WithSlot(int block, int slot)
        {
            ulong value = (ulong)slot & k_slotMask;
            int offset = block * SlotBits;
            ulong low = m_low;
            ulong high = m_high;
            if (offset >= 64)
            {
                int shift = offset - 64;
                high = (high & ~(k_slotMask << shift)) | (value << shift);
            }
            else if (offset + SlotBits <= 64)
            {
                low = (low & ~(k_slotMask << offset)) | (value << offset);
            }
            else
            {
                // The field straddles the two words: its low bits end the low word, the rest start the high word.
                int highShift = 64 - offset;
                low = (low & ~(k_slotMask << offset)) | (value << offset);
                high = (high & ~(k_slotMask >> highShift)) | (value >> highShift);
            }

            return new LevelStateKey(low, high);
        }

        public bool IsExited(int block)
        {
            return (m_high & (1UL << (k_exitedOffset - 64 + block))) != 0UL;
        }

        /// <summary>The exited set, one bit per block (bit block), as the search records which blocks ever left.</summary>
        public int ExitedMask => (int)(m_high >> (k_exitedOffset - 64)) & 0xFFFF;

        /// <summary>Marks the block exited and clears its slot, so equal exited sets give equal keys.</summary>
        public LevelStateKey WithExited(int block)
        {
            LevelStateKey cleared = WithSlot(block, 0);
            return new LevelStateKey(cleared.m_low, cleared.m_high | (1UL << (k_exitedOffset - 64 + block)));
        }

        public bool Equals(LevelStateKey other) => m_low == other.m_low && m_high == other.m_high;

        public override bool Equals(object obj) => obj is LevelStateKey other && Equals(other);

        // A SplitMix64-style finaliser over both words, so nearby positions spread across buckets.
        public override int GetHashCode()
        {
            ulong hash = m_low ^ (m_high * 0x9E3779B97F4A7C15UL);
            hash ^= hash >> 30;
            hash *= 0xBF58476D1CE4E5B9UL;
            hash ^= hash >> 27;
            hash *= 0x94D049BB133111EBUL;
            hash ^= hash >> 31;
            return (int)hash ^ (int)(hash >> 32);
        }
    }
}
