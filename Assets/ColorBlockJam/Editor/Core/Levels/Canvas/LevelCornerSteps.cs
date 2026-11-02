using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The square steps of a board corner for the corner cut (TASK-20260926-181022947-224a7aef, DEC-190): step k
    /// (k >= 1) holds every board cell whose Chebyshev distance from the corner cell is k - 1, an L-shaped ring, so k
    /// cut steps leave a k x k notch. A corner's depth is the number of leading steps whose cells are all void. The
    /// depth is read from the level's void cells every time, never stored, so Undo, the reload copy and a reloaded
    /// JSON always agree (docs/Level-Editor-Design.md, corner block).
    /// </summary>
    internal static class LevelCornerSteps
    {
        /// <summary>The number of steps from one corner of a width x height board to its farthest line.</summary>
        public static int GetStepCount(int width, int height)
        {
            return width > 0 && height > 0 ? Mathf.Max(width, height) : 0;
        }

        /// <summary>True when the cell is one of the four corner cells of a width x height board.</summary>
        public static bool IsCorner(int width, int height, Vector2Int cell)
        {
            return width > 0 && height > 0 && (cell.x == 0 || cell.x == width - 1) && (cell.y == 0 || cell.y == height - 1);
        }

        /// <summary>
        /// Clears cells and fills it with step k of this corner of a width x height board: its board cells in a fixed
        /// order, from the one on the corner's column along the ring's row to the ring's inner corner, then along the
        /// ring's column to the one on the corner's row. Empty when the cell is no corner or k is outside 1 to
        /// <see cref="GetStepCount"/>.
        /// </summary>
        public static void GetStepCells(int width, int height, Vector2Int corner, int step, List<Vector2Int> cells)
        {
            cells.Clear();
            if (!IsCorner(width, height, corner) || step < 1 || step > GetStepCount(width, height))
            {
                return;
            }

            int distance = step - 1;
            int stepX = corner.x == 0 ? 1 : -1;
            int stepY = corner.y == 0 ? 1 : -1;
            for (int along = 0; along <= distance; along++)
            {
                AddOnBoard(width, height, new Vector2Int(corner.x + stepX * along, corner.y + stepY * distance), cells);
            }

            for (int along = distance - 1; along >= 0; along--)
            {
                AddOnBoard(width, height, new Vector2Int(corner.x + stepX * distance, corner.y + stepY * along), cells);
            }
        }

        // A ring beyond the board's narrower side keeps only its arm that is still on the board.
        private static void AddOnBoard(int width, int height, Vector2Int cell, List<Vector2Int> cells)
        {
            if (cell.x >= 0 && cell.x < width && cell.y >= 0 && cell.y < height)
            {
                cells.Add(cell);
            }
        }

        /// <summary>
        /// The corner's depth in this level: how many leading steps hold only void cells, 0 while the corner cell is
        /// floor or the cell is no corner.
        /// </summary>
        public static int GetDepth(LevelData level, Vector2Int corner)
        {
            if (level == null || !IsCorner(level.Width, level.Height, corner))
            {
                return 0;
            }

            List<Vector2Int> cells = new List<Vector2Int>();
            int steps = GetStepCount(level.Width, level.Height);
            for (int step = 1; step <= steps; step++)
            {
                GetStepCells(level.Width, level.Height, corner, step, cells);
                for (int index = 0; index < cells.Count; index++)
                {
                    if (level.IsFloor(cells[index]))
                    {
                        return step - 1;
                    }
                }
            }

            return steps;
        }

        /// <summary>
        /// The number of cells the next cut of this corner turns void (the floor cells of the step after its depth) and
        /// the number of cells its next fill turns back to floor (the cells of its last cut step; 0 at depth 0).
        /// </summary>
        public static void GetStepSizes(LevelData level, Vector2Int corner, out int cutCells, out int fillCells)
        {
            cutCells = 0;
            fillCells = 0;
            if (level == null || !IsCorner(level.Width, level.Height, corner))
            {
                return;
            }

            int depth = GetDepth(level, corner);
            List<Vector2Int> cells = new List<Vector2Int>();
            GetStepCells(level.Width, level.Height, corner, depth + 1, cells);
            for (int index = 0; index < cells.Count; index++)
            {
                cutCells += level.IsFloor(cells[index]) ? 1 : 0;
            }

            GetStepCells(level.Width, level.Height, corner, depth, cells);
            fillCells = cells.Count;
        }
    }
}
