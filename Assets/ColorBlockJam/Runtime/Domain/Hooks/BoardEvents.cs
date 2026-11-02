using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>A block passed a door in the command phase (RULE-28); the exit already freed its cells when Outcome is Leave.</summary>
    public readonly struct BlockExitedEvent
    {
        public BlockExitedEvent(int block, int door, Side side, Cell origin, ExitOutcome outcome)
        {
            Block = block;
            Door = door;
            Side = side;
            Origin = origin;
            Outcome = outcome;
        }

        public int Block { get; }

        public int Door { get; }

        public Side Side { get; }

        public Cell Origin { get; }

        public ExitOutcome Outcome { get; }
    }

    /// <summary>
    /// A booster removed a whole block (RULE-28): Hammer or Vacuum, never a door exit. Cause is the
    /// command kind that removed it and Source the block that command targeted: the block itself for
    /// Hammer, the tapped block for Vacuum (B-5).
    /// </summary>
    public readonly struct BlockRemovedEvent
    {
        /// <summary>A removal without a recorded cause: Cause is None and Source is the block itself.</summary>
        public BlockRemovedEvent(int block)
            : this(block, CommandKind.None, block)
        {
        }

        public BlockRemovedEvent(int block, CommandKind cause, int source)
        {
            Block = block;
            Cause = cause;
            Source = source;
        }

        public int Block { get; }

        public CommandKind Cause { get; }

        public int Source { get; }
    }

    /// <summary>A released drag changed the board: the block rests at To instead of From (RULE-28, DEC-113).</summary>
    public readonly struct MoveCommittedEvent
    {
        public MoveCommittedEvent(int block, Cell from, Cell to)
        {
            Block = block;
            From = from;
            To = to;
        }

        public int Block { get; }

        public Cell From { get; }

        public Cell To { get; }
    }

    /// <summary>
    /// Rocket removed one cell of a block (RULE-28, B-5). The split block's own event has Block equal
    /// to Parent and comes first; every new part then has one event with Block not equal to Parent,
    /// in ascending ID. RemovedCell is the removed board cell. A split is neither an exit nor a removal.
    /// </summary>
    public readonly struct FootprintChangedEvent
    {
        /// <summary>A footprint change of the block itself: Parent is the block and RemovedCell is (0, 0).</summary>
        public FootprintChangedEvent(int block)
            : this(block, block, Cell.Zero)
        {
        }

        public FootprintChangedEvent(int block, int parent, Cell removedCell)
        {
            Block = block;
            Parent = parent;
            RemovedCell = removedCell;
        }

        public int Block { get; }

        /// <summary>The block that lost the cell; equal to Block for the split block's own event.</summary>
        public int Parent { get; }

        public Cell RemovedCell { get; }
    }
}
