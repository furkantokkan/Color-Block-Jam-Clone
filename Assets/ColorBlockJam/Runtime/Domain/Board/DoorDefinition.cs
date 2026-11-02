namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// One authored door span on a board side. Start counts from the left for Top/Bottom and from
    /// the bottom for Left/Right, and the span covers lanes Start to Start + Length - 1.
    /// </summary>
    public sealed class DoorDefinition
    {
        public DoorDefinition(int index, Side side, int start, int length, BlockColor color)
        {
            Index = index;
            Side = side;
            Start = start;
            Length = length;
            Color = color;
        }

        public int Index { get; }

        public Side Side { get; }

        public int Start { get; }

        public int Length { get; }

        public BlockColor Color { get; }

        /// <summary>True when lanes lo to hi all lie inside this one span (the whole-width rule, DEC-6).</summary>
        public bool Covers(int lo, int hi) => lo >= Start && hi < Start + Length;
    }
}
