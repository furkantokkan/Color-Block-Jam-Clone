using ColorBlockJam.Domain.Board;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// Receives the board cell a targeting press chose (B-12, B-13). It validates the target on the committed board,
    /// captures and submits the intent, and reports whether the targeting mode ends. It runs inside the pointer
    /// step of the Playing tick, so it allocates nothing and starts no store work there.
    /// </summary>
    public interface IBoosterTargetSink
    {
        BoosterTargetResult TrySubmitTarget(BoosterId id, LevelAttempt attempt, Cell cell);
    }
}
