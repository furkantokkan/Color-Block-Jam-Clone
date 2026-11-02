using System.Globalization;

namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// One applied command as the log keeps it: the command plus when it applied. The stamp stays out of
    /// GameCommand, so a replayed command still equals the recorded one and replay stays deterministic.
    /// Turn is the command's 1-based place among the attempt's applied commands (one command is one move,
    /// DEC-122). Tick and Seconds count Playing time only: the executor advances them once per Playing
    /// tick, so pause, Saving and Home never add time, and the same recording gives the same stamps.
    /// </summary>
    public readonly struct CommandRecord
    {
        public CommandRecord(in GameCommand command, int turn, int tick, float seconds)
        {
            Command = command;
            Turn = turn;
            Tick = tick;
            Seconds = seconds;
        }

        public GameCommand Command { get; }

        /// <summary>1 for the attempt's first applied command, then 2, 3 and so on.</summary>
        public int Turn { get; }

        /// <summary>The 1-based Playing tick the command applied in; 0 before the first Playing tick.</summary>
        public int Tick { get; }

        /// <summary>Playing seconds since the attempt started, up to and including the applying tick.</summary>
        public float Seconds { get; }

        public override string ToString()
        {
            return "turn " + Turn + " tick " + Tick + " at " + Seconds.ToString("0.000", CultureInfo.InvariantCulture) + " s: " + Command;
        }
    }
}
