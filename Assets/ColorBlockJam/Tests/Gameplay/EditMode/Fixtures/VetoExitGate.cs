using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>An exit gate that refuses every exit and counts how often it was asked.</summary>
    public sealed class VetoExitGate : IExitGate
    {
        public int Calls { get; private set; }

        public bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome)
        {
            Calls++;
            return false;
        }
    }
}
