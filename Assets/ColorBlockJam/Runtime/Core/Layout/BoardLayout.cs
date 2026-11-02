using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Runtime.Layout
{
    /// <summary>
    /// Attempt-owned floor mask and door slots of one level, built once at level
    /// load and never mutated. It is the single source of board geometry.
    /// </summary>
    public sealed class BoardLayout : IBoardLayout
    {
        public const float CellSize = 1f;

        // Calibrated wall/gate height of the board kit (docs/art/board-kit/README.md).
        private const float k_wallHeight = 0.72f;
        // Walls stand in a half-cell ring outside every floor edge.
        private const float k_ringThickness = 0.5f;

        private static readonly PaletteColor[] s_doorColors =
        {
            PaletteColor.Red, PaletteColor.Orange, PaletteColor.Yellow, PaletteColor.LightGreen,
            PaletteColor.DarkGreen, PaletteColor.LightTurquoise, PaletteColor.DarkTurquoise,
            PaletteColor.LightBlue, PaletteColor.Blue, PaletteColor.Purple, PaletteColor.Pink
        };

        private static readonly string[] s_doorColorIds = Array.ConvertAll(s_doorColors, color => color.ToString());

        private readonly int m_width;
        private readonly int m_height;
        private readonly bool[] m_floor;
        private readonly DoorSlot[] m_doorSlots;

        private BoardLayout(int width, int height, bool[] floor, DoorSlot[] doorSlots)
        {
            m_width = width;
            m_height = height;
            m_floor = floor;
            m_doorSlots = doorSlots;
        }

        public int Width => m_width;
        public int Height => m_height;
        public int DoorSlotCount => m_doorSlots.Length;

        public Bounds LocalBounds
        {
            get
            {
                // Cell centres run 0..W-1; the floor edge and the wall ring add half a cell each.
                Vector3 size = new Vector3(
                    m_width * CellSize + 2f * k_ringThickness,
                    k_wallHeight,
                    m_height * CellSize + 2f * k_ringThickness);
                Vector3 center = new Vector3((m_width - 1) * 0.5f * CellSize, k_wallHeight * 0.5f,
                    (m_height - 1) * 0.5f * CellSize);
                return new Bounds(center, size);
            }
        }

        public bool IsFloor(int x, int y)
        {
            return x >= 0 && x < m_width && y >= 0 && y < m_height && m_floor[y * m_width + x];
        }

        public Vector3 CellToLocal(Vector2Int cell)
        {
            return new Vector3(cell.x * CellSize, 0f, cell.y * CellSize);
        }

        public bool TryLocalToCell(Vector3 localPosition, out Vector2Int cell)
        {
            cell = new Vector2Int(
                Mathf.FloorToInt(localPosition.x / CellSize + 0.5f),
                Mathf.FloorToInt(localPosition.z / CellSize + 0.5f));
            return IsFloor(cell.x, cell.y);
        }

        public DoorSlot GetDoorSlotAt(int index)
        {
            return m_doorSlots[index];
        }

        /// <summary>Builds the layout of a level: its void cells are holes and its doors become slots.</summary>
        public static bool TryCreate(LevelData level, out BoardLayout layout, out string error)
        {
            if (level == null)
            {
                layout = null;
                error = "Level data is missing.";
                return false;
            }

            Vector2Int[] voidCells = new Vector2Int[level.VoidCellCount];
            for (int index = 0; index < voidCells.Length; index++)
            {
                voidCells[index] = level.GetVoidCellAt(index);
            }

            LevelDoorData[] doors = new LevelDoorData[level.DoorCount];
            for (int index = 0; index < doors.Length; index++)
            {
                doors[index] = level.GetDoorAt(index);
            }

            return TryCreate(level.Width, level.Height, voidCells, doors, out layout, out error);
        }

        public static bool TryCreate(
            int width,
            int height,
            IReadOnlyList<Vector2Int> voidCells,
            IReadOnlyList<LevelDoorData> doors,
            out BoardLayout layout,
            out string error)
        {
            layout = null;
            if (width < 1 || height < 1 || voidCells == null || doors == null)
            {
                error = "A board layout needs a positive size, a void cell list and a door list.";
                return false;
            }

            if (!TryCreateFloor(width, height, voidCells, out bool[] floor, out error)
                || !TryCreateDoorSlots(width, height, floor, doors, out DoorSlot[] doorSlots, out error))
            {
                return false;
            }

            layout = new BoardLayout(width, height, floor, doorSlots);
            return true;
        }

        /// <summary>
        /// Door colours are exact gate PaletteColor names. Wall, Ground, numbers
        /// and comma lists are rejected, so a typo never becomes another colour.
        /// </summary>
        public static bool TryParseDoorColor(string colorId, out PaletteColor color)
        {
            for (int index = 0; index < s_doorColorIds.Length; index++)
            {
                if (string.Equals(colorId, s_doorColorIds[index], StringComparison.Ordinal))
                {
                    color = s_doorColors[index];
                    return true;
                }
            }

            color = default;
            return false;
        }

        private static bool TryCreateFloor(
            int width,
            int height,
            IReadOnlyList<Vector2Int> voidCells,
            out bool[] floor,
            out string error)
        {
            floor = new bool[width * height];
            for (int index = 0; index < floor.Length; index++)
            {
                floor[index] = true;
            }

            for (int index = 0; index < voidCells.Count; index++)
            {
                Vector2Int cell = voidCells[index];
                if (cell.x < 0 || cell.x >= width || cell.y < 0 || cell.y >= height)
                {
                    error = $"Void cell {cell} is outside the {width} x {height} board.";
                    return false;
                }

                int cellIndex = cell.y * width + cell.x;
                if (!floor[cellIndex])
                {
                    error = $"Void cell {cell} is listed twice.";
                    return false;
                }

                floor[cellIndex] = false;
            }

            if (voidCells.Count == floor.Length)
            {
                error = "The board has no floor cell.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TryCreateDoorSlots(
            int width,
            int height,
            bool[] floor,
            IReadOnlyList<LevelDoorData> doors,
            out DoorSlot[] doorSlots,
            out string error)
        {
            doorSlots = null;
            int slotCount = 0;
            for (int index = 0; index < doors.Count; index++)
            {
                LevelDoorData door = doors[index];
                if (door == null || !TryParseDoorColor(door.ColorId, out _))
                {
                    error = $"Door {index} needs a gate colour name.";
                    return false;
                }

                if (!TryGetSideLength(door.Side, width, height, out int sideLength)
                    || door.Start < 0 || door.Length < 1 || door.Length > sideLength - door.Start)
                {
                    error = $"Door {door.Id} is outside its board edge.";
                    return false;
                }

                slotCount += door.Length;
            }

            // Each boundary edge segment can hold one door piece.
            bool[] usedSegments = new bool[2 * (width + height)];
            DoorSlot[] slots = new DoorSlot[slotCount];
            int next = 0;
            for (int doorIndex = 0; doorIndex < doors.Count; doorIndex++)
            {
                LevelDoorData door = doors[doorIndex];
                TryParseDoorColor(door.ColorId, out PaletteColor color);
                for (int segment = 0; segment < door.Length; segment++)
                {
                    int position = door.Start + segment;
                    Vector2Int cell = GetPerimeterCell(door.Side, position, width, height);
                    if (!floor[cell.y * width + cell.x])
                    {
                        error = $"Door {door.Id} opens onto void cell {cell}.";
                        return false;
                    }

                    int segmentIndex = GetSegmentIndex(door.Side, position, width, height);
                    if (usedSegments[segmentIndex])
                    {
                        error = $"Door {door.Id} overlaps another door.";
                        return false;
                    }

                    usedSegments[segmentIndex] = true;
                    slots[next++] = new DoorSlot(doorIndex, segment, cell, door.Side, color);
                }
            }

            doorSlots = slots;
            error = null;
            return true;
        }

        private static bool TryGetSideLength(BoardSide side, int width, int height, out int length)
        {
            switch (side)
            {
                case BoardSide.Top:
                case BoardSide.Bottom:
                    length = width;
                    return true;
                case BoardSide.Left:
                case BoardSide.Right:
                    length = height;
                    return true;
                default:
                    length = 0;
                    return false;
            }
        }

        private static Vector2Int GetPerimeterCell(BoardSide side, int position, int width, int height)
        {
            switch (side)
            {
                case BoardSide.Top: return new Vector2Int(position, height - 1);
                case BoardSide.Bottom: return new Vector2Int(position, 0);
                case BoardSide.Left: return new Vector2Int(0, position);
                default: return new Vector2Int(width - 1, position);
            }
        }

        private static int GetSegmentIndex(BoardSide side, int position, int width, int height)
        {
            switch (side)
            {
                case BoardSide.Top: return position;
                case BoardSide.Bottom: return width + position;
                case BoardSide.Left: return 2 * width + position;
                default: return 2 * width + height + position;
            }
        }
    }
}
