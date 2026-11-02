using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>The exit an exit gate is asked about: which block leaves through which door from which origin.</summary>
    public readonly struct ExitQuery
    {
        public ExitQuery(int block, int door, Side side, Cell origin)
        {
            Block = block;
            Door = door;
            Side = side;
            Origin = origin;
        }

        public int Block { get; }

        public int Door { get; }

        public Side Side { get; }

        public Cell Origin { get; }
    }
}
