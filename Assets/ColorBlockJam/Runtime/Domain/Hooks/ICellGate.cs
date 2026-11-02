using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>Decides whether a block may occupy one board cell (XP-3). Gates combine with AND.</summary>
    public interface ICellGate
    {
        bool AllowsCell(IBoardReader board, int block, Cell cell);
    }
}
