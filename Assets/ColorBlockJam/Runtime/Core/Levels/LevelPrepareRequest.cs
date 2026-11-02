using System;
using Onity.Unity.SceneFlow;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The typed payload of a Game scene loaded behind the current scene to prepare a level (DEC-199): the Game scene
    /// builds that level hidden, on Home, and plays it only when the level preloader starts it.
    /// </summary>
    /// <remarks>
    /// Immutable. The level preloader stores it in Onity's transition store right before the additive load; the Game
    /// scene installer consumes it once while the scene wakes. It carries no starting boosters: those come with the
    /// launch request that starts the level.
    /// </remarks>
    public sealed class LevelPrepareRequest : IOnitySceneEnterData
    {
        /// <param name="levelIndex">The player's level index to prepare, 0 or more (as <see cref="LevelLaunchRequest"/>).</param>
        /// <exception cref="ArgumentOutOfRangeException">The level index is negative.</exception>
        public LevelPrepareRequest(int levelIndex)
        {
            if (levelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex, "The level index cannot be negative.");
            }

            LevelIndex = levelIndex;
        }

        /// <summary>The player's level index to build; the Game scene resolves it as it resolves a launch request.</summary>
        public int LevelIndex { get; }
    }
}
