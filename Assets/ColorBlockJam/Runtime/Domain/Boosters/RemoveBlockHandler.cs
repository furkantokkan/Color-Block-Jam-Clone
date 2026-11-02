using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>
    /// Applies RemoveBlock commands, the one way Hammer changes the board (B-6): a block standing on
    /// the board at From is removed whole, whatever its footprint, including an active Ice shell unless a
    /// booster restriction keeps Hammer off it (DEC-171), and listeners hear exactly one BlockRemoved,
    /// never a BlockExited. No drag gate is asked. A refused
    /// command changes nothing. It is internal, so only the attempt's executor runs it.
    /// </summary>
    internal sealed class RemoveBlockHandler : ICommandHandler
    {
        private readonly BoardState m_board;
        private readonly AttemptHooks m_hooks;
        private readonly BoardEventLog m_events;

        public RemoveBlockHandler(BoardState board, AttemptHooks hooks, BoardEventLog events)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public CommandKind Kind => CommandKind.RemoveBlock;

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
            if (!m_board.AllowsBoosterTarget(CommandKind.RemoveBlock, block))
            {
                return CommandOutcome.EffectRestricted;
            }

            m_events.ThrowIfNoRoom(1);

            m_board.Release(block, BlockStatus.Removed);
            BlockRemovedEvent removed = new BlockRemovedEvent(block, CommandKind.RemoveBlock, block);
            m_events.Add(BoardEvent.Removed(removed));
            m_hooks.Publish(removed);
            return CommandOutcome.Applied;
        }
    }
}
