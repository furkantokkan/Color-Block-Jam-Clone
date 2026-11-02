using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;

namespace ColorBlockJam.Domain.Exit
{
    /// <summary>
    /// The pure exit predicate (RULE-12..14, DEC-6): a block at an origin leaves through a side only
    /// when it is flush, one door span covers its whole extent along that side, the colours match,
    /// the outward swept path is clear and every exit gate agrees. It never mutates, so the Level
    /// Editor solver can ask it about any origin (AR-10, DEC-144).
    /// </summary>
    public static class ExitRules
    {
        public static ExitVerdict Evaluate(IBoardReader board, AttemptHooks hooks, int block, Cell origin, Side side,
            out ExitCandidate candidate)
        {
            candidate = default;
            if (!MoveRules.IsFlush(board, block, origin, side))
            {
                return ExitVerdict.NotFlush;
            }

            // The block's current footprint gives its lanes and colour, so a Rocket part exits by its own cells (B-3).
            BlockFootprint footprint = board.GetFootprint(block);
            bool alongX = SideMath.RunsAlongX(side);
            int lo = alongX ? origin.X + footprint.MinX : origin.Y + footprint.MinY;
            int hi = alongX ? origin.X + footprint.MaxX : origin.Y + footprint.MaxY;
            int door = board.Model.GetDoorAtLane(side, lo);
            if (door < 0)
            {
                return HasDoorInLanes(board.Model, side, lo, hi) ? ExitVerdict.TooNarrow : ExitVerdict.NoDoor;
            }

            DoorDefinition doorDefinition = board.Model.GetDoor(door);
            if (!doorDefinition.Covers(lo, hi))
            {
                return ExitVerdict.TooNarrow;
            }

            if (footprint.Color != doorDefinition.Color)
            {
                return ExitVerdict.WrongColor;
            }

            if (!IsPathClear(board, hooks, block, origin, footprint, SideMath.GetOutward(side)))
            {
                return ExitVerdict.PathBlocked;
            }

            ExitQuery query = new ExitQuery(block, door, side, origin);
            if (!hooks.AllowsExit(board, query, out ExitOutcome outcome))
            {
                return ExitVerdict.Vetoed;
            }

            candidate = new ExitCandidate(door, side, outcome);
            return ExitVerdict.Fits;
        }

        private static bool HasDoorInLanes(LevelModel model, Side side, int lo, int hi)
        {
            for (int lane = lo; lane <= hi; lane++)
            {
                if (model.GetDoorAtLane(side, lane) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        // Sweeps every footprint cell outward to the edge, so a concave shape (L arm, C notch) cannot
        // leave past another block, a void cell or a cell a cell gate closes (XP-3, e.g. a closed
        // Barrier) standing in its path. The block's own cells are already its, so no gate is asked.
        private static bool IsPathClear(IBoardReader board, AttemptHooks hooks, int block, Cell origin,
            in BlockFootprint footprint, Cell outward)
        {
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell cell = origin + footprint.GetCell(i) + outward;

                // Owner: this call. Stops: when the sweep leaves the board.
                while (cell.X >= 0 && cell.Y >= 0 && cell.X < board.Width && cell.Y < board.Height)
                {
                    int occupant = board.GetOccupant(cell);
                    if (occupant == block)
                    {
                        cell += outward;
                        continue;
                    }

                    if (!board.IsFloor(cell) || occupant != Occupant.k_empty || !hooks.AllowsCell(board, block, cell))
                    {
                        return false;
                    }

                    cell += outward;
                }
            }

            return true;
        }
    }
}
