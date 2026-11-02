using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Currencies;
using Onity.Reactive;

namespace ColorBlockJam.Player.Lives
{
    /// <summary>
    /// The player's lives (DEC-174): a level needs one to start, a failed, retried or abandoned attempt costs one, one
    /// refills per interval up to the maximum, and all refill at once for the currency's refill price. Every change is
    /// one save through the player save service; the count changes only after it commits.
    /// </summary>
    public interface ILives
    {
        /// <summary>The life currency (icon, maximum, refill time and price).</summary>
        CurrencyDefinition Currency { get; }

        /// <summary>The committed life count; due refills appear after <see cref="RefreshAsync"/> commits them.</summary>
        IReadOnlyReactiveProperty<int> Count { get; }

        /// <summary>Lives available now: the committed count plus refills already due by the clock.</summary>
        int GetAvailable();

        /// <summary>Whole seconds until the next life; 0 at the maximum.</summary>
        long GetSecondsUntilNext();

        /// <summary>Commits refills that are due; Saved without a write when none is.</summary>
        Task<PlayerSaveResult> RefreshAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Removes one life (never below 0) after committing due refills, as one save. Owner: the caller that awaits
        /// it; its token cancels only the wait for the write lock, a started write completes under the service lifetime.
        /// </summary>
        Task<PlayerSaveResult> LoseOneAsync(CancellationToken cancellationToken);

        /// <summary>Pays the refill price and fills lives to the maximum in one save.</summary>
        Task<CurrencyRefillResult> RefillAsync(CancellationToken cancellationToken);
    }
}
