using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Stage 2 static pre-checks, each a proof of unsolvability without a search. PRE-1: every block, alone
    /// on the board, can slide (respecting its Arrow axis, BLK-01) from its authored origin to an origin where it
    /// fits a door on a side its axis allows. PRE-2: an Ice block's count is no greater than the number of other
    /// blocks that could leave to thaw it. PRE-3 (DEC-176): no group of blocks is wedged, that is, left without a
    /// slide or an exit by the walls, the void and each other alone.
    /// </summary>
    internal static class LevelPreChecks
    {
        /// <summary>Returns the first failing block's reason in designer words, or null when every block passes.</summary>
        public static string FindReason(LevelData level, LevelSolverTables tables, LevelBlockDescriber describe)
        {
            if (level == null || tables == null || describe == null)
            {
                throw new ArgumentNullException(level == null ? nameof(level) : tables == null ? nameof(tables) : nameof(describe));
            }

            for (int block = 0; block < tables.BlockCount; block++)
            {
                if (!CanReachFittingExit(tables, block))
                {
                    return DescribeNoExit(level, tables, block, describe);
                }
            }

            int others = tables.BlockCount - 1;
            for (int block = 0; block < tables.BlockCount; block++)
            {
                int ice = tables.GetIceCount(block);
                if (ice > others)
                {
                    return describe(level, block) + " needs " + LevelDesignerText.Count(ice, "other block", "other blocks")
                        + " to leave before it thaws, but the level has only "
                        + LevelDesignerText.Count(others, "other block", "other blocks");
                }
            }

            int wedged = FindWedgedBlocks(tables);
            if (wedged != 0)
            {
                // PRE-1 above already proved every block alone can reach a fitting exit through some sequence of
                // axis-respecting slides, so a lone survivor here is impossible: the first step of that path is
                // always a valid single-hop move from the block's authored slot, which is exactly what
                // CanMoveOrExit checks. FindWedgedBlocks can therefore only leave two or more blocks jammed
                // against each other.
                return LevelDesignerText.DescribeBlocks(level, wedged, describe)
                    + " are wedged against each other and can never move";
            }

            return null;
        }

        // PRE-3: the largest set of blocks in which, with every block outside it taken away, no block has a slide or an
        // exit from its authored origin (a greatest fixpoint). Blocks outside the set only add obstacles, so the set's
        // blocks never move, and a block that never moves never leaves. Ice is ignored and Arrow axes apply, as in the
        // search's moves: ignoring Ice only allows more, so the proof holds.
        private static int FindWedgedBlocks(LevelSolverTables tables)
        {
            int wedged = (1 << tables.BlockCount) - 1;
            bool changed = true;
            // Owner: this call. Stops: each pass removes a block or ends the loop, so it runs at most BlockCount + 1 times.
            while (changed && wedged != 0)
            {
                changed = false;
                CellMask occupancy = CellMask.Empty;
                for (int block = 0; block < tables.BlockCount; block++)
                {
                    if ((wedged & (1 << block)) != 0)
                    {
                        occupancy = occupancy.Or(tables.GetFootprint(block, tables.GetAuthoredSlot(block)));
                    }
                }

                for (int block = 0; block < tables.BlockCount; block++)
                {
                    if ((wedged & (1 << block)) == 0)
                    {
                        continue;
                    }

                    int slot = tables.GetAuthoredSlot(block);
                    CellMask others = occupancy.Without(tables.GetFootprint(block, slot));
                    if (CanMoveOrExit(tables, block, slot, others))
                    {
                        wedged &= ~(1 << block);
                        changed = true;
                    }
                }
            }

            return wedged;
        }

        private static bool CanMoveOrExit(LevelSolverTables tables, int block, int slot, in CellMask others)
        {
            for (int direction = 0; direction < LevelSolverTables.DirectionCount; direction++)
            {
                if (LevelSolverMoves.GetSlide(tables, block, slot, direction, others) >= 0)
                {
                    return true;
                }
            }

            for (int side = 0; side < LevelSolverTables.SideCount; side++)
            {
                if (LevelSolverMoves.CanExit(tables, block, slot, (Side)side, others))
                {
                    return true;
                }
            }

            return false;
        }

        // Breadth-first over the block's standing origins on an otherwise empty board.
        private static bool CanReachFittingExit(LevelSolverTables tables, int block)
        {
            int cellCount = tables.Width * tables.Height;
            bool[] reached = new bool[cellCount];
            int[] queue = new int[cellCount];
            int start = tables.GetAuthoredSlot(block);
            reached[start] = true;
            queue[0] = start;
            int head = 0;
            int tail = 1;

            // Owner: this call. Stops: each slot is queued at most once.
            while (head < tail)
            {
                int slot = queue[head++];
                for (int side = 0; side < LevelSolverTables.SideCount; side++)
                {
                    if (LevelSolverMoves.CanExit(tables, block, slot, (Side)side, CellMask.Empty))
                    {
                        return true;
                    }
                }

                for (int direction = 0; direction < LevelSolverTables.DirectionCount; direction++)
                {
                    int next = LevelSolverMoves.GetSlide(tables, block, slot, direction, CellMask.Empty);
                    if (next >= 0 && !reached[next])
                    {
                        reached[next] = true;
                        queue[tail++] = next;
                    }
                }
            }

            return false;
        }

        // No same-colour door (the message names the narrowest gate the block could use, its smaller extent); else
        // every same-colour door narrower than the block's extent along its side (the width the block needs); else,
        // for an Arrow block, every wide-enough same-colour door sits on a side the block's axis can never exit
        // through (the axis case); else the block cannot reach a door it fits through.
        private static string DescribeNoExit(LevelData level, LevelSolverTables tables, int block,
            LevelBlockDescriber describe)
        {
            LevelModel model = tables.Model;
            BlockDefinition definition = model.GetBlock(block);
            string colour = LevelDesignerText.GetColourName(level.GetBlockAt(block).ColorId);
            string subject = describe(level, block);
            int extentX = definition.MaxX - definition.MinX + 1;
            int extentY = definition.MaxY - definition.MinY + 1;
            AxisMask axis = tables.GetAxes(block);
            bool hasDoor = false;
            bool hasWideDoor = false;
            bool hasWideDoorOnAxis = false;
            int neededWidth = int.MaxValue;
            for (int door = 0; door < model.DoorCount; door++)
            {
                DoorDefinition doorDefinition = model.GetDoor(door);
                if (doorDefinition.Color != definition.Color)
                {
                    continue;
                }

                hasDoor = true;
                bool alongX = SideMath.RunsAlongX(doorDefinition.Side);
                int extent = alongX ? extentX : extentY;
                if (doorDefinition.Length >= extent)
                {
                    hasWideDoor = true;
                    AxisMask needed = alongX ? AxisMask.Vertical : AxisMask.Horizontal;
                    if ((axis & needed) != 0)
                    {
                        hasWideDoorOnAxis = true;
                    }
                }
                else
                {
                    neededWidth = Math.Min(neededWidth, extent);
                }
            }

            if (!hasDoor)
            {
                return subject + " has no " + colour + " gate; it needs one at least "
                    + LevelDesignerText.Count(Math.Min(extentX, extentY), "cell", "cells") + " wide";
            }

            if (!hasWideDoor)
            {
                return subject + " needs a " + colour + " gate at least "
                    + LevelDesignerText.Count(neededWidth, "cell", "cells") + " wide";
            }

            if (!hasWideDoorOnAxis)
            {
                string axisName = axis == AxisMask.Horizontal ? "Horizontal" : "Vertical";
                return subject + " is a " + axisName + " Arrow block; its axis never lines it up to leave through a "
                    + colour + " gate";
            }

            return subject + " cannot reach a " + colour + " gate it fits through";
        }
    }
}
