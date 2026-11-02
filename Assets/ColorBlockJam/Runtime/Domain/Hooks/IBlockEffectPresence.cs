using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Whether a block carries one effect right now, so booster restrictions ask the effect's own attempt state
    /// instead of a copy: a thawed Ice block no longer carries Ice. A call allocates nothing.
    /// </summary>
    public interface IBlockEffectPresence
    {
        bool HasEffect(IBoardReader board, int block);
    }
}
