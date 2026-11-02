using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// One authored block: its colour, starting origin and footprint cells relative to that origin,
    /// with the footprint extents precomputed so rules never scan for them during play.
    /// </summary>
    public sealed class BlockDefinition
    {
        private readonly Cell[] m_cells;

        public BlockDefinition(int index, BlockColor color, Cell origin, Cell[] cells)
        {
            if (cells == null || cells.Length == 0)
            {
                throw new ArgumentException("A block needs at least one cell.", nameof(cells));
            }

            Index = index;
            Color = color;
            Origin = origin;
            m_cells = (Cell[])cells.Clone();

            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < m_cells.Length; i++)
            {
                Cell cell = m_cells[i];
                minX = Math.Min(minX, cell.X);
                maxX = Math.Max(maxX, cell.X);
                minY = Math.Min(minY, cell.Y);
                maxY = Math.Max(maxY, cell.Y);
            }

            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
        }

        /// <summary>The authored order of the block; it is also the stable pick tie id (DEC-115).</summary>
        public int Index { get; }

        public BlockColor Color { get; }

        /// <summary>The authored origin where every attempt starts.</summary>
        public Cell Origin { get; }

        public int CellCount => m_cells.Length;

        public int MinX { get; }

        public int MaxX { get; }

        public int MinY { get; }

        public int MaxY { get; }

        /// <summary>Returns footprint cell i relative to the origin.</summary>
        public Cell GetCell(int index) => m_cells[index];

        /// <summary>The footprint length along the side's outward axis: how far the block travels to clear that side.</summary>
        public int Depth(Side side) => SideMath.RunsAlongX(side) ? MaxY - MinY + 1 : MaxX - MinX + 1;
    }
}
