using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Currencies;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Commits one level win as a single snapshot: the coin reward and the saved level index together (DEC-92,
    /// AC-6.14). A failed or cancelled write changes nothing, so retrying the same call records the same win once.
    /// </summary>
    public interface ILevelWinRecorder
    {
        /// <summary>
        /// Adds <paramref name="reward"/> of <paramref name="currency"/> (clamped to its maximum) and raises the saved
        /// level index to at least <paramref name="wonLevelIndex"/> + 1, never lowering it, in one write. The index keeps
        /// counting past the last database level, whose levels then play again (DEC-173). Owner: the caller that awaits
        /// the task. Its token cancels only the wait for the write lock; a started store write runs under the service
        /// lifetime, so an ending attempt cannot cancel a committed reward.
        /// </summary>
        Task<PlayerSaveResult> RecordWinAsync(int wonLevelIndex, CurrencyDefinition currency, int reward,
            CancellationToken cancellationToken);
    }
}
