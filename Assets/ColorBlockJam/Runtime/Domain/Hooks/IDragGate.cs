using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>Decides whether a block may start a drag (XP-1; IceRules refuses frozen blocks). Gates combine with AND.</summary>
    public interface IDragGate
    {
        bool AllowsDrag(IBoardReader board, int block);
    }
}
