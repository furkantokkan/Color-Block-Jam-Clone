using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// The editor-only piece library (DEC-193): the templates the Piece Editor writes, each a unique name and cells
    /// whose ShapeKey is its identity, loaded from one JSON file; the game never reads it, because every level block
    /// carries its own cells. The project's library is the fixed <see cref="BoardKitAssets.ShapesJsonPath"/> with no
    /// wrapper asset (DEC-195); a library bound to no file is a transient one that never writes.
    /// </summary>
    public sealed class BlockShapeLibrary : ScriptableObject
    {
        /// <summary>
        /// The longest template name: a level piece's name limit, because a deleted template's blocks keep its name as
        /// level pieces (DEC-193).
        /// </summary>
        public const int MaxNameLength = LevelPieceName.MaxNameLength;

        /// <summary>One template: its name and cells, and the ShapeKey derived from those cells, which nothing stores.</summary>
        public sealed class Shape
        {
            private readonly ReadOnlyCollection<Vector2Int> m_cells;

            /// <summary>
            /// Copies the cells and derives the key. Throws ArgumentException for a blank or overlong name, or for cells
            /// that are not a piece (ShapeKey limits: at most 25 edge-connected cells inside 5 x 5).
            /// </summary>
            public Shape(string displayName, IReadOnlyList<Vector2Int> cells)
            {
                if (cells == null)
                    throw new ArgumentNullException(nameof(cells));
                if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > MaxNameLength)
                    throw new ArgumentException($"Every piece needs a name of 1 to {MaxNameLength} characters.");
                if (!ShapeKey.TryCreate(cells, out ShapeKey key))
                {
                    ShapeKey.TryValidate(cells, out string issue);
                    throw new ArgumentException($"Piece {displayName}: {issue}.");
                }

                Key = key;
                DisplayName = displayName;
                var copy = new Vector2Int[cells.Count];
                for (int i = 0; i < copy.Length; i++)
                    copy[i] = cells[i];
                m_cells = Array.AsReadOnly(copy);
            }

            /// <summary>The template's identity: the ShapeKey of its cells (DEC-193).</summary>
            public ShapeKey Key { get; }

            /// <summary>The name designers see; unique in its library, ignoring case.</summary>
            public string DisplayName { get; }

            /// <summary>The template's cells in the orientation the Level Editor stamps, starting at (0, 0).</summary>
            public IReadOnlyList<Vector2Int> Cells => m_cells;
        }

        private static BlockShapeLibrary s_project;

        // Serialized so the project's hidden instance keeps its file across a domain reload (see LoadProject).
        [SerializeField] private string m_jsonPath;
        [NonSerialized] private ReadOnlyCollection<Shape> m_shapes;
        [NonSerialized] private string m_loadedJson;

        /// <summary>The JSON file this library reads and writes, or null for a transient library.</summary>
        public string JsonPath => m_jsonPath;
        public string LoadedJson => m_loadedJson;

        /// <summary>
        /// The project's piece library, bound to <see cref="BoardKitAssets.ShapesJsonPath"/>. It is one hidden,
        /// never-saved instance that outlives a domain reload, so the library's Undo states keep pointing at it.
        /// </summary>
        public static BlockShapeLibrary LoadProject()
        {
            if (s_project != null)
                return s_project;

            foreach (BlockShapeLibrary library in Resources.FindObjectsOfTypeAll<BlockShapeLibrary>())
            {
                if (string.Equals(library.m_jsonPath, BoardKitAssets.ShapesJsonPath, StringComparison.Ordinal))
                    return s_project = library;
            }

            s_project = CreateInstance<BlockShapeLibrary>();
            s_project.hideFlags = HideFlags.HideAndDontSave;
            s_project.name = Path.GetFileNameWithoutExtension(BoardKitAssets.ShapesJsonPath);
            s_project.m_jsonPath = BoardKitAssets.ShapesJsonPath;
            return s_project;
        }
        public IReadOnlyList<Shape> Shapes
        {
            get
            {
                LoadIfNeeded();
                return m_shapes;
            }
        }

        public void Load()
        {
            if (m_jsonPath == null || !File.Exists(m_jsonPath))
            {
                ClearCache();
                throw new InvalidOperationException($"The piece library JSON is missing: {m_jsonPath}");
            }
            Load(File.ReadAllText(m_jsonPath));
        }

        // Explicit cache loading also supports transient Editor libraries. It never writes assets.
        public void Load(string json)
        {
            ClearCache();
            m_shapes = Array.AsReadOnly(BlockShapeLibraryJson.Read(json));
            m_loadedJson = json;
        }

        /// <summary>The index of the template whose key is <paramref name="key"/>, or -1 when the library has none.</summary>
        public int IndexOf(ShapeKey key)
        {
            LoadIfNeeded();
            for (int i = 0; key.IsValid && i < m_shapes.Count; i++)
            {
                if (m_shapes[i].Key == key)
                    return i;
            }
            return -1;
        }

        /// <summary>The template whose key is <paramref name="key"/>; false when the library has none.</summary>
        public bool TryGet(ShapeKey key, out Shape shape)
        {
            int index = IndexOf(key);
            shape = index >= 0 ? m_shapes[index] : null;
            return shape != null;
        }

        private void LoadIfNeeded()
        {
            if (m_shapes == null)
                Load();
        }

        private void ClearCache()
        {
            m_shapes = null;
            m_loadedJson = null;
        }

        private void OnEnable() => ClearCache();
    }
}
