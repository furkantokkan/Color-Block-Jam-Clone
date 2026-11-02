using System;
using System.IO;
using System.Text;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Turns a single-file level JSON (formatVersion 3) into a level database folder (DEC-196): the file moves to
    /// <c>&lt;name&gt;/manifest.json</c> and keeps its GUID, so every reference to it (the project scope's active
    /// database) stays valid; its text becomes the manifest, its levels go into packs of up to
    /// <see cref="LevelPackLayout.LevelsPerPack"/>, and the Level Database group registers them. Older formats are
    /// migrated to version 3 first (Color Block Jam > Data > Migrate Level JSON to v3).
    /// </summary>
    public static class LevelPackSplit
    {
        public const string MenuPath = "Color Block Jam/Data/Split Level JSON into Packs";

        private const string k_dialogTitle = "Split Level JSON into Packs";
        private static readonly UTF8Encoding s_encoding = new UTF8Encoding(false);

        [MenuItem(MenuPath)]
        private static void SplitFromMenu()
        {
            string picked = EditorUtility.OpenFilePanel("Single-file level JSON to split", LevelJsonFiles.Folder, "json");
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            string path = FileUtil.GetProjectRelativePath(picked.Replace('\\', '/'));
            string name = Path.GetFileNameWithoutExtension(path);
            if (!EditorUtility.DisplayDialog(k_dialogTitle,
                    $"Make {Path.GetFileName(path)} the level database {name}? The file becomes {name}/"
                    + $"{LevelJsonFiles.ManifestFile} with the same GUID, and its levels move into pack files.",
                    "Split", "Cancel"))
            {
                return;
            }

            bool split = TrySplit(path, name, out string manifestPath, out string error);
            EditorUtility.DisplayDialog(k_dialogTitle, split ? $"Created the level database {manifestPath}." : error, "OK");
        }

        /// <summary>
        /// Splits the level JSON at <paramref name="jsonPath"/>, directly in <see cref="LevelJsonFiles.Folder"/>, into
        /// the database named <paramref name="name"/>. Nothing is moved or written unless every level reads and the new
        /// folder is free.
        /// </summary>
        public static bool TrySplit(string jsonPath, string name, out string manifestPath, out string error)
        {
            manifestPath = null;
            string directory = Path.GetDirectoryName(jsonPath ?? string.Empty)?.Replace('\\', '/');
            if (!LevelJsonFiles.IsLevelJson(jsonPath) || !string.Equals(directory, LevelJsonFiles.Folder, StringComparison.Ordinal))
            {
                error = $"Pick a single-file level JSON directly in {LevelJsonFiles.Folder}.";
                return false;
            }

            if (!LevelJsonFiles.IsValidName(name))
            {
                error = "Use a database name containing only letters, digits, hyphens or underscores.";
                return false;
            }

            string folder = $"{LevelJsonFiles.Folder}/{name}";
            if (Directory.Exists(folder))
            {
                error = $"A level database named {name} already exists.";
                return false;
            }

            if (!TryReadLevels(jsonPath, out LevelData[] levels, out error)
                || !TryWriteTexts(name, levels, out LevelManifest manifest, out string manifestJson, out string[] packJson,
                    out error))
            {
                return false;
            }

            BoardKitAssets.Folder(folder);
            string target = $"{folder}/{LevelJsonFiles.ManifestFile}";
            string moveError = AssetDatabase.MoveAsset(jsonPath, target);
            if (!string.IsNullOrEmpty(moveError))
            {
                AssetDatabase.DeleteAsset(folder);
                error = $"Could not move {jsonPath}: {moveError}";
                return false;
            }

            try
            {
                for (int packIndex = 0; packIndex < packJson.Length; packIndex++)
                {
                    File.WriteAllText($"{folder}/{manifest.GetPackAt(packIndex).File}", packJson[packIndex], s_encoding);
                }

                File.WriteAllText(target, manifestJson, s_encoding);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Moved {jsonPath} to {target} but could not write the packs: {exception.Message}";
                return false;
            }

            for (int packIndex = 0; packIndex < packJson.Length; packIndex++)
            {
                AssetDatabase.ImportAsset($"{folder}/{manifest.GetPackAt(packIndex).File}", ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
            if (!LevelDatabaseAddressables.TrySync(target, out error))
            {
                return false;
            }

            manifestPath = target;
            return true;
        }

        private static bool TryReadLevels(string jsonPath, out LevelData[] levels, out string error)
        {
            levels = null;
            string json;
            try
            {
                json = File.ReadAllText(jsonPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not read {jsonPath}: {exception.Message}";
                return false;
            }

            if (!LevelDatabaseJson.TryRead(json, out levels, out error))
            {
                error = $"{Path.GetFileName(jsonPath)}: {error}";
                return false;
            }

            return true;
        }

        private static bool TryWriteTexts(string name, LevelData[] levels, out LevelManifest manifest,
            out string manifestJson, out string[] packJson, out string error)
        {
            manifestJson = null;
            packJson = null;
            string[] ids = new string[levels.Length];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = levels[index].Id;
            }

            if (!LevelPackLayout.TryPlan(name, null, ids, out manifest, out error)
                || !LevelManifestJson.TryWrite(manifest, out manifestJson, out error))
            {
                return false;
            }

            packJson = new string[manifest.PackCount];
            for (int packIndex = 0; packIndex < packJson.Length; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                LevelData[] packLevels = new LevelData[pack.LevelCount];
                Array.Copy(levels, pack.FirstLevelIndex, packLevels, 0, pack.LevelCount);
                if (!LevelDatabaseJson.TryWrite(packLevels, out packJson[packIndex], out error))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
