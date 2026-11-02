using System;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// A set of board cells as 128 bits, bit y * width + x; the largest board (8 x 10) needs 80. The
    /// search combines footprints and swept paths with these masks instead of asking the rules again.
    /// </summary>
    internal readonly struct CellMask : IEquatable<CellMask>
    {
        public const int Capacity = 128;

        public readonly ulong Low;
        public readonly ulong High;

        public CellMask(ulong low, ulong high)
        {
            Low = low;
            High = high;
        }

        public static CellMask Empty => default;

        public bool IsEmpty => (Low | High) == 0UL;

        public CellMask With(int cellIndex)
        {
            return cellIndex < 64
                ? new CellMask(Low | (1UL << cellIndex), High)
                : new CellMask(Low, High | (1UL << (cellIndex - 64)));
        }

        public bool Contains(int cellIndex)
        {
            return cellIndex < 64
                ? (Low & (1UL << cellIndex)) != 0UL
                : (High & (1UL << (cellIndex - 64))) != 0UL;
        }

        public bool Intersects(in CellMask other)
        {
            return ((Low & other.Low) | (High & other.High)) != 0UL;
        }

        public CellMask Or(in CellMask other)
        {
            return new CellMask(Low | other.Low, High | other.High);
        }

        public CellMask Without(in CellMask other)
        {
            return new CellMask(Low & ~other.Low, High & ~other.High);
        }

        public bool Equals(CellMask other) => Low == other.Low && High == other.High;

        public override bool Equals(object obj) => obj is CellMask other && Equals(other);

        public override int GetHashCode() => (Low ^ (High * 0x9E3779B97F4A7C15UL)).GetHashCode();
    }
}
