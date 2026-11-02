using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// The pure placement rules: whether a block may stand at an origin (RULE-2, RULE-3, XP-3) and
    /// whether it touches a board side there. A door is never an occupiable cell, so every door the
    /// block cannot use acts as a wall (RULE-14).
    /// </summary>
    public static class MoveRules
    {
        public static bool CanOccupy(IBoardReader board, AttemptHooks hooks, int block, Cell origin)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell cell = origin + footprint.GetCell(i);
                if (!board.IsFloor(cell))
                {
                    return false;
                }

                int occupant = board.GetOccupant(cell);
                if (occupant != Occupant.k_empty && occupant != block)
                {
                    return false;
                }

                if (!hooks.AllowsCell(board, block, cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>True when the footprint at origin touches the side's edge row or column.</summary>
        public static bool IsFlush(IBoardReader board, int block, Cell origin, Side side)
        {
            BlockFootprint footprint = board.GetFootprint(block);
            switch (side)
            {
                case Side.Top: return origin.Y + footprint.MaxY == board.Height - 1;
                case Side.Bottom: return origin.Y + footprint.MinY == 0;
                case Side.Left: return origin.X + footprint.MinX == 0;
                default: return origin.X + footprint.MaxX == board.Width - 1;
            }
        }
    }
}
