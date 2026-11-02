using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// Level-dependent rules of individual catalog definitions (path colours, leaves, tracks and Barrier
    /// cells). Each returns null or the message tail after "Level {id} {entity} effect {type} ".
    /// </summary>
    internal static class LevelEffectChecks
    {
        public static string CheckPathColor(LevelData level, LevelEffectOwner owner,
            LevelEffectParameters parameters)
        {
            // Catalog OBJ-01: a path cell under a block of another colour at start is an error.
            LevelCellObjectData path = owner.CellObject;
            string color = ((ColorfulPathObject)parameters).Color.ToString();
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (string.Equals(block.ColorId, color, StringComparison.Ordinal))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
                {
                    Vector2Int cell = block.Origin + block.GetCellAt(cellIndex);
                    if (path.ContainsCell(cell))
                    {
                        return $"cell ({cell.x}, {cell.y}) is under block {block.Id} of another colour.";
                    }
                }
            }

            return null;
        }

        public static string CheckFlowerCrateLeaves(LevelData level, LevelEffectOwner owner,
            LevelEffectParameters parameters)
        {
            // Catalog OBJ-04: a leaf listed twice would count twice.
            FlowerCrateObject crate = (FlowerCrateObject)parameters;
            for (int index = 0; index < crate.LeafCount; index++)
            {
                string leafId = crate.GetLeafIdAt(index);
                for (int earlier = 0; earlier < index; earlier++)
                {
                    if (string.Equals(crate.GetLeafIdAt(earlier), leafId, StringComparison.Ordinal))
                    {
                        return $"key leaves lists block {leafId} twice.";
                    }
                }
            }

            return null;
        }

        public static string CheckMovingPath(LevelData level, LevelEffectOwner owner,
            LevelEffectParameters parameters)
        {
            LevelCellObjectData track = owner.CellObject;
            int platformIndex = ((ColorfulMovingPathObject)parameters).PlatformIndex;
            if (platformIndex >= track.CellCount)
            {
                return $"key platformIndex has value '{platformIndex}'; expected an integer from 0 to {track.CellCount - 1}.";
            }

            // The track is the cell order: one row or column of neighbouring cells.
            Vector2Int first = track.GetCellAt(0);
            bool sameColumn = true;
            bool sameRow = true;
            for (int index = 1; index < track.CellCount; index++)
            {
                Vector2Int previous = track.GetCellAt(index - 1);
                Vector2Int cell = track.GetCellAt(index);
                sameColumn &= cell.x == first.x;
                sameRow &= cell.y == first.y;
                if (Math.Abs(cell.x - previous.x) + Math.Abs(cell.y - previous.y) != 1)
                {
                    return "needs a straight track of neighbouring cells in order.";
                }
            }

            if (!sameColumn && !sameRow)
            {
                return "needs a straight track of neighbouring cells in order.";
            }

            return null;
        }

        public static string CheckBarrier(LevelData level, LevelEffectOwner owner,
            LevelEffectParameters parameters)
        {
            LevelCellObjectData barrier = owner.CellObject;
            if (!CellConnectivity.AreFourConnected(barrier))
            {
                return "cells are not four-connected.";
            }

            if (((BarrierObject)parameters).StartsOpen)
            {
                return null;
            }

            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
                {
                    Vector2Int cell = block.Origin + block.GetCellAt(cellIndex);
                    if (barrier.ContainsCell(cell))
                    {
                        return $"starts closed on block {block.Id} at ({cell.x}, {cell.y}).";
                    }
                }
            }

            return null;
        }
    }
}
