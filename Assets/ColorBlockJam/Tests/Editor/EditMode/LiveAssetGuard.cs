using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// The safety guard proving a Level Editor test never writes the live piece or level files: the paths it must
    /// leave untouched, and a hash of each so a fixture can assert none changed.
    /// </summary>
    internal static class LiveAssetGuard
    {
        /// <summary>The live piece JSON, library asset, the prefab that names the active level JSON (a database switch
        /// writes it, DEC-194) and that JSON (no enum or shape prefab exists any more, DEC-193).</summary>
        internal static string[] GetLevelEditorLiveFiles()
        {
            List<string> files = new List<string>
            {
                BoardKitAssets.ShapesJsonPath, LevelDatabaseAddressables.ProjectContextPath
            };

            string levels = LevelDatabaseAddressables.GetActivePath(LevelDatabaseAddressables.LoadProjectScope());
            if (!string.IsNullOrEmpty(levels))
            {
                files.Add(levels);
            }

            return files.ToArray();
        }

        /// <summary>A hash per file, or "missing" for one that does not exist.</summary>
        internal static string[] Hash(string[] files)
        {
            string[] hashes = new string[files.Length];
            using (SHA256 sha = SHA256.Create())
            {
                for (int index = 0; index < files.Length; index++)
                {
                    hashes[index] = File.Exists(files[index])
                        ? BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(files[index])))
                        : "missing";
                }
            }

            return hashes;
        }
    }
}
