using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure C# conversion of one parsed CbjLevelSource into the project's LevelData: board and void
    /// cells, doors, the wall consistency check, block cells and colour mapping (DEC-101). A block is its
    /// cells, whose ShapeKey is its piece, so nothing is matched against the piece library (DEC-193); the
    /// level's own validation refuses a footprint beyond the piece limits. It writes nothing; the menu
    /// command owns the file save.
    /// </summary>
    internal static class CbjLevelConverter
    {
        internal static bool TryConvert(
            CbjLevelSource source,
            List<string> mechanicViolations,
            CbjColorTable colorTable,
            string levelId,
            string levelName,
            int timerSeconds,
            out LevelData level,
            out List<string> warnings,
            out string error)
        {
            level = null;
            warnings = new List<string>();
            error = null;

            if (mechanicViolations != null && mechanicViolations.Count > 0)
            {
                error = "Level uses unsupported mechanics: " + string.Join("; ", mechanicViolations) + ".";
                return false;
            }

            if (!TryConvertBoard(source, out int minX, out int minY, out int width, out int height,
                    out Vector2Int[] voidCells, out error))
            {
                return false;
            }

            if (!TryConvertDoors(source, minX, minY, width, height, colorTable,
                    out LevelDoorData[] doors, out List<(int Bct, string Entity)> unmappedDoorColors, out error))
            {
                return false;
            }

            if (!TryValidateWalls(source, minX, minY, width, height, out error))
            {
                return false;
            }

            if (!TryConvertBlocks(source, minX, minY, colorTable, warnings,
                    out LevelBlockData[] blocks, out List<(int Bct, string Entity)> unmappedBlockColors, out error))
            {
                return false;
            }

            List<(int Bct, string Entity)> unmapped = new List<(int, string)>(unmappedDoorColors);
            unmapped.AddRange(unmappedBlockColors);
            if (unmapped.Count > 0)
            {
                error = BuildColorError(unmapped);
                return false;
            }

            level = new LevelData(levelId, levelName, width, height, timerSeconds, voidCells, doors, blocks,
                Array.Empty<LevelCellObjectData>());
            if (!level.Validate(out error))
            {
                level = null;
                return false;
            }

            return true;
        }

        private static bool TryConvertBoard(
            CbjLevelSource source, out int minX, out int minY, out int width, out int height,
            out Vector2Int[] voidCells, out string error)
        {
            minX = minY = width = height = 0;
            voidCells = Array.Empty<Vector2Int>();

            if (source.FloorCells.Length == 0)
            {
                error = "The CBJ level has no floor cells (CMS is empty).";
                return false;
            }

            HashSet<Vector2Int> floorSet = new HashSet<Vector2Int>();
            int maxX = int.MinValue, maxY = int.MinValue;
            minX = int.MaxValue;
            minY = int.MaxValue;
            foreach (Vector2Int cell in source.FloorCells)
            {
                if (!floorSet.Add(cell))
                {
                    error = $"CBJ floor cell ({cell.x}, {cell.y}) is listed twice.";
                    return false;
                }

                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y);
                maxY = Math.Max(maxY, cell.y);
            }

            width = maxX - minX + 1;
            height = maxY - minY + 1;

            Vector2Int expectedRingOrigin = new Vector2Int(minX - 1, minY - 1);
            if (source.RingOrigin != expectedRingOrigin)
            {
                error = $"CBJ ring origin (BSP) is ({source.RingOrigin.x}, {source.RingOrigin.y}); " +
                        $"expected ({expectedRingOrigin.x}, {expectedRingOrigin.y}).";
                return false;
            }

            // Contract change (path rebase and v2 void cells): a CMS gap inside the bounding box becomes a
            // void cell (formatVersion 2), not a rejection.
            List<Vector2Int> voids = new List<Vector2Int>();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (!floorSet.Contains(cell))
                    {
                        voids.Add(new Vector2Int(x - minX, y - minY));
                    }
                }
            }

            voidCells = voids.ToArray();
            error = null;
            return true;
        }

        private static bool TryConvertDoors(
            CbjLevelSource source, int minX, int minY, int width, int height, CbjColorTable colorTable,
            out LevelDoorData[] doors, out List<(int Bct, string Entity)> unmappedColors, out string error)
        {
            doors = new LevelDoorData[source.Doors.Length];
            unmappedColors = new List<(int, string)>();
            error = null;
            int maxX = minX + width - 1;
            int maxY = minY + height - 1;

            for (int index = 0; index < source.Doors.Length; index++)
            {
                CbjDoor door = source.Doors[index];
                string doorId = "d" + (index + 1);

                if (!TryResolveDoorSide(door.Cells, minX, minY, maxX, maxY, out BoardSide side, out int start,
                        out int length, out string sideError))
                {
                    error = $"CBJ door {doorId} {sideError}";
                    return false;
                }

                bool expectHorizontal = side == BoardSide.Top || side == BoardSide.Bottom;
                if (door.IsHorizontal != expectHorizontal)
                {
                    error = $"CBJ door {doorId} has IH={(door.IsHorizontal ? "true" : "false")}, " +
                            $"which disagrees with side {side}.";
                    return false;
                }

                string colorId;
                if (colorTable.TryGetColor(door.Bct, out PaletteColor color))
                {
                    colorId = color.ToString();
                }
                else
                {
                    colorId = "Unmapped";
                    unmappedColors.Add((door.Bct, $"door {doorId}"));
                }

                doors[index] = new LevelDoorData(doorId, side, start, length, colorId);
            }

            return true;
        }

        // A door's cells lie on exactly one ring side, board-local and contiguous (contract item 3).
        private static bool TryResolveDoorSide(
            Vector2Int[] cells, int minX, int minY, int maxX, int maxY,
            out BoardSide side, out int start, out int length, out string error)
        {
            side = default;
            start = 0;
            length = 0;

            bool allTop = true, allBottom = true, allLeft = true, allRight = true;
            foreach (Vector2Int cell in cells)
            {
                if (!(cell.y == maxY + 1 && cell.x >= minX && cell.x <= maxX)) allTop = false;
                if (!(cell.y == minY - 1 && cell.x >= minX && cell.x <= maxX)) allBottom = false;
                if (!(cell.x == minX - 1 && cell.y >= minY && cell.y <= maxY)) allLeft = false;
                if (!(cell.x == maxX + 1 && cell.y >= minY && cell.y <= maxY)) allRight = false;
            }

            int matchCount = (allTop ? 1 : 0) + (allBottom ? 1 : 0) + (allLeft ? 1 : 0) + (allRight ? 1 : 0);
            if (matchCount != 1)
            {
                Vector2Int first = cells[0];
                error = $"has a non-contiguous or corner cell near ({first.x}, {first.y}).";
                return false;
            }

            side = allTop ? BoardSide.Top : allBottom ? BoardSide.Bottom : allLeft ? BoardSide.Left : BoardSide.Right;
            bool alongX = side == BoardSide.Top || side == BoardSide.Bottom;

            int[] positions = new int[cells.Length];
            for (int index = 0; index < cells.Length; index++)
            {
                positions[index] = alongX ? cells[index].x : cells[index].y;
            }

            Array.Sort(positions);
            for (int index = 1; index < positions.Length; index++)
            {
                if (positions[index] == positions[index - 1])
                {
                    error = "lists a door cell twice.";
                    return false;
                }

                if (positions[index] != positions[index - 1] + 1)
                {
                    error = "has non-contiguous door cells.";
                    return false;
                }
            }

            start = alongX ? positions[0] - minX : positions[0] - minY;
            length = positions.Length;
            error = null;
            return true;
        }

        // WMS must equal the ring cells the doors do not cover; the project derives walls itself
        // (contract item 4).
        private static bool TryValidateWalls(
            CbjLevelSource source, int minX, int minY, int width, int height, out string error)
        {
            int maxX = minX + width - 1;
            int maxY = minY + height - 1;

            HashSet<Vector2Int> ring = new HashSet<Vector2Int>();
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                ring.Add(new Vector2Int(x, minY - 1));
                ring.Add(new Vector2Int(x, maxY + 1));
            }

            for (int y = minY - 1; y <= maxY + 1; y++)
            {
                ring.Add(new Vector2Int(minX - 1, y));
                ring.Add(new Vector2Int(maxX + 1, y));
            }

            HashSet<Vector2Int> doorCells = new HashSet<Vector2Int>();
            foreach (CbjDoor door in source.Doors)
            {
                foreach (Vector2Int cell in door.Cells)
                {
                    doorCells.Add(cell);
                }
            }

            HashSet<Vector2Int> expectedWalls = new HashSet<Vector2Int>(ring);
            expectedWalls.ExceptWith(doorCells);

            HashSet<Vector2Int> actualWalls = new HashSet<Vector2Int>();
            foreach (Vector2Int cell in source.WallCells)
            {
                if (!actualWalls.Add(cell))
                {
                    error = $"CBJ wall cell ({cell.x}, {cell.y}) is listed twice.";
                    return false;
                }
            }

            foreach (Vector2Int cell in actualWalls)
            {
                if (!expectedWalls.Contains(cell))
                {
                    error = $"CBJ wall cell ({cell.x}, {cell.y}) is unsupported; walls are derived from the ring.";
                    return false;
                }
            }

            foreach (Vector2Int cell in expectedWalls)
            {
                if (!actualWalls.Contains(cell))
                {
                    error = $"CBJ level is missing wall cell ({cell.x}, {cell.y}) on the ring.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryConvertBlocks(
            CbjLevelSource source, int minX, int minY, CbjColorTable colorTable,
            List<string> warnings, out LevelBlockData[] blocks, out List<(int Bct, string Entity)> unmappedColors,
            out string error)
        {
            blocks = new LevelBlockData[source.Blocks.Length];
            unmappedColors = new List<(int, string)>();
            error = null;

            for (int index = 0; index < source.Blocks.Length; index++)
            {
                CbjBlock block = source.Blocks[index];
                string blockId = "b" + (index + 1);

                List<Vector2Int> unique = new List<Vector2Int>();
                HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
                bool duplicateFound = false;
                foreach (Vector2Int cell in block.Cells)
                {
                    if (seen.Add(cell))
                    {
                        unique.Add(cell);
                    }
                    else
                    {
                        duplicateFound = true;
                    }
                }

                if (duplicateFound)
                {
                    warnings.Add($"CBJ block {blockId} listed a duplicate cell; it was removed.");
                }

                if (unique.Count == 0)
                {
                    error = $"CBJ block {blockId} has no cells.";
                    return false;
                }

                int localMinX = int.MaxValue, localMinY = int.MaxValue;
                Vector2Int[] shifted = new Vector2Int[unique.Count];
                for (int cellIndex = 0; cellIndex < unique.Count; cellIndex++)
                {
                    Vector2Int shiftedCell = new Vector2Int(unique[cellIndex].x - minX, unique[cellIndex].y - minY);
                    shifted[cellIndex] = shiftedCell;
                    localMinX = Math.Min(localMinX, shiftedCell.x);
                    localMinY = Math.Min(localMinY, shiftedCell.y);
                }

                Vector2Int origin = new Vector2Int(localMinX, localMinY);
                Vector2Int[] relative = new Vector2Int[shifted.Length];
                for (int cellIndex = 0; cellIndex < shifted.Length; cellIndex++)
                {
                    relative[cellIndex] = shifted[cellIndex] - origin;
                }

                string colorId;
                if (colorTable.TryGetColor(block.Bct, out PaletteColor color))
                {
                    colorId = color.ToString();
                }
                else
                {
                    colorId = "Unmapped";
                    unmappedColors.Add((block.Bct, $"block {blockId}"));
                }

                blocks[index] = new LevelBlockData(blockId, colorId, origin, relative);
            }

            return true;
        }

        private static string BuildColorError(List<(int Bct, string Entity)> unmapped)
        {
            SortedDictionary<int, List<string>> byBct = new SortedDictionary<int, List<string>>();
            foreach ((int bct, string entity) in unmapped)
            {
                if (!byBct.TryGetValue(bct, out List<string> entities))
                {
                    entities = new List<string>();
                    byBct[bct] = entities;
                }

                entities.Add(entity);
            }

            List<string> parts = new List<string>();
            foreach (KeyValuePair<int, List<string>> pair in byBct)
            {
                parts.Add($"BCT {pair.Key} ({string.Join(", ", pair.Value)})");
            }

            return "The colour table does not map " + string.Join("; ", parts) + ".";
        }
    }
}
