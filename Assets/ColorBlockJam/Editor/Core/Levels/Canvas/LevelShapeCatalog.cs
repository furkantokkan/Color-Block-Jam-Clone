using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One piece the Level Editor can place: its cells, the ShapeKey they form (the piece's identity, DEC-193), the name
    /// designers see and, for a piece-library template, the key the library holds for it.
    /// </summary>
    internal sealed class LevelShapeChoice
    {
        private readonly Vector2Int[] m_cells;

        /// <summary>A level piece: its name and cells; the key comes from the cells.</summary>
        public LevelShapeChoice(string displayName, IReadOnlyList<Vector2Int> cells)
            : this(displayName, cells, default)
        {
        }

        /// <summary>
        /// A template when <paramref name="templateKey"/> is valid: the key the piece library holds for it, which is
        /// the choice's own key unless a Pen template change gives it a pending footprint (DEC-125).
        /// </summary>
        public LevelShapeChoice(string displayName, IReadOnlyList<Vector2Int> cells, ShapeKey templateKey)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            m_cells = LevelCells.Copy(cells);
            Key = ShapeKey.TryCreate(m_cells, out ShapeKey key) ? key : default;
            TemplateKey = templateKey;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Key.ToString() : displayName;
        }

        /// <summary>The piece's identity: the ShapeKey of its cells, the default key when they break a piece limit.</summary>
        public ShapeKey Key { get; }

        /// <summary>The key the piece library holds for this template; the default key for a level piece.</summary>
        public ShapeKey TemplateKey { get; }

        /// <summary>True for a piece-library template, with or without a pending footprint.</summary>
        public bool IsTemplate => TemplateKey.IsValid;

        public string DisplayName { get; }
        public IReadOnlyList<Vector2Int> Cells => m_cells;

        /// <summary>True when the other choice lists the same cells in the same order.</summary>
        public bool HasSameCells(LevelShapeChoice other)
        {
            if (other == null || other.m_cells.Length != m_cells.Length)
            {
                return false;
            }

            for (int index = 0; index < m_cells.Length; index++)
            {
                if (other.m_cells[index] != m_cells[index])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The Level Editor's read of the piece library's templates, taken again whenever the library's loaded JSON
    /// changes. The library may be missing or hold invalid JSON while the Piece Editor edits it; then the catalog is
    /// empty and carries the error message, so the Blocks group disables itself instead of breaking the window
    /// (docs/Level-Editor-Design.md section 2).
    /// </summary>
    internal sealed class LevelShapeCatalog
    {
        public static readonly LevelShapeCatalog Missing =
            new LevelShapeCatalog(Array.Empty<LevelShapeChoice>(), "The piece library asset is missing.");

        private readonly LevelShapeChoice[] m_shapes;

        private LevelShapeCatalog(LevelShapeChoice[] shapes, string error)
        {
            m_shapes = shapes;
            Error = error;
        }

        /// <summary>Null when the library loaded; otherwise a designer message.</summary>
        public string Error { get; }
        public int Count => m_shapes.Length;

        public static LevelShapeCatalog Read(BlockShapeLibrary library)
        {
            if (library == null)
            {
                return Missing;
            }

            IReadOnlyList<BlockShapeLibrary.Shape> shapes;
            try
            {
                shapes = library.Shapes;
            }
            catch (Exception exception)
            {
                // Any parse or identity failure of the library JSON; the Piece Editor owns fixing it.
                return new LevelShapeCatalog(Array.Empty<LevelShapeChoice>(),
                    $"The piece library could not be read: {exception.Message}");
            }

            LevelShapeChoice[] choices = new LevelShapeChoice[shapes.Count];
            for (int index = 0; index < choices.Length; index++)
            {
                BlockShapeLibrary.Shape shape = shapes[index];
                choices[index] = new LevelShapeChoice(shape.DisplayName, shape.Cells, shape.Key);
            }

            return new LevelShapeCatalog(choices, null);
        }

        /// <summary>
        /// A catalog from ready choices with no load error: derived catalogs (pending template overlays, a save plan's
        /// unedited templates) and tests.
        /// </summary>
        internal static LevelShapeCatalog Create(LevelShapeChoice[] shapes)
        {
            return new LevelShapeCatalog(shapes, null);
        }

        public LevelShapeChoice Get(int index)
        {
            return m_shapes[index];
        }

        /// <summary>The template whose cells form this key, or null.</summary>
        public LevelShapeChoice Find(ShapeKey key)
        {
            int index = IndexOf(key);
            return index >= 0 ? m_shapes[index] : null;
        }

        /// <summary>The position of the template whose cells form this key; -1 for none or the default key.</summary>
        public int IndexOf(ShapeKey key)
        {
            for (int index = 0; key.IsValid && index < m_shapes.Length; index++)
            {
                if (m_shapes[index].Key == key)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>The template with this display name, ignoring case, or null.</summary>
        public LevelShapeChoice FindByName(string displayName)
        {
            int index = IndexOfName(displayName);
            return index >= 0 ? m_shapes[index] : null;
        }

        /// <summary>
        /// The position of the template a pending change names (DEC-125): the one the library holds under
        /// <paramref name="templateKey"/>, else the one named <paramref name="displayName"/>, because a Save Level that
        /// wrote the change gave the record new cells, so a new key, while its name stayed. -1 when neither exists.
        /// </summary>
        public int IndexOfTemplate(ShapeKey templateKey, string displayName)
        {
            for (int index = 0; templateKey.IsValid && index < m_shapes.Length; index++)
            {
                if (m_shapes[index].TemplateKey == templateKey)
                {
                    return index;
                }
            }

            return IndexOfName(displayName);
        }

        private int IndexOfName(string displayName)
        {
            for (int index = 0; !string.IsNullOrWhiteSpace(displayName) && index < m_shapes.Length; index++)
            {
                if (string.Equals(m_shapes[index].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
