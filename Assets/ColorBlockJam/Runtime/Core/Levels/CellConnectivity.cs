using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// Four-connectivity of a cell list, shared by structural block validation and the Barrier effect
    /// check (AD-15) so the same fixed-point algorithm is not written twice.
    /// </summary>
    internal static class CellConnectivity
    {
        internal static bool AreFourConnected(LevelBlockData block)
        {
            return AreFourConnected(block.CellCount, block.GetCellAt);
        }

        internal static bool AreFourConnected(LevelCellObjectData cellObject)
        {
            return AreFourConnected(cellObject.CellCount, cellObject.GetCellAt);
        }

        private static bool AreFourConnected(int cellCount, Func<int, Vector2Int> getCellAt)
        {
            bool[] connected = new bool[cellCount];
            connected[0] = true;
            int connectedCount = 1;
            bool foundNeighbor;

            do
            {
                foundNeighbor = false;
                for (int index = 0; index < cellCount; index++)
                {
                    if (connected[index])
                    {
                        continue;
                    }

                    Vector2Int cell = getCellAt(index);
                    for (int other = 0; other < cellCount; other++)
                    {
                        if (!connected[other])
                        {
                            continue;
                        }

                        Vector2Int neighbor = getCellAt(other);
                        long distance = Math.Abs((long)cell.x - neighbor.x)
                            + Math.Abs((long)cell.y - neighbor.y);
                        if (distance != 1)
                        {
                            continue;
                        }

                        connected[index] = true;
                        connectedCount++;
                        foundNeighbor = true;
                        break;
                    }
                }
            } while (foundNeighbor);

            return connectedCount == cellCount;
        }
    }
}
