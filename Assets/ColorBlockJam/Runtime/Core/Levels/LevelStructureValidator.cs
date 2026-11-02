using System;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The structural authoring rules of a level (void cells, doors, blocks, cell objects, piece names), read
    /// through LevelData's public accessors and mirroring LevelEffectValidator (AD-15). LevelData.Validate stays
    /// the public facade.
    /// </summary>
    internal static class LevelStructureValidator
    {
        internal static bool ValidateVoidCells(LevelData level, out bool[] floor, out string error)
        {
            floor = new bool[level.Width * level.Height];
            for (int index = 0; index < floor.Length; index++)
            {
                floor[index] = true;
            }

            for (int index = 0; index < level.VoidCellCount; index++)
            {
                Vector2Int cell = level.GetVoidCellAt(index);
                if (cell.x < 0 || cell.x >= level.Width || cell.y < 0 || cell.y >= level.Height)
                {
                    error = $"Level {level.Id} void cell ({cell.x}, {cell.y}) is outside the board.";
                    return false;
                }

                int cellIndex = cell.y * level.Width + cell.x;
                if (!floor[cellIndex])
                {
                    error = $"Level {level.Id} lists void cell ({cell.x}, {cell.y}) twice.";
                    return false;
                }

                floor[cellIndex] = false;
            }

            if (level.VoidCellCount == floor.Length)
            {
                error = $"Level {level.Id} has no floor cell.";
                return false;
            }

            if (!IsFloorConnected(floor, level.Width, level.Height))
            {
                error = $"Level {level.Id} floor cells are not four-connected.";
                return false;
            }

            error = null;
            return true;
        }

        internal static bool ValidateDoors(LevelData level, bool[] floor, out string error)
        {
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door == null || string.IsNullOrWhiteSpace(door.Id)
                    || string.IsNullOrWhiteSpace(door.ColorId))
                {
                    error = $"Level {level.Id} door {index} needs an ID and color ID.";
                    return false;
                }

                if (!IsValidSide(door.Side))
                {
                    error = $"Level {level.Id} door {door.Id} has an invalid side.";
                    return false;
                }

                if (door.Length > LevelData.MaxDoorLength)
                {
                    error = $"Level {level.Id} door {door.Id} spans {door.Length} cells; gates span 1 to {LevelData.MaxDoorLength}.";
                    return false;
                }

                int sideLength = door.Side == BoardSide.Top || door.Side == BoardSide.Bottom
                    ? level.Width : level.Height;
                if (door.Start < 0 || door.Start >= sideLength || door.Length < 1
                    || door.Length > sideLength - door.Start)
                {
                    error = $"Level {level.Id} door {door.Id} is outside its board edge.";
                    return false;
                }

                if (!BoardLayout.TryParseDoorColor(door.ColorId, out _))
                {
                    error = $"Level {level.Id} door {door.Id} uses '{door.ColorId}', which is not a gate colour.";
                    return false;
                }

                for (int segment = 0; segment < door.Length; segment++)
                {
                    Vector2Int cell = LevelData.GetDoorCell(door.Side, door.Start + segment, level.Width, level.Height);
                    if (!floor[cell.y * level.Width + cell.x])
                    {
                        error = $"Level {level.Id} door {door.Id} opens onto void cell ({cell.x}, {cell.y}).";
                        return false;
                    }
                }

                for (int previous = 0; previous < index; previous++)
                {
                    LevelDoorData other = level.GetDoorAt(previous);
                    if (string.Equals(other.Id, door.Id, StringComparison.Ordinal))
                    {
                        error = $"Level {level.Id} has duplicate door ID {door.Id}.";
                        return false;
                    }

                    if (other.Side == door.Side && door.Start < other.Start + other.Length
                        && other.Start < door.Start + door.Length)
                    {
                        error = $"Level {level.Id} doors {other.Id} and {door.Id} overlap.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        internal static bool ValidateBlocks(LevelData level, bool[] floor, out string error)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block == null || string.IsNullOrWhiteSpace(block.Id)
                    || string.IsNullOrWhiteSpace(block.ColorId))
                {
                    error = $"Level {level.Id} block {index} needs an ID and color ID.";
                    return false;
                }

                if (block.CellCount == 0)
                {
                    error = $"Level {level.Id} block {block.Id} has no cells.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(level.GetBlockAt(previous).Id, block.Id, StringComparison.Ordinal))
                    {
                        error = $"Level {level.Id} has duplicate block ID {block.Id}.";
                        return false;
                    }
                }

                for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
                {
                    Vector2Int relativeCell = block.GetCellAt(cellIndex);
                    long x = (long)block.Origin.x + relativeCell.x;
                    long y = (long)block.Origin.y + relativeCell.y;
                    if (x < 0 || x >= level.Width || y < 0 || y >= level.Height)
                    {
                        error = $"Level {level.Id} block {block.Id} has a cell outside the board.";
                        return false;
                    }

                    if (!floor[y * level.Width + x])
                    {
                        error = $"Level {level.Id} block {block.Id} has a cell on void cell ({x}, {y}).";
                        return false;
                    }

                    for (int earlierCell = 0; earlierCell < cellIndex; earlierCell++)
                    {
                        if (relativeCell == block.GetCellAt(earlierCell))
                        {
                            error = $"Level {level.Id} block {block.Id} lists a cell twice.";
                            return false;
                        }
                    }

                    for (int previous = 0; previous < index; previous++)
                    {
                        LevelBlockData other = level.GetBlockAt(previous);
                        for (int otherCell = 0; otherCell < other.CellCount; otherCell++)
                        {
                            Vector2Int otherRelative = other.GetCellAt(otherCell);
                            if (x == (long)other.Origin.x + otherRelative.x
                                && y == (long)other.Origin.y + otherRelative.y)
                            {
                                error = $"Level {level.Id} blocks {other.Id} and {block.Id} overlap.";
                                return false;
                            }
                        }
                    }
                }

                if (!CellConnectivity.AreFourConnected(block))
                {
                    error = $"Level {level.Id} block {block.Id} has disconnected cells.";
                    return false;
                }

                // Every block is a piece (DEC-193): the checks above leave only the size limits to break here, so
                // any client can derive the block's ShapeKey from its cells.
                if (!block.ValidateKey(out string issue))
                {
                    error = $"Level {level.Id} block {block.Id}: {issue}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>
        /// The level's piece names (DEC-193): each names a well-formed key once, with a name of 1 to
        /// LevelPieceName.MaxNameLength characters. Whether a block uses the key is the Level Editor's pruning, not a
        /// load rule.
        /// </summary>
        internal static bool ValidatePieceNames(LevelData level, out string error)
        {
            for (int index = 0; index < level.PieceNameCount; index++)
            {
                LevelPieceName piece = level.GetPieceNameAt(index);
                ShapeKey key = piece != null ? piece.Key : default;
                if (!key.IsValid)
                {
                    error = $"Level {level.Id} piece name {index} has no valid piece key.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(piece.Name) || piece.Name.Length > LevelPieceName.MaxNameLength)
                {
                    error = $"Level {level.Id} piece {key} needs a name of 1 to {LevelPieceName.MaxNameLength} characters.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (level.GetPieceNameAt(previous).Key == key)
                    {
                        error = $"Level {level.Id} names piece {key} more than once.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        internal static bool ValidateCellObjects(LevelData level, bool[] floor, out string error)
        {
            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (cellObject == null || string.IsNullOrWhiteSpace(cellObject.Id))
                {
                    error = $"Level {level.Id} cell object {index} needs an ID.";
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(level.GetCellObjectAt(previous).Id, cellObject.Id, StringComparison.Ordinal))
                    {
                        error = $"Level {level.Id} has duplicate cell object ID {cellObject.Id}.";
                        return false;
                    }
                }

                if (cellObject.CellCount == 0)
                {
                    error = $"Level {level.Id} cell object {cellObject.Id} has no cells.";
                    return false;
                }

                for (int cellIndex = 0; cellIndex < cellObject.CellCount; cellIndex++)
                {
                    Vector2Int cell = cellObject.GetCellAt(cellIndex);
                    if (cell.x < 0 || cell.x >= level.Width || cell.y < 0 || cell.y >= level.Height)
                    {
                        error = $"Level {level.Id} cell object {cellObject.Id} has cell ({cell.x}, {cell.y}) outside the board.";
                        return false;
                    }

                    if (!floor[cell.y * level.Width + cell.x])
                    {
                        error = $"Level {level.Id} cell object {cellObject.Id} has cell ({cell.x}, {cell.y}) on a void cell.";
                        return false;
                    }

                    for (int earlierCell = 0; earlierCell < cellIndex; earlierCell++)
                    {
                        if (cell == cellObject.GetCellAt(earlierCell))
                        {
                            error = $"Level {level.Id} cell object {cellObject.Id} lists cell ({cell.x}, {cell.y}) twice.";
                            return false;
                        }
                    }

                    for (int previous = 0; previous < index; previous++)
                    {
                        LevelCellObjectData other = level.GetCellObjectAt(previous);
                        if (other.ContainsCell(cell))
                        {
                            error = $"Level {level.Id} cell objects {other.Id} and {cellObject.Id} share cell ({cell.x}, {cell.y}).";
                            return false;
                        }
                    }
                }
            }

            error = null;
            return true;
        }

        private static bool IsValidSide(BoardSide side)
        {
            return side == BoardSide.Top || side == BoardSide.Bottom
                || side == BoardSide.Left || side == BoardSide.Right;
        }

        // Load-time flood fill over the floor mask; it runs outside Playing, so its arrays may allocate.
        private static bool IsFloorConnected(bool[] floor, int width, int height)
        {
            int start = Array.IndexOf(floor, true);
            bool[] reached = new bool[floor.Length];
            int[] pending = new int[floor.Length];
            int pendingCount = 0;
            pending[pendingCount++] = start;
            reached[start] = true;
            int reachedCount = 1;
            while (pendingCount > 0)
            {
                int current = pending[--pendingCount];
                int x = current % width;
                int y = current / width;
                for (int direction = 0; direction < 4; direction++)
                {
                    int nextX = x + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                    int nextY = y + (direction == 2 ? 1 : direction == 3 ? -1 : 0);
                    if (nextX < 0 || nextX >= width || nextY < 0 || nextY >= height)
                    {
                        continue;
                    }

                    int next = nextY * width + nextX;
                    if (floor[next] && !reached[next])
                    {
                        reached[next] = true;
                        reachedCount++;
                        pending[pendingCount++] = next;
                    }
                }
            }

            int floorCount = 0;
            for (int index = 0; index < floor.Length; index++)
            {
                if (floor[index])
                {
                    floorCount++;
                }
            }

            return reachedCount == floorCount;
        }
    }
}
