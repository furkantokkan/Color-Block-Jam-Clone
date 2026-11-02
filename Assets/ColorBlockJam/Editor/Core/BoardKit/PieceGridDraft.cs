using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// The Piece Editor's painted cells on a square grid of at most <see cref="MaxSize"/> cells per side, the widest a
    /// piece can be (ShapeKey, DEC-193), so a designer cannot paint what the library cannot save.
    /// </summary>
    [Serializable]
    public sealed class PieceGridDraft : ISerializationCallbackReceiver
    {
        public const int MaxSize = ShapeKey.MaxExtent;
        private const int k_center = MaxSize / 2;
        [SerializeField] private int m_width = MaxSize;
        [SerializeField] private int m_height = MaxSize;
        [SerializeField] private bool[] m_cells = new bool[MaxSize * MaxSize];
        public int Width => m_width;
        public int Height => m_height;
        public int MinX => -m_width / 2;
        public int MinY => -m_height / 2;
        /// <summary>The square grid's side; the Piece Editor's single Grid size slider edits it.</summary>
        public int Size => Math.Max(m_width, m_height);

        /// <summary>
        /// Resizes the area to <paramref name="size"/> x <paramref name="size"/> around (0, 0). Refuses, changing
        /// nothing, when a painted cell would fall outside the new area.
        /// </summary>
        public bool TrySetSize(int size)
        {
            if (size < 1 || size > MaxSize) return false;
            int minX = -size / 2, minY = -size / 2;
            for (int y = MinY; y < MinY + m_height; y++) for (int x = MinX; x < MinX + m_width; x++)
                if (Get(x, y) && (x < minX || x >= minX + size || y < minY || y >= minY + size)) return false;
            m_width = size; m_height = size;
            return true;
        }

        public bool Get(int x, int y) => m_cells[(y + k_center) * MaxSize + x + k_center];
        public void Set(int x, int y, bool filled) => m_cells[(y + k_center) * MaxSize + x + k_center] = filled;
        public void Clear() => Array.Clear(m_cells, 0, m_cells.Length);

        public void CopyFrom(PieceGridDraft source)
        {
            m_width = source.m_width; m_height = source.m_height;
            Array.Copy(source.m_cells, m_cells, m_cells.Length);
        }

        public bool Matches(PieceGridDraft other)
        {
            if (m_width != other.m_width || m_height != other.m_height) return false;
            for (int i = 0; i < m_cells.Length; i++) if (m_cells[i] != other.m_cells[i]) return false;
            return true;
        }

        /// <summary>Whether both drafts paint the same cells, whatever their grid size.</summary>
        public bool HasSameCells(PieceGridDraft other)
        {
            for (int i = 0; i < m_cells.Length; i++) if (m_cells[i] != other.m_cells[i]) return false;
            return true;
        }

        public void ReadCells(List<Vector2Int> output)
        {
            output.Clear();
            for (int y = MinY; y < MinY + m_height; y++) for (int x = MinX; x < MinX + m_width; x++)
                if (Get(x, y)) output.Add(new Vector2Int(x, y));
        }

        /// <summary>Shows <paramref name="cells"/> centred on the whole grid; a footprint wider than the grid throws.</summary>
        public void Load(IReadOnlyList<Vector2Int> cells)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int i = 0; i < cells.Count; i++)
            {
                minX = Math.Min(minX, cells[i].x); minY = Math.Min(minY, cells[i].y);
                maxX = Math.Max(maxX, cells[i].x); maxY = Math.Max(maxY, cells[i].y);
            }
            int width = maxX - minX + 1, height = maxY - minY + 1;
            if (width > MaxSize || height > MaxSize) throw new ArgumentException($"Shape exceeds the {MaxSize}×{MaxSize} grid.");
            Clear();
            m_width = MaxSize;
            m_height = MaxSize;
            for (int i = 0; i < cells.Count; i++) Set(cells[i].x - minX - width / 2, cells[i].y - minY - height / 2, true);
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        // A draft serialized for the earlier 12 x 12 grid (an open Piece Editor, its undo state, the saved
        // LevelEditorLayout.wlt) cannot be indexed on this grid, so it restarts empty instead of throwing on the next draw.
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (m_cells != null && m_cells.Length == MaxSize * MaxSize
                && m_width >= 1 && m_width <= MaxSize && m_height >= 1 && m_height <= MaxSize)
                return;

            m_cells = new bool[MaxSize * MaxSize];
            m_width = MaxSize;
            m_height = MaxSize;
        }
    }
}
