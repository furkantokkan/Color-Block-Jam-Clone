using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>Reads a block's current footprint into arrays, so tests compare exact cell lists in slice order.</summary>
    public static class FootprintReader
    {
        /// <summary>The block's board cells: its origin plus each footprint cell.</summary>
        public static Cell[] GetWorldCells(IBoardReader board, int block)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            Cell origin = board.GetOrigin(block);
            Cell[] cells = new Cell[footprint.CellCount];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = origin + footprint.GetCell(i);
            }

            return cells;
        }
    }
}
