using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// A read-only view of one block's current footprint in an attempt: its cells relative to its
    /// origin, their extents, its colour and the authored block it came from (B-3). It wraps the
    /// board's cell buffer instead of copying it, so reading a footprint allocates nothing; it stays
    /// valid until the board next changes.
    /// </summary>
    public readonly struct BlockFootprint
    {
        private readonly Cell[] m_cells;
        private readonly int m_start;

        internal BlockFootprint(Cell[] cells, int start, int count, int minX, int maxX, int minY, int maxY,
            BlockColor color, int source)
        {
            m_cells = cells;
            m_start = start;
            CellCount = count;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            Color = color;
            Source = source;
        }

        public int CellCount { get; }

        public int MinX { get; }

        public int MaxX { get; }

        public int MinY { get; }

        public int MaxY { get; }

        public BlockColor Color { get; }

        /// <summary>The authored block index this block is, or was split from; -1 for an ID not issued yet.</summary>
        public int Source { get; }

        /// <summary>Returns footprint cell i relative to the block's origin.</summary>
        public Cell GetCell(int index)
        {
            if (index < 0 || index >= CellCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return m_cells[m_start + index];
        }

        /// <summary>The footprint length along the side's outward axis: how far the block travels to clear that side.</summary>
        public int Depth(Side side) => SideMath.RunsAlongX(side) ? MaxY - MinY + 1 : MaxX - MinX + 1;
    }
}
