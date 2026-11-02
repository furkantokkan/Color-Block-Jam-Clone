using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Keeps the player snapshot as plain JSON in memory for one app scope, so Editor play never reads or writes the
    /// player's save (RULE-25, DEC-30, AC-6.6) and tests run the same JSON rules without files.
    /// </summary>
    public sealed class MemoryPlayerSnapshotStore : IPlayerSnapshotStore
    {
        public MemoryPlayerSnapshotStore(string json = null)
        {
            Json = json;
        }

        /// <summary>The stored JSON, or null when nothing was written.</summary>
        public string Json { get; private set; }

        /// <summary>Completed saves.</summary>
        public int SaveCount { get; private set; }

        public Task<PlayerSnapshot> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Json == null ? null : PlayerSnapshotJson.Read(Json));
        }

        public Task SaveAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            cancellationToken.ThrowIfCancellationRequested();
            Json = PlayerSnapshotJson.Write(snapshot);
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
