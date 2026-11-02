using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>
    /// Which targets the board boosters accept (B-11), as pure reads of a board, so in-game targeting,
    /// the opening Rocket and the command handlers share one rule. No drag gate is asked; Ice and Arrow
    /// blocks are valid targets unless the attempt's booster restrictions keep a booster off them
    /// (IBoardReader.AllowsBoosterTarget, DEC-171). Exited and removed blocks hold no cell and are never on the board.
    /// </summary>
    public static class BoosterTargets
    {
        private const int k_none = -1;

        /// <summary>
        /// Rocket: the cell's block is on the board, no restriction keeps Rocket off it and it keeps at
        /// least one other cell. On refusal, NotBlockCell means no block holds the cell (block is -1),
        /// EffectRestricted that a restricted effect guards the block and FinalCell that it is that
        /// block's only cell; refusal is Applied when the cell is a target.
        /// </summary>
        public static bool CanRemoveCell(IBoardReader board, Cell cell, out int block, out CommandOutcome refusal)
        {
            int occupant = board.GetOccupant(cell);
            if (occupant < 0)
            {
                block = k_none;
                refusal = CommandOutcome.NotBlockCell;
                return false;
            }

            block = occupant;
            if (!board.AllowsBoosterTarget(CommandKind.RemoveCell, occupant))
            {
                refusal = CommandOutcome.EffectRestricted;
                return false;
            }

            if (board.GetFootprint(occupant).CellCount < 2)
            {
                refusal = CommandOutcome.FinalCell;
                return false;
            }

            refusal = CommandOutcome.Applied;
            return true;
        }

        /// <summary>Hammer: any unrestricted on-board block, whatever its footprint (Ice included by default).</summary>
        public static bool CanRemoveBlock(IBoardReader board, int block)
            => IsOnBoard(board, block) && board.AllowsBoosterTarget(CommandKind.RemoveBlock, block);

        /// <summary>
        /// Vacuum: any unrestricted on-board block; its colour chooses what the Vacuum removes, and restricted
        /// blocks of that colour stay on the board.
        /// </summary>
        public static bool CanRemoveColor(IBoardReader board, int block)
            => IsOnBoard(board, block) && board.AllowsBoosterTarget(CommandKind.RemoveColor, block);

        private static bool IsOnBoard(IBoardReader board, int block)
        {
            return block >= 0 && block < board.BlockCount && board.GetStatus(block) == BlockStatus.OnBoard;
        }
    }
}
