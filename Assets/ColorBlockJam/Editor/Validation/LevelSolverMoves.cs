using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The search's legal-move test: it combines the cached Domain answers with the other blocks' occupancy,
    /// the one Ice hook and the one Arrow hook. It never branches on an effect type; a frozen block simply has
    /// no move, and a block without Arrow has both axes.
    /// </summary>
    internal static class LevelSolverMoves
    {
        /// <summary>
        /// Ice hook: an on-board block is held while max(0, clearsToThaw - exits of other blocks) &gt; 0, the
        /// count IceRules keeps. exitedCount never includes the block itself, because it is still on the board.
        /// </summary>
        public static bool IsFrozen(LevelSolverTables tables, int block, int exitedCount)
        {
            return tables.GetIceCount(block) - exitedCount > 0;
        }

        /// <summary>
        /// The slot one cell away when the block may slide there past the other blocks, or -1. A direction off the
        /// block's axes is refused, as MoveBlockHandler refuses a change along a locked axis (BLK-01).
        /// </summary>
        public static int GetSlide(LevelSolverTables tables, int block, int slot, int direction, in CellMask others)
        {
            AxisMask needed = LevelSolverTables.GetAxis(direction);
            if ((tables.GetAxes(block) & needed) == 0)
            {
                return -1;
            }

            int target = tables.GetSlideTarget(block, slot, direction);
            if (target < 0 || !tables.IsStanding(block, target) || tables.GetFootprint(block, target).Intersects(others))
            {
                return -1;
            }

            return target;
        }

        /// <summary>
        /// True when the block fits the side's door from this slot and no other block holds a swept cell. A side
        /// perpendicular to the block's axes is refused, as ArrowExitGate vetoes it: Top and Bottom run along X, so
        /// leaving through them needs Vertical; Left and Right need Horizontal (BLK-01).
        /// </summary>
        public static bool CanExit(LevelSolverTables tables, int block, int slot, Side side, in CellMask others)
        {
            AxisMask needed = SideMath.RunsAlongX(side) ? AxisMask.Vertical : AxisMask.Horizontal;
            return (tables.GetAxes(block) & needed) != 0
                && tables.ExitFits(block, slot, side) && !tables.GetSwept(block, slot, side).Intersects(others);
        }
    }
}
