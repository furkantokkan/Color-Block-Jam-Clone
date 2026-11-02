using System;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>Absolute board cells the canvas outlines as one shape: a block or a board object.</summary>
    internal interface ILevelCanvasFootprint
    {
        int CellCount { get; }
        Vector2Int GetCell(int index);
        bool Contains(Vector2Int cell);
    }

    /// <summary>
    /// One block as the Level Editor canvas draws it: absolute board cells, colour, label cell and typed effects.
    /// </summary>
    internal sealed class LevelCanvasBlock : ILevelCanvasFootprint
    {
        private readonly Vector2Int[] m_cells;
        private readonly LevelEffectParameters[] m_effects;

        public LevelCanvasBlock(string id, string colorId, Vector2Int[] cells, Vector2Int labelCell,
            LevelEffectParameters[] effects = null)
        {
            Id = id;
            HasColor = BoardLayout.TryParseDoorColor(colorId, out PaletteColor color);
            Color = color;
            m_cells = cells;
            LabelCell = labelCell;
            m_effects = effects != null ? (LevelEffectParameters[])effects.Clone() : Array.Empty<LevelEffectParameters>();
        }

        public string Id { get; }
        public bool HasColor { get; }
        public PaletteColor Color { get; }
        public int CellCount => m_cells.Length;
        /// <summary>The block cell nearest the footprint's centre, where its id label sits.</summary>
        public Vector2Int LabelCell { get; }

        public bool TryGetEffect<T>(out T effect) where T : LevelEffectParameters
        {
            for (int index = 0; index < m_effects.Length; index++)
                if (m_effects[index] is T value) { effect = value; return true; }
            effect = null;
            return false;
        }

        public Vector2Int GetCell(int index)
        {
            return m_cells[index];
        }

        public bool Contains(Vector2Int cell)
        {
            for (int index = 0; index < m_cells.Length; index++)
            {
                if (m_cells[index] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>One door as the Level Editor canvas draws it.</summary>
    internal sealed class LevelCanvasDoor
    {
        public LevelCanvasDoor(string id, BoardSide side, int start, int length, string colorId)
        {
            Id = id;
            Side = side;
            Start = start;
            Length = length;
            HasColor = BoardLayout.TryParseDoorColor(colorId, out PaletteColor color);
            Color = color;
        }

        public string Id { get; }
        public BoardSide Side { get; }
        public int Start { get; }
        public int Length { get; }
        public bool HasColor { get; }
        public PaletteColor Color { get; }
    }

    /// <summary>
    /// One board object (level cell object) as the canvas draws and selects it: its id, board cells and, for a
    /// Barrier, its Starts open state.
    /// </summary>
    internal sealed class LevelCanvasObstacle : ILevelCanvasFootprint
    {
        private readonly Vector2Int[] m_cells;

        public LevelCanvasObstacle(string id, Vector2Int[] cells, bool isBarrier, bool startsOpen)
        {
            Id = id;
            m_cells = cells;
            IsBarrier = isBarrier;
            StartsOpen = startsOpen;
        }

        public string Id { get; }
        public int CellCount => m_cells.Length;
        public bool IsBarrier { get; }
        public bool StartsOpen { get; }

        public Vector2Int GetCell(int index)
        {
            return m_cells[index];
        }

        public bool Contains(Vector2Int cell)
        {
            for (int index = 0; index < m_cells.Length; index++)
            {
                if (m_cells[index] == cell)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Immutable view data the Level Editor canvas draws and hit-tests, built once from a draft level snapshot:
    /// cell occupancy, the cells a wall or barrier may be painted on, door segments per side, block footprints and
    /// which of them overlap another block, and board objects with their barrier state.
    /// It never changes the level it was built from (docs/Level-Editor-Design.md sections 3 and 5).
    /// </summary>
    internal sealed class LevelCanvasModel
    {
        public static readonly LevelCanvasModel Empty = new LevelCanvasModel(null);

        private const int k_sideCount = 4;
        private readonly LevelCanvasBlock[] m_blocks;
        // True for a block that shares a cell with another block, which only a re-stamp leaves (contract D3-B); the
        // canvas strikes its label through.
        private readonly bool[] m_overlapping;
        private readonly LevelCanvasDoor[] m_doors;
        // Index of the block covering cell (x, y) at y * Width + x, or -1. On overlap the last block wins, the one
        // the canvas paints on top, so a click picks the block the designer sees.
        private readonly int[] m_blockIndex;
        // Door index per ring segment, one array per BoardSide value, or -1. The last (topmost drawn) door wins.
        private readonly int[][] m_doorIndex;
        // True for a void cell (level JSON v2) at y * Width + x.
        private readonly bool[] m_void;
        // True where the Wall brush may paint cell (x, y), at y * Width + x: LevelEditCommands.GetVoidIssue finds no
        // block, board object, gate or floor split in the way.
        private readonly bool[] m_wallPaintable;
        private readonly LevelCanvasObstacle[] m_obstacles;
        // Index of the board object covering cell (x, y) at y * Width + x, or -1; the last one wins, as for blocks.
        private readonly int[] m_obstacleIndex;
        // True where no block cell may go because a closed barrier covers the cell, at y * Width + x.
        private readonly bool[] m_closedBarrier;
        // True where the Barrier brush may paint a new, closed barrier cell (LevelBarrierCommands), at y * Width + x.
        private readonly bool[] m_barrierPaintable;
        // Why each board corner cannot be cut one more step, or null when it can; index GetCornerIndex(right, top).
        private readonly string[] m_cornerCutIssues = new string[4];
        // Per corner, the cells its next cut turns void and its next fill turns back to floor (LevelCornerSteps).
        private readonly int[] m_cornerCutCells = new int[4];
        private readonly int[] m_cornerFillCells = new int[4];

        private LevelCanvasModel(LevelData level)
        {
            Width = level != null ? Mathf.Max(0, level.Width) : 0;
            Height = level != null ? Mathf.Max(0, level.Height) : 0;
            m_blockIndex = new int[Width * Height];
            Fill(m_blockIndex);
            m_void = new bool[Width * Height];
            for (int index = 0; index < (level?.VoidCellCount ?? 0); index++)
            {
                Vector2Int cell = level.GetVoidCellAt(index);
                if (IsOnBoard(cell) && !m_void[cell.y * Width + cell.x])
                {
                    m_void[cell.y * Width + cell.x] = true;
                    VoidCount++;
                }
            }

            // One command check per cell, like the corner cut issues below, so the wall ghost matches the command.
            m_wallPaintable = new bool[Width * Height];
            for (int y = 0; level != null && y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    string issue = LevelEditCommands.GetVoidIssue(level, new Vector2Int(x, y), "cell");
                    m_wallPaintable[y * Width + x] = issue == null;
                }
            }

            m_obstacles = new LevelCanvasObstacle[level?.CellObjectCount ?? 0];
            m_obstacleIndex = new int[Width * Height];
            Fill(m_obstacleIndex);
            for (int index = 0; index < m_obstacles.Length; index++)
            {
                m_obstacles[index] = CreateObstacle(level.GetCellObjectAt(index));
                MarkObstacle(index);
            }

            // One command check per cell, so the block and barrier ghosts match their commands.
            m_closedBarrier = new bool[Width * Height];
            m_barrierPaintable = new bool[Width * Height];
            for (int y = 0; level != null && y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    m_closedBarrier[y * Width + x] = LevelBarrierCommands.GetBlockingObjectIndex(level, cell) >= 0;
                    m_barrierPaintable[y * Width + x] = LevelBarrierCommands.GetCellIssue(level, cell) == null
                                                        && !LevelBarrierCommands.IsUnderBlock(level, cell);
                }
            }

            m_doorIndex = new int[k_sideCount][];
            for (int side = 0; side < k_sideCount; side++)
            {
                m_doorIndex[side] = new int[GetSideLength((BoardSide)side)];
                Fill(m_doorIndex[side]);
            }

            m_blocks = new LevelCanvasBlock[level?.BlockCount ?? 0];
            for (int index = 0; index < m_blocks.Length; index++)
            {
                m_blocks[index] = CreateBlock(level.GetBlockAt(index));
                MarkBlock(index);
            }

            m_overlapping = LevelBlockOverlap.Find(level);

            m_doors = new LevelCanvasDoor[level?.DoorCount ?? 0];
            for (int index = 0; index < m_doors.Length; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                m_doors[index] = door != null
                    ? new LevelCanvasDoor(door.Id, door.Side, door.Start, door.Length, door.ColorId)
                    : new LevelCanvasDoor(null, BoardSide.Top, 0, 0, null);
                MarkDoor(index);
            }

            for (int corner = 0; corner < m_cornerCutIssues.Length; corner++)
            {
                Vector2Int cell = GetCornerCell((corner & 1) != 0, (corner & 2) != 0);
                bool hasBoard = level != null && Width > 0 && Height > 0;
                m_cornerCutIssues[corner] = hasBoard
                    ? LevelEditCommands.GetCornerCutIssue(level, cell)
                    : "Select a level first.";
                if (hasBoard)
                {
                    LevelCornerSteps.GetStepSizes(level, cell, out m_cornerCutCells[corner],
                        out m_cornerFillCells[corner]);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }
        /// <summary>Distinct void cells on the board.</summary>
        public int VoidCount { get; }
        public int BlockCount => m_blocks.Length;
        public int DoorCount => m_doors.Length;
        /// <summary>Board objects (level cell objects), in level order.</summary>
        public int ObstacleCount => m_obstacles.Length;

        /// <summary>Builds the model from a level; null gives an empty board.</summary>
        public static LevelCanvasModel Create(LevelData level)
        {
            return level != null ? new LevelCanvasModel(level) : Empty;
        }

        public LevelCanvasBlock GetBlock(int index)
        {
            return m_blocks[index];
        }

        /// <summary>True when this block shares a cell with another block (contract D3-B).</summary>
        public bool IsOverlapping(int blockIndex)
        {
            return m_overlapping[blockIndex];
        }

        public LevelCanvasDoor GetDoor(int index)
        {
            return m_doors[index];
        }

        public LevelCanvasObstacle GetObstacle(int index)
        {
            return m_obstacles[index];
        }

        /// <summary>Index of the board object on this board cell, or -1 for an uncovered or off-board cell.</summary>
        public int GetObstacleIndexAt(Vector2Int cell)
        {
            return IsOnBoard(cell) ? m_obstacleIndex[cell.y * Width + cell.x] : -1;
        }

        /// <summary>True when a barrier covers this board cell, open or closed.</summary>
        public bool IsBarrier(Vector2Int cell)
        {
            int index = GetObstacleIndexAt(cell);
            return index >= 0 && m_obstacles[index].IsBarrier;
        }

        /// <summary>
        /// True when the Barrier brush may paint this cell into a new barrier, which starts closed: board floor with no
        /// other board object and no block (LevelBarrierCommands). The stroke command still decides on release; a
        /// stroke that joins an open barrier may cover a block.
        /// </summary>
        public bool CanPaintBarrier(Vector2Int cell)
        {
            return IsOnBoard(cell) && m_barrierPaintable[cell.y * Width + cell.x];
        }

        public bool IsOnBoard(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Width && cell.y >= 0 && cell.y < Height;
        }

        /// <summary>True when this board cell is a void cell (level JSON v2).</summary>
        public bool IsVoid(Vector2Int cell)
        {
            return IsOnBoard(cell) && m_void[cell.y * Width + cell.x];
        }

        /// <summary>The board's corner cell on that side: (0, 0) is bottom-left.</summary>
        public Vector2Int GetCornerCell(bool right, bool top)
        {
            return new Vector2Int(right ? Width - 1 : 0, top ? Height - 1 : 0);
        }

        /// <summary>The board cell a ring segment of this side sits against.</summary>
        public Vector2Int GetEdgeCell(BoardSide side, int segment)
        {
            return LevelEditCommands.GetEdgeCell(Width, Height, side, segment);
        }

        /// <summary>Why that corner cell cannot be cut into a void, or null when it can.</summary>
        public string GetCornerCutIssue(bool right, bool top)
        {
            return m_cornerCutIssues[GetCornerIndex(right, top)];
        }

        /// <summary>The number of cells the corner's next cut turns void (its next staircase step).</summary>
        public int GetCornerCutCells(bool right, bool top)
        {
            return m_cornerCutCells[GetCornerIndex(right, top)];
        }

        /// <summary>The number of cells the corner's next fill turns back to floor (its last cut step; 0 when uncut).</summary>
        public int GetCornerFillCells(bool right, bool top)
        {
            return m_cornerFillCells[GetCornerIndex(right, top)];
        }

        /// <summary>Index of the block on this board cell, or -1 for an empty or off-board cell.</summary>
        public int GetBlockIndexAt(Vector2Int cell)
        {
            return IsOnBoard(cell) ? m_blockIndex[cell.y * Width + cell.x] : -1;
        }

        /// <summary>
        /// True when a block cell may go here: on the board, not void, not under a closed barrier (an open barrier is
        /// floor), and empty or held by ignoredBlock (the block being moved). The canvas colours its ghost with this;
        /// the edit commands check the same barriers and still decide on commit.
        /// </summary>
        public bool IsFree(Vector2Int cell, int ignoredBlock)
        {
            int blockIndex = GetBlockIndexAt(cell);
            return IsOnBoard(cell) && !IsVoid(cell) && !m_closedBarrier[cell.y * Width + cell.x]
                && (blockIndex < 0 || blockIndex == ignoredBlock);
        }

        /// <summary>
        /// True when the Wall brush may turn this board cell into a wall: it is floor with no block, board object or
        /// gate on it, and the rest of the floor stays connected (LevelEditCommands.GetVoidIssue). The wall ghost uses
        /// this; the command still decides on release, after the stroke's earlier cells.
        /// </summary>
        public bool CanPaintWall(Vector2Int cell)
        {
            return IsOnBoard(cell) && m_wallPaintable[cell.y * Width + cell.x];
        }

        /// <summary>True when any board object (a level cell object, such as a barrier) covers this cell.</summary>
        public bool IsBoardObject(Vector2Int cell) => GetObstacleIndexAt(cell) >= 0;

        /// <summary>Top and Bottom run along the width; Left and Right along the height.</summary>
        public int GetSideLength(BoardSide side)
        {
            return LevelEditCommands.GetSideLength(Width, Height, side);
        }

        /// <summary>Index of the door on this ring segment, or -1.</summary>
        public int GetDoorIndexAt(BoardSide side, int segment)
        {
            int sideIndex = (int)side;
            if (sideIndex < 0 || sideIndex >= k_sideCount)
            {
                return -1;
            }

            int[] segments = m_doorIndex[sideIndex];
            return segment >= 0 && segment < segments.Length ? segments[segment] : -1;
        }

        /// <summary>
        /// Finds a stored selection in this level: by a unique id first, else by its index when that is in range,
        /// else it clears. Ids are editable in Raw data and may repeat until Save.
        /// </summary>
        public LevelCanvasSelection Resolve(LevelCanvasSelection selection)
        {
            switch (selection.Kind)
            {
                case LevelCanvasSelectionKind.Block:
                {
                    int index = FindUnique(selection.Id, true);
                    if (index < 0 && selection.Index >= 0 && selection.Index < m_blocks.Length)
                    {
                        index = selection.Index;
                    }

                    return index >= 0 ? LevelCanvasSelection.ForBlock(index, m_blocks[index].Id) : LevelCanvasSelection.None;
                }
                case LevelCanvasSelectionKind.Door:
                {
                    int index = FindUnique(selection.Id, false);
                    if (index < 0 && selection.Index >= 0 && selection.Index < m_doors.Length)
                    {
                        index = selection.Index;
                    }

                    return index >= 0 ? LevelCanvasSelection.ForDoor(index, m_doors[index].Id) : LevelCanvasSelection.None;
                }
                case LevelCanvasSelectionKind.Obstacle:
                {
                    int index = FindUniqueObstacle(selection.Id);
                    if (index < 0 && selection.Index >= 0 && selection.Index < m_obstacles.Length)
                    {
                        index = selection.Index;
                    }

                    return index >= 0
                        ? LevelCanvasSelection.ForObstacle(index, m_obstacles[index].Id)
                        : LevelCanvasSelection.None;
                }
                default:
                    return LevelCanvasSelection.None;
            }
        }

        private static LevelCanvasBlock CreateBlock(LevelBlockData block)
        {
            if (block == null)
            {
                return new LevelCanvasBlock(null, null, Array.Empty<Vector2Int>(), default);
            }

            Vector2Int[] cells = new Vector2Int[block.CellCount];
            Vector2 centre = Vector2.zero;
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = block.Origin + block.GetCellAt(index);
                centre += cells[index];
            }

            Vector2Int labelCell = block.Origin;
            if (cells.Length > 0)
            {
                centre /= cells.Length;
                float nearest = float.MaxValue;
                for (int index = 0; index < cells.Length; index++)
                {
                    float distance = (cells[index] - centre).sqrMagnitude;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        labelCell = cells[index];
                    }
                }
            }

            var effects = new LevelEffectParameters[block.EffectCount];
            for (int index = 0; index < effects.Length; index++) effects[index] = block.GetEffectAt(index).Parameters;
            return new LevelCanvasBlock(block.Id, block.ColorId, cells, labelCell, effects);
        }

        private void MarkBlock(int blockIndex)
        {
            LevelCanvasBlock block = m_blocks[blockIndex];
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int cell = block.GetCell(index);
                if (IsOnBoard(cell))
                {
                    m_blockIndex[cell.y * Width + cell.x] = blockIndex;
                }
            }
        }

        private static LevelCanvasObstacle CreateObstacle(LevelCellObjectData cellObject)
        {
            if (cellObject == null)
            {
                return new LevelCanvasObstacle(null, Array.Empty<Vector2Int>(), false, false);
            }

            Vector2Int[] cells = new Vector2Int[cellObject.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = cellObject.GetCellAt(index);
            }

            return new LevelCanvasObstacle(cellObject.Id, cells, LevelBarrierCommands.IsBarrier(cellObject),
                LevelBarrierCommands.StartsOpen(cellObject));
        }

        private void MarkObstacle(int obstacleIndex)
        {
            LevelCanvasObstacle obstacle = m_obstacles[obstacleIndex];
            for (int index = 0; index < obstacle.CellCount; index++)
            {
                Vector2Int cell = obstacle.GetCell(index);
                if (IsOnBoard(cell))
                {
                    m_obstacleIndex[cell.y * Width + cell.x] = obstacleIndex;
                }
            }
        }

        // Like FindUnique: the only board object with this id, or -1 when none or several have it.
        private int FindUniqueObstacle(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            int found = -1;
            for (int index = 0; index < m_obstacles.Length; index++)
            {
                if (!string.Equals(m_obstacles[index].Id, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found >= 0)
                {
                    return -1;
                }

                found = index;
            }

            return found;
        }

        private void MarkDoor(int doorIndex)
        {
            LevelCanvasDoor door = m_doors[doorIndex];
            int sideIndex = (int)door.Side;
            if (sideIndex < 0 || sideIndex >= k_sideCount)
            {
                return;
            }

            int[] segments = m_doorIndex[sideIndex];
            for (int segment = door.Start; segment < door.Start + door.Length; segment++)
            {
                if (segment >= 0 && segment < segments.Length)
                {
                    segments[segment] = doorIndex;
                }
            }
        }

        private int FindUnique(string id, bool blocks)
        {
            if (string.IsNullOrEmpty(id))
            {
                return -1;
            }

            int found = -1;
            int count = blocks ? m_blocks.Length : m_doors.Length;
            for (int index = 0; index < count; index++)
            {
                string candidate = blocks ? m_blocks[index].Id : m_doors[index].Id;
                if (!string.Equals(candidate, id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (found >= 0)
                {
                    return -1;
                }

                found = index;
            }

            return found;
        }

        // Encodes a board corner into its m_cornerCutIssues/m_cornerCutCells/m_cornerFillCells index.
        private static int GetCornerIndex(bool right, bool top)
        {
            return (right ? 1 : 0) + (top ? 2 : 0);
        }

        private static void Fill(int[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = -1;
            }
        }
    }
}
