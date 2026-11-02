using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// Computes how far the held block's view leads its logical cell toward the pointer (DEC-112):
    /// up to leadCells toward a free neighbour and never toward a blocked cell or a door, so the view
    /// never overlaps anything. A held block touching a door it fits has already exited (DEC-164).
    /// </summary>
    public static class FollowOffset
    {
        private const float k_epsilon = 1e-4f;

        public static BoardPoint Compute(IBoardReader board, AttemptHooks hooks, int block, Cell origin,
            BoardPoint desired, AxisMask axes, float leadCells)
        {
            float dx = (axes & AxisMask.Horizontal) != 0 ? desired.X - origin.X : 0f;
            float dy = (axes & AxisMask.Vertical) != 0 ? desired.Y - origin.Y : 0f;
            float leadX = Lead(board, hooks, block, origin, dx, new Cell(Math.Sign(dx), 0), leadCells);
            float leadY = Lead(board, hooks, block, origin, dy, new Cell(0, Math.Sign(dy)), leadCells);

            // A diagonal lead into a blocked corner would overlap it: keep only the larger lead (tie keeps X).
            if (leadX != 0f && leadY != 0f
                && !MoveRules.CanOccupy(board, hooks, block, origin + new Cell(Math.Sign(leadX), Math.Sign(leadY))))
            {
                if (Math.Abs(leadX) >= Math.Abs(leadY))
                {
                    leadY = 0f;
                }
                else
                {
                    leadX = 0f;
                }
            }

            return new BoardPoint(leadX, leadY);
        }

        private static float Lead(IBoardReader board, AttemptHooks hooks, int block, Cell origin, float delta,
            Cell direction, float leadCells)
        {
            if (Math.Abs(delta) < k_epsilon)
            {
                return 0f;
            }

            return MoveRules.CanOccupy(board, hooks, block, origin + direction) ? Clamp(delta, leadCells) : 0f;
        }

        private static float Clamp(float value, float limit)
        {
            return value < -limit ? -limit : value > limit ? limit : value;
        }
    }
}
