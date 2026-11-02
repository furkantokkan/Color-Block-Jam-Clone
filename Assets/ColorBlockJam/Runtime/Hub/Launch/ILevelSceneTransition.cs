using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Hub.Launch
{
    /// <summary>
    /// Loads the Game scene with a launch request; the seam between <see cref="LevelLauncher"/> and Onity's scene flow,
    /// so EditMode tests can replace the scene load.
    /// </summary>
    public interface ILevelSceneTransition
    {
        /// <summary>
        /// Builds <paramref name="levelIndex"/> hidden behind Hub, so a later <see cref="TransitionAsync"/> for it opens
        /// at once (DEC-199). Completes when the level waits or its preparation failed; cancellation stops waiting only.
        /// </summary>
        Task PrepareAsync(int levelIndex, CancellationToken cancellationToken);

        /// <summary>
        /// Opens the prepared level at once when it matches the request, else loads the Game scene behind the fade
        /// (DEC-198, DEC-199). Completes when the level shows; cancellation stops waiting, not a started load.
        /// </summary>
        Task TransitionAsync(LevelLaunchRequest request, CancellationToken cancellationToken);
    }
}
