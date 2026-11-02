using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// An integer board coordinate: column X and row Y counted from the Bottom edge, the same
    /// convention as IBoardLayout. It is the Domain's own type so no UnityEngine type crosses in (DEC-110).
    /// </summary>
    public readonly struct Cell : IEquatable<Cell>
    {
        public static readonly Cell Zero = new Cell(0, 0);

        public readonly int X;
        public readonly int Y;

        public Cell(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Cell operator +(Cell a, Cell b) => new Cell(a.X + b.X, a.Y + b.Y);

        public static Cell operator -(Cell a, Cell b) => new Cell(a.X - b.X, a.Y - b.Y);

        public static bool operator ==(Cell a, Cell b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(Cell a, Cell b) => a.X != b.X || a.Y != b.Y;

        public bool Equals(Cell other) => X == other.X && Y == other.Y;

        public override bool Equals(object obj) => obj is Cell other && Equals(other);

        public override int GetHashCode() => (X * 397) ^ Y;

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
