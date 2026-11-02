using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>
    /// Applies RemoveCell commands, the one way Rocket changes the board (B-4, B-6). It checks the whole
    /// command before touching the board:
    /// - the block stands on the board at From;
    /// - To is one of its cells, no booster restriction guards the block (DEC-171) and To is not its
    ///   only cell (BoosterTargets).
    /// No drag gate is asked, so a frozen or Arrow block is a valid target unless restricted. Only then is the cell
    /// removed, once, and listeners hear one FootprintChanged for the block, then one for each new
    /// part in ascending ID (B-5); a split is neither an exit nor a removal. A refused command changes
    /// nothing. It is internal, so only the attempt's executor runs it and every applied use is logged.
    /// </summary>
    internal sealed class RemoveCellHandler : ICommandHandler
    {
        private readonly BoardState m_board;
        private readonly AttemptHooks m_hooks;
        private readonly BoardEventLog m_events;
        private readonly int[] m_created = new int[BoardState.k_maxSplitChildren];

        public RemoveCellHandler(BoardState board, AttemptHooks hooks, BoardEventLog events)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public CommandKind Kind => CommandKind.RemoveCell;

        public CommandOutcome Execute(in GameCommand command)
        {
            if (command.Option != GameCommand.k_noOption)
            {
                return CommandOutcome.Malformed;
            }

            if (CommandPreconditions.TryRefuseStale(m_board, command, out CommandOutcome refusal))
            {
                return refusal;
            }

            int block = command.Block;
            if (m_board.GetOccupant(command.To) != block)
            {
                return CommandOutcome.NotBlockCell;
            }

            if (!BoosterTargets.CanRemoveCell(m_board, command.To, out int _, out CommandOutcome cellRefusal))
            {
                return cellRefusal;
            }

            // One event per remaining part; a full tick log is a caller bug, so failing here, before
            // any write, keeps the board and the log in step.
            int parts = m_board.CountPartsWithout(block, command.To);
            m_events.ThrowIfNoRoom(parts);

            int created = m_board.RemoveCell(block, command.To, m_created);
            Raise(new FootprintChangedEvent(block, block, command.To));
            for (int i = 0; i < created; i++)
            {
                Raise(new FootprintChangedEvent(m_created[i], block, command.To));
            }

            return CommandOutcome.Applied;
        }

        private void Raise(in FootprintChangedEvent changed)
        {
            m_events.Add(BoardEvent.FootprintChanged(changed));
            m_hooks.Publish(changed);
        }
    }
}
