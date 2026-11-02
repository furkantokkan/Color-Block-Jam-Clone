using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Whether a block is an Arrow block: its authored source (BlockFootprint.Source) has one axis, so a Rocket part
    /// stays an Arrow block like its parent. The axes are fixed for the attempt (DEC-MECH-4).
    /// </summary>
    public sealed class ArrowEffectPresence : IBlockEffectPresence
    {
        private readonly AxisMask[] m_axes;

        /// <param name="axes">One entry per authored block index, as ArrowAxisConstraint takes them.</param>
        public ArrowEffectPresence(AxisMask[] axes)
        {
            m_axes = ArrowAxes.Copy(axes);
        }

        public bool HasEffect(IBoardReader board, int block) => ArrowAxes.Get(m_axes, board, block) != AxisMask.Both;
    }
}
