using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A target sink that records each chosen cell and answers with Result. With a Session it instead submits a
    /// Hammer intent for the tapped block, as the coordinator would, and answers Accepted when the session takes it.
    /// </summary>
    public sealed class RecordingTargetSink : IBoosterTargetSink
    {
        public BoosterTargetResult Result { get; set; } = BoosterTargetResult.Rejected;

        public LevelSession Session { get; set; }

        public int Count { get; private set; }

        public BoosterId LastId { get; private set; }

        public LevelAttempt LastAttempt { get; private set; }

        public Cell LastCell { get; private set; }

        public BoosterTargetResult TrySubmitTarget(BoosterId id, LevelAttempt attempt, Cell cell)
        {
            Count++;
            LastId = id;
            LastAttempt = attempt;
            LastCell = cell;
            if (Session == null)
            {
                return Result;
            }

            IBoardReader board = attempt.Simulation.Board;
            int block = board.GetOccupant(cell);
            BoosterIntent intent = BoosterIntent.ForBoard(attempt.Generation, BoosterId.Hammer,
                GameCommand.RemoveBlock(block, board.GetOrigin(block)));
            return Session.TrySubmitBooster(intent) ? BoosterTargetResult.Accepted : BoosterTargetResult.Rejected;
        }
    }
}
