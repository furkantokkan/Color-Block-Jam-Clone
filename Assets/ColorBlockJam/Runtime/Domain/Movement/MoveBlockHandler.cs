using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// Applies MoveBlock commands, the one way a block moves or exits during play (DEC-122). It checks
    /// the whole move before touching the board:
    /// - the block stands on the board at From;
    /// - every drag gate allows it (XP-1);
    /// - the move and any exit side stay on its axes (XP-2);
    /// - To is reachable in single validated cell steps (RULE-10, XP-3);
    /// - an exit fits its door at To (RULE-12..14, XP-4).
    /// Only then does the board change, once, and one event is raised: BlockExited for an exit,
    /// MoveCommitted otherwise (DEC-113). A refused command changes nothing. It is internal, so only
    /// the attempt's executor runs it and every applied move reaches the command log.
    /// </summary>
    internal sealed class MoveBlockHandler : ICommandHandler
    {
        private readonly BoardState m_board;
        private readonly AttemptHooks m_hooks;
        private readonly BoardEventLog m_events;
        private readonly MoveReach m_reach;

        // Shows the block at To, so the exit check sees the occupancy the move will leave: the block's
        // From cells free, as the swept path and the exit gates saw them during a live drag.
        private readonly PreviewBoard m_atTarget;

        public MoveBlockHandler(BoardState board, AttemptHooks hooks, BoardEventLog events)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_events = events ?? throw new ArgumentNullException(nameof(events));
            m_reach = new MoveReach(board);
            m_atTarget = new PreviewBoard(board);
        }

        public CommandKind Kind => CommandKind.MoveBlock;

        public CommandOutcome Execute(in GameCommand command)
        {
            if (command.Option < GameCommand.k_noOption || command.Option > GameCommand.k_highestMoveOption)
            {
                return CommandOutcome.Malformed;
            }

            if (CommandPreconditions.TryRefuseStale(m_board, command, out CommandOutcome refusal))
            {
                return refusal;
            }

            int block = command.Block;
            if (!m_hooks.AllowsDrag(m_board, block))
            {
                return CommandOutcome.DragRefused;
            }

            AxisMask axes = m_hooks.GetAxes(m_board, block);
            bool exits = command.TryGetExit(out Side side);
            if (!IsOnAxes(axes, command.To - command.From, exits, side))
            {
                return CommandOutcome.AxisRefused;
            }

            bool moves = command.To != command.From;
            if (!moves && !exits)
            {
                return CommandOutcome.NoChange;
            }

            if (moves && !m_reach.CanReach(m_hooks, block, command.From, command.To, axes))
            {
                return CommandOutcome.Unreachable;
            }

            ExitCandidate exit = default;
            if (exits)
            {
                m_atTarget.Show(block, command.To);
                ExitVerdict verdict = ExitRules.Evaluate(m_atTarget, m_hooks, block, command.To, side, out exit);
                m_atTarget.Clear();
                if (verdict != ExitVerdict.Fits)
                {
                    return CommandOutcome.ExitRefused;
                }
            }

            // A full tick log is a caller bug (a headless caller must clear it between commands, as a
            // tick does); failing here, before any write, keeps the board and the log in step.
            m_events.ThrowIfNoRoom(1);

            // Every check passed: the board changes once and listeners hear exactly one event.
            if (moves)
            {
                m_board.Move(block, command.To);
            }

            if (exits)
            {
                // The exit commits in logic at once (RULE-13): its cells are free before any later command.
                if (exit.Outcome == ExitOutcome.Leave)
                {
                    m_board.Release(block, BlockStatus.Exited);
                }

                BlockExitedEvent exited = new BlockExitedEvent(block, exit.Door, exit.Side, command.To, exit.Outcome);
                m_events.Add(BoardEvent.Exited(exited));
                m_hooks.Publish(exited);
            }
            else
            {
                MoveCommittedEvent committed = new MoveCommittedEvent(block, command.From, command.To);
                m_events.Add(BoardEvent.MoveCommitted(committed));
                m_hooks.Publish(committed);
            }

            return CommandOutcome.Applied;
        }

        // A change along X needs Horizontal and a change along Y needs Vertical; an exit side must lie
        // on an allowed axis, as the live drag never exits across a locked axis (DragResolver).
        private static bool IsOnAxes(AxisMask axes, Cell delta, bool exits, Side side)
        {
            bool horizontal = (axes & AxisMask.Horizontal) != 0;
            bool vertical = (axes & AxisMask.Vertical) != 0;
            if ((delta.X != 0 && !horizontal) || (delta.Y != 0 && !vertical))
            {
                return false;
            }

            return !exits || (SideMath.GetExitAxis(side) & axes) != 0;
        }
    }
}
