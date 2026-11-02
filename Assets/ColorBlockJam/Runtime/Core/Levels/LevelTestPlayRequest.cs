using System;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The Level Editor's Test Level request: the level database (its manifest GUID) and the stable level ID that the
    /// Game scene plays instead of its own level. It lives in Editor session storage until Play Mode ends, so a player
    /// never has one (PRD 11.5, DEC-30, RISK-12, DEC-127, DEC-196).
    /// </summary>
    public sealed class LevelTestPlayRequest
    {
        // Session storage rather than a static field: it survives a domain reload on entering Play Mode, and an Editor
        // restart clears it.
        private const string k_databaseGuidKey = "ColorBlockJam.TestPlay.DatabaseGuid";
        private const string k_levelIdKey = "ColorBlockJam.TestPlay.LevelId";

        public LevelTestPlayRequest(string databaseGuid, string levelId)
        {
            if (string.IsNullOrWhiteSpace(databaseGuid))
            {
                throw new ArgumentException("The level JSON GUID is required.", nameof(databaseGuid));
            }

            if (string.IsNullOrWhiteSpace(levelId))
            {
                throw new ArgumentException("The level ID is required.", nameof(levelId));
            }

            DatabaseGuid = databaseGuid;
            LevelId = levelId;
        }

        /// <summary>The GUID of the manifest of the level database the Level Editor saved the level to.</summary>
        public string DatabaseGuid { get; }

        /// <summary>The stable ID of the level to play (DEC-2).</summary>
        public string LevelId { get; }

        /// <summary>
        /// Finds the play-order index of the requested level in the database the project scope loaded; its pack loads
        /// afterwards (DEC-196). False, with the reason, when that is another database or it no longer holds the level:
        /// the Game scene then plays nothing rather than a level the designer did not choose.
        /// </summary>
        public bool TryFindIndex(LevelCatalog levels, out int index, out string error)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            index = -1;
            if (!string.Equals(levels.JsonGuid, DatabaseGuid, StringComparison.Ordinal))
            {
                error = $"the Level Editor saved level {LevelId} to level database {DatabaseGuid}, but the game loaded " +
                        $"{levels.JsonGuid}.";
                return false;
            }

            index = levels.IndexOf(LevelId);
            if (index < 0)
            {
                error = $"level {LevelId} is not in the loaded level database.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>Reads the pending request; always false outside the Editor.</summary>
        public static bool TryRead(out LevelTestPlayRequest request)
        {
#if UNITY_EDITOR
            string databaseGuid = UnityEditor.SessionState.GetString(k_databaseGuidKey, string.Empty);
            string levelId = UnityEditor.SessionState.GetString(k_levelIdKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(databaseGuid) && !string.IsNullOrWhiteSpace(levelId))
            {
                request = new LevelTestPlayRequest(databaseGuid, levelId);
                return true;
            }
#endif
            request = null;
            return false;
        }

#if UNITY_EDITOR
        /// <summary>Keeps this request for the Game scene builds of this Editor session until <see cref="Clear"/>.</summary>
        public void Write()
        {
            UnityEditor.SessionState.SetString(k_databaseGuidKey, DatabaseGuid);
            UnityEditor.SessionState.SetString(k_levelIdKey, LevelId);
        }

        /// <summary>Removes the pending request.</summary>
        public static void Clear()
        {
            UnityEditor.SessionState.EraseString(k_databaseGuidKey);
            UnityEditor.SessionState.EraseString(k_levelIdKey);
        }
#endif
    }
}
