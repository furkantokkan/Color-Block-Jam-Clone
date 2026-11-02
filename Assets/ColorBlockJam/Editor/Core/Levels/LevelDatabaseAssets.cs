using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Saves a level database from the editor side (DEC-196): the file save of <see cref="LevelDatabaseFiles"/> with
    /// Unity deleting the packs the manifest no longer lists, then imports of the written files and the Addressables
    /// sync. Save Level, the Piece Editor's template delete and the CBJ import all save through it, so files, asset
    /// database and Level Database group stay in step.
    /// </summary>
    internal static class LevelDatabaseAssets
    {
        /// <summary>The folder that keeps one previous version of the database with this manifest GUID.</summary>
        internal static string GetBackupFolder(string manifestGuid)
        {
            return Path.Combine("Library", "ColorBlockJam", "LevelBackups", manifestGuid);
        }

        /// <summary>
        /// Saves <paramref name="levels"/> over the files read into <paramref name="expected"/>. True when the save was
        /// written. A pack that could not be deleted or a failed Addressables sync only fills <paramref name="note"/>:
        /// the levels are saved, and the next save or Configure Level Databases repairs the rest.
        /// </summary>
        internal static bool TrySave(LevelDatabaseSnapshot expected, IReadOnlyList<LevelData> levels, string backupFolder,
            out LevelDatabaseSnapshot saved, out string note, out string error)
        {
            note = null;
            // A project database's files are assets: Unity deletes them with their .meta and their group entries.
            bool isAsset = LevelJsonFiles.IsManifest(expected.ManifestPath);
            Func<string, bool> deleteFile = isAsset ? AssetDatabase.DeleteAsset : (Func<string, bool>)null;
            if (!LevelDatabaseFiles.TrySave(expected, levels, backupFolder, deleteFile, out saved,
                    out LevelPackWriteReport report, out error))
            {
                return false;
            }

            if (!isAsset)
            {
                return true;
            }

            Import(report.Created);
            Import(report.Written);
            if (report.ManifestWritten)
            {
                AssetDatabase.ImportAsset(expected.ManifestPath, ImportAssetOptions.ForceUpdate);
            }

            if (report.Leftovers.Count > 0)
            {
                note = $"Could not delete {string.Join(", ", report.Leftovers)}; the next save removes them.";
            }

            if (!LevelDatabaseAddressables.TrySync(expected.ManifestPath, out string syncError))
            {
                string message = $"The Level Database group was not updated: {syncError} Run Color Block Jam > Data > "
                                 + "Configure Level Databases.";
                note = note == null ? message : note + " " + message;
            }

            return true;
        }

        private static void Import(IReadOnlyList<string> paths)
        {
            for (int index = 0; index < paths.Count; index++)
            {
                AssetDatabase.ImportAsset(paths[index], ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
