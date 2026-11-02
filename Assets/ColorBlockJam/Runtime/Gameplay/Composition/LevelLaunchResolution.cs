using ColorBlockJam.Player;

namespace ColorBlockJam.Gameplay.Composition
{
    /// <summary>Names where the Game scene's level came from (design 9.3).</summary>
    public enum LevelLaunchSource
    {
        /// <summary>No level can play; the attempt is skipped.</summary>
        None = 0,

        /// <summary>Hub's <c>LevelLaunchRequest</c>.</summary>
        Request = 1,

        /// <summary>The Level Editor's Test Level request, which names its level by stable ID (DEC-127).</summary>
        TestPlay = 2,

        /// <summary>The Game scene installer's serialized index, for direct Play of Game (DEC-116).</summary>
        Serialized = 3
    }

    /// <summary>
    /// Decides which level the Game scene plays: Hub's launch request, then the Level Editor's Test Level, then the
    /// installer's serialized index. A pure function, so the precedence and the level numbering are EditMode-testable.
    /// </summary>
    /// <remarks>
    /// A requested index is the player's level index (its number minus one), which keeps counting past the last
    /// database level; it plays the database entry at that index modulo the level count (DEC-173), so Level 6 of a
    /// five-level database plays the first level's board and still reads "Level 6".
    /// </remarks>
    public readonly struct LevelLaunchResolution
    {
        private LevelLaunchResolution(LevelLaunchSource source, int levelIndex, int levelNumber)
        {
            Source = source;
            LevelIndex = levelIndex;
            LevelNumber = levelNumber;
        }

        /// <summary>Which input chose the level, or <see cref="LevelLaunchSource.None"/> when nothing plays.</summary>
        public LevelLaunchSource Source { get; }

        /// <summary>
        /// The database entry to play for <see cref="LevelLaunchSource.Request"/> and
        /// <see cref="LevelLaunchSource.Serialized"/>; otherwise -1.
        /// </summary>
        public int LevelIndex { get; }

        /// <summary>
        /// The 1-based level number the HUD shows for <see cref="LevelLaunchSource.Request"/> and
        /// <see cref="LevelLaunchSource.Serialized"/>; otherwise 0 (a Test Level finds its number by ID).
        /// </summary>
        public int LevelNumber { get; }

        /// <param name="requestedIndex">Hub's requested level index, or null without a launch request.</param>
        /// <param name="hasTestPlay">True when the Level Editor left a Test Level request.</param>
        /// <param name="serializedIndex">The Game scene installer's index.</param>
        /// <param name="levelCount">The number of levels the project scope loaded.</param>
        public static LevelLaunchResolution Resolve(int? requestedIndex, bool hasTestPlay, int serializedIndex, int levelCount)
        {
            // Hub's request: the database entry repeats after the last level, the number keeps counting (DEC-173).
            if (requestedIndex.HasValue)
            {
                int requested = requestedIndex.Value < 0 ? 0 : requestedIndex.Value;
                int entry = PlayerProgress.GetDatabaseIndex(requested, levelCount);
                return entry < 0
                    ? new LevelLaunchResolution(LevelLaunchSource.None, -1, 0)
                    : new LevelLaunchResolution(LevelLaunchSource.Request, entry, requested + 1);
            }

            // The test-play request resolves its level by stable ID; the caller looks it up.
            if (hasTestPlay)
            {
                return new LevelLaunchResolution(LevelLaunchSource.TestPlay, -1, 0);
            }

            // Direct Play keeps its existing rule: an index outside the database skips the attempt.
            if (serializedIndex >= 0 && serializedIndex < levelCount)
            {
                return new LevelLaunchResolution(LevelLaunchSource.Serialized, serializedIndex, serializedIndex + 1);
            }

            return new LevelLaunchResolution(LevelLaunchSource.None, -1, 0);
        }
    }
}
