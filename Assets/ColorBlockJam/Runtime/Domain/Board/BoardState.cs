using System;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// The mutable occupancy of one attempt: which block holds each cell, each block's current
    /// footprint, where each block is and whether it is still on the board. Footprints live in one
    /// flat cell buffer with a slice per block ID (B-1), so a Rocket split re-slices the parent's cells
    /// in place and appends new IDs (B-2, B-4). Every array is allocated in the constructor, so moves,
    /// releases and splits during play allocate nothing (NFR-1). Its mutators are internal to the
    /// Domain, so during play only command handlers change it (DEC-122).
    /// </summary>
    public sealed class BoardState : IBoardReader
    {
        /// <summary>
        /// The most new parts one removed cell can create: it has four neighbours, one of which stays
        /// with the parent ID.
        /// </summary>
        internal const int k_maxSplitChildren = 3;

        private const int k_maxSplitParts = k_maxSplitChildren + 1;
        private const int k_unlabelled = -1;
        private const int k_noSource = -1;

        private readonly int[] m_occupant;

        // One slot per block ID, sized to LevelModel.TotalCellCount: every live block keeps a cell and
        // each Rocket removes one, so an attempt never issues more IDs (B-1).
        private readonly Cell[] m_cells;
        private readonly int[] m_cellStart;
        private readonly int[] m_cellCount;
        private readonly int[] m_minX;
        private readonly int[] m_maxX;
        private readonly int[] m_minY;
        private readonly int[] m_maxY;
        private readonly BlockColor[] m_color;
        private readonly int[] m_source;
        private readonly Cell[] m_origin;
        private readonly BlockStatus[] m_status;

        // Split scratch: a part label, a search queue and a partition copy per cell of the block being
        // split, sized to the largest authored block because a part never grows, plus each board cell's
        // position in that block's slice.
        private readonly int[] m_partLabel;
        private readonly int[] m_partQueue;
        private readonly Cell[] m_partCells;
        private readonly int[] m_partSize;
        private readonly int[] m_positionAt;

        // The attempt's hooks answer AllowsBoosterTarget, so every reader-based target check shares one rule (DEC-171).
        private readonly AttemptHooks m_hooks;

        private int m_blockCount;

        /// <summary>A board with no booster restrictions, for boards outside an attempt such as the editor solver.</summary>
        public BoardState(LevelModel model)
            : this(model, AttemptHooks.CreateEmpty())
        {
        }

        public BoardState(LevelModel model, AttemptHooks hooks)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_occupant = new int[model.Width * model.Height];

            int capacity = model.TotalCellCount;
            m_cells = new Cell[capacity];
            m_cellStart = new int[capacity];
            m_cellCount = new int[capacity];
            m_minX = new int[capacity];
            m_maxX = new int[capacity];
            m_minY = new int[capacity];
            m_maxY = new int[capacity];
            m_color = new BlockColor[capacity];
            m_source = new int[capacity];
            m_origin = new Cell[capacity];
            m_status = new BlockStatus[capacity];

            int largest = 0;
            for (int block = 0; block < model.BlockCount; block++)
            {
                largest = Math.Max(largest, model.GetBlock(block).CellCount);
            }

            m_partLabel = new int[largest];
            m_partQueue = new int[largest];
            m_partCells = new Cell[largest];
            m_partSize = new int[k_maxSplitParts];
            m_positionAt = new int[model.Width * model.Height];
            Reset();
        }

        public LevelModel Model { get; }

        public int Width => Model.Width;

        public int Height => Model.Height;

        public int BlockCount => m_blockCount;

        public int RequiredRemaining { get; private set; }

        public Cell GetOrigin(int block) => m_origin[block];

        /// <summary>The block's status; an ID not issued yet reads as Removed, so it is never on the board.</summary>
        public BlockStatus GetStatus(int block) => m_status[block];

        public BlockFootprint GetFootprint(int block)
        {
            return new BlockFootprint(m_cells, m_cellStart[block], m_cellCount[block], m_minX[block], m_maxX[block],
                m_minY[block], m_maxY[block], m_color[block], m_source[block]);
        }

        public int GetOccupant(Cell cell)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= Model.Width || cell.Y >= Model.Height)
            {
                return Occupant.k_offBoard;
            }

            return m_occupant[cell.Y * Model.Width + cell.X];
        }

        public bool IsFloor(Cell cell) => Model.IsFloor(cell.X, cell.Y);

        public bool AllowsBoosterTarget(CommandKind kind, int block) => m_hooks.AllowsBoosterTarget(this, kind, block);

        /// <summary>
        /// Moves an on-board block to a new origin. The caller has already checked the target with
        /// MoveRules.CanOccupy; the guard only turns a broken caller into an exception.
        /// </summary>
        internal void Move(int block, Cell origin)
        {
            if (m_status[block] != BlockStatus.OnBoard)
            {
                throw new InvalidOperationException("Only an on-board block can move.");
            }

            int start = m_cellStart[block];
            int end = start + m_cellCount[block];
            for (int i = start; i < end; i++)
            {
                Cell cell = origin + m_cells[i];
                int occupant = GetOccupant(cell);
                if (!IsFloor(cell) || (occupant != Occupant.k_empty && occupant != block))
                {
                    throw new InvalidOperationException("A block moves only onto free floor inside the board.");
                }
            }

            Clear(block, m_origin[block]);
            Write(block, origin);
            m_origin[block] = origin;
        }

        /// <summary>
        /// Frees the cells of an on-board block and records how it left; a block leaves only once. Its
        /// footprint stays readable, so views can show the block leaving.
        /// </summary>
        internal void Release(int block, BlockStatus status)
        {
            if (status == BlockStatus.OnBoard)
            {
                throw new ArgumentException("Release needs an off-board status.", nameof(status));
            }

            if (m_status[block] != BlockStatus.OnBoard)
            {
                throw new InvalidOperationException("A block leaves the board only once.");
            }

            Clear(block, m_origin[block]);
            m_status[block] = status;
            RequiredRemaining--;
        }

        /// <summary>
        /// The number of 4-connected parts the block's cells would form without worldCell, without
        /// changing anything, so a handler can check its event room before it splits (B-6).
        /// </summary>
        internal int CountPartsWithout(int block, Cell worldCell)
        {
            int removed = FindRemovedCell(block, worldCell);
            return LabelParts(block, removed);
        }

        /// <summary>
        /// Removes one cell of an on-board block with at least two cells (B-4). The remaining cells are
        /// grouped into 4-connected parts and the block's slice is partitioned by part, keeping slice
        /// order inside each part. The part holding the first remaining cell in slice order keeps the
        /// block's ID; every other part becomes a new on-board block with the next ID, in the order of
        /// its first cell, with the parent's colour, source and current origin, so no world cell moves.
        /// Each new part is one more required block. Writes the new IDs, ascending, into createdIds and
        /// returns how many it created (0 when the block stays whole).
        /// </summary>
        internal int RemoveCell(int block, Cell worldCell, int[] createdIds)
        {
            if (createdIds == null || createdIds.Length < k_maxSplitChildren)
            {
                throw new ArgumentException("The ID buffer needs room for three new parts.", nameof(createdIds));
            }

            int removed = FindRemovedCell(block, worldCell);
            int parts = LabelParts(block, removed);
            if (m_blockCount + parts - 1 > m_status.Length)
            {
                throw new InvalidOperationException("The split needs more block IDs than the level's cell count.");
            }

            int start = m_cellStart[block];
            int count = m_cellCount[block];
            int written = 0;
            for (int part = 0; part < parts; part++)
            {
                m_partSize[part] = 0;
                for (int i = 0; i < count; i++)
                {
                    if (i != removed && m_partLabel[i] == part)
                    {
                        m_partCells[written] = m_cells[start + i];
                        written++;
                        m_partSize[part]++;
                    }
                }
            }

            for (int i = 0; i < written; i++)
            {
                m_cells[start + i] = m_partCells[i];
            }

            m_occupant[worldCell.Y * Model.Width + worldCell.X] = Occupant.k_empty;
            m_cellCount[block] = m_partSize[0];
            UpdateBounds(block);

            int next = start + m_partSize[0];
            for (int part = 1; part < parts; part++)
            {
                int child = m_blockCount;
                m_blockCount++;
                m_cellStart[child] = next;
                m_cellCount[child] = m_partSize[part];
                next += m_partSize[part];
                m_color[child] = m_color[block];
                m_source[child] = m_source[block];
                m_origin[child] = m_origin[block];
                m_status[child] = BlockStatus.OnBoard;
                UpdateBounds(child);
                Write(child, m_origin[child]);
                createdIds[part - 1] = child;
            }

            RequiredRemaining += parts - 1;
            return parts - 1;
        }

        /// <summary>
        /// Restores the authored layout: every authored block on board at its authored origin with its
        /// authored cells, and no Rocket part issued.
        /// </summary>
        internal void Reset()
        {
            for (int i = 0; i < m_occupant.Length; i++)
            {
                m_occupant[i] = Occupant.k_empty;
            }

            int next = 0;
            for (int block = 0; block < Model.BlockCount; block++)
            {
                BlockDefinition definition = Model.GetBlock(block);
                m_cellStart[block] = next;
                m_cellCount[block] = definition.CellCount;
                for (int i = 0; i < definition.CellCount; i++)
                {
                    m_cells[next + i] = definition.GetCell(i);
                }

                next += definition.CellCount;
                m_minX[block] = definition.MinX;
                m_maxX[block] = definition.MaxX;
                m_minY[block] = definition.MinY;
                m_maxY[block] = definition.MaxY;
                m_color[block] = definition.Color;
                m_source[block] = block;
                m_origin[block] = definition.Origin;
                m_status[block] = BlockStatus.OnBoard;
                Write(block, definition.Origin);
            }

            for (int slot = Model.BlockCount; slot < m_status.Length; slot++)
            {
                m_cellStart[slot] = 0;
                m_cellCount[slot] = 0;
                m_minX[slot] = 0;
                m_maxX[slot] = 0;
                m_minY[slot] = 0;
                m_maxY[slot] = 0;
                m_color[slot] = default;
                m_source[slot] = k_noSource;
                m_origin[slot] = Cell.Zero;
                m_status[slot] = BlockStatus.Removed;
            }

            m_blockCount = Model.BlockCount;
            RequiredRemaining = Model.BlockCount;
        }

        // The slice position of the cell a split removes; the guard only turns a broken caller into an exception.
        private int FindRemovedCell(int block, Cell worldCell)
        {
            if (block < 0 || block >= m_blockCount || m_status[block] != BlockStatus.OnBoard)
            {
                throw new InvalidOperationException("Only an on-board block can lose a cell.");
            }

            if (GetOccupant(worldCell) != block || m_cellCount[block] < 2)
            {
                throw new InvalidOperationException("A split removes a cell of the block and never its only cell.");
            }

            int start = m_cellStart[block];
            Cell relative = worldCell - m_origin[block];
            for (int i = 0; i < m_cellCount[block]; i++)
            {
                if (m_cells[start + i] == relative)
                {
                    return i;
                }
            }

            throw new InvalidOperationException("The occupancy and the block's footprint disagree.");
        }

        // Labels every cell of the block except the removed one with its 4-connected part, visiting
        // cells in slice order, so part 0 holds the first remaining cell and each later part is
        // numbered when its first cell is reached. Returns the number of parts.
        private int LabelParts(int block, int removed)
        {
            int start = m_cellStart[block];
            int count = m_cellCount[block];
            Cell origin = m_origin[block];
            Cell removedCell = origin + m_cells[start + removed];
            for (int i = 0; i < count; i++)
            {
                m_partLabel[i] = k_unlabelled;
                Cell cell = origin + m_cells[start + i];
                m_positionAt[cell.Y * Model.Width + cell.X] = i;
            }

            int parts = 0;
            for (int first = 0; first < count; first++)
            {
                if (first == removed || m_partLabel[first] != k_unlabelled)
                {
                    continue;
                }

                m_partLabel[first] = parts;
                m_partQueue[0] = first;
                int head = 0;
                int tail = 1;

                // Owner: this call. Stops: when the queue empties; each cell is queued at most once.
                while (head < tail)
                {
                    Cell cell = origin + m_cells[start + m_partQueue[head]];
                    head++;
                    tail = QueueNeighbour(block, cell + new Cell(1, 0), removedCell, parts, tail);
                    tail = QueueNeighbour(block, cell + new Cell(-1, 0), removedCell, parts, tail);
                    tail = QueueNeighbour(block, cell + new Cell(0, 1), removedCell, parts, tail);
                    tail = QueueNeighbour(block, cell + new Cell(0, -1), removedCell, parts, tail);
                }

                parts++;
            }

            return parts;
        }

        private int QueueNeighbour(int block, Cell neighbour, Cell removedCell, int part, int tail)
        {
            if (neighbour == removedCell || GetOccupant(neighbour) != block)
            {
                return tail;
            }

            int position = m_positionAt[neighbour.Y * Model.Width + neighbour.X];
            if (m_partLabel[position] != k_unlabelled)
            {
                return tail;
            }

            m_partLabel[position] = part;
            m_partQueue[tail] = position;
            return tail + 1;
        }

        private void UpdateBounds(int block)
        {
            int start = m_cellStart[block];
            int end = start + m_cellCount[block];
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = start; i < end; i++)
            {
                Cell cell = m_cells[i];
                minX = Math.Min(minX, cell.X);
                maxX = Math.Max(maxX, cell.X);
                minY = Math.Min(minY, cell.Y);
                maxY = Math.Max(maxY, cell.Y);
            }

            m_minX[block] = minX;
            m_maxX[block] = maxX;
            m_minY[block] = minY;
            m_maxY[block] = maxY;
        }

        private void Clear(int block, Cell origin)
        {
            int start = m_cellStart[block];
            int end = start + m_cellCount[block];
            for (int i = start; i < end; i++)
            {
                Cell cell = origin + m_cells[i];
                int slot = cell.Y * Model.Width + cell.X;
                if (m_occupant[slot] == block)
                {
                    m_occupant[slot] = Occupant.k_empty;
                }
            }
        }

        private void Write(int block, Cell origin)
        {
            int start = m_cellStart[block];
            int end = start + m_cellCount[block];
            for (int i = start; i < end; i++)
            {
                Cell cell = origin + m_cells[i];
                m_occupant[cell.Y * Model.Width + cell.X] = block;
            }
        }
    }
}
