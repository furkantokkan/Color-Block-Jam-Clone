using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Picking
{
    /// <summary>
    /// The board interaction area where a press may select a block (DEC-91, RULE-9): the interior,
    /// the half-cell wall and gate ring and a half-cell border beyond it. Presses outside select nothing.
    /// </summary>
    public static class InteractionArea
    {
        // Cells span x - 0.5 to x + 0.5, so the floor edge is 0.5 from the outer cell centres;
        // the wall ring (0.5) and the border (0.5) extend it by one more cell.
        private const float k_margin = 1.5f;

        public static bool Contains(BoardPoint point, int width, int height)
        {
            return point.X >= -k_margin && point.X <= width - 1 + k_margin
                && point.Y >= -k_margin && point.Y <= height - 1 + k_margin;
        }
    }
}
