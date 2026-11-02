using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// The Barrier board object rule (catalog OBJ-05, XP-3 and XP-5): a closed barrier's cells refuse every block that
    /// does not already stand on them (DEC-MECH-18), an open barrier's cells are plain floor, and every barrier flips
    /// once for each cleared block, a door exit that leaves the board or a booster removal, so a Vacuum of N blocks
    /// flips it N times (DEC-MECH-3). It reads occupancy only from the reader it is asked with, so the drag preview, a
    /// move command and a solver get the same answer, and a Rocket part needs no state of its own (DEC-146). One
    /// instance belongs to one attempt; its arrays are copied at construction and a call allocates nothing.
    /// </summary>
    public sealed class BarrierRule : ICellGate, IBoardEventListener
    {
        /// <summary>GetBarrierAt's answer for a cell no barrier covers, or a cell outside the board.</summary>
        public const int k_noBarrier = -1;

        private readonly int m_width;
        private readonly int m_height;
        // Per board cell (y * width + x): the index of the barrier covering it, or k_noBarrier.
        private readonly int[] m_cellBarriers;
        private readonly bool[] m_open;
        private readonly int[] m_versions;

        /// <param name="width">The board width in cells.</param>
        /// <param name="height">The board height in cells.</param>
        /// <param name="barrierCells">Per barrier, its board cells; a cell belongs to one barrier at most.</param>
        /// <param name="startsOpen">Per barrier, its authored start state.</param>
        public BarrierRule(int width, int height, Cell[][] barrierCells, bool[] startsOpen)
        {
            if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "The board needs a positive width.");
            if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "The board needs a positive height.");
            if (barrierCells == null) throw new ArgumentNullException(nameof(barrierCells));
            if (startsOpen == null) throw new ArgumentNullException(nameof(startsOpen));
            if (barrierCells.Length != startsOpen.Length)
                throw new ArgumentException("Every barrier needs one start state.", nameof(startsOpen));

            m_width = width;
            m_height = height;
            m_cellBarriers = new int[width * height];
            for (int index = 0; index < m_cellBarriers.Length; index++) m_cellBarriers[index] = k_noBarrier;
            for (int barrier = 0; barrier < barrierCells.Length; barrier++)
            {
                Cell[] cells = barrierCells[barrier];
                if (cells == null || cells.Length == 0)
                    throw new ArgumentException("Every barrier needs at least one cell.", nameof(barrierCells));
                for (int i = 0; i < cells.Length; i++)
                {
                    Cell cell = cells[i];
                    if ((uint)cell.X >= (uint)width || (uint)cell.Y >= (uint)height)
                        throw new ArgumentOutOfRangeException(nameof(barrierCells), "A barrier cell lies outside the board.");
                    int index = cell.Y * width + cell.X;
                    if (m_cellBarriers[index] != k_noBarrier)
                        throw new ArgumentException("A board cell belongs to one barrier at most.", nameof(barrierCells));
                    m_cellBarriers[index] = barrier;
                }
            }

            m_open = (bool[])startsOpen.Clone();
            m_versions = new int[startsOpen.Length];
        }

        public int BarrierCount => m_open.Length;

        public bool IsOpen(int barrier) => m_open[barrier];

        /// <summary>The flips of this barrier since the attempt began; every version step is exactly one flip.</summary>
        public int GetVersion(int barrier) => m_versions[barrier];

        /// <summary>The index of the barrier covering the cell, or k_noBarrier.</summary>
        public int GetBarrierAt(Cell cell)
        {
            if ((uint)cell.X >= (uint)m_width || (uint)cell.Y >= (uint)m_height) return k_noBarrier;
            return m_cellBarriers[cell.Y * m_width + cell.X];
        }

        /// <summary>
        /// A closed barrier cell admits only the block the reader already shows on it, so a barrier closing under a
        /// block keeps that block in place and lets it leave, and no other block enters until it opens (DEC-MECH-18).
        /// </summary>
        public bool AllowsCell(IBoardReader board, int block, Cell cell)
        {
            int barrier = GetBarrierAt(cell);
            return barrier == k_noBarrier || m_open[barrier] || board.GetOccupant(cell) == block;
        }

        /// <summary>A door exit clears the block only when it leaves the board; a Stay outcome keeps it (no flip).</summary>
        public void OnBlockExited(in BlockExitedEvent e)
        {
            if (e.Outcome == ExitOutcome.Leave) Flip();
        }

        /// <summary>Hammer raises one removal and Vacuum one per removed block, so each removal flips once (DEC-147).</summary>
        public void OnBlockRemoved(in BlockRemovedEvent e) => Flip();

        public void OnMoveCommitted(in MoveCommittedEvent e) { }

        // A Rocket split is neither an exit nor a removal; its parts' new IDs occupy their cells on the reader.
        public void OnFootprintChanged(in FootprintChangedEvent e) { }

        private void Flip()
        {
            for (int barrier = 0; barrier < m_open.Length; barrier++)
            {
                m_open[barrier] = !m_open[barrier];
                m_versions[barrier]++;
            }
        }
    }
}
