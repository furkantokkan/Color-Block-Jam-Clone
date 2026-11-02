using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Asks the Domain rules once per block and origin and caches the answers as cell masks, so the search
    /// never re-implements a rule (AR-10). On a board that holds only that block it records whether the block
    /// may stand at the origin (MoveRules.CanOccupy), whether each side's exit fits (ExitRules.Evaluate), and
    /// for each fitting exit the cells that must stay empty, found by probing: a one-cell block is placed on
    /// each other floor cell and the cell is kept when the verdict turns into PathBlocked. The per-block Ice
    /// counts and Arrow axes are kept beside the answers for LevelSolverMoves, which applies them.
    /// </summary>
    internal sealed class LevelSolverTables
    {
        public const int DirectionCount = 4;
        public const int SideCount = 4;

        // The probe block's colour matches no palette entry; the probe is never asked about itself.
        private static readonly BlockColor s_probeColor = new BlockColor(-1);

        private readonly LevelModel m_model;
        private readonly int m_cellCount;
        private readonly int[] m_authoredSlot;
        private readonly int[] m_iceCounts;
        private readonly AxisMask[] m_axes;

        // Indexed [block * cellCount + slot], and [(block * cellCount + slot) * 4 + side or direction].
        private readonly bool[] m_standing;
        private readonly CellMask[] m_footprint;
        private readonly int[] m_slideTarget;
        private readonly bool[] m_exitFits;
        private readonly CellMask[] m_swept;

        private LevelSolverTables(LevelModel model, int[] iceCounts, AxisMask[] axes)
        {
            m_model = model;
            m_cellCount = model.Width * model.Height;
            int blockCount = model.BlockCount;
            m_authoredSlot = new int[blockCount];
            m_iceCounts = (int[])iceCounts.Clone();
            m_axes = (AxisMask[])axes.Clone();
            m_standing = new bool[blockCount * m_cellCount];
            m_footprint = new CellMask[blockCount * m_cellCount];
            m_slideTarget = new int[blockCount * m_cellCount * DirectionCount];
            m_exitFits = new bool[blockCount * m_cellCount * SideCount];
            m_swept = new CellMask[blockCount * m_cellCount * SideCount];
        }

        public int Width => m_model.Width;

        public int Height => m_model.Height;

        public int BlockCount => m_model.BlockCount;

        public LevelModel Model => m_model;

        /// <summary>
        /// Builds the tables for a model whose boards fit a CellMask. iceCounts holds each block's authored
        /// clearsToThaw, 0 for a block without Ice; axes holds each block's movement axes, Horizontal or Vertical
        /// for an Arrow block and Both for any other (the masks Gameplay's Arrow hooks receive).
        /// </summary>
        public static LevelSolverTables Create(LevelModel model, int[] iceCounts, AxisMask[] axes)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model));
            }

            if (iceCounts == null || iceCounts.Length != model.BlockCount)
            {
                throw new ArgumentException("One Ice count per block is required.", nameof(iceCounts));
            }

            if (axes == null || axes.Length != model.BlockCount)
            {
                throw new ArgumentException("One axis mask per block is required.", nameof(axes));
            }

            for (int block = 0; block < axes.Length; block++)
            {
                if (axes[block] != AxisMask.Horizontal && axes[block] != AxisMask.Vertical && axes[block] != AxisMask.Both)
                {
                    throw new ArgumentException($"Block {block} has no movement axis.", nameof(axes));
                }
            }

            if (model.Width * model.Height > CellMask.Capacity)
            {
                throw new ArgumentException("The board has more cells than a cell mask holds.", nameof(model));
            }

            LevelSolverTables tables = new LevelSolverTables(model, iceCounts, axes);
            bool[] floor = CreateFloor(model);
            DoorDefinition[] doors = CreateDoors(model);
            AttemptHooks hooks = AttemptHooks.CreateEmpty();
            for (int block = 0; block < model.BlockCount; block++)
            {
                tables.FillBlock(block, floor, doors, hooks);
            }

            return tables;
        }

        public int GetIceCount(int block) => m_iceCounts[block];

        /// <summary>The block's movement axes, fixed for the attempt (DEC-MECH-4): Both for a block without Arrow.</summary>
        public AxisMask GetAxes(int block) => m_axes[block];

        public int GetAuthoredSlot(int block) => m_authoredSlot[block];

        /// <summary>The slot of an origin: the cell index of its footprint box's bottom-left corner, or -1 off the board.</summary>
        public int GetSlot(int block, Cell origin)
        {
            BlockDefinition definition = m_model.GetBlock(block);
            int left = origin.X + definition.MinX;
            int bottom = origin.Y + definition.MinY;
            if (left < 0 || bottom < 0 || origin.X + definition.MaxX >= Width || origin.Y + definition.MaxY >= Height)
            {
                return -1;
            }

            return bottom * Width + left;
        }

        public Cell GetOrigin(int block, int slot)
        {
            BlockDefinition definition = m_model.GetBlock(block);
            return new Cell(slot % Width - definition.MinX, slot / Width - definition.MinY);
        }

        public bool IsStanding(int block, int slot) => m_standing[block * m_cellCount + slot];

        public CellMask GetFootprint(int block, int slot) => m_footprint[block * m_cellCount + slot];

        /// <summary>The slot one cell away in a direction (+X, -X, +Y, -Y), or -1 when the footprint box leaves the board.</summary>
        public int GetSlideTarget(int block, int slot, int direction)
        {
            return m_slideTarget[(block * m_cellCount + slot) * DirectionCount + direction];
        }

        /// <summary>True when the block, alone on the board at this slot, fits the door on the side.</summary>
        public bool ExitFits(int block, int slot, Side side) => m_exitFits[(block * m_cellCount + slot) * SideCount + (int)side];

        /// <summary>The cells another block must not hold for a fitting exit on the side.</summary>
        public CellMask GetSwept(int block, int slot, Side side) => m_swept[(block * m_cellCount + slot) * SideCount + (int)side];

        /// <summary>The fixed direction order +X, -X, +Y, -Y (the order MoveReach uses).</summary>
        public static Cell GetDirection(int direction)
        {
            switch (direction)
            {
                case 0: return new Cell(1, 0);
                case 1: return new Cell(-1, 0);
                case 2: return new Cell(0, 1);
                default: return new Cell(0, -1);
            }
        }

        /// <summary>The axis GetDirection's slide changes: Horizontal for 0 and 1 (+X, -X), Vertical for 2 and 3 (+Y, -Y).</summary>
        public static AxisMask GetAxis(int direction)
        {
            return direction < 2 ? AxisMask.Horizontal : AxisMask.Vertical;
        }

        private void FillBlock(int block, bool[] floor, DoorDefinition[] doors, AttemptHooks hooks)
        {
            BlockDefinition definition = m_model.GetBlock(block);
            Cell[] cells = GetCells(definition);
            m_authoredSlot[block] = GetSlot(block, definition.Origin);

            // The block alone at its authored origin: CanOccupy then asks only about floor and the board edge.
            BoardState alone = CreateBoard(floor, doors, definition.Color, definition.Origin, cells, -1);
            for (int slot = 0; slot < m_cellCount; slot++)
            {
                int index = block * m_cellCount + slot;
                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    m_slideTarget[index * DirectionCount + direction] = -1;
                }

                Cell origin = GetOrigin(block, slot);
                if (GetSlot(block, origin) != slot)
                {
                    continue;
                }

                for (int direction = 0; direction < DirectionCount; direction++)
                {
                    m_slideTarget[index * DirectionCount + direction] = GetSlot(block, origin + GetDirection(direction));
                }

                if (!MoveRules.CanOccupy(alone, hooks, 0, origin))
                {
                    continue;
                }

                m_standing[index] = true;
                m_footprint[index] = GetMask(origin, cells);
                FillExits(index, floor, doors, hooks, definition.Color, origin, cells);
            }
        }

        // The exit questions use a board showing the block at the origin, as MoveBlockHandler's target view does.
        private void FillExits(int index, bool[] floor, DoorDefinition[] doors, AttemptHooks hooks, BlockColor color,
            Cell origin, Cell[] cells)
        {
            BoardState atOrigin = CreateBoard(floor, doors, color, origin, cells, -1);
            CellMask footprint = m_footprint[index];
            for (int side = 0; side < SideCount; side++)
            {
                if (ExitRules.Evaluate(atOrigin, hooks, 0, origin, (Side)side, out _) != ExitVerdict.Fits)
                {
                    continue;
                }

                int exitIndex = index * SideCount + side;
                m_exitFits[exitIndex] = true;
                CellMask swept = CellMask.Empty;
                for (int cell = 0; cell < m_cellCount; cell++)
                {
                    if (!floor[cell] || footprint.Contains(cell))
                    {
                        continue;
                    }

                    BoardState probe = CreateBoard(floor, doors, color, origin, cells, cell);
                    if (probe != null
                        && ExitRules.Evaluate(probe, hooks, 0, origin, (Side)side, out _) == ExitVerdict.PathBlocked)
                    {
                        swept = swept.With(cell);
                    }
                }

                m_swept[exitIndex] = swept;
            }
        }

        // A board with the block (index 0) at origin and, when probeCell >= 0, a one-cell block (index 1) there.
        // Returns null when the Domain refuses that board; the caller then skips the probe.
        private BoardState CreateBoard(bool[] floor, DoorDefinition[] doors, BlockColor color, Cell origin, Cell[] cells,
            int probeCell)
        {
            BlockDefinition[] blocks = probeCell < 0
                ? new[] { new BlockDefinition(0, color, origin, cells) }
                : new[]
                {
                    new BlockDefinition(0, color, origin, cells),
                    new BlockDefinition(1, s_probeColor, new Cell(probeCell % Width, probeCell / Width), new[] { Cell.Zero })
                };
            if (!LevelModel.TryCreate(Width, Height, floor, doors, blocks, m_model.TimerSeconds, out LevelModel model, out string error))
            {
                if (probeCell >= 0)
                {
                    return null;
                }

                throw new InvalidOperationException("A single-block board was refused: " + error);
            }

            return new BoardState(model);
        }

        private CellMask GetMask(Cell origin, Cell[] cells)
        {
            CellMask mask = CellMask.Empty;
            for (int i = 0; i < cells.Length; i++)
            {
                Cell cell = origin + cells[i];
                mask = mask.With(cell.Y * Width + cell.X);
            }

            return mask;
        }

        private static Cell[] GetCells(BlockDefinition definition)
        {
            Cell[] cells = new Cell[definition.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = definition.GetCell(i);
            }

            return cells;
        }

        private static bool[] CreateFloor(LevelModel model)
        {
            bool[] floor = new bool[model.Width * model.Height];
            for (int y = 0; y < model.Height; y++)
            {
                for (int x = 0; x < model.Width; x++)
                {
                    floor[y * model.Width + x] = model.IsFloor(x, y);
                }
            }

            return floor;
        }

        private static DoorDefinition[] CreateDoors(LevelModel model)
        {
            DoorDefinition[] doors = new DoorDefinition[model.DoorCount];
            for (int i = 0; i < doors.Length; i++)
            {
                doors[i] = model.GetDoor(i);
            }

            return doors;
        }
    }
}
