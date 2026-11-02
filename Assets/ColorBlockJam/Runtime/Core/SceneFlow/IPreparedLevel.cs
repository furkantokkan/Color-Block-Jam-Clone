using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// A Game scene that built its level hidden on Home, behind the scene that asked for it, and waits to be started
    /// (DEC-199); the Game scene implements it and the level preloader starts it.
    /// </summary>
    public interface IPreparedLevel
    {
        /// <summary>True once the level is built and waits hidden; false for a scene that did not prepare one.</summary>
        bool IsPrepared { get; }

        /// <summary>The player's level index it was prepared for, or -1 while it is not prepared.</summary>
        int LevelIndex { get; }

        /// <summary>
        /// Commits the starting boosters of <paramref name="request"/> while the level is still hidden, so the level
        /// shows with its opening at once. Owner: the level preloader, which awaits it. Stops: the commit's end or
        /// <paramref name="cancellationToken"/>.
        /// </summary>
        Task PrepareStartAsync(LevelLaunchRequest request, CancellationToken cancellationToken);

        /// <summary>Shows the level and starts play; called once, after <see cref="PrepareStartAsync"/>.</summary>
        void Play();
    }
}
