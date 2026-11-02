using System;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// Safe-area padding in panel units for each edge of the screen.
    /// </summary>
    public readonly struct SafeAreaInsets : IEquatable<SafeAreaInsets>
    {
        public readonly float Left;
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;

        public SafeAreaInsets(float left, float top, float right, float bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public bool Equals(SafeAreaInsets other)
        {
            return Left.Equals(other.Left) && Top.Equals(other.Top)
                && Right.Equals(other.Right) && Bottom.Equals(other.Bottom);
        }

        public override bool Equals(object obj)
        {
            return obj is SafeAreaInsets other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Left.GetHashCode();
                hash = (hash * 397) ^ Top.GetHashCode();
                hash = (hash * 397) ^ Right.GetHashCode();
                return (hash * 397) ^ Bottom.GetHashCode();
            }
        }
    }
}
