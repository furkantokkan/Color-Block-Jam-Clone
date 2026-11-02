using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// Leaves the Game scene for Hub; a seam so the exit navigator's guard can be tested without loading scenes
    /// (Game HUD design 4).
    /// </summary>
    public interface IHubSceneTransition
    {
        /// <summary>
        /// Owner: the exit navigator, which awaits it. Stops: <paramref name="cancellationToken"/>; a scene load Unity
        /// already started still finishes.
        /// </summary>
        Task TransitionAsync(CancellationToken cancellationToken);
    }
}
