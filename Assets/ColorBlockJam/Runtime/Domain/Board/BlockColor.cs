using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// A palette identity (the PaletteColor ordinal) that the Domain compares without referencing
    /// the Runtime enum (DEC-110).
    /// </summary>
    public readonly struct BlockColor : IEquatable<BlockColor>
    {
        public readonly int Id;

        public BlockColor(int id)
        {
            Id = id;
        }

        public static bool operator ==(BlockColor a, BlockColor b) => a.Id == b.Id;

        public static bool operator !=(BlockColor a, BlockColor b) => a.Id != b.Id;

        public bool Equals(BlockColor other) => Id == other.Id;

        public override bool Equals(object obj) => obj is BlockColor other && Equals(other);

        public override int GetHashCode() => Id;

        public override string ToString() => "Color " + Id;
    }
}
