using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Board;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Edits the saved piece library, the editor-only list of templates that only the Piece Editor writes (DEC-193).
    /// Every change goes through one transaction that writes the JSON and the library asset together; no change
    /// imports a script or reloads the domain.
    /// </summary>
    public static class ShapeLibraryAuthoring
    {
        /// <summary>
        /// Editor-tool event raised once after every successful <see cref="Commit"/>, library undo and redo included,
        /// so open editor windows can re-read the library. Each subscriber names its stop point and unsubscribes there.
        /// </summary>
        internal static event Action LibraryCommitted;

        public static void Load(BlockShapeLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            if (library.LoadedJson != null)
                return;

            if (library.JsonPath == null)
                library.Load(BlockShapeLibraryJson.Write(Array.Empty<BlockShapeLibrary.Shape>()));
            else
                library.Load();
        }

        /// <summary>
        /// Saves the Piece Editor's draft in one <see cref="Commit"/> checked against <paramref name="expectedDisk"/>,
        /// so a JSON file changed on disk since the window read it is refused. A valid <paramref name="existingKey"/>
        /// replaces that template's name and cells, so its key follows the new cells; the default key adds a new
        /// template. A footprint another template already has, turned or mirrored, is refused ("already the template
        /// {name}"), as is another template's name. Returns the saved template's index.
        /// </summary>
        internal static int SavePiece(BlockShapeLibrary library, ShapeKey existingKey, string displayName,
            IReadOnlyList<Vector2Int> cells, string expectedDisk)
        {
            Load(library);
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Enter a piece name.");
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("Paint at least one cell before saving.");

            var shapes = new List<BlockShapeLibrary.Shape>(library.Shapes);
            var record = new BlockShapeLibrary.Shape(displayName, Normalize(cells));
            int index = existingKey.IsValid ? IndexOf(shapes, existingKey) : shapes.Count;
            if (index < 0)
                throw new ArgumentException("The selected piece is no longer in the piece library.");

            for (int i = 0; i < shapes.Count; i++)
            {
                if (i == index)
                    continue;
                if (shapes[i].Key == record.Key)
                    throw new ArgumentException(
                        $"This shape is already the template {shapes[i].DisplayName}, counting turns and mirrors.");
                if (string.Equals(shapes[i].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"Another piece is already named {shapes[i].DisplayName}.");
            }

            if (index < shapes.Count)
                shapes[index] = record;
            else
                shapes.Add(record);

            Commit(library, shapes, "Save piece", expectedDisk: expectedDisk);
            return index;
        }

        private static int IndexOf(IReadOnlyList<BlockShapeLibrary.Shape> shapes, ShapeKey key)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].Key == key)
                    return i;
            }
            return -1;
        }

        public static void Delete(BlockShapeLibrary library, int index)
        {
            Load(library);
            var shapes = new List<BlockShapeLibrary.Shape>(library.Shapes);
            if (index < 0 || index >= shapes.Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            shapes.RemoveAt(index);
            Commit(library, shapes, "Delete piece");
        }

        public static void Import(BlockShapeLibrary library, string json, bool merge)
        {
            Load(library);
            var incoming = BlockShapeLibraryJson.Read(json);
            var shapes = merge ? BlockShapeLibraryJson.Merge(library.Shapes, incoming) : incoming;
            Commit(library, shapes, merge ? "Merge piece JSON" : "Import piece JSON");
        }

        public static void Export(BlockShapeLibrary library, string path)
        {
            Load(library);
            if (!string.Equals(Path.GetExtension(path), ".json", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Choose a .json export file.");

            // Export cannot replace project assets; those writes need the import transaction.
            string exportPath = Path.GetFullPath(path);
            string assetsFolder = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (exportPath.StartsWith(assetsFolder, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Export outside Assets. Use Save or Import to change project data.");

            if (library.JsonPath != null)
                ShapeLibraryFile.CheckCurrent(GetJsonPath(library), library.LoadedJson);

            string previousExport = File.Exists(exportPath) ? File.ReadAllText(exportPath) : null;
            ShapeLibraryFile.Write(exportPath, previousExport, library.LoadedJson);
        }

        public static void Reload(BlockShapeLibrary library)
        {
            string diskJson = File.ReadAllText(GetJsonPath(library));
            var shapes = BlockShapeLibraryJson.Read(diskJson);
            Commit(library, shapes, "Reload piece JSON", expectedDisk: diskJson);
        }

        public static void ClearHistory(BlockShapeLibrary library) => ShapeLibraryUndoState.Clear(library);

        internal static void Commit(BlockShapeLibrary library, IReadOnlyList<BlockShapeLibrary.Shape> shapes,
            string label, bool recordUndo = true, string expectedDisk = null)
        {
            Load(library);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before changing piece assets.");

            var transaction = new ShapeLibraryTransaction(library, shapes, expectedDisk);
            transaction.Save(label, recordUndo);
            RaiseLibraryCommitted();
        }

        private static void RaiseLibraryCommitted()
        {
            Action listeners = LibraryCommitted;
            if (listeners == null)
                return;

            // The library is already written, so a failing listener must not look like a failed commit or stop the
            // other windows from refreshing.
            foreach (Action listener in listeners.GetInvocationList())
            {
                try { listener(); }
                catch (Exception error) { Debug.LogException(error); }
            }
        }

        /// <summary>Returns a copy of <paramref name="cells"/> moved so the lowest x and y are 0.</summary>
        internal static Vector2Int[] Normalize(IReadOnlyList<Vector2Int> cells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var cell in cells)
            {
                minX = Math.Min(minX, cell.x);
                minY = Math.Min(minY, cell.y);
            }

            var result = new Vector2Int[cells.Count];
            var offset = new Vector2Int(minX, minY);
            for (int i = 0; i < cells.Count; i++)
                result[i] = cells[i] - offset;
            return result;
        }

        internal static string GetJsonPath(BlockShapeLibrary library)
        {
            if (library == null)
                throw new ArgumentNullException(nameof(library));

            string path = library.JsonPath;
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("This piece library is bound to no JSON file.");
            return path;
        }
    }
}
