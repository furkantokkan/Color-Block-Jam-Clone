using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure Level Editor block, door and board commands: each takes a draft level and returns a new level with one
    /// edit, or refuses with a designer message that names the offending cell or segment. They never change the level
    /// they are given (docs/Level-Editor-Design.md sections 4 and 5).
    /// </summary>
    internal static class LevelEditCommands
    {
        private static readonly Vector2Int[] s_neighbours =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };

        /// <summary>A gate spans 1 to 3 edge segments (user rule of 2026-09-25).</summary>
        internal const int k_maxDoorLength = LevelData.MaxDoorLength;

        /// <summary>
        /// Adds a block with these cells and colour at origin with the lowest free id "b{n}"; its piece is the key of
        /// its cells (DEC-193). Refused when a cell leaves the board, overlaps another block or sits on a closed
        /// barrier.
        /// </summary>
        public static bool TryPlaceBlock(LevelData level, IReadOnlyList<Vector2Int> cells,
            string colorId, Vector2Int origin, out LevelData result, out string issue)
        {
            result = null;
            if (!TryCheckLevel(level, out issue))
            {
                return false;
            }

            if (cells == null || cells.Count == 0)
            {
                issue = "Pick a shape with at least one cell first.";
                return false;
            }

            string blockId = GetNextBlockId(level);
            if (!TryCheckFootprint(level, cells, origin, -1, blockId, false, out issue))
            {
                return false;
            }

            Vector2Int[] copy = new Vector2Int[cells.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = cells[index];
            }

            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
            Array.Resize(ref blocks, blocks.Length + 1);
            blocks[blocks.Length - 1] = new LevelBlockData(blockId, colorId, origin, copy);
            result = LevelRecordCopy.WithBlocks(level, blocks);
            return true;
        }

        /// <summary>
        /// Moves a block's origin. Refused when a cell leaves the board, overlaps another block or sits on a closed
        /// barrier.
        /// </summary>
        public static bool TryMoveBlock(LevelData level, int blockIndex, Vector2Int origin,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetBlock(level, blockIndex, out LevelBlockData block, out issue)
                || !TryCheckFootprint(level, LevelRecordCopy.GetCells(block), origin, blockIndex, block.Id, false,
                    out issue))
            {
                return false;
            }

            result = Replace(level, blockIndex, LevelRecordCopy.WithOrigin(block, origin));
            return true;
        }

        /// <summary>
        /// Turns a block around its footprint centre and slides it inside the board if it crosses an edge.
        /// Checks the landed cells before rebasing the accepted footprint; colour, effects and identity stay.
        /// </summary>
        public static bool TryRotateBlock(LevelData level, int blockIndex, bool clockwise,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetBlock(level, blockIndex, out LevelBlockData block, out issue))
            {
                return false;
            }

            Vector2Int[] cells = LevelRecordCopy.GetCells(block);
            if (cells.Length == 0)
            {
                issue = $"Block {block.Id} needs at least one cell.";
                return false;
            }

            Vector2Int[] turned = clockwise
                ? LevelPieceTools.RotateClockwise(cells)
                : LevelPieceTools.RotateCounterClockwise(cells);
            Vector2Int shift = LevelPieceTools.GetTurnOriginShift(cells, clockwise);
            shift += LevelPieceTools.GetBoardKick(turned, block.Origin + shift, level.Width, level.Height);
            // TryReplaceBlockCells keeps the old origin while checking where the turned cells land.
            Vector2Int[] landed = new Vector2Int[turned.Length];
            for (int index = 0; index < turned.Length; index++)
            {
                landed[index] = turned[index] + shift;
            }

            if (!TryReplaceBlockCells(level, blockIndex, landed, out result, out issue))
            {
                return false;
            }

            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(result);
            blocks[blockIndex] = LevelRecordCopy.WithOrigin(
                LevelRecordCopy.WithCells(blocks[blockIndex], turned), block.Origin + shift);
            result = LevelRecordCopy.WithBlocks(result, blocks);
            return true;
        }

        /// <summary>Removes one block; the other blocks keep their order and ids.</summary>
        public static bool TryRemoveBlock(LevelData level, int blockIndex, out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetBlock(level, blockIndex, out _, out issue))
            {
                return false;
            }

            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount - 1];
            for (int index = 0, target = 0; index < level.BlockCount; index++)
            {
                if (index != blockIndex)
                {
                    blocks[target++] = level.GetBlockAt(index);
                }
            }

            result = LevelRecordCopy.WithBlocks(level, blocks);
            return true;
        }

        /// <summary>Gives a block another colour id; its cells and place stay.</summary>
        public static bool TryRecolorBlock(LevelData level, int blockIndex, string colorId,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetBlock(level, blockIndex, out LevelBlockData block, out issue))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(colorId))
            {
                issue = "Pick a colour first.";
                return false;
            }

            result = Replace(level, blockIndex, LevelRecordCopy.WithColor(block, colorId));
            return true;
        }

        /// <summary>
        /// Replaces a block's cells (Rotate, Flip, Piece Type) around the same origin. Refused when the new footprint
        /// leaves the board, overlaps another block or sits on a closed barrier.
        /// </summary>
        public static bool TryReplaceBlockCells(LevelData level, int blockIndex,
            IReadOnlyList<Vector2Int> cells, out LevelData result, out string issue)
        {
            return TryReplaceBlockCells(level, blockIndex, cells, false, out result, out issue);
        }

        /// <summary>
        /// TryReplaceBlockCells whose new footprint may overlap other blocks when <paramref name="allowBlockOverlap"/>
        /// is true. Only a re-stamp passes true (contract D3-B): the canvas then strikes the overlapping blocks'
        /// labels through and Save Level waits until they are apart. Off-board, void and closed-barrier cells are
        /// still refused.
        /// </summary>
        public static bool TryReplaceBlockCells(LevelData level, int blockIndex,
            IReadOnlyList<Vector2Int> cells, bool allowBlockOverlap, out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetBlock(level, blockIndex, out LevelBlockData block, out issue))
            {
                return false;
            }

            if (cells == null || cells.Count == 0)
            {
                issue = $"Block {block.Id} needs at least one cell.";
                return false;
            }

            if (!TryCheckFootprint(level, cells, block.Origin, blockIndex, block.Id, allowBlockOverlap, out issue))
            {
                return false;
            }

            Vector2Int[] copy = new Vector2Int[cells.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = cells[index];
            }

            result = Replace(level, blockIndex, LevelRecordCopy.WithCells(block, copy));
            return true;
        }

        /// <summary>The lowest unused "b{n}" block id.</summary>
        public static string GetNextBlockId(LevelData level)
        {
            for (int number = 1; ; number++)
            {
                string id = "b" + number;
                bool used = false;
                for (int index = 0; index < level.BlockCount && !used; index++)
                {
                    used = string.Equals(level.GetBlockAt(index)?.Id, id, StringComparison.Ordinal);
                }

                if (!used)
                {
                    return id;
                }
            }
        }

        /// <summary>
        /// Puts the segment into a door of this colour on that side (PRD 11.2). A same-colour door that ends right
        /// before the segment or starts right after it grows over it; when the segment joins two such doors they
        /// become one, kept at the lower door-list index with that door's id. Otherwise a new one-segment door with
        /// the lowest free id "d{n}" is added at the end. doorIndex names the door that now holds the segment.
        /// Refused off the edge, without a colour, on a segment another door already holds, or when the gate would
        /// span more than <see cref="k_maxDoorLength"/> segments.
        /// </summary>
        public static bool TryPlaceOrExtendDoor(LevelData level, BoardSide side, int segment, string colorId,
            out LevelData result, out int doorIndex, out string issue)
        {
            result = null;
            doorIndex = -1;
            if (!TryCheckLevel(level, out issue) || !TryCheckSide(side, out issue))
            {
                return false;
            }

            if (segment < 0 || segment >= GetSideLength(level, side))
            {
                issue = $"Segment {segment} is off the {side} edge.";
                return false;
            }

            Vector2Int behind = GetEdgeCell(level, side, segment);
            if (!level.IsFloor(behind))
            {
                issue = $"Cell ({behind.x},{behind.y}) behind the {side} edge at {segment} is a void.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(colorId))
            {
                issue = "Pick a colour first.";
                return false;
            }

            int endsBefore = -1;
            int startsAfter = -1;
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door == null || door.Side != side)
                {
                    continue;
                }

                if (segment >= door.Start && segment < door.Start + door.Length)
                {
                    issue = $"The {side} edge at {segment} already holds door {door.Id}.";
                    return false;
                }

                if (door.Length < 1 || !string.Equals(door.ColorId, colorId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (endsBefore < 0 && door.Start + door.Length == segment)
                {
                    endsBefore = index;
                }

                if (startsAfter < 0 && door.Start == segment + 1)
                {
                    startsAfter = index;
                }
            }

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            int grownLength = endsBefore >= 0 && startsAfter >= 0
                ? doors[startsAfter].Start + doors[startsAfter].Length - doors[endsBefore].Start
                : endsBefore >= 0 ? doors[endsBefore].Length + 1
                : startsAfter >= 0 ? doors[startsAfter].Length + 1
                : 1;
            if (grownLength > k_maxDoorLength)
            {
                string grown = doors[endsBefore >= 0 ? endsBefore : startsAfter].Id;
                issue = $"Gate {grown} would span {grownLength} cells; gates span 1 to {k_maxDoorLength}.";
                return false;
            }

            if (endsBefore >= 0 && startsAfter >= 0)
            {
                // Keeping the lower list index means no door before it shifts when the other entry goes.
                LevelDoorData low = doors[endsBefore];
                LevelDoorData high = doors[startsAfter];
                LevelDoorData absorbed = doors[Math.Max(endsBefore, startsAfter)];
                if (absorbed.EffectCount > 0)
                {
                    // Joining would silently drop the absorbed gate's effects.
                    issue = $"Joining would remove gate {absorbed.Id} and its effects; remove them first.";
                    return false;
                }

                doorIndex = Math.Min(endsBefore, startsAfter);
                doors[doorIndex] = LevelRecordCopy.WithSpan(doors[doorIndex], low.Start,
                    high.Start + high.Length - low.Start);
                doors = RemoveDoorAt(doors, Math.Max(endsBefore, startsAfter));
            }
            else if (endsBefore >= 0)
            {
                doorIndex = endsBefore;
                LevelDoorData door = doors[doorIndex];
                doors[doorIndex] = LevelRecordCopy.WithSpan(door, door.Start, door.Length + 1);
            }
            else if (startsAfter >= 0)
            {
                doorIndex = startsAfter;
                LevelDoorData door = doors[doorIndex];
                doors[doorIndex] = LevelRecordCopy.WithSpan(door, segment, door.Length + 1);
            }
            else
            {
                Array.Resize(ref doors, doors.Length + 1);
                doorIndex = doors.Length - 1;
                doors[doorIndex] = new LevelDoorData(GetNextDoorId(level), side, segment, 1, colorId);
            }

            result = LevelRecordCopy.WithDoors(level, doors);
            return true;
        }

        /// <summary>Removes one whole door; the other doors keep their order and ids.</summary>
        public static bool TryRemoveDoor(LevelData level, int doorIndex, out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetDoor(level, doorIndex, out _, out issue))
            {
                return false;
            }

            result = LevelRecordCopy.WithDoors(level, RemoveDoorAt(LevelRecordCopy.GetDoors(level), doorIndex));
            return true;
        }

        /// <summary>
        /// Takes one segment off a door (a Pen right click on a gate,
        /// TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove). An end segment shrinks the door by
        /// one, keeping its id, colour and effects; the only segment of a one-segment door removes it. A middle segment
        /// splits the door into two of the same colour on the same side: the part before the segment keeps the id and
        /// effects, and the part after it is added at the end of the list with the lowest free id "d{n}" and no
        /// effects, because gate effects are hand-written and the editor cannot add them. Every other door keeps its
        /// order and id. Refused for a missing door, or a side or segment the door does not hold.
        /// </summary>
        public static bool TryRemoveDoorSegment(LevelData level, int doorIndex, BoardSide side, int segment,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetDoor(level, doorIndex, out LevelDoorData door, out issue))
            {
                return false;
            }

            int end = door.Start + door.Length;
            if (door.Side != side || segment < door.Start || segment >= end)
            {
                issue = $"Door {door.Id} does not hold the {side} edge at {segment}.";
                return false;
            }

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            if (door.Length == 1)
            {
                doors = RemoveDoorAt(doors, doorIndex);
            }
            else if (segment == door.Start)
            {
                doors[doorIndex] = LevelRecordCopy.WithSpan(door, door.Start + 1, door.Length - 1);
            }
            else if (segment == end - 1)
            {
                doors[doorIndex] = LevelRecordCopy.WithSpan(door, door.Start, door.Length - 1);
            }
            else
            {
                // Appending the new part keeps every other door's list index, so a selection by index stays valid.
                doors[doorIndex] = LevelRecordCopy.WithSpan(door, door.Start, segment - door.Start);
                Array.Resize(ref doors, doors.Length + 1);
                doors[doors.Length - 1] = new LevelDoorData(GetNextDoorId(level), side, segment + 1, end - segment - 1,
                    door.ColorId);
            }

            result = LevelRecordCopy.WithDoors(level, doors);
            return true;
        }

        /// <summary>Gives a door another colour id; its side and span stay.</summary>
        public static bool TryRecolorDoor(LevelData level, int doorIndex, string colorId,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetDoor(level, doorIndex, out LevelDoorData door, out issue))
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(colorId))
            {
                issue = "Pick a colour first.";
                return false;
            }

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            doors[doorIndex] = LevelRecordCopy.WithColor(door, colorId);
            result = LevelRecordCopy.WithDoors(level, doors);
            return true;
        }

        /// <summary>
        /// Adds delta lines on one board side, or removes -delta lines when delta is negative, and keeps the remaining
        /// content in place (<see cref="LevelBoardResize.TryResize"/>). The report counts and locates what a removal
        /// takes away, in the given level's coordinates, so the canvas can hatch it and ask before applying. Delta 0
        /// returns an equal copy. Refused outside 3-8 x 3-10.
        /// </summary>
        public static bool TryResizeBoard(LevelData level, BoardSide side, int delta, out LevelData result,
            out LevelResizeReport report, out string issue)
        {
            result = null;
            report = default;
            if (!TryCheckLevel(level, out issue) || !TryCheckSide(side, out issue))
            {
                return false;
            }

            return LevelBoardResize.TryResize(level, side, delta, out result, out report, out issue);
        }

        /// <summary>The lowest unused "d{n}" door id.</summary>
        public static string GetNextDoorId(LevelData level)
        {
            for (int number = 1; ; number++)
            {
                string id = "d" + number;
                bool used = false;
                for (int index = 0; index < level.DoorCount && !used; index++)
                {
                    used = string.Equals(level.GetDoorAt(index)?.Id, id, StringComparison.Ordinal);
                }

                if (!used)
                {
                    return id;
                }
            }
        }

        /// <summary>
        /// Why the corner cannot be cut one more step, or null when it can (staircase corners,
        /// TASK-20260926-181022947-224a7aef): the cell must be one of the board's four corner cells, and the step after
        /// its depth must pass <see cref="GetCornerStepIssue"/>. The first step is the corner cell under the single-cell
        /// rules (void cells are level JSON v2).
        /// </summary>
        public static string GetCornerCutIssue(LevelData level, Vector2Int cell)
        {
            return GetCornerCutIssue(level, cell, null);
        }

        // The cut check; stepCells, when given, receives the cells the cut turns void.
        private static string GetCornerCutIssue(LevelData level, Vector2Int cell, List<Vector2Int> stepCells)
        {
            if (!TryCheckLevel(level, out string issue))
            {
                return issue;
            }

            if (!LevelCornerSteps.IsCorner(level.Width, level.Height, cell))
            {
                return $"Cell ({cell.x},{cell.y}) is not a board corner.";
            }

            return GetCornerStepIssue(level, cell, LevelCornerSteps.GetDepth(level, cell) + 1, stepCells);
        }

        /// <summary>
        /// Why step `step` of this board corner cannot become void, or null when it can (LevelCornerSteps).
        /// floorCells, when given, receives the step's cells that are still floor, in step order. A step already
        /// all void passes with none. Step 1, the corner cell, keeps the single-cell rules and messages
        /// (<see cref="GetVoidIssue"/>, "corner cell"). A later step checks each still-floor cell for blocks, board
        /// objects and gates opening onto it. It then checks the whole step at once: the floor must stay
        /// four-connected and keep at least one cell. A step is refused whole, never cut in part.
        /// </summary>
        internal static string GetCornerStepIssue(LevelData level, Vector2Int corner, int step,
            List<Vector2Int> floorCells)
        {
            List<Vector2Int> cells = floorCells ?? new List<Vector2Int>();
            LevelCornerSteps.GetStepCells(level.Width, level.Height, corner, step, cells);
            if (cells.Count == 0)
            {
                return $"The corner ({corner.x},{corner.y}) has no step left to cut.";
            }

            int kept = 0;
            for (int index = 0; index < cells.Count; index++)
            {
                if (level.IsFloor(cells[index]))
                {
                    cells[kept++] = cells[index];
                }
            }

            cells.RemoveRange(kept, cells.Count - kept);
            if (cells.Count == 0)
            {
                return null;
            }

            if (step == 1)
            {
                return GetVoidIssue(level, corner, "corner cell");
            }

            for (int index = 0; index < cells.Count; index++)
            {
                string issue = GetCellVoidIssue(level, cells[index], "corner step cell");
                if (issue != null)
                {
                    return issue;
                }
            }

            if (!IsFloorConnectedWithout(level, cells, out int floorLeft))
            {
                return "Cutting the next corner step would split the floor.";
            }

            return floorLeft == 0 ? "Cutting the next corner step would leave no floor." : null;
        }

        /// <summary>
        /// Why this board cell cannot become a void, or null when it can: it must still be floor, with no block, board
        /// object or gate on it, and the rest of the floor must stay four-connected (LevelData.Validate). noun names
        /// the cell in the message, for example "corner cell".
        /// </summary>
        internal static string GetVoidIssue(LevelData level, Vector2Int cell, string noun)
        {
            string issue = GetCellVoidIssue(level, cell, noun);
            if (issue != null)
            {
                return issue;
            }

            // LevelData.Validate needs one four-connected floor; other void cells can make this cell its only link.
            if (!IsFloorConnectedWithout(level, cell))
            {
                return $"Cutting the {noun} ({cell.x},{cell.y}) would split the floor.";
            }

            return null;
        }

        // The cell rules of GetVoidIssue without the floor check: still floor, with no block, board object or gate on
        // it. A corner step checks its floor once for the whole step.
        private static string GetCellVoidIssue(LevelData level, Vector2Int cell, string noun)
        {
            if (!level.IsFloor(cell))
            {
                return $"The {noun} ({cell.x},{cell.y}) is already a void.";
            }

            int block = FindBlockAt(level, cell, -1);
            if (block >= 0)
            {
                return $"Block {level.GetBlockAt(block).Id} covers the {noun} ({cell.x},{cell.y}).";
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (cellObject != null && cellObject.ContainsCell(cell))
                {
                    return $"Board object {cellObject.Id} covers the {noun} ({cell.x},{cell.y}).";
                }
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door != null && OpensOnto(level, door, cell))
                {
                    return $"Gate {door.Id} opens onto the {noun} ({cell.x},{cell.y}).";
                }
            }

            return null;
        }

        /// <summary>
        /// Cuts the corner's next step (<see cref="GetCornerCutIssue"/>): its still-floor cells become void cells,
        /// appended in step order. The first cut is the corner cell. Every other field is kept.
        /// </summary>
        public static bool TryCutCorner(LevelData level, Vector2Int cell, out LevelData result, out string issue)
        {
            result = null;
            List<Vector2Int> cells = new List<Vector2Int>();
            issue = GetCornerCutIssue(level, cell, cells);
            if (issue != null)
            {
                return false;
            }

            result = WithVoidCells(level, cells);
            return true;
        }

        /// <summary>
        /// Turns the corner's last cut step (its depth, LevelCornerSteps) back into floor as one result. The last step
        /// of a single cut is the corner cell, and every other void cell stays. Refused when the corner cell is floor,
        /// or when no cell of the step touches floor (other void cells around it).
        /// </summary>
        public static bool TryFillCorner(LevelData level, Vector2Int cell, out LevelData result, out string issue)
        {
            result = null;
            if (!TryCheckLevel(level, out issue))
            {
                return false;
            }

            if (!LevelCornerSteps.IsCorner(level.Width, level.Height, cell))
            {
                issue = $"Cell ({cell.x},{cell.y}) is not a board corner.";
                return false;
            }

            int depth = LevelCornerSteps.GetDepth(level, cell);
            if (depth == 0)
            {
                issue = $"The corner cell ({cell.x},{cell.y}) is already floor.";
                return false;
            }

            // A step is one connected L-shaped ring (DEC-190), so one of its cells touching floor joins it all.
            List<Vector2Int> cells = new List<Vector2Int>();
            LevelCornerSteps.GetStepCells(level.Width, level.Height, cell, depth, cells);
            for (int index = 0; index < cells.Count; index++)
            {
                if (TouchesFloor(level, cells[index]))
                {
                    result = WithoutVoidCells(level, cells);
                    return true;
                }
            }

            issue = $"The corner step of ({cell.x},{cell.y}) touches no floor; restore a cell next to it first.";
            return false;
        }

        /// <summary>
        /// The Wall brush: turns any free board cell into a permanent interior wall, a level void cell that the play
        /// area walls in (<see cref="GetVoidIssue"/>). Every other field is kept.
        /// </summary>
        public static bool TryPaintVoid(LevelData level, Vector2Int cell, out LevelData result, out string issue)
        {
            result = null;
            if (!TryCheckLevel(level, out issue) || !TryCheckOnBoard(level, cell, out issue))
            {
                return false;
            }

            issue = GetVoidIssue(level, cell, "cell");
            if (issue != null)
            {
                return false;
            }

            result = WithVoidCell(level, cell);
            return true;
        }

        /// <summary>
        /// Turns any void cell, a cut corner included, back into floor. Refused when no floor cell touches it, since
        /// LevelData.Validate needs one four-connected floor; every other void cell stays.
        /// </summary>
        public static bool TryEraseVoid(LevelData level, Vector2Int cell, out LevelData result, out string issue)
        {
            result = null;
            if (!TryCheckLevel(level, out issue) || !TryCheckOnBoard(level, cell, out issue))
            {
                return false;
            }

            if (level.IsFloor(cell))
            {
                issue = $"The cell ({cell.x},{cell.y}) is already floor.";
                return false;
            }

            if (!TouchesFloor(level, cell))
            {
                issue = $"The cell ({cell.x},{cell.y}) touches no floor; restore a cell next to it first.";
                return false;
            }

            result = WithoutVoidCells(level, new[] { cell });
            return true;
        }

        // True when a four-connected neighbour of this cell is floor.
        private static bool TouchesFloor(LevelData level, Vector2Int cell)
        {
            for (int direction = 0; direction < s_neighbours.Length; direction++)
            {
                if (level.IsFloor(cell + s_neighbours[direction]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// One Wall brush or wall-erase stroke as one result: paint applies <see cref="TryPaintVoid"/> to each cell in
        /// order, erase <see cref="TryEraseVoid"/>. A cell already in the wanted state is kept, like a gate segment the
        /// same colour already holds; a refused cell is skipped and the rest of the stroke applies. Passes repeat
        /// while one changes a cell, so the order of the stroke does not matter (a dead-end cell painted after its
        /// neighbour, or the middle of a wide wall erased first). firstIssue names the first cell still refused.
        /// False, with no result, when the stroke changes nothing.
        /// </summary>
        public static bool TryApplyVoidStroke(LevelData level, IReadOnlyList<Vector2Int> cells, bool paint,
            out LevelData result, out string firstIssue)
        {
            result = null;
            if (!TryCheckLevel(level, out firstIssue) || cells == null)
            {
                return false;
            }

            LevelData current = level;
            bool changed = false;
            // Each pass that changes a cell moves it one way only (floor to void, or back), so passes are bounded.
            for (bool progress = true; progress;)
            {
                progress = false;
                firstIssue = null;
                for (int index = 0; index < cells.Count; index++)
                {
                    Vector2Int cell = cells[index];
                    if (IsOnBoard(current, cell) && current.IsFloor(cell) != paint)
                    {
                        continue;
                    }

                    LevelData next;
                    string issue;
                    bool applied = paint
                        ? TryPaintVoid(current, cell, out next, out issue)
                        : TryEraseVoid(current, cell, out next, out issue);
                    if (!applied)
                    {
                        firstIssue = firstIssue ?? issue;
                        continue;
                    }

                    current = next;
                    progress = true;
                    changed = true;
                }
            }

            result = changed ? current : null;
            return changed;
        }

        /// <summary>The board cell a ring segment of this side sits against.</summary>
        internal static Vector2Int GetEdgeCell(LevelData level, BoardSide side, int segment)
        {
            return GetEdgeCell(level.Width, level.Height, side, segment);
        }

        /// <summary>The cell a ring segment of this side sits against, on a width x height board.</summary>
        internal static Vector2Int GetEdgeCell(int width, int height, BoardSide side, int segment)
        {
            switch (side)
            {
                case BoardSide.Top:
                    return new Vector2Int(segment, height - 1);
                case BoardSide.Bottom:
                    return new Vector2Int(segment, 0);
                case BoardSide.Left:
                    return new Vector2Int(0, segment);
                default:
                    return new Vector2Int(width - 1, segment);
            }
        }

        // Flood fill over the floor with the removed cell treated as void: true when every floor cell is reached.
        private static bool IsFloorConnectedWithout(LevelData level, Vector2Int removed)
        {
            return IsFloorConnectedWithout(level, new[] { removed }, out _);
        }

        // Flood fill over the floor with the removed cells treated as void: true when every other floor cell is
        // reached. floorLeft counts the floor cells that remain.
        private static bool IsFloorConnectedWithout(LevelData level, IReadOnlyList<Vector2Int> removed,
            out int floorLeft)
        {
            bool[] gone = new bool[level.Width * level.Height];
            for (int index = 0; index < removed.Count; index++)
            {
                Vector2Int cell = removed[index];
                if (IsOnBoard(level, cell))
                {
                    gone[cell.y * level.Width + cell.x] = true;
                }
            }

            bool[] reached = new bool[level.Width * level.Height];
            Stack<Vector2Int> open = new Stack<Vector2Int>();
            floorLeft = 0;
            for (int y = 0; y < level.Height; y++)
            {
                for (int x = 0; x < level.Width; x++)
                {
                    Vector2Int cell = new Vector2Int(x, y);
                    if (gone[y * level.Width + x] || !level.IsFloor(cell))
                    {
                        continue;
                    }

                    floorLeft++;
                    if (floorLeft == 1)
                    {
                        open.Push(cell);
                        reached[y * level.Width + x] = true;
                    }
                }
            }

            int reachedCount = 0;
            while (open.Count > 0)
            {
                Vector2Int cell = open.Pop();
                reachedCount++;
                for (int direction = 0; direction < 4; direction++)
                {
                    Vector2Int next = cell + s_neighbours[direction];
                    if (!level.IsFloor(next) || gone[next.y * level.Width + next.x]
                        || reached[next.y * level.Width + next.x])
                    {
                        continue;
                    }

                    reached[next.y * level.Width + next.x] = true;
                    open.Push(next);
                }
            }

            return reachedCount == floorLeft;
        }

        private static LevelData WithVoidCell(LevelData level, Vector2Int cell)
        {
            return WithVoidCells(level, new[] { cell });
        }

        // The level with these cells appended to its void cells, in order.
        internal static LevelData WithVoidCells(LevelData level, IReadOnlyList<Vector2Int> cells)
        {
            Vector2Int[] voidCells = LevelRecordCopy.GetVoidCells(level);
            int start = voidCells.Length;
            Array.Resize(ref voidCells, start + cells.Count);
            for (int index = 0; index < cells.Count; index++)
            {
                voidCells[start + index] = cells[index];
            }

            return LevelRecordCopy.WithVoidCells(level, voidCells);
        }

        // The level without these void cells; the other void cells keep their order.
        private static LevelData WithoutVoidCells(LevelData level, IReadOnlyList<Vector2Int> cells)
        {
            Vector2Int[] voidCells = LevelRecordCopy.GetVoidCells(level);
            int kept = 0;
            for (int index = 0; index < voidCells.Length; index++)
            {
                bool removed = false;
                for (int cell = 0; cell < cells.Count && !removed; cell++)
                {
                    removed = voidCells[index] == cells[cell];
                }

                if (!removed)
                {
                    voidCells[kept++] = voidCells[index];
                }
            }

            Array.Resize(ref voidCells, kept);
            return LevelRecordCopy.WithVoidCells(level, voidCells);
        }

        private static bool IsOnBoard(LevelData level, Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < level.Width && cell.y >= 0 && cell.y < level.Height;
        }

        private static bool TryCheckOnBoard(LevelData level, Vector2Int cell, out string issue)
        {
            bool onBoard = IsOnBoard(level, cell);
            issue = onBoard ? null : $"Cell ({cell.x},{cell.y}) is off the {level.Width} x {level.Height} board.";
            return onBoard;
        }

        private static bool OpensOnto(LevelData level, LevelDoorData door, Vector2Int cell)
        {
            for (int segment = door.Start; segment < door.Start + door.Length; segment++)
            {
                if (GetEdgeCell(level, door.Side, segment) == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryCheckLevel(LevelData level, out string issue)
        {
            issue = level == null ? "Select a level first." : null;
            return level != null;
        }

        private static bool TryCheckSide(BoardSide side, out string issue)
        {
            bool valid = IsValidSide(side);
            issue = valid ? null : "Pick a board side first.";
            return valid;
        }

        /// <summary>True for one of the four board sides; shared with LevelDraftIssues.</summary>
        internal static bool IsValidSide(BoardSide side)
        {
            return side == BoardSide.Top || side == BoardSide.Bottom
                || side == BoardSide.Left || side == BoardSide.Right;
        }

        // Segments run along x on Top and Bottom and along y on Left and Right.
        private static int GetSideLength(LevelData level, BoardSide side)
        {
            return GetSideLength(level.Width, level.Height, side);
        }

        /// <summary>Top and Bottom run along width, Left and Right along height; shared with LevelDraftIssues and
        /// LevelCanvasModel.</summary>
        internal static int GetSideLength(int width, int height, BoardSide side)
        {
            return side == BoardSide.Top || side == BoardSide.Bottom ? width : height;
        }

        private static bool TryGetDoor(LevelData level, int doorIndex, out LevelDoorData door, out string issue)
        {
            door = null;
            if (!TryCheckLevel(level, out issue))
            {
                return false;
            }

            if (doorIndex < 0 || doorIndex >= level.DoorCount || level.GetDoorAt(doorIndex) == null)
            {
                issue = "Select a door first.";
                return false;
            }

            door = level.GetDoorAt(doorIndex);
            return true;
        }

        private static LevelDoorData[] RemoveDoorAt(LevelDoorData[] doors, int doorIndex)
        {
            LevelDoorData[] remaining = new LevelDoorData[doors.Length - 1];
            for (int index = 0, target = 0; index < doors.Length; index++)
            {
                if (index != doorIndex)
                {
                    remaining[target++] = doors[index];
                }
            }

            return remaining;
        }

        private static bool TryGetBlock(LevelData level, int blockIndex, out LevelBlockData block, out string issue)
        {
            block = null;
            if (!TryCheckLevel(level, out issue))
            {
                return false;
            }

            if (blockIndex < 0 || blockIndex >= level.BlockCount || level.GetBlockAt(blockIndex) == null)
            {
                issue = "Select a block first.";
                return false;
            }

            block = level.GetBlockAt(blockIndex);
            return true;
        }

        // Every cell origin + cells[i] must be on the board, free of blocks other than ignoredBlock (unless
        // allowBlockOverlap, for a re-stamp), and free of closed barriers; an open barrier is floor. blockId names the
        // block in the barrier message.
        private static bool TryCheckFootprint(LevelData level, IReadOnlyList<Vector2Int> cells, Vector2Int origin,
            int ignoredBlock, string blockId, bool allowBlockOverlap, out string issue)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                Vector2Int cell = origin + cells[index];
                if (cell.x < 0 || cell.x >= level.Width || cell.y < 0 || cell.y >= level.Height)
                {
                    issue = $"Cell ({cell.x},{cell.y}) is off the {level.Width} x {level.Height} board.";
                    return false;
                }

                if (!level.IsFloor(cell))
                {
                    issue = $"Cell ({cell.x},{cell.y}) is a void.";
                    return false;
                }

                int other = allowBlockOverlap ? -1 : FindBlockAt(level, cell, ignoredBlock);
                if (other >= 0)
                {
                    LevelBlockData block = level.GetBlockAt(other);
                    issue = $"Overlaps {block.ColorId} block {block.Id} at ({cell.x},{cell.y}).";
                    return false;
                }

                issue = LevelBarrierCommands.GetBlockCellIssue(level, blockId, cell);
                if (issue != null)
                {
                    return false;
                }
            }

            issue = null;
            return true;
        }

        private static int FindBlockAt(LevelData level, Vector2Int cell, int ignoredBlock)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (index == ignoredBlock || block == null)
                {
                    continue;
                }

                for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
                {
                    if (block.Origin + block.GetCellAt(cellIndex) == cell)
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static LevelData Replace(LevelData level, int blockIndex, LevelBlockData block)
        {
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
            blocks[blockIndex] = block;
            return LevelRecordCopy.WithBlocks(level, blocks);
        }
    }
}
