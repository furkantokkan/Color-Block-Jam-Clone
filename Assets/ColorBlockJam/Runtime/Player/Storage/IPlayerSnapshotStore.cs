using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Player.Storage
{
    /// <summary>
    /// Where the player snapshot lives: an encrypted file in players, memory in Editor play and tests. It is the
    /// only persistence seam: a future backend arrives as one more implementation bound by PlayerDataInstaller, with
    /// no other layer (design section 5).
    /// </summary>
    public interface IPlayerSnapshotStore
    {
        /// <summary>
        /// The last saved snapshot, or null when none was ever written or none can be read. Throws
        /// PlayerSnapshotVersionException when the saved data comes from a newer format, so nothing overwrites it.
        /// </summary>
        Task<PlayerSnapshot> LoadAsync(CancellationToken cancellationToken);

        /// <summary>Completes only after the snapshot is durable; throws on failure and keeps the previous one.</summary>
        Task SaveAsync(PlayerSnapshot snapshot, CancellationToken cancellationToken);
    }
}
