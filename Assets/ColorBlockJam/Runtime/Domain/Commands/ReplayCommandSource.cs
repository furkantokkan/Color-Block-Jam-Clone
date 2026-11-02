using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// Plays a recorded command list back into an attempt, one command per offer, in order. The
    /// same level and the same list rebuild the same board, so the first refused command means the
    /// recording does not belong to this board: the replay stops there and reports it.
    /// </summary>
    public sealed class ReplayCommandSource : ICommandSource
    {
        private readonly GameCommand[] m_commands;
        private int m_next;

        /// <summary>Copies the list, so the caller may reuse its array.</summary>
        public ReplayCommandSource(GameCommand[] commands)
        {
            if (commands == null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            m_commands = (GameCommand[])commands.Clone();
        }

        public int Count => m_commands.Length;

        /// <summary>How many commands have applied so far.</summary>
        public int Position => m_next;

        public bool HasFailed { get; private set; }

        /// <summary>The index of the refused command, or -1 while nothing was refused.</summary>
        public int FailedIndex { get; private set; } = -1;

        public CommandOutcome FailedOutcome { get; private set; }

        /// <summary>True when every command applied or the replay stopped at a refusal.</summary>
        public bool IsFinished => HasFailed || m_next >= m_commands.Length;

        public bool TryGetNext(IBoardReader board, out GameCommand command)
        {
            if (IsFinished)
            {
                command = default;
                return false;
            }

            command = m_commands[m_next];
            return true;
        }

        public void OnExecuted(in GameCommand command, CommandOutcome outcome)
        {
            if (IsFinished)
            {
                return;
            }

            if (outcome == CommandOutcome.Applied)
            {
                m_next++;
                return;
            }

            HasFailed = true;
            FailedIndex = m_next;
            FailedOutcome = outcome;
        }
    }
}
