using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Lays a piece's cells out in a square thumbnail: centred on the piece's bounding box, row 0 at the bottom, at
    /// one cell size shared by every tile of a catalog so relative piece sizes stay comparable.
    /// </summary>
    internal static class LevelShapeThumbnail
    {
        /// <summary>The largest bounding-box side over the catalog's shapes, at least 1.</summary>
        public static int GetExtent(LevelShapeCatalog catalog)
        {
            int extent = 1;
            for (int index = 0; catalog != null && index < catalog.Count; index++)
            {
                RectInt bounds = GetBounds(catalog.Get(index).Cells);
                extent = Mathf.Max(extent, Mathf.Max(bounds.width, bounds.height));
            }

            return extent;
        }

        public static float GetCellSize(float gridSize, int extent)
        {
            return Mathf.Floor(gridSize / Mathf.Max(1, extent));
        }

        /// <summary>The cells' bounding box; an empty list gives an empty box at (0, 0).</summary>
        public static RectInt GetBounds(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return new RectInt(0, 0, 0, 0);
            }

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int index = 0; index < cells.Count; index++)
            {
                minX = Mathf.Min(minX, cells[index].x);
                minY = Mathf.Min(minY, cells[index].y);
                maxX = Mathf.Max(maxX, cells[index].x);
                maxY = Mathf.Max(maxY, cells[index].y);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>The square of one cell in thumbnail pixels (y down), with the bounding box centred in the grid.</summary>
        public static Rect GetCellRect(RectInt bounds, Vector2Int cell, float gridSize, float cellSize)
        {
            // Floor keeps every cell edge on a whole pixel, so the centre is off by at most half a pixel.
            float left = Mathf.Floor(0.5f * (gridSize - bounds.width * cellSize));
            float top = Mathf.Floor(0.5f * (gridSize - bounds.height * cellSize));
            return new Rect(left + (cell.x - bounds.xMin) * cellSize, top + (bounds.yMax - 1 - cell.y) * cellSize,
                cellSize, cellSize);
        }
    }
}
