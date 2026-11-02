using System;
using System.IO;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The naming rules of authored level JSON, shared by the Level Editor, <see cref="LevelJsonReference"/> and the
    /// game: a level database is a folder under <see cref="Folder"/> holding one manifest and its pack files, and every
    /// file's Addressables address follows from the database name and the file name, so no JSON stores an address
    /// (DEC-194, DEC-196).
    /// </summary>
    public static class LevelJsonFiles
    {
        /// <summary>The folder of the authored level JSON files, which the Level Database Addressables group holds.</summary>
        public const string Folder = "Assets/ColorBlockJam/Data/Json/Levels";

        /// <summary>The file name of every level database's manifest.</summary>
        public const string ManifestFile = "manifest.json";

        /// <summary>The start of every level JSON address in the Level Database group.</summary>
        public const string AddressPrefix = "LevelDatabase/";

        /// <summary>
        /// The Addressables label of every manifest and pack, so a published content update downloads them in one call
        /// before any of them is read (DEC-162, DEC-196).
        /// </summary>
        public const string Label = "level-json";

        /// <summary>The longest database name (also its folder name) and the longest pack file name.</summary>
        public const int MaxNameLength = 64;

        private const string k_jsonExtension = ".json";

        /// <summary>True for a .json file under <see cref="Folder"/>, subfolders included.</summary>
        public static bool IsLevelJson(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.StartsWith(Folder + "/", StringComparison.Ordinal)
                && string.Equals(Path.GetExtension(path), k_jsonExtension, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>True for the manifest of a level database: <c>Folder/&lt;name&gt;/manifest.json</c>.</summary>
        public static bool IsManifest(string path)
        {
            if (!IsLevelJson(path))
            {
                return false;
            }

            string relative = path.Substring(Folder.Length + 1);
            int slash = relative.IndexOf('/');
            return slash > 0
                && IsValidName(relative.Substring(0, slash))
                && string.Equals(relative.Substring(slash + 1), ManifestFile, StringComparison.Ordinal);
        }

        /// <summary>The asset path of the manifest of the database named <paramref name="name"/>.</summary>
        public static string GetManifestPath(string name)
        {
            return $"{Folder}/{name}/{ManifestFile}";
        }

        /// <summary>The Addressables address of <paramref name="file"/> in the database named <paramref name="name"/>.</summary>
        public static string GetAddress(string name, string file)
        {
            return string.Concat(AddressPrefix, name, "/", file);
        }

        /// <summary>
        /// True for a database name: 1 to <see cref="MaxNameLength"/> letters, digits, hyphens or underscores. The name is
        /// also the database's folder and the middle of its addresses.
        /// </summary>
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
            {
                return false;
            }

            foreach (char character in name)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_')
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// True for a pack file name: a .json name of letters, digits, hyphens, underscores and dots, without folders,
        /// that is not the manifest and does not start with a dot.
        /// </summary>
        public static bool IsValidPackFile(string file)
        {
            if (string.IsNullOrEmpty(file) || file.Length > MaxNameLength
                || file.Length == k_jsonExtension.Length
                || !file.EndsWith(k_jsonExtension, StringComparison.OrdinalIgnoreCase)
                || string.Equals(file, ManifestFile, StringComparison.OrdinalIgnoreCase)
                || file[0] == '.')
            {
                return false;
            }

            foreach (char character in file)
            {
                if (!char.IsLetterOrDigit(character) && character != '-' && character != '_' && character != '.')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
