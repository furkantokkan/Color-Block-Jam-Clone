using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// One gameplay command as plain data: a kind plus the integer fields its handler reads. Every
    /// command is one struct, so the pointer, a replay or another system can submit it, the attempt
    /// can log it without allocating, and a later task can write it as JSON (DEC-122).
    /// MoveBlock uses Block, From, To and Option: 0 keeps the block on the board at To, 1 + Side
    /// leaves through that side from To. The booster kinds (B-6) take From as the block's origin when
    /// the command was captured, so a board that changed since refuses it as stale: RemoveCell removes
    /// the board cell To, RemoveBlock uses no other field and RemoveColor keeps the colour id in Option.
    /// Block is the runtime block ID: the authored index (BlockDefinition.Index) for an authored block
    /// and a later ID for a Rocket part (B-2).
    /// </summary>
    public readonly struct GameCommand : IEquatable<GameCommand>
    {
        /// <summary>Option for MoveBlock: keeps the block on the board at To. Also RemoveCell's and RemoveBlock's unused Option.</summary>
        public const int k_noOption = 0;

        private const int k_exitOptionBase = 1;

        /// <summary>The highest Option MoveBlock defines: an exit through Side.Right (k_exitOptionBase + Side.Right).</summary>
        internal const int k_highestMoveOption = k_exitOptionBase + (int)Side.Right;

        public GameCommand(CommandKind kind, int block, Cell from, Cell to, int option)
        {
            Kind = kind;
            Block = block;
            From = from;
            To = to;
            Option = option;
        }

        public CommandKind Kind { get; }

        public int Block { get; }

        /// <summary>The origin the block stands on when the command runs; any other origin refuses it as stale.</summary>
        public Cell From { get; }

        /// <summary>For MoveBlock, the origin the block ends on; for RemoveCell, the board cell it removes.</summary>
        public Cell To { get; }

        public int Option { get; }

        /// <summary>A move that keeps the block on the board at to.</summary>
        public static GameCommand Move(int block, Cell from, Cell to)
        {
            return new GameCommand(CommandKind.MoveBlock, block, from, to, k_noOption);
        }

        /// <summary>A move to a flush origin followed by an exit through the door on that side.</summary>
        public static GameCommand MoveAndExit(int block, Cell from, Cell to, Side side)
        {
            return new GameCommand(CommandKind.MoveBlock, block, from, to, k_exitOptionBase + (int)side);
        }

        /// <summary>Rocket: removes the board cell of the block standing at origin.</summary>
        public static GameCommand RemoveCell(int block, Cell origin, Cell cell)
        {
            return new GameCommand(CommandKind.RemoveCell, block, origin, cell, k_noOption);
        }

        /// <summary>Hammer: removes the whole block standing at origin.</summary>
        public static GameCommand RemoveBlock(int block, Cell origin)
        {
            return new GameCommand(CommandKind.RemoveBlock, block, origin, default, k_noOption);
        }

        /// <summary>
        /// Vacuum: removes every on-board block of the colour of the tapped block standing at origin.
        /// The colour rides in Option, so a replay needs no palette.
        /// </summary>
        public static GameCommand RemoveColor(int block, Cell origin, BlockColor color)
        {
            return new GameCommand(CommandKind.RemoveColor, block, origin, default, color.Id);
        }

        /// <summary>The exit side of a MoveBlock; false when the move keeps the block on the board or Option is not a side.</summary>
        public bool TryGetExit(out Side side)
        {
            if (Option <= k_noOption || Option > k_highestMoveOption)
            {
                side = default;
                return false;
            }

            side = (Side)(Option - k_exitOptionBase);
            return true;
        }

        public bool Equals(GameCommand other)
        {
            return Kind == other.Kind && Block == other.Block && From == other.From && To == other.To
                && Option == other.Option;
        }

        public override bool Equals(object obj) => obj is GameCommand other && Equals(other);

        public override int GetHashCode()
        {
            int hash = (int)Kind;
            hash = (hash * 397) ^ Block;
            hash = (hash * 397) ^ From.GetHashCode();
            hash = (hash * 397) ^ To.GetHashCode();
            return (hash * 397) ^ Option;
        }

        public override string ToString()
        {
            return Kind + " block " + Block + " " + From + " -> " + To + " option " + Option;
        }
    }
}
