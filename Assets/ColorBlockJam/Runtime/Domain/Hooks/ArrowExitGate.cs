using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// The Arrow block exit gate (XP-4, catalog BLK-01, RULE-34): a block leaves only through a side its authored
    /// source's axis points to. Top and Bottom run along X, so leaving through them moves the block along Y and needs
    /// Vertical; Left and Right need Horizontal. It vetoes a perpendicular side, so ExitRules.Evaluate answers Vetoed
    /// for every caller: the drag, the move command and the Level Editor solver alike (DEC-144). A Rocket part reads
    /// its parent's axis through BlockFootprint.Source. The axes are copied once and fixed for the attempt; a call
    /// allocates nothing.
    /// </summary>
    public sealed class ArrowExitGate : IExitGate
    {
        private readonly AxisMask[] m_axes;

        /// <param name="axes">One entry per authored block index: Horizontal, Vertical, or Both for a block without Arrow.</param>
        public ArrowExitGate(AxisMask[] axes)
        {
            m_axes = ArrowAxes.Copy(axes);
        }

        public bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome)
        {
            AxisMask needed = SideMath.GetExitAxis(query.Side);
            return (ArrowAxes.Get(m_axes, board, query.Block) & needed) != 0;
        }
    }
}
