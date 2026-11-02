using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// The immutable domain form of one level: board size, floor cells, doors and blocks.
    /// TryCreate re-validates the converted data (AC-8.5) and precomputes a door index per side lane
    /// so exit checks never search the door list.
    /// </summary>
    public sealed class LevelModel
    {
        private const int k_sideCount = 4;
        private const int k_noDoor = -1;
        private const int k_noBlock = -1;

        private readonly bool[] m_floor;
        private readonly DoorDefinition[] m_doors;
        private readonly BlockDefinition[] m_blocks;
        private readonly int[] m_doorAtLane;
        private readonly int m_laneCapacity;

        private LevelModel(int width, int height, bool[] floor, DoorDefinition[] doors,
            BlockDefinition[] blocks, int timerSeconds, int[] doorAtLane, int laneCapacity, int totalCellCount)
        {
            Width = width;
            Height = height;
            TimerSeconds = timerSeconds;
            TotalCellCount = totalCellCount;
            m_floor = floor;
            m_doors = doors;
            m_blocks = blocks;
            m_doorAtLane = doorAtLane;
            m_laneCapacity = laneCapacity;
        }

        public int Width { get; }

        public int Height { get; }

        public int TimerSeconds { get; }

        public int DoorCount => m_doors.Length;

        public int BlockCount => m_blocks.Length;

        /// <summary>
        /// The sum of the authored blocks' cell counts. It bounds the block IDs one attempt can issue:
        /// every live block keeps at least one cell and each Rocket removes one (B-1), so it sizes every
        /// per-block runtime array once, on Home.
        /// </summary>
        public int TotalCellCount { get; }

        public DoorDefinition GetDoor(int index) => m_doors[index];

        public BlockDefinition GetBlock(int index) => m_blocks[index];

        /// <summary>False for void cells and for every point outside the board.</summary>
        public bool IsFloor(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height && m_floor[y * Width + x];
        }

        /// <summary>The number of lanes on a side: columns for Top/Bottom, rows for Left/Right.</summary>
        public int GetLaneCount(Side side) => SideMath.RunsAlongX(side) ? Width : Height;

        /// <summary>The index of the door covering a lane of a side, or -1 when that lane is wall.</summary>
        public int GetDoorAtLane(Side side, int lane)
        {
            if (lane < 0 || lane >= GetLaneCount(side))
            {
                return k_noDoor;
            }

            return m_doorAtLane[(int)side * m_laneCapacity + lane];
        }

        /// <summary>
        /// Builds a model after checking the board size, the floor array, every door span (inside its
        /// edge, onto floor, no shared lane) and every block (inside the board, on floor, no overlap,
        /// edge-connected). Arrays are copied; the caller's arrays are never kept.
        /// </summary>
        public static bool TryCreate(int width, int height, bool[] floor, DoorDefinition[] doors,
            BlockDefinition[] blocks, int timerSeconds, out LevelModel model, out string error)
        {
            model = null;
            if (width < 1 || height < 1)
            {
                error = "The board needs a positive width and height.";
                return false;
            }

            if (floor == null || floor.Length != width * height)
            {
                error = "The floor array does not match the board size.";
                return false;
            }

            if (doors == null || blocks == null)
            {
                error = "The door and block lists are required.";
                return false;
            }

            if (timerSeconds < 0)
            {
                error = "The timer cannot be negative.";
                return false;
            }

            bool[] floorCopy = (bool[])floor.Clone();
            int laneCapacity = width > height ? width : height;
            int[] doorAtLane = new int[k_sideCount * laneCapacity];
            for (int i = 0; i < doorAtLane.Length; i++)
            {
                doorAtLane[i] = k_noDoor;
            }

            if (!TryPlaceDoors(width, height, floorCopy, doors, doorAtLane, laneCapacity, out error)
                || !TryCheckBlocks(width, height, floorCopy, blocks, out error))
            {
                return false;
            }

            int totalCellCount = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                totalCellCount += blocks[i].CellCount;
            }

            model = new LevelModel(width, height, floorCopy, (DoorDefinition[])doors.Clone(),
                (BlockDefinition[])blocks.Clone(), timerSeconds, doorAtLane, laneCapacity, totalCellCount);
            error = null;
            return true;
        }

        private static bool TryPlaceDoors(int width, int height, bool[] floor, DoorDefinition[] doors,
            int[] doorAtLane, int laneCapacity, out string error)
        {
            for (int i = 0; i < doors.Length; i++)
            {
                DoorDefinition door = doors[i];
                if (door == null || door.Index != i)
                {
                    error = $"Door {i} is missing or has the wrong index.";
                    return false;
                }

                if (door.Side < Side.Top || door.Side > Side.Right)
                {
                    error = $"Door {i} has an invalid side.";
                    return false;
                }

                int laneCount = SideMath.RunsAlongX(door.Side) ? width : height;
                if (door.Length < 1 || door.Start < 0 || door.Start > laneCount - door.Length)
                {
                    error = $"Door {i} lies outside its board edge.";
                    return false;
                }

                for (int lane = door.Start; lane < door.Start + door.Length; lane++)
                {
                    Cell behind = GetPerimeterCell(door.Side, lane, width, height);
                    if (!floor[behind.Y * width + behind.X])
                    {
                        error = $"Door {i} opens onto a void cell.";
                        return false;
                    }

                    int slot = (int)door.Side * laneCapacity + lane;
                    if (doorAtLane[slot] != k_noDoor)
                    {
                        error = $"Doors {doorAtLane[slot]} and {i} share a lane.";
                        return false;
                    }

                    doorAtLane[slot] = i;
                }
            }

            error = null;
            return true;
        }

        private static bool TryCheckBlocks(int width, int height, bool[] floor, BlockDefinition[] blocks,
            out string error)
        {
            int[] owner = new int[width * height];
            for (int i = 0; i < owner.Length; i++)
            {
                owner[i] = k_noBlock;
            }

            for (int i = 0; i < blocks.Length; i++)
            {
                BlockDefinition block = blocks[i];
                if (block == null || block.Index != i)
                {
                    error = $"Block {i} is missing or has the wrong index.";
                    return false;
                }

                for (int c = 0; c < block.CellCount; c++)
                {
                    Cell cell = block.Origin + block.GetCell(c);
                    if (cell.X < 0 || cell.Y < 0 || cell.X >= width || cell.Y >= height)
                    {
                        error = $"Block {i} has a cell outside the board.";
                        return false;
                    }

                    int slot = cell.Y * width + cell.X;
                    if (!floor[slot])
                    {
                        error = $"Block {i} has a cell on a void cell.";
                        return false;
                    }

                    if (owner[slot] != k_noBlock)
                    {
                        error = owner[slot] == i
                            ? $"Block {i} lists a cell twice."
                            : $"Blocks {owner[slot]} and {i} overlap.";
                        return false;
                    }

                    owner[slot] = i;
                }

                if (!IsConnected(block))
                {
                    error = $"Block {i} has disconnected cells.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsConnected(BlockDefinition block)
        {
            bool[] reached = new bool[block.CellCount];
            reached[0] = true;
            int reachedCount = 1;
            bool grew = true;
            while (grew)
            {
                grew = false;
                for (int i = 0; i < block.CellCount; i++)
                {
                    if (reached[i])
                    {
                        continue;
                    }

                    Cell cell = block.GetCell(i);
                    for (int j = 0; j < block.CellCount; j++)
                    {
                        if (!reached[j])
                        {
                            continue;
                        }

                        Cell other = block.GetCell(j);
                        int distance = Math.Abs(cell.X - other.X) + Math.Abs(cell.Y - other.Y);
                        if (distance == 1)
                        {
                            reached[i] = true;
                            reachedCount++;
                            grew = true;
                            break;
                        }
                    }
                }
            }

            return reachedCount == block.CellCount;
        }

        private static Cell GetPerimeterCell(Side side, int lane, int width, int height)
        {
            switch (side)
            {
                case Side.Top: return new Cell(lane, height - 1);
                case Side.Bottom: return new Cell(lane, 0);
                case Side.Left: return new Cell(0, lane);
                default: return new Cell(width - 1, lane);
            }
        }
    }
}
