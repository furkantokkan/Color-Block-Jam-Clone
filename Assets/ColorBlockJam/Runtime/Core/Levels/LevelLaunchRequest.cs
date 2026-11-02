using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ColorBlockJam.Runtime.Boosters;
using Onity.Unity.SceneFlow;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The typed Hub-to-Game scene payload: the level index to play and the starting boosters the player selected
    /// on the pre-level popup (Hub-And-Player-Data-Design 9.3).
    /// </summary>
    /// <remarks>
    /// Immutable. Onity's scene flow carries it to the Game scene, which consumes it once. The starting boosters are
    /// carried only: applying and debiting them belongs to the booster opening task (AC-5.7), so nothing is spent.
    /// </remarks>
    public sealed class LevelLaunchRequest : IOnitySceneEnterData
    {
        private readonly ReadOnlyCollection<BoosterId> m_startingBoosters;

        /// <param name="levelIndex">The player's level index (its number minus one), 0 or more; it may pass the last
        /// database level, whose levels then play again (DEC-173).</param>
        /// <param name="startingBoosters">Selected starting boosters in selector order; null or empty for none.</param>
        /// <exception cref="ArgumentOutOfRangeException">The level index is negative.</exception>
        /// <exception cref="ArgumentException">A starting booster is <see cref="BoosterId.None"/> or listed twice.</exception>
        public LevelLaunchRequest(int levelIndex, IReadOnlyList<BoosterId> startingBoosters)
        {
            if (levelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex, "The level index cannot be negative.");
            }

            int count = startingBoosters?.Count ?? 0;
            BoosterId[] copy = count == 0 ? Array.Empty<BoosterId>() : new BoosterId[count];
            for (int i = 0; i < count; i++)
            {
                BoosterId id = startingBoosters[i];
                if (id == BoosterId.None)
                {
                    throw new ArgumentException("A starting booster cannot be None.", nameof(startingBoosters));
                }

                for (int previous = 0; previous < i; previous++)
                {
                    if (copy[previous] == id)
                    {
                        throw new ArgumentException($"The starting booster {id} is listed twice.", nameof(startingBoosters));
                    }
                }

                copy[i] = id;
            }

            LevelIndex = levelIndex;
            m_startingBoosters = Array.AsReadOnly(copy);
        }

        /// <summary>
        /// The player's level index chosen on Home or by Continue. The Game scene plays the database entry at this index
        /// modulo the level count and shows the number index + 1 (DEC-173).
        /// </summary>
        public int LevelIndex { get; }

        /// <summary>
        /// The selected starting boosters, in selector order: a read-only view of a private copy, so neither the caller's
        /// list nor a consumer can change the request after it is built.
        /// </summary>
        public IReadOnlyList<BoosterId> StartingBoosters => m_startingBoosters;
    }
}
