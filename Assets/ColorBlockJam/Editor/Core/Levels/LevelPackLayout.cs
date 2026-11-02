using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Decides which pack file holds each level when a level database is saved (DEC-196), so a save rewrites as few packs
    /// as it can: a saved level keeps its pack, a level added before or among the saved ones joins the pack of the level
    /// next to it, and levels added after the last saved one fill the last pack up to <see cref="LevelsPerPack"/> before
    /// a new pack opens. Packs are never rebalanced, so a pack can hold more than <see cref="LevelsPerPack"/> levels when
    /// levels are inserted in the middle. Pure: no file or asset access.
    /// </summary>
    public static class LevelPackLayout
    {
        /// <summary>The level count at which appended levels open a new pack.</summary>
        public const int LevelsPerPack = 100;

        private const string k_packPrefix = "pack-";
        private const string k_packExtension = ".json";
        private const int k_minDigits = 3;

        /// <summary>The file name of pack <paramref name="number"/>: pack-001.json, pack-002.json and so on.</summary>
        public static string GetPackFileName(int number)
        {
            return string.Concat(k_packPrefix, number.ToString("D3", CultureInfo.InvariantCulture), k_packExtension);
        }

        /// <summary>True for a pack-NNN.json name (three or more digits, above zero), with its number.</summary>
        public static bool TryGetPackNumber(string file, out int number)
        {
            number = 0;
            if (file == null
                || !file.StartsWith(k_packPrefix, StringComparison.Ordinal)
                || !file.EndsWith(k_packExtension, StringComparison.Ordinal))
            {
                return false;
            }

            int digitCount = file.Length - k_packPrefix.Length - k_packExtension.Length;
            if (digitCount < k_minDigits)
            {
                return false;
            }

            for (int index = 0; index < digitCount; index++)
            {
                if (!char.IsDigit(file[k_packPrefix.Length + index]))
                {
                    return false;
                }
            }

            return int.TryParse(file.Substring(k_packPrefix.Length, digitCount), NumberStyles.None,
                       CultureInfo.InvariantCulture, out number)
                   && number > 0;
        }

        /// <summary>
        /// Plans the manifest of <paramref name="levelIds"/>, the database's levels in play order, from its
        /// <paramref name="current"/> manifest (null for a new database). A new pack is named after the highest pack
        /// number of the current manifest. False, with the reason, for an invalid name or an ID listed twice.
        /// </summary>
        public static bool TryPlan(string name, LevelManifest current, IReadOnlyList<string> levelIds,
            out LevelManifest planned, out string error)
        {
            if (levelIds == null)
            {
                throw new ArgumentNullException(nameof(levelIds));
            }

            Dictionary<string, int> oldPackOf = new Dictionary<string, int>(StringComparer.Ordinal);
            int highestNumber = 0;
            for (int packIndex = 0; current != null && packIndex < current.PackCount; packIndex++)
            {
                LevelPackInfo pack = current.GetPackAt(packIndex);
                if (TryGetPackNumber(pack.File, out int number))
                {
                    highestNumber = Math.Max(highestNumber, number);
                }

                for (int index = 0; index < pack.LevelCount; index++)
                {
                    oldPackOf[current.GetLevelIdAt(pack.FirstLevelIndex + index)] = packIndex;
                }
            }

            int lastSaved = -1;
            for (int position = 0; position < levelIds.Count; position++)
            {
                if (levelIds[position] != null && oldPackOf.ContainsKey(levelIds[position]))
                {
                    lastSaved = position;
                }
            }

            List<string> files = new List<string>();
            List<List<string>> packs = new List<List<string>>();
            List<string> leading = new List<string>();
            List<string> open = null;
            int openOldPack = -1;
            for (int position = 0; position < levelIds.Count; position++)
            {
                string id = levelIds[position];
                if (id != null && oldPackOf.TryGetValue(id, out int oldPack))
                {
                    // A saved level keeps its pack. The cursor only moves forward, so each old pack opens once and packs
                    // stay contiguous; a saved level found behind the cursor (a reorder) joins the open pack instead.
                    if (open == null || (openOldPack >= 0 && oldPack > openOldPack))
                    {
                        open = Open(files, packs, current.GetPackAt(oldPack).File);
                        openOldPack = oldPack;
                        open.AddRange(leading);
                        leading.Clear();
                    }

                    open.Add(id);
                }
                else if (position > lastSaved)
                {
                    // After the last saved level (or in a database with none): fill the last pack, then open new ones.
                    if (open == null || open.Count >= LevelsPerPack)
                    {
                        highestNumber++;
                        open = Open(files, packs, GetPackFileName(highestNumber));
                        openOldPack = -1;
                    }

                    open.Add(id);
                }
                else if (open == null)
                {
                    // Before the first saved level: it joins that level's pack.
                    leading.Add(id);
                }
                else
                {
                    // Among the saved levels: it joins the pack of the level before it.
                    open.Add(id);
                }
            }

            LevelManifestPack[] manifestPacks = new LevelManifestPack[packs.Count];
            for (int packIndex = 0; packIndex < manifestPacks.Length; packIndex++)
            {
                manifestPacks[packIndex] = new LevelManifestPack(files[packIndex], packs[packIndex]);
            }

            return LevelManifest.TryCreate(name, manifestPacks, out planned, out error);
        }

        private static List<string> Open(List<string> files, List<List<string>> packs, string file)
        {
            List<string> levelIds = new List<string>();
            files.Add(file);
            packs.Add(levelIds);
            return levelIds;
        }
    }
}
