using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// A booster target hook (XP-6): false keeps a board booster of the given command kind off the block. Targeting,
    /// crosshairs, the opening Rocket and the command handlers all read it through BoosterTargets, so a use is never
    /// debited for a target its handler would refuse (DEC-171).
    /// </summary>
    public interface IBoosterTargetGate
    {
        bool AllowsBoosterTarget(IBoardReader board, CommandKind kind, int block);
    }
}
