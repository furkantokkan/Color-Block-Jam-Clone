using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Debits one charge per listed booster as one saved snapshot, before the effect applies (DEC-92, AC-BST-0.3,
    /// Booster-Gameplay B-14).
    /// </summary>
    public interface IBoosterConsumer
    {
        /// <summary>
        /// Spends exactly one charge of each listed booster in one write; the list is copied before the method first
        /// awaits. Null, empty, None, duplicate or unknown IDs are InvalidRequest. The token only cancels the wait
        /// for the write; a write that already started completes as Consumed or SaveFailed.
        /// The call can run to completion on the caller's stack: IBoosterShop.IsSaving is published at once, and
        /// with a free lock and a store that finishes synchronously (the in-memory Editor store) the snapshot is
        /// serialized, stored and published before the task returns, already complete. A caller inside the Playing
        /// tick therefore yields out of the tick before it calls this method (Booster-Gameplay B-13).
        /// </summary>
        Task<BoosterConsumeResult> ConsumeAsync(IReadOnlyList<BoosterId> debits, CancellationToken cancellationToken);
    }
}
