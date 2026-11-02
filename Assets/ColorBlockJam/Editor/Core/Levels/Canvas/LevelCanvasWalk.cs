using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Walks between two cells one side-sharing step at a time, so a fast drag crosses every cell in between and a
    /// stroke built from the walk stays four-connected.
    /// </summary>
    internal static class LevelCanvasWalk
    {
        /// <summary>
        /// Appends every cell after <paramref name="from"/> up to and including <paramref name="to"/>, stepping along
        /// the axis whose next cell edge the straight line crosses first (ties along x). Appends nothing when the
        /// two cells are the same.
        /// </summary>
        public static void AddCrossedCells(Vector2Int from, Vector2Int to, List<Vector2Int> crossed)
        {
            Vector2Int cell = from;
            int stepsX = Mathf.Abs(to.x - from.x);
            int stepsY = Mathf.Abs(to.y - from.y);
            Vector2Int step = new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
            for (int x = 0, y = 0; x < stepsX || y < stepsY;)
            {
                // (0.5 + x) / stepsX <= (0.5 + y) / stepsY, multiplied out so a zero count never divides.
                if (y >= stepsY || (x < stepsX && (0.5f + x) * stepsY <= (0.5f + y) * stepsX))
                {
                    cell.x += step.x;
                    x++;
                }
                else
                {
                    cell.y += step.y;
                    y++;
                }

                crossed.Add(cell);
            }
        }
    }
}
