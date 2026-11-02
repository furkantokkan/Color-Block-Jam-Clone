using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Limits the axes a block may move on, read once at drag start (XP-2, DEC-MECH-25).
    /// Constraints intersect.
    /// </summary>
    public interface IAxisConstraint
    {
        AxisMask GetAxes(IBoardReader board, int block);
    }
}
