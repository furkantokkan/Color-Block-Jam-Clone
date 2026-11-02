using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A cell gate that forbids one cell to every block.</summary>
    public sealed class DenyCellGate : ICellGate
    {
        private readonly Cell m_cell;

        public DenyCellGate(Cell cell)
        {
            m_cell = cell;
        }

        public bool AllowsCell(IBoardReader board, int block, Cell cell) => cell != m_cell;
    }
}
