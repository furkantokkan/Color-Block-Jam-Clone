using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Exit
{
    /// <summary>A fitting exit: the door it uses, the side it leaves through and what the exit gates decided.</summary>
    public readonly struct ExitCandidate
    {
        public ExitCandidate(int door, Side side, ExitOutcome outcome)
        {
            Door = door;
            Side = side;
            Outcome = outcome;
        }

        public int Door { get; }

        public Side Side { get; }

        public ExitOutcome Outcome { get; }
    }
}
