using System;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>The axes a block may move on during one drag (XP-2; Arrow blocks restrict it, DEC-158).</summary>
    [Flags]
    public enum AxisMask
    {
        None = 0,
        Horizontal = 1,
        Vertical = 2,
        Both = Horizontal | Vertical
    }
}
