using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Draws a block footprint in the BlockPiece Inspector, laid out like the Level Editor's piece tiles with row 0 at
    /// the bottom: every cell of the footprint's bounding box, the footprint's own cells filled, and the block origin
    /// (local 0, 0) outlined when it lies inside the box. It draws its own copy of the cells, so a piece that changes
    /// later is redrawn only through SetCells.
    /// </summary>
    internal sealed class BlockPieceShapeGrid : VisualElement
    {
        public const float Size = 96f;

        private const float k_maxCellSize = 32f;
        private const float k_originLineWidth = 2f;
        private static readonly Color s_filledCell = new Color(0.85f, 0.87f, 0.9f);
        private static readonly Color s_emptyCellDark = new Color(0.3f, 0.3f, 0.3f);
        private static readonly Color s_emptyCellLight = new Color(0.62f, 0.62f, 0.62f);
        private static readonly Color s_origin = new Color(0.25f, 0.5f, 1f);

        private readonly List<Vector2Int> m_cells = new List<Vector2Int>();

        public BlockPieceShapeGrid()
        {
            style.width = Size;
            style.height = Size;
            // Owner: this element. Stop point: it is dropped with the Inspector's root.
            generateVisualContent += Draw;
        }

        /// <summary>The cells the grid draws, in the order SetCells received them.</summary>
        public IReadOnlyList<Vector2Int> Cells => m_cells;

        /// <summary>Copies the cells and repaints; returns false, and keeps the drawing, when they are unchanged.</summary>
        public bool SetCells(IReadOnlyList<Vector2Int> cells)
        {
            if (IsSame(cells))
            {
                return false;
            }

            m_cells.Clear();
            for (int index = 0; index < cells.Count; index++)
            {
                m_cells.Add(cells[index]);
            }

            MarkDirtyRepaint();
            return true;
        }

        private bool IsSame(IReadOnlyList<Vector2Int> cells)
        {
            if (cells.Count != m_cells.Count)
            {
                return false;
            }

            for (int index = 0; index < cells.Count; index++)
            {
                if (cells[index] != m_cells[index])
                {
                    return false;
                }
            }

            return true;
        }

        private void Draw(MeshGenerationContext context)
        {
            if (m_cells.Count == 0)
            {
                return;
            }

            RectInt bounds = LevelShapeThumbnail.GetBounds(m_cells);
            float cellSize = Mathf.Min(k_maxCellSize,
                LevelShapeThumbnail.GetCellSize(Size, Mathf.Max(bounds.width, bounds.height)));
            Painter2D painter = context.painter2D;
            painter.fillColor = EditorGUIUtility.isProSkin ? s_emptyCellDark : s_emptyCellLight;
            for (int y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (int x = bounds.xMin; x < bounds.xMax; x++)
                {
                    FillCell(painter, LevelShapeThumbnail.GetCellRect(bounds, new Vector2Int(x, y), Size, cellSize));
                }
            }

            painter.fillColor = s_filledCell;
            for (int index = 0; index < m_cells.Count; index++)
            {
                FillCell(painter, LevelShapeThumbnail.GetCellRect(bounds, m_cells[index], Size, cellSize));
            }

            if (bounds.Contains(Vector2Int.zero))
            {
                OutlineCell(painter, LevelShapeThumbnail.GetCellRect(bounds, Vector2Int.zero, Size, cellSize));
            }
        }

        // A one-pixel gap between cells keeps the footprint readable, as on the Level Editor's tiles.
        private static void FillCell(Painter2D painter, Rect rect)
        {
            float left = rect.x + 0.5f;
            float top = rect.y + 0.5f;
            float size = rect.width - 1f;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, top));
            painter.LineTo(new Vector2(left + size, top));
            painter.LineTo(new Vector2(left + size, top + size));
            painter.LineTo(new Vector2(left, top + size));
            painter.ClosePath();
            painter.Fill();
        }

        private static void OutlineCell(Painter2D painter, Rect rect)
        {
            // The stroke is centred on the path, so an inset of half its width keeps it inside the cell.
            float inset = 0.5f + k_originLineWidth * 0.5f;
            float left = rect.x + inset;
            float top = rect.y + inset;
            float size = rect.width - 2f * inset;
            painter.strokeColor = s_origin;
            painter.lineWidth = k_originLineWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(left, top));
            painter.LineTo(new Vector2(left + size, top));
            painter.LineTo(new Vector2(left + size, top + size));
            painter.LineTo(new Vector2(left, top + size));
            painter.ClosePath();
            painter.Stroke();
        }
    }
}
