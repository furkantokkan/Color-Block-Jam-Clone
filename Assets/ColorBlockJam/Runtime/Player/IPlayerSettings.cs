using System.Threading;
using System.Threading.Tasks;
using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>Reads the committed Settings toggles and writes them through the single save owner.</summary>
    public interface IPlayerSettings
    {
        IReadOnlyReactiveProperty<bool> Get(PlayerSettingId id);

        /// <summary>Saves the toggle; on SaveFailed or Cancelled the committed value is unchanged.</summary>
        Task<PlayerSaveResult> SetAsync(PlayerSettingId id, bool value, CancellationToken cancellationToken);
    }
}
