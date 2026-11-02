using System;
using System.Collections.Generic;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>One pack of a level database as its manifest lists it: the file and its level IDs in play order.</summary>
    public readonly struct LevelManifestPack
    {
        public LevelManifestPack(string file, IReadOnlyList<string> levelIds)
        {
            File = file;
            LevelIds = levelIds;
        }

        /// <summary>The pack's file name in the database folder.</summary>
        public string File { get; }

        public IReadOnlyList<string> LevelIds { get; }
    }

    /// <summary>Where one pack's levels sit in the play order of its database.</summary>
    public readonly struct LevelPackInfo
    {
        public LevelPackInfo(string file, int firstLevelIndex, int levelCount)
        {
            File = file;
            FirstLevelIndex = firstLevelIndex;
            LevelCount = levelCount;
        }

        /// <summary>The pack's file name, or null for the one pack of a single-file level JSON.</summary>
        public string File { get; }

        public int FirstLevelIndex { get; }
        public int LevelCount { get; }
    }

    /// <summary>
    /// The play order of one level database (DEC-196): its name, its pack files and each pack's level IDs, with the
    /// index of every ID. Immutable. <see cref="LevelManifestJson"/> reads it from a manifest, the Level Editor plans a
    /// new one on every save, and a single-file level JSON gets a nameless manifest with one pack.
    /// </summary>
    public sealed class LevelManifest
    {
        private readonly LevelPackInfo[] m_packs;
        private readonly string[] m_levelIds;
        private readonly Dictionary<string, int> m_indexById;

        private LevelManifest(string name, LevelPackInfo[] packs, string[] levelIds, Dictionary<string, int> indexById)
        {
            Name = name;
            m_packs = packs;
            m_levelIds = levelIds;
            m_indexById = indexById;
        }

        /// <summary>The database name, which is also its folder name; null for a single-file level JSON.</summary>
        public string Name { get; }

        /// <summary>The number of levels in the database.</summary>
        public int Count => m_levelIds.Length;

        public int PackCount => m_packs.Length;

        public LevelPackInfo GetPackAt(int packIndex)
        {
            return m_packs[packIndex];
        }

        public string GetLevelIdAt(int levelIndex)
        {
            return m_levelIds[levelIndex];
        }

        /// <summary>The play-order index of the level with <paramref name="id"/>, or -1 when the database has none.</summary>
        public int IndexOf(string id)
        {
            return id != null && m_indexById.TryGetValue(id, out int index) ? index : -1;
        }

        /// <summary>The index of the pack that holds the level at <paramref name="levelIndex"/>.</summary>
        public int GetPackIndex(int levelIndex)
        {
            if ((uint)levelIndex >= (uint)m_levelIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex,
                    $"The database has {m_levelIds.Length} level(s).");
            }

            // Packs are contiguous runs of the play order, so the last pack starting at or before the index holds it.
            int low = 0;
            int high = m_packs.Length - 1;
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (m_packs[middle].FirstLevelIndex <= levelIndex)
                {
                    low = middle;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        /// <summary>
        /// Checks that <paramref name="levels"/> are exactly the pack's levels in play order. A pack and its manifest are
        /// written together, so other content means the pack belongs to another version of the database.
        /// </summary>
        public bool TryMatchPack(int packIndex, IReadOnlyList<LevelData> levels, out string error)
        {
            if (levels == null)
            {
                throw new ArgumentNullException(nameof(levels));
            }

            LevelPackInfo pack = m_packs[packIndex];
            string name = pack.File ?? "The level JSON";
            if (levels.Count != pack.LevelCount)
            {
                error = $"Pack {name} holds {levels.Count} level(s), but the manifest lists {pack.LevelCount}.";
                return false;
            }

            for (int index = 0; index < levels.Count; index++)
            {
                string expected = m_levelIds[pack.FirstLevelIndex + index];
                string found = levels[index].Id;
                if (!string.Equals(found, expected, StringComparison.Ordinal))
                {
                    error = $"Pack {name} holds level {found} where the manifest lists {expected}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Builds a database's manifest. False, with a designer-facing reason, for an invalid name, a pack file that is
        /// not a plain .json name or is listed twice, a pack without levels, a level without an ID, or an ID listed twice.
        /// A database may have no packs.
        /// </summary>
        public static bool TryCreate(string name, IReadOnlyList<LevelManifestPack> packs, out LevelManifest manifest,
            out string error)
        {
            manifest = null;
            if (!LevelJsonFiles.IsValidName(name))
            {
                error = $"The database name '{name}' must be 1 to {LevelJsonFiles.MaxNameLength} letters, digits, "
                        + "hyphens or underscores.";
                return false;
            }

            if (packs == null)
            {
                error = "The manifest has no packs list.";
                return false;
            }

            LevelPackInfo[] infos = new LevelPackInfo[packs.Count];
            List<string> levelIds = new List<string>();
            Dictionary<string, int> indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int packIndex = 0; packIndex < packs.Count; packIndex++)
            {
                LevelManifestPack pack = packs[packIndex];
                if (!LevelJsonFiles.IsValidPackFile(pack.File))
                {
                    error = $"Pack {packIndex + 1} has the file name '{pack.File}', which is not a .json file name "
                            + "without folders.";
                    return false;
                }

                if (!files.Add(pack.File))
                {
                    error = $"Pack file {pack.File} is listed twice.";
                    return false;
                }

                if (pack.LevelIds == null || pack.LevelIds.Count == 0)
                {
                    error = $"Pack {pack.File} lists no level.";
                    return false;
                }

                infos[packIndex] = new LevelPackInfo(pack.File, levelIds.Count, pack.LevelIds.Count);
                for (int index = 0; index < pack.LevelIds.Count; index++)
                {
                    string id = pack.LevelIds[index];
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        error = $"Pack {pack.File} lists a level without an ID.";
                        return false;
                    }

                    if (indexById.ContainsKey(id))
                    {
                        error = $"Level ID {id} is listed twice in the manifest.";
                        return false;
                    }

                    indexById.Add(id, levelIds.Count);
                    levelIds.Add(id);
                }
            }

            manifest = new LevelManifest(name, infos, levelIds.ToArray(), indexById);
            error = null;
            return true;
        }

        /// <summary>
        /// The nameless manifest of a single-file level JSON: one pack with every level, or none for an empty file. The
        /// levels were read and checked for unique IDs already.
        /// </summary>
        internal static LevelManifest Single(IReadOnlyList<LevelData> levels)
        {
            string[] levelIds = new string[levels.Count];
            Dictionary<string, int> indexById = new Dictionary<string, int>(levels.Count, StringComparer.Ordinal);
            for (int index = 0; index < levels.Count; index++)
            {
                levelIds[index] = levels[index].Id;
                indexById.Add(levelIds[index], index);
            }

            LevelPackInfo[] packs = levels.Count == 0
                ? Array.Empty<LevelPackInfo>()
                : new[] { new LevelPackInfo(null, 0, levels.Count) };
            return new LevelManifest(null, packs, levelIds, indexById);
        }
    }
}
