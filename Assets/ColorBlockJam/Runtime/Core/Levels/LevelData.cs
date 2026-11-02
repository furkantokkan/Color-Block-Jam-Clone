using System;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    public enum BoardSide
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>One authored gate span on a board edge with its colour and effects.</summary>
    [Serializable]
    public sealed class LevelDoorData
    {
        [SerializeField] private string m_id;
        [SerializeField] private BoardSide m_side;
        [SerializeField] private int m_start;
        [SerializeField, Min(1)] private int m_length = 1;
        [SerializeField] private string m_colorId;
        [SerializeField] private LevelEffectData[] m_effects = Array.Empty<LevelEffectData>();

        public string Id => m_id;
        public BoardSide Side => m_side;
        public int Start => m_start;
        public int Length => m_length;
        public string ColorId => m_colorId;
        public int EffectCount => m_effects?.Length ?? 0;

        public LevelDoorData()
        {
        }

        public LevelDoorData(string id, BoardSide side, int start, int length, string colorId)
            : this(id, side, start, length, colorId, Array.Empty<LevelEffectData>())
        {
        }

        public LevelDoorData(string id, BoardSide side, int start, int length, string colorId,
            LevelEffectData[] effects)
        {
            m_id = id;
            m_side = side;
            m_start = start;
            m_length = length;
            m_colorId = colorId;
            m_effects = LevelEffectData.CloneAll(effects);
        }

        public LevelEffectData GetEffectAt(int index)
        {
            return m_effects[index];
        }

        /// <summary>The typed parameters of this door's effect of type T; no door effect type is registered (DEC-177).</summary>
        public bool TryGetEffect<T>(out T parameters) where T : LevelEffectParameters
        {
            return LevelEffectData.TryGet(m_effects, out parameters);
        }

        public LevelDoorData Clone()
        {
            return new LevelDoorData(m_id, m_side, m_start, m_length, m_colorId, m_effects);
        }
    }

    /// <summary>
    /// One authored block: colour, origin, relative cells and effects. Its piece is the ShapeKey of its cells, which
    /// nothing else names or stores (DEC-193).
    /// </summary>
    [Serializable]
    public sealed class LevelBlockData
    {
        [SerializeField] private string m_id;
        [SerializeField] private string m_colorId;
        [SerializeField] private Vector2Int m_origin;
        [SerializeField] private Vector2Int[] m_cells = { Vector2Int.zero };
        [SerializeField] private LevelEffectData[] m_effects = Array.Empty<LevelEffectData>();

        public string Id => m_id;
        public string ColorId => m_colorId;
        public Vector2Int Origin => m_origin;
        public int CellCount => m_cells?.Length ?? 0;
        public int EffectCount => m_effects?.Length ?? 0;

        /// <summary>
        /// The block's piece: the ShapeKey of its cells, or the default, invalid key when they break a piece limit.
        /// Computed on every access, because Unity writes the serialized cells without a constructor (Level Editor
        /// drafts, Undo); allocation-free.
        /// </summary>
        public ShapeKey Key => ShapeKey.TryCreate(m_cells, out ShapeKey key) ? key : default;

        public LevelBlockData()
        {
        }

        public LevelBlockData(
            string id,
            string colorId,
            Vector2Int origin,
            Vector2Int[] cells)
            : this(id, colorId, origin, cells, Array.Empty<LevelEffectData>())
        {
        }

        public LevelBlockData(
            string id,
            string colorId,
            Vector2Int origin,
            Vector2Int[] cells,
            LevelEffectData[] effects)
        {
            m_id = id;
            m_colorId = colorId;
            m_origin = origin;
            m_cells = cells == null ? null : (Vector2Int[])cells.Clone();
            m_effects = LevelEffectData.CloneAll(effects);
        }

        public Vector2Int GetCellAt(int index)
        {
            return m_cells[index];
        }

        public LevelEffectData GetEffectAt(int index)
        {
            return m_effects[index];
        }

        /// <summary>The typed parameters of this block's effect of type T, for example ArrowEffect.</summary>
        public bool TryGetEffect<T>(out T parameters) where T : LevelEffectParameters
        {
            return LevelEffectData.TryGet(m_effects, out parameters);
        }

        public LevelBlockData Clone()
        {
            return new LevelBlockData(m_id, m_colorId, m_origin, m_cells, m_effects);
        }

        /// <summary>The piece limits for these cells (ShapeKey.TryValidate): false with the designer sentence when one breaks.</summary>
        internal bool ValidateKey(out string issue)
        {
            return ShapeKey.TryValidate(m_cells, out issue);
        }
    }

    /// <summary>
    /// One level parsed from the canonical JSON. Game attempts and editor
    /// drafts use deep copies, so their changes cannot alter the source.
    /// </summary>
    [Serializable]
    public sealed class LevelData
    {
        // Gates span 1 to 3 cells (user decision 2026-09-25).
        public const int MaxDoorLength = 3;

        // Board and timer limits (AD-15): the Range attributes, Validate and its messages below share these
        // with the Level Editor's LevelBoardResize and LevelDraftIssues, so the two sides cannot drift.
        public const int MinBoardSize = 3;
        public const int MaxBoardWidth = 8;
        public const int MaxBoardHeight = 10;
        public const int MinTimerSeconds = 10;
        public const int MaxTimerSeconds = 300;

        [SerializeField] private string m_id;
        [SerializeField] private string m_name;
        [SerializeField, Range(MinBoardSize, MaxBoardWidth)] private int m_width = 3;
        [SerializeField, Range(MinBoardSize, MaxBoardHeight)] private int m_height = 3;
        [SerializeField, Range(MinTimerSeconds, MaxTimerSeconds)] private int m_timerSeconds = 60;
        [SerializeField] private Vector2Int[] m_voidCells = Array.Empty<Vector2Int>();
        [SerializeField] private LevelDoorData[] m_doors = Array.Empty<LevelDoorData>();
        [SerializeField] private LevelBlockData[] m_blocks = Array.Empty<LevelBlockData>();
        [SerializeField] private LevelCellObjectData[] m_cellObjects = Array.Empty<LevelCellObjectData>();
        [SerializeField] private LevelPieceName[] m_pieceNames = Array.Empty<LevelPieceName>();

        public string Id => m_id;
        public string Name => m_name;
        public int Width => m_width;
        public int Height => m_height;
        public int TimerSeconds => m_timerSeconds;
        public int VoidCellCount => m_voidCells?.Length ?? 0;
        public int DoorCount => m_doors?.Length ?? 0;
        public int BlockCount => m_blocks?.Length ?? 0;
        public int CellObjectCount => m_cellObjects?.Length ?? 0;

        /// <summary>Names of the level's pieces that the piece library lacks (DEC-193); the runtime only validates them.</summary>
        public int PieceNameCount => m_pieceNames?.Length ?? 0;

        public LevelData()
        {
        }

        public LevelData(
            string id,
            string name,
            int width,
            int height,
            int timerSeconds,
            LevelDoorData[] doors,
            LevelBlockData[] blocks)
            : this(id, name, width, height, timerSeconds, Array.Empty<Vector2Int>(), doors, blocks,
                Array.Empty<LevelCellObjectData>())
        {
        }

        public LevelData(
            string id,
            string name,
            int width,
            int height,
            int timerSeconds,
            Vector2Int[] voidCells,
            LevelDoorData[] doors,
            LevelBlockData[] blocks,
            LevelCellObjectData[] cellObjects)
            : this(id, name, width, height, timerSeconds, voidCells, doors, blocks, cellObjects,
                Array.Empty<LevelPieceName>())
        {
        }

        public LevelData(
            string id,
            string name,
            int width,
            int height,
            int timerSeconds,
            Vector2Int[] voidCells,
            LevelDoorData[] doors,
            LevelBlockData[] blocks,
            LevelCellObjectData[] cellObjects,
            LevelPieceName[] pieceNames)
        {
            m_id = id;
            m_name = name;
            m_width = width;
            m_height = height;
            m_timerSeconds = timerSeconds;
            m_voidCells = voidCells == null ? null : (Vector2Int[])voidCells.Clone();
            m_doors = CloneDoors(doors);
            m_blocks = CloneBlocks(blocks);
            m_cellObjects = CloneCellObjects(cellObjects);
            m_pieceNames = ClonePieceNames(pieceNames);
        }

        public Vector2Int GetVoidCellAt(int index)
        {
            return m_voidCells[index];
        }

        public LevelDoorData GetDoorAt(int index)
        {
            return m_doors[index];
        }

        public LevelBlockData GetBlockAt(int index)
        {
            return m_blocks[index];
        }

        public LevelCellObjectData GetCellObjectAt(int index)
        {
            return m_cellObjects[index];
        }

        public LevelPieceName GetPieceNameAt(int index)
        {
            return m_pieceNames[index];
        }

        public LevelData Clone()
        {
            return new LevelData(m_id, m_name, m_width, m_height, m_timerSeconds, m_voidCells, m_doors, m_blocks,
                m_cellObjects, m_pieceNames);
        }

        /// <summary>
        /// Authoring validation: structure, void cells, doors, blocks (each a piece, DEC-193), cell objects, piece
        /// names and every effect. It accepts every catalog type, implemented in gameplay or not
        /// (docs/Level-Data-Format.md).
        /// </summary>
        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(m_id) || string.IsNullOrWhiteSpace(m_name))
            {
                error = "Level ID and name are required.";
                return false;
            }

            if (m_width < MinBoardSize || m_width > MaxBoardWidth || m_height < MinBoardSize || m_height > MaxBoardHeight)
            {
                error = $"Level {m_id} has a board size outside {MinBoardSize}-{MaxBoardWidth} by {MinBoardSize}-{MaxBoardHeight}.";
                return false;
            }

            if (m_timerSeconds < MinTimerSeconds || m_timerSeconds > MaxTimerSeconds)
            {
                error = $"Level {m_id} has a timer outside {MinTimerSeconds}-{MaxTimerSeconds} seconds.";
                return false;
            }

            if (m_doors == null)
            {
                error = $"Level {m_id} has no door list.";
                return false;
            }

            if (m_blocks == null || m_blocks.Length == 0)
            {
                error = $"Level {m_id} needs at least one block.";
                return false;
            }

            if (!LevelStructureValidator.ValidateVoidCells(this, out bool[] floor, out error)
                || !LevelStructureValidator.ValidateDoors(this, floor, out error)
                || !LevelStructureValidator.ValidateBlocks(this, floor, out error)
                || !LevelStructureValidator.ValidateCellObjects(this, floor, out error)
                || !LevelStructureValidator.ValidatePieceNames(this, out error)
                || !LevelEffectValidator.Validate(this, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Project-scope check after Validate: refuses effects and board objects whose gameplay rule
        /// is not implemented yet, so no level loads with something that silently does nothing.
        /// </summary>
        public bool ValidateRuntimeSupport(out string error)
        {
            return LevelEffectValidator.ValidateRuntimeSupport(this, out error);
        }

        /// <summary>True when the cell is inside the board and not a void cell.</summary>
        public bool IsFloor(Vector2Int cell)
        {
            if (cell.x < 0 || cell.x >= m_width || cell.y < 0 || cell.y >= m_height)
            {
                return false;
            }

            for (int index = 0; index < VoidCellCount; index++)
            {
                if (m_voidCells[index] == cell)
                {
                    return false;
                }
            }

            return true;
        }

        internal LevelBlockData FindBlock(string id)
        {
            for (int index = 0; index < BlockCount; index++)
            {
                LevelBlockData block = m_blocks[index];
                if (block != null && string.Equals(block.Id, id, StringComparison.Ordinal))
                {
                    return block;
                }
            }

            return null;
        }

        internal LevelDoorData FindDoor(string id)
        {
            for (int index = 0; index < DoorCount; index++)
            {
                LevelDoorData door = m_doors[index];
                if (door != null && string.Equals(door.Id, id, StringComparison.Ordinal))
                {
                    return door;
                }
            }

            return null;
        }

        internal LevelCellObjectData FindCellObject(string id)
        {
            for (int index = 0; index < CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = m_cellObjects[index];
                if (cellObject != null && string.Equals(cellObject.Id, id, StringComparison.Ordinal))
                {
                    return cellObject;
                }
            }

            return null;
        }

        /// <summary>The board cell a door segment opens onto (handoff-voidcells-schema.md rule 6).</summary>
        internal static Vector2Int GetDoorCell(BoardSide side, int position, int width, int height)
        {
            switch (side)
            {
                case BoardSide.Top: return new Vector2Int(position, height - 1);
                case BoardSide.Bottom: return new Vector2Int(position, 0);
                case BoardSide.Left: return new Vector2Int(0, position);
                default: return new Vector2Int(width - 1, position);
            }
        }

        private static LevelDoorData[] CloneDoors(LevelDoorData[] doors)
        {
            if (doors == null)
            {
                return null;
            }

            LevelDoorData[] copy = new LevelDoorData[doors.Length];
            for (int index = 0; index < doors.Length; index++)
            {
                copy[index] = doors[index]?.Clone();
            }

            return copy;
        }

        private static LevelBlockData[] CloneBlocks(LevelBlockData[] blocks)
        {
            if (blocks == null)
            {
                return null;
            }

            LevelBlockData[] copy = new LevelBlockData[blocks.Length];
            for (int index = 0; index < blocks.Length; index++)
            {
                copy[index] = blocks[index]?.Clone();
            }

            return copy;
        }

        private static LevelCellObjectData[] CloneCellObjects(LevelCellObjectData[] cellObjects)
        {
            if (cellObjects == null)
            {
                return null;
            }

            LevelCellObjectData[] copy = new LevelCellObjectData[cellObjects.Length];
            for (int index = 0; index < cellObjects.Length; index++)
            {
                copy[index] = cellObjects[index]?.Clone();
            }

            return copy;
        }

        private static LevelPieceName[] ClonePieceNames(LevelPieceName[] pieceNames)
        {
            if (pieceNames == null)
            {
                return null;
            }

            LevelPieceName[] copy = new LevelPieceName[pieceNames.Length];
            for (int index = 0; index < pieceNames.Length; index++)
            {
                copy[index] = pieceNames[index]?.Clone();
            }

            return copy;
        }
    }
}
