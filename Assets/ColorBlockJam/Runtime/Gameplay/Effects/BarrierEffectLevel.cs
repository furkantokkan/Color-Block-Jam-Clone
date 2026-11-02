using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Immutable Barrier board objects of one level (catalog OBJ-05), in authored order: each barrier's cell-object ID,
    /// board cells and start state, copied on Home. It never changes the level it read, and CreateRule gives each
    /// attempt fresh rule state, so Restart returns every barrier to its start state.
    /// </summary>
    public sealed class BarrierEffectLevel : IBlockEffectLevel
    {
        private readonly string[] m_ids;
        private readonly Cell[][] m_cells;
        private readonly bool[] m_startsOpen;

        /// <param name="blockCount">The authored block count, which effect metadata reports for its level.</param>
        /// <param name="width">The board width in cells.</param>
        /// <param name="height">The board height in cells.</param>
        /// <param name="ids">Per barrier, its cell-object ID, which the play area shows it under.</param>
        /// <param name="cells">Per barrier, its board cells.</param>
        /// <param name="startsOpen">Per barrier, its authored start state.</param>
        public BarrierEffectLevel(int blockCount, int width, int height, string[] ids, Cell[][] cells, bool[] startsOpen)
        {
            if (blockCount < 0) throw new ArgumentOutOfRangeException(nameof(blockCount));
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (startsOpen == null) throw new ArgumentNullException(nameof(startsOpen));
            if (cells.Length != ids.Length || startsOpen.Length != ids.Length)
                throw new ArgumentException("Every barrier needs one ID, one cell list and one start state.", nameof(ids));

            m_ids = (string[])ids.Clone();
            m_cells = new Cell[cells.Length][];
            for (int barrier = 0; barrier < cells.Length; barrier++)
            {
                if (string.IsNullOrEmpty(m_ids[barrier]))
                    throw new ArgumentException("Every barrier needs an ID.", nameof(ids));
                m_cells[barrier] = cells[barrier] != null
                    ? (Cell[])cells[barrier].Clone()
                    : throw new ArgumentException("Every barrier needs its cells.", nameof(cells));
            }

            m_startsOpen = (bool[])startsOpen.Clone();
            BlockCount = blockCount;
            Width = width;
            Height = height;
            // Validates the board size and every cell once, on Home, so no attempt can fail later.
            CreateRule();
        }

        public string TypeId => "barrier";
        public int BlockCount { get; }
        public int Width { get; }
        public int Height { get; }
        public int BarrierCount => m_ids.Length;

        public string GetId(int barrier) => m_ids[barrier];
        public bool GetStartsOpen(int barrier) => m_startsOpen[barrier];
        public int GetCellCount(int barrier) => m_cells[barrier].Length;
        public Cell GetCell(int barrier, int index) => m_cells[barrier][index];

        /// <summary>
        /// Copies the level's Barrier cell objects (schema v2: CellObjectCount, GetCellObjectAt, TryGetEffect of
        /// BarrierObject). Typed parameters convert on first access, so this runs on Home only; other board objects are
        /// ignored and the level is never changed.
        /// </summary>
        public static BarrierEffectLevel Create(LevelData authored)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            int count = 0;
            for (int index = 0; index < authored.CellObjectCount; index++)
            {
                if (authored.GetCellObjectAt(index).TryGetEffect(out BarrierObject _)) count++;
            }

            string[] ids = new string[count];
            Cell[][] cells = new Cell[count][];
            bool[] startsOpen = new bool[count];
            int barrier = 0;
            for (int index = 0; index < authored.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = authored.GetCellObjectAt(index);
                if (!cellObject.TryGetEffect(out BarrierObject parameters)) continue;
                ids[barrier] = cellObject.Id;
                startsOpen[barrier] = parameters.StartsOpen;
                Cell[] objectCells = new Cell[cellObject.CellCount];
                for (int cell = 0; cell < objectCells.Length; cell++)
                {
                    Vector2Int authoredCell = cellObject.GetCellAt(cell);
                    // The converter maps authored cells to Domain cells one to one (AttemptLevelConverter).
                    objectCells[cell] = new Cell(authoredCell.x, authoredCell.y);
                }

                cells[barrier++] = objectCells;
            }

            return new BarrierEffectLevel(authored.BlockCount, authored.Width, authored.Height, ids, cells, startsOpen);
        }

        /// <summary>A fresh rule with every barrier at its start state, for one attempt.</summary>
        public BarrierRule CreateRule() => new BarrierRule(Width, Height, m_cells, m_startsOpen);
    }
}
