using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// Reloads the Game scene for another level with no starting boosters (the win popup's Continue, AC-4.9). EditMode
    /// tests fake it.
    /// </summary>
    public interface INextLevelTransition
    {
        /// <summary>
        /// Builds <paramref name="levelIndex"/> hidden behind the current Game scene, so a later
        /// <see cref="TransitionAsync"/> for it opens at once (DEC-199). Completes when the level waits or its
        /// preparation failed; cancellation stops waiting only.
        /// </summary>
        Task PrepareAsync(int levelIndex, CancellationToken cancellationToken);

        /// <summary>
        /// Opens <paramref name="levelIndex"/>: the prepared level at once, else a new Game scene behind the fade; either
        /// way the current scene unloads.
        /// </summary>
        Task TransitionAsync(int levelIndex, CancellationToken cancellationToken);
    }
}
