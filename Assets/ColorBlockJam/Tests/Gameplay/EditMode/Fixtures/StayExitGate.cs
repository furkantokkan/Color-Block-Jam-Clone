using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>An exit gate that allows every exit but keeps the block on the board (a future layer peel).</summary>
    public sealed class StayExitGate : IExitGate
    {
        public bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome)
        {
            outcome = ExitOutcome.Stay;
            return true;
        }
    }
}
