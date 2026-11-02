using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// One entry of the tick's event log: any of the four board events flattened into one struct so
    /// the log is a single preallocated array. Fields a kind does not use hold -1 or their default.
    /// </summary>
    public readonly struct BoardEvent
    {
        private BoardEvent(BoardEventKind kind, int block, int door, Side side, Cell from, Cell to, ExitOutcome outcome,
            int parent, CommandKind cause)
        {
            Kind = kind;
            Block = block;
            Door = door;
            Side = side;
            From = from;
            To = to;
            Outcome = outcome;
            Parent = parent;
            Cause = cause;
        }

        public BoardEventKind Kind { get; }

        public int Block { get; }

        /// <summary>The door an exit used; -1 for other kinds.</summary>
        public int Door { get; }

        public Side Side { get; }

        /// <summary>The origin a move started from, the origin an exit left from, or the cell a Rocket removed.</summary>
        public Cell From { get; }

        /// <summary>The origin a move ended at, or the origin an exit left from.</summary>
        public Cell To { get; }

        public ExitOutcome Outcome { get; }

        /// <summary>
        /// For FootprintChanged, the block that lost the cell (equal to Block for its own entry); for
        /// Removed, the block the command targeted (BlockRemovedEvent.Source); -1 for other kinds.
        /// </summary>
        public int Parent { get; }

        /// <summary>The command kind that removed the block; None for other kinds.</summary>
        public CommandKind Cause { get; }

        public static BoardEvent Exited(in BlockExitedEvent e)
        {
            return new BoardEvent(BoardEventKind.Exited, e.Block, e.Door, e.Side, e.Origin, e.Origin, e.Outcome, -1,
                CommandKind.None);
        }

        public static BoardEvent Removed(in BlockRemovedEvent e)
        {
            return new BoardEvent(BoardEventKind.Removed, e.Block, -1, default, default, default, default, e.Source, e.Cause);
        }

        public static BoardEvent MoveCommitted(in MoveCommittedEvent e)
        {
            return new BoardEvent(BoardEventKind.MoveCommitted, e.Block, -1, default, e.From, e.To, default, -1,
                CommandKind.None);
        }

        public static BoardEvent FootprintChanged(in FootprintChangedEvent e)
        {
            return new BoardEvent(BoardEventKind.FootprintChanged, e.Block, -1, default, e.RemovedCell, default, default,
                e.Parent, CommandKind.None);
        }
    }
}
