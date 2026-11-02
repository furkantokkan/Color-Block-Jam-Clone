using System;
using System.IO;
using System.Text;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>Atomically replaces an authoring text file after checking it still matches the expected disk content.</summary>
    internal static class ShapeLibraryFile
    {
        public static void CheckCurrent(string path, string expected)
        {
            string current = File.Exists(path) ? File.ReadAllText(path) : null;
            if (current != expected) throw new IOException("Piece data changed on disk. Reload the piece data before saving: " + path);
        }

        public static void Write(string path, string expected, string next)
        {
            CheckCurrent(path, expected);
            if (expected == next) return;
            if (File.Exists(path)) BoardKitAssetFiles.ValidateWritable(path);
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporary, next, new UTF8Encoding(false));
                CheckCurrent(path, expected);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
