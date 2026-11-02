using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Decides whether a fitting exit may happen and may turn it into Stay (XP-4). Gates combine with
    /// AND; any gate may set the outcome.
    /// </summary>
    public interface IExitGate
    {
        bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome);
    }
}
