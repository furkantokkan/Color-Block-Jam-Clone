using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// The Arrow block axis constraint (XP-2, catalog BLK-01, RULE-34): a block moves only on the axis of the authored
    /// block it is, or was split from (BlockFootprint.Source), so a Rocket part keeps its parent's axis with no state of
    /// its own. The axes are copied once and fixed for the attempt (DEC-MECH-4); a call reads one footprint and one
    /// array entry and allocates nothing.
    /// </summary>
    public sealed class ArrowAxisConstraint : IAxisConstraint
    {
        private readonly AxisMask[] m_axes;

        /// <param name="axes">One entry per authored block index: Horizontal, Vertical, or Both for a block without Arrow.</param>
        public ArrowAxisConstraint(AxisMask[] axes)
        {
            m_axes = ArrowAxes.Copy(axes);
        }

        public AxisMask GetAxes(IBoardReader board, int block) => ArrowAxes.Get(m_axes, board, block);
    }

    /// <summary>Checks, copies and looks up the per-authored-block axes the Arrow hooks share.</summary>
    internal static class ArrowAxes
    {
        public static AxisMask[] Copy(AxisMask[] axes)
        {
            if (axes == null)
            {
                throw new ArgumentNullException(nameof(axes));
            }

            for (int i = 0; i < axes.Length; i++)
            {
                AxisMask mask = axes[i];
                if (mask != AxisMask.Horizontal && mask != AxisMask.Vertical && mask != AxisMask.Both)
                {
                    throw new ArgumentException($"Block {i} has no movement axis.", nameof(axes));
                }
            }

            return (AxisMask[])axes.Clone();
        }

        // The axis of the block's authored source; an ID with no source yet, or past the authored blocks, has both.
        public static AxisMask Get(AxisMask[] axes, IBoardReader board, int block)
        {
            int source = board.GetFootprint(block).Source;
            return (uint)source < (uint)axes.Length ? axes[source] : AxisMask.Both;
        }
    }
}
