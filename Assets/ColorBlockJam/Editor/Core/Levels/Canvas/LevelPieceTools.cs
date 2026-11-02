using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Rotates and mirrors block footprints on the y-up board; each result is shifted back to start at cell (0, 0).
    /// </summary>
    internal static class LevelPieceTools
    {
        internal static Vector2Int[] RotateClockwise(IReadOnlyList<Vector2Int> cells)
        {
            return Transform(cells, Operation.RotateClockwise);
        }

        internal static Vector2Int[] RotateCounterClockwise(IReadOnlyList<Vector2Int> cells)
        {
            return Transform(cells, Operation.RotateCounterClockwise);
        }

        /// <summary>
        /// How far a quarter turn in place moves the origin: add it to the origin of the turned cells that
        /// RotateClockwise or RotateCounterClockwise return. The footprint turns around its bounding-box centre when
        /// that centre is a cell centre or a grid corner, else around the cell nearest it (ties go to the first listed
        /// cell). So every turn is a visible quarter turn, even for a symmetric piece such as a domino, and four turns
        /// the same way restore the footprint (user request of 2026-09-26).
        /// </summary>
        internal static Vector2Int GetTurnOriginShift(IReadOnlyList<Vector2Int> cells, bool clockwise)
        {
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("A block needs occupied cells.", nameof(cells));

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

            // Doubled coordinates keep a half-cell pivot in integers.
            Vector2Int centre = new Vector2Int(minX + maxX, minY + maxY);
            Vector2Int pivot = centre;
            if ((maxX - minX - (maxY - minY)) % 2 != 0)
            {
                // A centre between two cells on one axis only: a turn around it would leave the grid.
                int nearest = int.MaxValue;
                for (int index = 0; index < cells.Count; index++)
                {
                    Vector2Int offset = cells[index] * 2 - centre;
                    int distance = offset.x * offset.x + offset.y * offset.y;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        pivot = cells[index] * 2;
                    }
                }
            }

            int shiftX = int.MaxValue;
            int shiftY = int.MaxValue;
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2Int offset = cells[index] * 2 - pivot;
                Vector2Int turned = pivot + (clockwise ? new Vector2Int(offset.y, -offset.x) : new Vector2Int(-offset.y, offset.x));
                shiftX = Mathf.Min(shiftX, turned.x / 2);
                shiftY = Mathf.Min(shiftY, turned.y / 2);
            }

            return new Vector2Int(shiftX, shiftY);
        }

        /// <summary>
        /// The smallest shift that brings <paramref name="cells"/> placed at <paramref name="origin"/> inside a
        /// width x height board, per axis; zero when they already fit. A footprint wider than the board is aligned
        /// to its left or bottom edge and the placement check then refuses it.
        /// </summary>
        internal static Vector2Int GetBoardKick(IReadOnlyList<Vector2Int> cells, Vector2Int origin, int width, int height)
        {
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("A block needs occupied cells.", nameof(cells));

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2Int cell = origin + cells[index];
                minX = Mathf.Min(minX, cell.x);
                minY = Mathf.Min(minY, cell.y);
                maxX = Mathf.Max(maxX, cell.x);
                maxY = Mathf.Max(maxY, cell.y);
            }

            return new Vector2Int(GetAxisKick(minX, maxX, width), GetAxisKick(minY, maxY, height));
        }

        private static int GetAxisKick(int min, int max, int size)
        {
            if (min < 0)
                return -min;
            return max >= size ? Mathf.Max(size - 1 - max, -min) : 0;
        }

        internal static Vector2Int[] FlipHorizontal(IReadOnlyList<Vector2Int> cells)
        {
            return Transform(cells, Operation.FlipHorizontal);
        }

        internal static Vector2Int[] FlipVertical(IReadOnlyList<Vector2Int> cells)
        {
            return Transform(cells, Operation.FlipVertical);
        }

        private enum Operation
        {
            RotateClockwise,
            RotateCounterClockwise,
            FlipHorizontal,
            FlipVertical
        }

        private static Vector2Int[] Transform(IReadOnlyList<Vector2Int> cells, Operation operation)
        {
            if (cells == null || cells.Count == 0)
                throw new ArgumentException("A block needs occupied cells.", nameof(cells));

            var rotated = new Vector2Int[cells.Count];
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2Int cell = cells[index];
                rotated[index] = operation == Operation.FlipHorizontal ? new Vector2Int(-cell.x, cell.y)
                    : operation == Operation.FlipVertical ? new Vector2Int(cell.x, -cell.y)
                    : operation == Operation.RotateClockwise ? new Vector2Int(cell.y, -cell.x)
                    : new Vector2Int(-cell.y, cell.x);
                minX = Mathf.Min(minX, rotated[index].x);
                minY = Mathf.Min(minY, rotated[index].y);
            }

            for (int index = 0; index < rotated.Length; index++)
                rotated[index] -= new Vector2Int(minX, minY);
            return rotated;
        }
    }
}
