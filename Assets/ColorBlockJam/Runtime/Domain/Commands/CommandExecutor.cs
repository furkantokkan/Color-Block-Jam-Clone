using System;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// The one place an attempt runs commands (the invoker in the Command pattern): it passes each
    /// command to the handler of its kind and logs every applied command in order. The pointer, a
    /// replay and any other source all come through here, so one log describes the whole attempt (DEC-122).
    /// </summary>
    public sealed class CommandExecutor
    {
        // Indexed by CommandKind; a null slot means no handler for that kind.
        private readonly ICommandHandler[] m_handlers;

        /// <summary>Takes the attempt's handlers, one per kind; a missing or duplicate kind throws.</summary>
        public CommandExecutor(ICommandHandler[] handlers, CommandLog log)
        {
            if (handlers == null)
            {
                throw new ArgumentNullException(nameof(handlers));
            }

            Log = log ?? throw new ArgumentNullException(nameof(log));

            int highest = 0;
            for (int i = 0; i < handlers.Length; i++)
            {
                if (handlers[i] == null || handlers[i].Kind == CommandKind.None)
                {
                    throw new ArgumentException($"Handler {i} is missing or has no kind.", nameof(handlers));
                }

                highest = Math.Max(highest, (int)handlers[i].Kind);
            }

            m_handlers = new ICommandHandler[highest + 1];
            for (int i = 0; i < handlers.Length; i++)
            {
                int kind = (int)handlers[i].Kind;
                if (m_handlers[kind] != null)
                {
                    throw new ArgumentException($"Two handlers are registered for {handlers[i].Kind}.", nameof(handlers));
                }

                m_handlers[kind] = handlers[i];
            }
        }

        /// <summary>Every command this attempt applied, in the order it applied them.</summary>
        public CommandLog Log { get; }

        /// <summary>Playing ticks counted so far; the stamp of every command logged during the current tick.</summary>
        public int Tick { get; private set; }

        /// <summary>Playing seconds counted so far, including the current tick.</summary>
        public float Seconds { get; private set; }

        /// <summary>
        /// Starts the next Playing tick for the log's stamps. The session calls it once at the start of
        /// each Playing tick, so paused, Saving and Home time never reach the stamps.
        /// </summary>
        public void AdvanceTick(float deltaTime)
        {
            Tick++;
            if (deltaTime > 0f)
            {
                Seconds += deltaTime;
            }
        }

        /// <summary>Runs the command's handler and logs the command, stamped with its turn and Playing time, when it applied; a refusal is not logged.</summary>
        public CommandOutcome Execute(in GameCommand command)
        {
            int kind = (int)command.Kind;
            ICommandHandler handler = kind > 0 && kind < m_handlers.Length ? m_handlers[kind] : null;
            if (handler == null)
            {
                return CommandOutcome.UnknownKind;
            }

            CommandOutcome outcome = handler.Execute(command);
            if (outcome == CommandOutcome.Applied)
            {
                Log.Add(command, Tick, Seconds);
            }

            return outcome;
        }
    }
}
