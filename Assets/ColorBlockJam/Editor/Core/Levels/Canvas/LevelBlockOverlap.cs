using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Finds the blocks of a level that share a cell with another block. Only a re-stamp can leave such an overlap
    /// (contract D3-B): the canvas strikes these blocks' labels through and the window names them until a move,
    /// rotate, mirror, delete or undo separates them. The issues list reports the same overlap as an error.
    /// </summary>
    internal static class LevelBlockOverlap
    {
        /// <summary>
        /// One entry per block of the level, true when that block shares a cell with another block. A block that
        /// lists one of its own cells twice does not overlap itself. A null level has no blocks.
        /// </summary>
        public static bool[] Find(LevelData level)
        {
            bool[] overlapping = new bool[level?.BlockCount ?? 0];
            // The first block found on each cell; a later block on that cell marks both.
            Dictionary<Vector2Int, int> firstBlocks = new Dictionary<Vector2Int, int>();
            for (int index = 0; index < overlapping.Length; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int cellIndex = 0; block != null && cellIndex < block.CellCount; cellIndex++)
                {
                    Vector2Int cell = block.Origin + block.GetCellAt(cellIndex);
                    if (!firstBlocks.TryGetValue(cell, out int first))
                    {
                        firstBlocks.Add(cell, index);
                    }
                    else if (first != index)
                    {
                        overlapping[first] = true;
                        overlapping[index] = true;
                    }
                }
            }

            return overlapping;
        }

        /// <summary>
        /// The ids of the blocks that share a cell with another block, in block order, as "b1 and b2" or
        /// "b1, b2 and b3"; null when no block overlaps.
        /// </summary>
        public static string Describe(LevelData level)
        {
            bool[] overlapping = Find(level);
            List<string> ids = new List<string>();
            for (int index = 0; index < overlapping.Length; index++)
            {
                if (overlapping[index])
                {
                    ids.Add(level.GetBlockAt(index).Id);
                }
            }

            // An overlap always involves two blocks, so the last id is never alone.
            return ids.Count > 0
                ? string.Join(", ", ids.GetRange(0, ids.Count - 1)) + " and " + ids[ids.Count - 1]
                : null;
        }
    }
}
