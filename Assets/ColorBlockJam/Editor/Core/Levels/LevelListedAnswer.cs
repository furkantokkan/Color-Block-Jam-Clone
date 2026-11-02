using System;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Validate Level answer the validation panel shows and what it was made for: the level, its content, the level
    /// database and the level index (TASK-20260928-091128541-cb48c84a). A save that reloads the same content keeps it;
    /// any other change of these makes it stale. Not kept across a domain reload or a GUI rebuild.
    /// </summary>
    internal sealed class LevelListedAnswer
    {
        private string m_json;
        private string m_databaseGuid;
        private int m_levelIndex = -1;

        /// <summary>The level the answer was made for, or null when none is listed.</summary>
        internal LevelData Level { get; private set; }

        /// <summary>The check's result with any solution list, or null.</summary>
        internal LevelCheckResult Result { get; private set; }

        /// <summary>True when no answer is listed.</summary>
        internal bool IsEmpty => m_json == null;

        internal void Set(LevelData level, string databaseGuid, int levelIndex, LevelCheckResult result)
        {
            Level = level;
            m_json = level != null ? JsonUtility.ToJson(level) : null;
            m_databaseGuid = databaseGuid;
            m_levelIndex = levelIndex;
            Result = result;
        }

        /// <summary>True while this level, at this index of this database, still has the content the answer was for.</summary>
        internal bool IsFor(LevelData level, string databaseGuid, int levelIndex)
        {
            return level != null && !IsEmpty && m_levelIndex == levelIndex
                   && string.Equals(m_databaseGuid, databaseGuid, StringComparison.Ordinal)
                   && string.Equals(m_json, JsonUtility.ToJson(level), StringComparison.Ordinal);
        }

        internal void Clear()
        {
            Level = null;
            Result = null;
            m_json = null;
            m_databaseGuid = null;
            m_levelIndex = -1;
        }
    }
}
