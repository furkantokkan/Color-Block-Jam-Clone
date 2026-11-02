using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>
    /// Applies RemoveColor commands, the one way Vacuum changes the board (B-6). It checks that the
    /// tapped block stands on the board at From with the colour in Option and no booster restriction
    /// keeps Vacuum off it, collects every on-board block of that colour that no restriction guards
    /// (restricted blocks stay, DEC-171; Ice and Arrow blocks are unrestricted by default), and checks
    /// the tick log has room for all of them. Only then are they removed, in one step, and listeners hear one BlockRemoved per block
    /// in ascending ID, each naming the tapped block as its source. A refused command changes nothing.
    /// It is internal, so only the attempt's executor runs it.
    /// </summary>
    internal sealed class RemoveColorHandler : ICommandHandler
    {
        private readonly BoardState m_board;
        private readonly AttemptHooks m_hooks;
        private readonly BoardEventLog m_events;

        // The blocks one command removes; every live block holds a cell, so the level's cell count bounds them.
        private readonly int[] m_targets;

        public RemoveColorHandler(BoardState board, AttemptHooks hooks, BoardEventLog events)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_events = events ?? throw new ArgumentNullException(nameof(events));
            m_targets = new int[board.Model.TotalCellCount];
        }

        public CommandKind Kind => CommandKind.RemoveColor;

        public CommandOutcome Execute(in GameCommand command)
        {
            if (CommandPreconditions.TryRefuseStale(m_board, command, out CommandOutcome refusal))
            {
                return refusal;
            }

            int tapped = command.Block;
            BlockColor color = m_board.GetFootprint(tapped).Color;
            if (color.Id != command.Option)
            {
                return CommandOutcome.ColorMismatch;
            }

            if (!m_board.AllowsBoosterTarget(CommandKind.RemoveColor, tapped))
            {
                return CommandOutcome.EffectRestricted;
            }

            int count = 0;
            for (int block = 0; block < m_board.BlockCount; block++)
            {
                if (m_board.GetStatus(block) == BlockStatus.OnBoard && m_board.GetFootprint(block).Color == color
                    && m_board.AllowsBoosterTarget(CommandKind.RemoveColor, block))
                {
                    m_targets[count] = block;
                    count++;
                }
            }

            // A full tick log is a caller bug; failing before the first removal keeps the board and the log in step.
            m_events.ThrowIfNoRoom(count);

            for (int i = 0; i < count; i++)
            {
                m_board.Release(m_targets[i], BlockStatus.Removed);
            }

            for (int i = 0; i < count; i++)
            {
                BlockRemovedEvent removed = new BlockRemovedEvent(m_targets[i], CommandKind.RemoveColor, tapped);
                m_events.Add(BoardEvent.Removed(removed));
                m_hooks.Publish(removed);
            }

            return CommandOutcome.Applied;
        }
    }
}
