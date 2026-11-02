using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Board size rules for the Level Editor: the 3-8 x 3-10 limits, and resizing one side of a draft board so the
    /// remaining content (blocks, doors, void cells and cell objects) keeps its place (<see cref="TryResize"/>).
    /// </summary>
    internal static class LevelBoardResize
    {
        // Aliases of LevelData's board limits (AD-15), so the two copies cannot drift.
        internal const int k_minSize = LevelData.MinBoardSize;
        internal const int k_maxWidth = LevelData.MaxBoardWidth;
        internal const int k_maxHeight = LevelData.MaxBoardHeight;

        /// <summary>
        /// Adds delta lines on one side, or removes -delta lines when delta is negative. Left and Bottom changes move
        /// the board origin, so every block origin and every door start along that axis shifts by delta and keeps its
        /// place. A block or cell object with a cell in a removed line is removed, and void cells in removed lines go
        /// with them; new lines are floor. A void on the resized side's outermost line follows that edge instead, so a
        /// cut corner stays a corner and a staircase corner keeps its steps, unless its new cells cannot be void. A door
        /// on the two sides that run across the
        /// removed lines loses the segments beside them and is removed when none remain; doors on the resized side and
        /// its opposite follow their edge unchanged. Expects a level and a valid side (LevelEditCommands checks both).
        /// </summary>
        internal static bool TryResize(LevelData level, BoardSide side, int delta, out LevelData result,
            out LevelResizeReport report, out string issue)
        {
            result = null;
            report = default;
            if (delta == 0)
            {
                result = level.Clone();
                issue = null;
                return true;
            }

            bool horizontal = side == BoardSide.Left || side == BoardSide.Right;
            long width = horizontal ? (long)level.Width + delta : level.Width;
            long height = horizontal ? level.Height : (long)level.Height + delta;
            if (!TryCheckSize(level, horizontal, delta, width, height, out issue))
            {
                return false;
            }

            // Removed lines [first, last] along the resized axis, in the source level's coordinates; empty on growth.
            bool lowEnd = side == BoardSide.Left || side == BoardSide.Bottom;
            int size = horizontal ? level.Width : level.Height;
            int removedLines = delta < 0 ? -delta : 0;
            int first = lowEnd ? 0 : size - removedLines;
            int last = lowEnd ? removedLines - 1 : size - 1;
            int shift = lowEnd ? delta : 0;

            LevelBlockData[] blocks = ResizeBlocks(level, horizontal, first, last, shift, out int removedBlocks);
            List<LevelEdgeSegment> segments = new List<LevelEdgeSegment>();
            LevelDoorData[] doors = ResizeDoors(level, horizontal, lowEnd, first, last, shift, segments,
                out int removedDoors, out int shortenedDoors);

            int edgeLine = lowEnd ? 0 : size - 1;
            int newEdgeLine = lowEnd ? 0 : size - 1 + delta;
            List<Vector2Int> edgeVoids = new List<Vector2Int>();
            // The depth of each staircase of two or more steps at the resized side's two corners (0 otherwise).
            int[] staircases = new int[2];
            Vector2Int[] voidCells = ResizeVoidCells(level, horizontal, first, last, shift, edgeLine, newEdgeLine,
                edgeVoids, staircases);
            LevelCellObjectData[] cellObjects = ResizeCellObjects(level, horizontal, first, last, shift,
                out int removedCellObjects);

            result = LevelRecordCopy.WithBoard(level, (int)width, (int)height, voidCells, doors, blocks, cellObjects);
            result = AddEdgeVoids(result, edgeVoids, horizontal, newEdgeLine, staircases);
            report = new LevelResizeReport(removedBlocks, removedDoors, shortenedDoors,
                GetRemovedCells(level, horizontal, first, last), segments.ToArray(), removedCellObjects);
            return true;
        }

        /// <summary>Pluralises a count for designer text, e.g. "1 cell" or "3 cells".</summary>
        internal static string FormatCount(int count, string noun)
        {
            return count == 1 ? $"1 {noun}" : $"{count} {noun}s";
        }

        private static bool TryCheckSize(LevelData level, bool horizontal, int delta, long width, long height,
            out string issue)
        {
            if (width >= k_minSize && width <= k_maxWidth && height >= k_minSize && height <= k_maxHeight)
            {
                issue = null;
                return true;
            }

            int size = horizontal ? level.Width : level.Height;
            int maxSize = horizontal ? k_maxWidth : k_maxHeight;
            string axis = horizontal ? "width" : "height";
            if (delta < 0 && size <= k_minSize)
            {
                issue = $"The board is already at its smallest {axis}.";
            }
            else if (delta > 0 && size >= maxSize)
            {
                issue = $"The board is already at its largest {axis}.";
            }
            else
            {
                issue = $"A {width} x {height} board is outside {k_minSize}-{k_maxWidth} by {k_minSize}-{k_maxHeight}.";
            }

            return false;
        }

        private static LevelBlockData[] ResizeBlocks(LevelData level, bool horizontal, int first, int last, int shift,
            out int removedCount)
        {
            Vector2Int offset = horizontal ? new Vector2Int(shift, 0) : new Vector2Int(0, shift);
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount];
            int count = 0;
            removedCount = 0;
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block != null && HasCellInLines(level, block, horizontal, first, last))
                {
                    removedCount++;
                    continue;
                }

                blocks[count++] = block == null || shift == 0
                    ? block
                    : LevelRecordCopy.WithOrigin(block, block.Origin + offset);
            }

            Array.Resize(ref blocks, count);
            return blocks;
        }

        // A void on the resized side's outermost line (edgeLine) follows the edge onto newEdgeLine: it is listed in
        // edgeVoids, in the new board's coordinates, for AddEdgeVoids (user report of 2026-09-25: a grown board left
        // its cut corner in the middle of the edge). Other voids in removed lines go with them; the rest shift with
        // the board origin. A staircase of two or more steps at one of the side's two corners follows its corner
        // instead (task 224a7aef): its depth goes into staircases and its cells are left for AddEdgeVoids to lay
        // again, so the edge and the shift can no longer tear it apart.
        private static Vector2Int[] ResizeVoidCells(LevelData level, bool horizontal, int first, int last, int shift,
            int edgeLine, int newEdgeLine, List<Vector2Int> edgeVoids, int[] staircases)
        {
            List<Vector2Int> staircaseCells = new List<Vector2Int>();
            List<Vector2Int> stepCells = new List<Vector2Int>();
            for (int end = 0; end < staircases.Length; end++)
            {
                Vector2Int corner = GetSideCorner(level.Width, level.Height, horizontal, edgeLine, end);
                int depth = LevelCornerSteps.GetDepth(level, corner);
                staircases[end] = depth >= 2 ? depth : 0;
                for (int step = 1; step <= staircases[end]; step++)
                {
                    LevelCornerSteps.GetStepCells(level.Width, level.Height, corner, step, stepCells);
                    staircaseCells.AddRange(stepCells);
                }
            }

            Vector2Int offset = horizontal ? new Vector2Int(shift, 0) : new Vector2Int(0, shift);
            Vector2Int[] cells = new Vector2Int[level.VoidCellCount];
            int count = 0;
            for (int index = 0; index < level.VoidCellCount; index++)
            {
                Vector2Int cell = level.GetVoidCellAt(index);
                if (staircaseCells.Contains(cell))
                {
                    continue;
                }

                if ((horizontal ? cell.x : cell.y) == edgeLine)
                {
                    edgeVoids.Add(horizontal ? new Vector2Int(newEdgeLine, cell.y) : new Vector2Int(cell.x, newEdgeLine));
                    continue;
                }

                if (IsInLines(level, cell, horizontal, first, last))
                {
                    continue;
                }

                cells[count++] = cell + offset;
            }

            Array.Resize(ref cells, count);
            return cells;
        }

        // Adds each moved edge void whose new cell may be void (LevelEditCommands.GetVoidIssue: no block, board object
        // or gate on it, and the floor stays connected); a void whose new cell is taken is dropped. Then each staircase
        // from ResizeVoidCells is laid again at its corner of the new board, step by step from the corner outward,
        // under the corner cut's rules; the first step that no longer fits ends it, and the steps before it stay.
        private static LevelData AddEdgeVoids(LevelData level, List<Vector2Int> edgeVoids, bool horizontal,
            int newEdgeLine, int[] staircases)
        {
            for (int index = 0; index < edgeVoids.Count; index++)
            {
                if (LevelEditCommands.GetVoidIssue(level, edgeVoids[index], "cell") != null)
                {
                    continue;
                }

                Vector2Int[] voidCells = LevelRecordCopy.GetVoidCells(level);
                Array.Resize(ref voidCells, voidCells.Length + 1);
                voidCells[voidCells.Length - 1] = edgeVoids[index];
                level = LevelRecordCopy.WithVoidCells(level, voidCells);
            }

            List<Vector2Int> stepCells = new List<Vector2Int>();
            for (int end = 0; end < staircases.Length; end++)
            {
                Vector2Int corner = GetSideCorner(level.Width, level.Height, horizontal, newEdgeLine, end);
                for (int step = 1; step <= staircases[end]; step++)
                {
                    if (LevelEditCommands.GetCornerStepIssue(level, corner, step, stepCells) != null)
                    {
                        break;
                    }

                    level = LevelEditCommands.WithVoidCells(level, stepCells);
                }
            }

            return level;
        }

        // One of the two corner cells on a board side: the side's outermost line is x = line for Left and Right and
        // y = line for Top and Bottom; end 0 is its lower corner and end 1 its upper one.
        private static Vector2Int GetSideCorner(int width, int height, bool horizontal, int line, int end)
        {
            return horizontal
                ? new Vector2Int(line, end == 0 ? 0 : height - 1)
                : new Vector2Int(end == 0 ? 0 : width - 1, line);
        }

        // A cell object with a cell in a removed line is removed whole, like a block; the rest shift.
        private static LevelCellObjectData[] ResizeCellObjects(LevelData level, bool horizontal, int first, int last,
            int shift, out int removedCount)
        {
            Vector2Int offset = horizontal ? new Vector2Int(shift, 0) : new Vector2Int(0, shift);
            LevelCellObjectData[] cellObjects = new LevelCellObjectData[level.CellObjectCount];
            int count = 0;
            removedCount = 0;
            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (cellObject != null && HasCellInLines(level, cellObject, horizontal, first, last))
                {
                    removedCount++;
                    continue;
                }

                if (cellObject == null || shift == 0)
                {
                    cellObjects[count++] = cellObject;
                    continue;
                }

                Vector2Int[] cells = LevelRecordCopy.GetCells(cellObject);
                for (int cell = 0; cell < cells.Length; cell++)
                {
                    cells[cell] += offset;
                }

                cellObjects[count++] = new LevelCellObjectData(cellObject.Id, cells, cellObject.Effect);
            }

            Array.Resize(ref cellObjects, count);
            return cellObjects;
        }

        // Only doors whose span runs along the resized axis (Top/Bottom for a width change, Left/Right for a height
        // change) cross the removed lines or need the shift.
        private static LevelDoorData[] ResizeDoors(LevelData level, bool horizontal, bool lowEnd, int first, int last,
            int shift, List<LevelEdgeSegment> removedSegments, out int removedCount, out int shortenedCount)
        {
            LevelDoorData[] doors = new LevelDoorData[level.DoorCount];
            int count = 0;
            removedCount = 0;
            shortenedCount = 0;
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door == null || !RunsAlongAxis(door.Side, horizontal))
                {
                    doors[count++] = door;
                    continue;
                }

                int start = door.Start;
                int end = door.Start + door.Length;
                if (first <= last && door.Length > 0 && start <= last && end > first)
                {
                    int lastRemoved = Math.Min(end - 1, last);
                    for (int segment = Math.Max(start, first); segment <= lastRemoved; segment++)
                    {
                        removedSegments.Add(new LevelEdgeSegment(door.Side, segment));
                    }

                    // The removed lines sit at one end of the side, so the part that remains is one run.
                    if (lowEnd)
                    {
                        start = Math.Max(start, last + 1);
                    }
                    else
                    {
                        end = Math.Min(end, first);
                    }

                    if (end <= start)
                    {
                        removedCount++;
                        continue;
                    }

                    shortenedCount++;
                }

                bool unchanged = shift == 0 && start == door.Start && end - start == door.Length;
                doors[count++] = unchanged ? door : LevelRecordCopy.WithSpan(door, start + shift, end - start);
            }

            Array.Resize(ref doors, count);
            return doors;
        }

        private static bool RunsAlongAxis(BoardSide side, bool horizontal)
        {
            return horizontal
                ? side == BoardSide.Top || side == BoardSide.Bottom
                : side == BoardSide.Left || side == BoardSide.Right;
        }

        // Cells off the board today are not in a removed line; such a block stays and the issues list reports it.
        private static bool HasCellInLines(LevelData level, LevelBlockData block, bool horizontal, int first, int last)
        {
            for (int index = 0; index < block.CellCount; index++)
            {
                if (IsInLines(level, block.Origin + block.GetCellAt(index), horizontal, first, last))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasCellInLines(LevelData level, LevelCellObjectData cellObject, bool horizontal, int first,
            int last)
        {
            for (int index = 0; index < cellObject.CellCount; index++)
            {
                if (IsInLines(level, cellObject.GetCellAt(index), horizontal, first, last))
                {
                    return true;
                }
            }

            return false;
        }

        // An on-board cell in the removed lines [first, last]; always false on growth (first > last).
        private static bool IsInLines(LevelData level, Vector2Int cell, bool horizontal, int first, int last)
        {
            int lineLength = horizontal ? level.Height : level.Width;
            int line = horizontal ? cell.x : cell.y;
            int along = horizontal ? cell.y : cell.x;
            return line >= first && line <= last && along >= 0 && along < lineLength;
        }

        // The removed lines plus every on-board cell of a block or cell object that goes with them, so the hatch
        // shows all of it.
        private static Vector2Int[] GetRemovedCells(LevelData level, bool horizontal, int first, int last)
        {
            List<Vector2Int> cells = new List<Vector2Int>(GetCellsInLines(level, horizontal, first, last));
            for (int index = 0; index < level.BlockCount && last >= first; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block == null || !HasCellInLines(level, block, horizontal, first, last))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
                {
                    AddOnBoard(level, cells, block.Origin + block.GetCellAt(cellIndex));
                }
            }

            for (int index = 0; index < level.CellObjectCount && last >= first; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (cellObject == null || !HasCellInLines(level, cellObject, horizontal, first, last))
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < cellObject.CellCount; cellIndex++)
                {
                    AddOnBoard(level, cells, cellObject.GetCellAt(cellIndex));
                }
            }

            return cells.ToArray();
        }

        private static void AddOnBoard(LevelData level, List<Vector2Int> cells, Vector2Int cell)
        {
            bool onBoard = cell.x >= 0 && cell.x < level.Width && cell.y >= 0 && cell.y < level.Height;
            if (onBoard && !cells.Contains(cell))
            {
                cells.Add(cell);
            }
        }

        private static Vector2Int[] GetCellsInLines(LevelData level, bool horizontal, int first, int last)
        {
            int lineLength = horizontal ? level.Height : level.Width;
            if (last < first || lineLength <= 0)
            {
                return Array.Empty<Vector2Int>();
            }

            Vector2Int[] cells = new Vector2Int[(last - first + 1) * lineLength];
            int count = 0;
            for (int line = first; line <= last; line++)
            {
                for (int along = 0; along < lineLength; along++)
                {
                    cells[count++] = horizontal ? new Vector2Int(line, along) : new Vector2Int(along, line);
                }
            }

            return cells;
        }
    }
}
