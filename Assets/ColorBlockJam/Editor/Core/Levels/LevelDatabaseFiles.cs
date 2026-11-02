using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One pack file of a level database as it was read or written: its path and its exact text.</summary>
    public readonly struct LevelPackFile
    {
        public LevelPackFile(string path, string json)
        {
            Path = path;
            Json = json;
        }

        public string Path { get; }
        public string Json { get; }
    }

    /// <summary>
    /// One level database as it is on disk (DEC-196): its manifest, every pack's exact text and the levels in play
    /// order. A save first compares the files with it, so a change made outside the Level Editor is never overwritten.
    /// </summary>
    public sealed class LevelDatabaseSnapshot
    {
        private readonly LevelPackFile[] m_packs;
        private readonly LevelData[] m_levels;
        private string m_contentHash;

        internal LevelDatabaseSnapshot(string manifestPath, LevelManifest manifest, string manifestJson,
            LevelPackFile[] packs, LevelData[] levels)
        {
            ManifestPath = manifestPath;
            Manifest = manifest;
            ManifestJson = manifestJson;
            m_packs = packs;
            m_levels = levels;
        }

        public string ManifestPath { get; }

        /// <summary>The database folder, which holds the manifest and every pack.</summary>
        public string Folder => LevelDatabaseFiles.GetFolder(ManifestPath);

        public LevelManifest Manifest { get; }
        public string ManifestJson { get; }

        /// <summary>The pack files in manifest order.</summary>
        public IReadOnlyList<LevelPackFile> Packs => m_packs;

        /// <summary>The database's levels in play order.</summary>
        public IReadOnlyList<LevelData> Levels => m_levels;

        /// <summary>
        /// A hash of the manifest and every pack text, computed once: the Level Editor's key for "the same saved
        /// database", across its caches and a domain reload.
        /// </summary>
        public string ContentHash
        {
            get
            {
                if (m_contentHash == null)
                {
                    Hash128 hash = Hash128.Compute(ManifestJson);
                    for (int index = 0; index < m_packs.Length; index++)
                    {
                        hash.Append(m_packs[index].Json);
                    }

                    m_contentHash = hash.ToString();
                }

                return m_contentHash;
            }
        }

        /// <summary>Loads every level into <paramref name="catalog"/>, all packs resident.</summary>
        public bool LoadInto(LevelCatalog catalog, out string error)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            return catalog.LoadLevels(Manifest, m_levels, out error);
        }
    }

    /// <summary>What one save changed on disk, as paths in the database folder.</summary>
    public sealed class LevelPackWriteReport
    {
        internal LevelPackWriteReport(IReadOnlyList<string> created, IReadOnlyList<string> written,
            IReadOnlyList<string> deleted, IReadOnlyList<string> leftovers, bool manifestWritten)
        {
            Created = created;
            Written = written;
            Deleted = deleted;
            Leftovers = leftovers;
            ManifestWritten = manifestWritten;
        }

        /// <summary>Pack files the save created.</summary>
        public IReadOnlyList<string> Created { get; }

        /// <summary>Pack files the save rewrote.</summary>
        public IReadOnlyList<string> Written { get; }

        /// <summary>Pack files the save deleted because the manifest no longer lists them.</summary>
        public IReadOnlyList<string> Deleted { get; }

        /// <summary>Unlisted pack files the save could not delete; the next save tries again.</summary>
        public IReadOnlyList<string> Leftovers { get; }

        public bool ManifestWritten { get; }
    }

    /// <summary>
    /// Reads, creates and saves one level database folder (DEC-196): a manifest and its pack files, each read all or
    /// nothing. A save copies the whole database into one backup folder, then writes only the packs whose text changes,
    /// then the manifest when its text changes (the commit point), then deletes the pack files the manifest no longer
    /// lists. The files are not replaced together: a failure part way names what was written, the next save clears
    /// leftover packs, and Restore Previous Save brings the backup back. No AssetDatabase or Addressables access; the
    /// Level Editor imports and registers the files afterwards.
    /// </summary>
    public static class LevelDatabaseFiles
    {
        /// <summary>The message of every save refused because the files differ from the snapshot.</summary>
        public const string ExternalChangeError = "Level JSON changed outside this editor. Reload before saving.";

        private static readonly UTF8Encoding s_encoding = new UTF8Encoding(false);

        /// <summary>The folder of the database whose manifest is at <paramref name="manifestPath"/>, with forward slashes.</summary>
        public static string GetFolder(string manifestPath)
        {
            return Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
        }

        /// <summary>
        /// Creates the folder <paramref name="directory"/>/<paramref name="name"/> with an empty manifest (no pack yet).
        /// A folder that already holds a readable database is kept as it is.
        /// </summary>
        public static bool TryCreate(string directory, string name, out string manifestPath, out string error)
        {
            manifestPath = null;
            if (!LevelJsonFiles.IsValidName(name))
            {
                error = "Use a database name containing only letters, digits, hyphens or underscores.";
                return false;
            }

            string path = $"{directory}/{name}/{LevelJsonFiles.ManifestFile}";
            if (File.Exists(path))
            {
                if (!TryRead(path, out _, out error))
                {
                    return false;
                }

                manifestPath = path;
                return true;
            }

            if (!LevelManifest.TryCreate(name, Array.Empty<LevelManifestPack>(), out LevelManifest manifest, out error)
                || !LevelManifestJson.TryWrite(manifest, out string json, out error))
            {
                return false;
            }

            try
            {
                Directory.CreateDirectory(GetFolder(path));
                WriteText(path, json);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not create level database {name}: {exception.Message}";
                return false;
            }

            manifestPath = path;
            error = null;
            return true;
        }

        /// <summary>
        /// Reads the manifest at <paramref name="manifestPath"/> and every pack it lists (authoring read). The folder
        /// must carry the manifest's database name, and each pack must hold exactly the levels the manifest lists for it.
        /// </summary>
        public static bool TryRead(string manifestPath, out LevelDatabaseSnapshot snapshot, out string error)
        {
            return TryRead(manifestPath, true, out snapshot, out error);
        }

        /// <summary>
        /// Reads only the manifest at <paramref name="manifestPath"/>, checking that the folder carries its name: what the
        /// Addressables registration needs, without parsing every pack.
        /// </summary>
        public static bool TryReadManifest(string manifestPath, out LevelManifest manifest, out string error)
        {
            manifest = null;
            if (!TryReadText(manifestPath, "The level database manifest", out string manifestJson, out error))
            {
                return false;
            }

            if (!LevelManifestJson.TryRead(manifestJson, out LevelManifest read, out error))
            {
                error = $"{LevelJsonFiles.ManifestFile}: {error}";
                return false;
            }

            if (!TryCheckFolderName(manifestPath, read, out error))
            {
                return false;
            }

            manifest = read;
            return true;
        }

        /// <summary>True when <paramref name="backupFolder"/> holds a previous save to restore.</summary>
        public static bool HasBackup(string backupFolder)
        {
            return !string.IsNullOrWhiteSpace(backupFolder)
                && File.Exists(Path.Combine(backupFolder, LevelJsonFiles.ManifestFile));
        }

        /// <summary>Reads the levels of the previous save kept in <paramref name="backupFolder"/>.</summary>
        public static bool TryReadBackup(string backupFolder, out LevelData[] levels, out string error)
        {
            levels = null;
            if (!HasBackup(backupFolder))
            {
                error = "There is no previous save to restore.";
                return false;
            }

            // The backup folder is named after the manifest GUID, not the database, so its name is not checked.
            if (!TryRead(Path.Combine(backupFolder, LevelJsonFiles.ManifestFile).Replace('\\', '/'), false,
                    out LevelDatabaseSnapshot snapshot, out error))
            {
                return false;
            }

            levels = new LevelData[snapshot.Levels.Count];
            for (int index = 0; index < levels.Length; index++)
            {
                levels[index] = snapshot.Levels[index];
            }

            return true;
        }

        /// <summary>
        /// Saves <paramref name="levels"/>, the database's levels in play order, over the files read into
        /// <paramref name="expected"/>. Refuses without writing when a level is invalid or listed twice, when a database
        /// file changed on disk since <paramref name="expected"/> was read, or when the backup fails.
        /// <paramref name="deleteFile"/> deletes a pack file the manifest no longer lists (null: the file and its .meta);
        /// the Level Editor passes AssetDatabase.DeleteAsset so Unity forgets the asset too.
        /// </summary>
        public static bool TrySave(LevelDatabaseSnapshot expected, IReadOnlyList<LevelData> levels, string backupFolder,
            Func<string, bool> deleteFile, out LevelDatabaseSnapshot saved, out LevelPackWriteReport report,
            out string error)
        {
            saved = null;
            report = null;
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            if (levels == null)
            {
                error = "Level data is missing.";
                return false;
            }

            string folder = expected.Folder;
            if (string.IsNullOrWhiteSpace(backupFolder) || IsSameOrInside(backupFolder, folder))
            {
                error = "A separate backup folder is required for level saves.";
                return false;
            }

            if (!TryGetIds(levels, out string[] ids, out error)
                || !LevelPackLayout.TryPlan(expected.Manifest.Name, expected.Manifest, ids, out LevelManifest planned,
                    out error)
                || !TryWriteTexts(planned, levels, out string manifestJson, out string[] packJson, out error))
            {
                return false;
            }

            if (!MatchesDisk(expected))
            {
                error = ExternalChangeError;
                return false;
            }

            Dictionary<string, string> oldText = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < expected.Packs.Count; index++)
            {
                oldText[Path.GetFileName(expected.Packs[index].Path)] = expected.Packs[index].Json;
            }

            LevelPackFile[] packs = new LevelPackFile[planned.PackCount];
            List<int> changedPacks = new List<int>();
            for (int packIndex = 0; packIndex < packs.Length; packIndex++)
            {
                string file = planned.GetPackAt(packIndex).File;
                packs[packIndex] = new LevelPackFile($"{folder}/{file}", packJson[packIndex]);
                if (!oldText.TryGetValue(file, out string previous)
                    || !string.Equals(previous, packJson[packIndex], StringComparison.Ordinal))
                {
                    changedPacks.Add(packIndex);
                }
            }

            // A save that changes no text keeps the backup, so Restore Previous Save still brings back the real previous
            // version.
            bool manifestWritten = !string.Equals(manifestJson, expected.ManifestJson, StringComparison.Ordinal);
            if ((changedPacks.Count > 0 || manifestWritten) && !TryBackup(expected, backupFolder, out error))
            {
                return false;
            }

            List<string> created = new List<string>();
            List<string> written = new List<string>();
            for (int index = 0; index < changedPacks.Count; index++)
            {
                LevelPackFile pack = packs[changedPacks[index]];
                bool existed = File.Exists(pack.Path);
                if (!TryWrite(pack.Path, pack.Json, created, written, out error))
                {
                    return false;
                }

                (existed ? written : created).Add(pack.Path);
            }

            // The manifest is the commit point: until it is written, the old manifest still names the old packs.
            if (manifestWritten && !TryWrite(expected.ManifestPath, manifestJson, created, written, out error))
            {
                return false;
            }

            DeleteUnlisted(expected, planned, deleteFile ?? DeleteFileAndMeta, out List<string> deleted,
                out List<string> leftovers);
            LevelData[] savedLevels = new LevelData[levels.Count];
            for (int index = 0; index < savedLevels.Length; index++)
            {
                savedLevels[index] = levels[index];
            }

            saved = new LevelDatabaseSnapshot(expected.ManifestPath, planned, manifestJson, packs, savedLevels);
            report = new LevelPackWriteReport(created, written, deleted, leftovers, manifestWritten);
            error = null;
            return true;
        }

        private static bool TryRead(string manifestPath, bool checkFolderName, out LevelDatabaseSnapshot snapshot,
            out string error)
        {
            snapshot = null;
            if (!TryReadText(manifestPath, "The level database manifest", out string manifestJson, out error))
            {
                return false;
            }

            if (!LevelManifestJson.TryRead(manifestJson, out LevelManifest manifest, out error))
            {
                error = $"{LevelJsonFiles.ManifestFile}: {error}";
                return false;
            }

            string folder = GetFolder(manifestPath);
            if (checkFolderName && !TryCheckFolderName(manifestPath, manifest, out error))
            {
                return false;
            }

            LevelPackFile[] packs = new LevelPackFile[manifest.PackCount];
            LevelData[] levels = new LevelData[manifest.Count];
            for (int packIndex = 0; packIndex < packs.Length; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                string path = $"{folder}/{pack.File}";
                if (!TryReadText(path, $"Level pack {pack.File}", out string json, out error))
                {
                    return false;
                }

                if (!LevelDatabaseJson.TryRead(json, out LevelData[] packLevels, out error)
                    || !manifest.TryMatchPack(packIndex, packLevels, out error))
                {
                    error = $"{pack.File}: {error}";
                    return false;
                }

                Array.Copy(packLevels, 0, levels, pack.FirstLevelIndex, packLevels.Length);
                packs[packIndex] = new LevelPackFile(path, json);
            }

            snapshot = new LevelDatabaseSnapshot(manifestPath, manifest, manifestJson, packs, levels);
            error = null;
            return true;
        }

        // The folder name is the database name, which every address is built from.
        private static bool TryCheckFolderName(string manifestPath, LevelManifest manifest, out string error)
        {
            string folderName = Path.GetFileName(GetFolder(manifestPath));
            if (!string.Equals(folderName, manifest.Name, StringComparison.Ordinal))
            {
                error = $"The manifest in folder {folderName} names the database {manifest.Name}; a database's folder "
                        + "and name must match.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryReadText(string path, string what, out string text, out string error)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = $"{what} is missing: {path}";
                return false;
            }

            try
            {
                text = File.ReadAllText(path);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not read {path}: {exception.Message}";
                return false;
            }
        }

        // Every level present and every ID once, in the words of the single-file save.
        private static bool TryGetIds(IReadOnlyList<LevelData> levels, out string[] ids, out string error)
        {
            ids = new string[levels.Count];
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < levels.Count; index++)
            {
                if (levels[index] == null)
                {
                    error = $"Level {index} is empty.";
                    return false;
                }

                ids[index] = levels[index].Id;
                if (ids[index] != null && !seen.Add(ids[index]))
                {
                    error = $"Level ID {ids[index]} appears more than once.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Validates and writes every pack (LevelDatabaseJson.TryWrite) and the manifest in memory, before any file.
        private static bool TryWriteTexts(LevelManifest planned, IReadOnlyList<LevelData> levels,
            out string manifestJson, out string[] packJson, out string error)
        {
            manifestJson = null;
            packJson = new string[planned.PackCount];
            for (int packIndex = 0; packIndex < packJson.Length; packIndex++)
            {
                LevelPackInfo pack = planned.GetPackAt(packIndex);
                LevelData[] packLevels = new LevelData[pack.LevelCount];
                for (int index = 0; index < packLevels.Length; index++)
                {
                    packLevels[index] = levels[pack.FirstLevelIndex + index];
                }

                if (!LevelDatabaseJson.TryWrite(packLevels, out packJson[packIndex], out error))
                {
                    return false;
                }
            }

            return LevelManifestJson.TryWrite(planned, out manifestJson, out error);
        }

        private static bool MatchesDisk(LevelDatabaseSnapshot expected)
        {
            if (!MatchesFile(expected.ManifestPath, expected.ManifestJson))
            {
                return false;
            }

            for (int index = 0; index < expected.Packs.Count; index++)
            {
                if (!MatchesFile(expected.Packs[index].Path, expected.Packs[index].Json))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool MatchesFile(string path, string text)
        {
            try
            {
                return File.Exists(path) && string.Equals(File.ReadAllText(path), text, StringComparison.Ordinal);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                return false;
            }
        }

        // The whole database, into a new folder that then replaces the old backup, so a failed backup keeps the last one.
        private static bool TryBackup(LevelDatabaseSnapshot expected, string backupFolder, out string error)
        {
            string staging = backupFolder.TrimEnd('/', '\\') + ".writing";
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, true);
                }

                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, LevelJsonFiles.ManifestFile), expected.ManifestJson, s_encoding);
                for (int index = 0; index < expected.Packs.Count; index++)
                {
                    File.WriteAllText(Path.Combine(staging, Path.GetFileName(expected.Packs[index].Path)),
                        expected.Packs[index].Json, s_encoding);
                }

                if (Directory.Exists(backupFolder))
                {
                    Directory.Delete(backupFolder, true);
                }

                Directory.Move(staging, backupFolder);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not back up the level database before saving, so nothing was written: {exception.Message}";
                return false;
            }
        }

        private static bool TryWrite(string path, string text, List<string> created, List<string> written,
            out string error)
        {
            try
            {
                WriteText(path, text);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                int done = created.Count + written.Count;
                error = done == 0
                    ? $"Could not save {Path.GetFileName(path)}: {exception.Message}. Nothing was written."
                    : $"Could not save {Path.GetFileName(path)}: {exception.Message}. {done} pack file(s) were written "
                      + "before it; Restore Previous Save brings the database back.";
                return false;
            }
        }

        // A temporary file in the same folder, then an atomic replace (or move for a new file), so a file is never torn.
        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            string tempPath = Path.Combine(string.IsNullOrEmpty(directory) ? "." : directory,
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tempPath, text, s_encoding);
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (IOException)
                {
                    // A leftover temporary file does not replace the source.
                }
                catch (UnauthorizedAccessException)
                {
                    // A leftover temporary file does not replace the source.
                }
            }
        }

        // The packs the old manifest listed and every pack-NNN.json in the folder, minus the ones the new manifest lists:
        // deleted packs and the leftovers of an interrupted save both go.
        private static void DeleteUnlisted(LevelDatabaseSnapshot expected, LevelManifest planned,
            Func<string, bool> deleteFile, out List<string> deleted, out List<string> leftovers)
        {
            deleted = new List<string>();
            leftovers = new List<string>();
            HashSet<string> listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int packIndex = 0; packIndex < planned.PackCount; packIndex++)
            {
                listed.Add(planned.GetPackAt(packIndex).File);
            }

            string folder = expected.Folder;
            SortedSet<string> candidates = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int index = 0; index < expected.Packs.Count; index++)
            {
                candidates.Add(Path.GetFileName(expected.Packs[index].Path));
            }

            try
            {
                foreach (string path in Directory.GetFiles(folder, "*.json"))
                {
                    string file = Path.GetFileName(path);
                    if (LevelPackLayout.TryGetPackNumber(file, out _))
                    {
                        candidates.Add(file);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // The folder cannot be listed; only the old manifest's packs are known.
            }

            foreach (string file in candidates)
            {
                string path = $"{folder}/{file}";
                if (listed.Contains(file) || !File.Exists(path))
                {
                    continue;
                }

                bool removed;
                try
                {
                    removed = deleteFile(path);
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    removed = false;
                }

                (removed ? deleted : leftovers).Add(path);
            }
        }

        private static bool DeleteFileAndMeta(string path)
        {
            File.Delete(path);
            if (File.Exists(path + ".meta"))
            {
                File.Delete(path + ".meta");
            }

            return true;
        }

        private static bool IsSameOrInside(string path, string folder)
        {
            string full = Path.GetFullPath(path).TrimEnd('\\', '/');
            string root = Path.GetFullPath(folder).TrimEnd('\\', '/');
            return string.Equals(full, root, StringComparison.OrdinalIgnoreCase)
                || full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
