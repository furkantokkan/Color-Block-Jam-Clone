namespace ColorBlockJam.Player
{
    /// <summary>
    /// Turns the saved level index into the level the Home button plays and the database entry that level uses. Level
    /// numbers keep counting past the last database level, and the levels then play again from the first (DEC-173):
    /// with five levels, Level 6 plays the first level's board, Level 7 the second's, and so on.
    /// </summary>
    public static class PlayerProgress
    {
        /// <summary>The saved index itself, never below 0; -1 when there is no level (DEC-44, DEC-173).</summary>
        public static int GetPlayableLevelIndex(int savedIndex, int levelCount)
        {
            if (levelCount <= 0)
            {
                return -1;
            }

            return savedIndex < 0 ? 0 : savedIndex;
        }

        /// <summary>
        /// The database entry level index <paramref name="levelIndex"/> plays: the index modulo
        /// <paramref name="levelCount"/>; -1 for a negative index or without a level (DEC-173).
        /// </summary>
        public static int GetDatabaseIndex(int levelIndex, int levelCount)
        {
            if (levelCount <= 0 || levelIndex < 0)
            {
                return -1;
            }

            return levelIndex % levelCount;
        }
    }
}
