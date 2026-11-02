using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// The stale-command check every board-mutating handler runs first: the command names a block that
    /// exists, still stands on the board and has not moved from the origin it was captured at (B-6). A
    /// board that changed since the command was captured refuses it here, before any write.
    /// </summary>
    internal static class CommandPreconditions
    {
        internal static bool TryRefuseStale(IBoardReader board, in GameCommand command, out CommandOutcome refusal)
        {
            int block = command.Block;
            if (block < 0 || block >= board.BlockCount)
            {
                refusal = CommandOutcome.UnknownBlock;
                return true;
            }

            if (board.GetStatus(block) != BlockStatus.OnBoard)
            {
                refusal = CommandOutcome.NotOnBoard;
                return true;
            }

            if (board.GetOrigin(block) != command.From)
            {
                refusal = CommandOutcome.StaleOrigin;
                return true;
            }

            refusal = CommandOutcome.Applied;
            return false;
        }
    }
}
