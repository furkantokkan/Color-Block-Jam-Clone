using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>Whether an issue blocks Save (it breaks a LevelData.Validate rule) or only warns about play.</summary>
    internal enum LevelIssueSeverity
    {
        Error,
        Warning
    }

    /// <summary>
    /// One problem in a draft level: a designer message plus the board cells or the door span the canvas highlights
    /// when the issue's row is clicked.
    /// </summary>
    internal sealed class LevelIssue
    {
        private readonly Vector2Int[] m_cells;

        private LevelIssue(LevelIssueSeverity severity, string message, string entityId, Vector2Int[] cells,
            bool hasSegment, BoardSide side, int segmentStart, int segmentLength)
        {
            Severity = severity;
            Message = message;
            EntityId = entityId;
            m_cells = cells ?? Array.Empty<Vector2Int>();
            HasSegment = hasSegment;
            Side = side;
            SegmentStart = segmentStart;
            SegmentLength = segmentLength;
        }

        public LevelIssueSeverity Severity { get; }
        public string Message { get; }
        /// <summary>The block or door id the issue is about, or null for a level-wide issue or a missing id.</summary>
        public string EntityId { get; }
        /// <summary>Board cells to highlight, possibly empty. Off-board cells are kept; the canvas clips them.</summary>
        public IReadOnlyList<Vector2Int> Cells => m_cells;
        /// <summary>True when the issue highlights a door span on the wall ring.</summary>
        public bool HasSegment { get; }
        public BoardSide Side { get; }
        /// <summary>
        /// First ring segment of the span, counted like LevelDoorData.Start. A door that runs off its edge keeps its
        /// authored span, so the canvas clips it the way it clips the door bar.
        /// </summary>
        public int SegmentStart { get; }
        public int SegmentLength { get; }

        /// <summary>An issue about a whole level (null cells) or about board cells.</summary>
        public static LevelIssue ForCells(LevelIssueSeverity severity, string message, string entityId,
            Vector2Int[] cells)
        {
            return new LevelIssue(severity, message, entityId, cells, false, BoardSide.Top, 0, 0);
        }

        /// <summary>An issue about a span of ring segments on one side.</summary>
        public static LevelIssue ForSegment(LevelIssueSeverity severity, string message, string entityId,
            BoardSide side, int segmentStart, int segmentLength)
        {
            return new LevelIssue(severity, message, entityId, null, true, side, segmentStart, segmentLength);
        }
    }

    /// <summary>
    /// Lists every problem of a draft level for the issues list: Errors for exactly the rules LevelData.Validate
    /// enforces, each located on the canvas, then Warnings for the stricter checks play makes. HasError(Collect(level))
    /// is true exactly when level.Validate fails, so the list and Save always agree. It never changes the level
    /// (docs/Level-Editor-Design.md section 7).
    /// </summary>
    internal static class LevelDraftIssues
    {
        /// <summary>
        /// Errors first, then warnings; each group runs level-wide, then blocks by index, then doors by index. A null
        /// level (nothing selected) has no issues. A block's piece is the key of its cells (DEC-193), so no warning
        /// compares it with the piece library: a footprint beyond the piece limits is a Validate error.
        /// </summary>
        public static List<LevelIssue> Collect(LevelData level)
            => Collect(level, LevelEffectWarnings.BlockWarnings);

        internal static List<LevelIssue> Collect(LevelData level, IReadOnlyList<ILevelBlockWarning> blockWarnings)
        {
            List<LevelIssue> issues = new List<LevelIssue>();
            if (level == null)
            {
                return issues;
            }

            AddLevelErrors(level, issues);
            for (int index = 0; index < level.BlockCount; index++)
            {
                AddBlockErrors(level, index, issues);
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                AddDoorErrors(level, index, issues);
            }

            if (issues.Count == 0 && !level.Validate(out string validateError))
            {
                // A Validate rule this list cannot locate, such as a missing door list: its own message keeps the
                // list in agreement with Save.
                issues.Add(CreateError(validateError, null, null));
            }

            bool hasError = issues.Count > 0;
            for (int index = 0; index < level.BlockCount; index++)
            {
                AddBlockWarning(level, index, blockWarnings, issues);
            }

            // Validate now checks door colours too, so the runtime layout refusing a valid level is unexpected; its
            // message still shows instead of passing silently.
            if (!hasError && !BoardLayout.TryCreate(level, out _, out string layoutError))
            {
                issues.Add(LevelIssue.ForCells(LevelIssueSeverity.Warning, layoutError, null, null));
            }

            return issues;
        }

        public static bool HasError(IReadOnlyList<LevelIssue> issues)
        {
            for (int index = 0; issues != null && index < issues.Count; index++)
            {
                if (issues[index].Severity == LevelIssueSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddLevelErrors(LevelData level, List<LevelIssue> issues)
        {
            if (!HasText(level.Id))
            {
                issues.Add(CreateError("The level needs an ID.", null, null));
            }

            if (!HasText(level.Name))
            {
                issues.Add(CreateError("The level needs a name.", null, null));
            }

            if (level.Width < LevelBoardResize.k_minSize || level.Width > LevelBoardResize.k_maxWidth
                || level.Height < LevelBoardResize.k_minSize || level.Height > LevelBoardResize.k_maxHeight)
            {
                issues.Add(CreateError(
                    $"The board is {level.Width} x {level.Height}; it must be {LevelBoardResize.k_minSize}-"
                    + $"{LevelBoardResize.k_maxWidth} wide and {LevelBoardResize.k_minSize}-"
                    + $"{LevelBoardResize.k_maxHeight} tall.", null, null));
            }

            if (level.TimerSeconds < LevelData.MinTimerSeconds || level.TimerSeconds > LevelData.MaxTimerSeconds)
            {
                issues.Add(CreateError(
                    $"The timer is {level.TimerSeconds} seconds; it must be {LevelData.MinTimerSeconds}-"
                    + $"{LevelData.MaxTimerSeconds} seconds.",
                    null, null));
            }

            if (level.BlockCount == 0)
            {
                issues.Add(CreateError("The level needs at least one block.", null, null));
            }
        }

        // Mirrors LevelData.ValidateBlocks, but reports every broken rule of the block with its cells.
        private static void AddBlockErrors(LevelData level, int index, List<LevelIssue> issues)
        {
            LevelBlockData block = level.GetBlockAt(index);
            if (block == null)
            {
                issues.Add(CreateError($"Block {index + 1} in the list is empty.", null, null));
                return;
            }

            string subject = GetBlockSubject(block);
            string entityId = GetEntityId(block.Id);
            Vector2Int[] cells = GetBoardCells(block);
            string missing = JoinParts(
                HasText(block.Id) ? null : "an ID",
                HasText(block.ColorId) ? null : "a colour");
            if (missing != null)
            {
                issues.Add(CreateError($"{subject} needs {missing}.", entityId, cells));
            }

            if (block.CellCount == 0)
            {
                issues.Add(CreateError($"{subject} has no cells.", entityId, null));
            }

            if (HasText(block.Id) && HasEarlierBlockId(level, index, block.Id))
            {
                issues.Add(CreateError($"Another block already uses the ID {block.Id}.", entityId, cells));
            }

            List<Vector2Int> found = new List<Vector2Int>();
            AddOffBoardCells(level, block, found);
            if (found.Count > 0)
            {
                string board = $"{level.Width} x {level.Height} board";
                issues.Add(CreateError(found.Count == 1
                    ? $"{subject} has cell {FormatCell(found[0])} off the {board}."
                    : $"{subject} has {found.Count} cells off the {board}.", entityId, found.ToArray()));
            }

            found.Clear();
            AddRepeatedCells(block, found);
            if (found.Count > 0)
            {
                issues.Add(CreateError(found.Count == 1
                    ? $"{subject} lists cell {FormatCell(found[0])} more than once."
                    : $"{subject} lists {found.Count} cells more than once.", entityId, found.ToArray()));
            }

            for (int previous = 0; previous < index; previous++)
            {
                LevelBlockData other = level.GetBlockAt(previous);
                if (other == null)
                {
                    continue;
                }

                found.Clear();
                AddSharedCells(block, other, found);
                if (found.Count > 0)
                {
                    string pair = $"Blocks {GetLabel(other.Id)} and {GetLabel(block.Id)}";
                    issues.Add(CreateError(found.Count == 1
                        ? $"{pair} overlap at {FormatCell(found[0])}."
                        : $"{pair} overlap on {found.Count} cells.", entityId, found.ToArray()));
                }
            }

            found.Clear();
            AddDetachedCells(block, found);
            if (found.Count > 0)
            {
                issues.Add(CreateError(found.Count == 1
                    ? $"{subject} has a cell at {FormatCell(found[0])} that does not touch the rest of the block."
                    : $"{subject} has {found.Count} cells that do not touch the rest of the block.",
                    entityId, found.ToArray()));
            }

            string limit = GetPieceLimitIssue(block, subject);
            if (limit != null)
            {
                issues.Add(CreateError(limit, entityId, cells));
            }
        }

        // Every block is a piece (DEC-193): at most ShapeKey.MaxCells cells inside a ShapeKey.MaxExtent square box.
        // Validate checks it after the rules above, which already report repeated and detached cells, so only the two
        // size limits are left to report here.
        private static string GetPieceLimitIssue(LevelBlockData block, string subject)
        {
            if (block.CellCount > ShapeKey.MaxCells)
            {
                return $"{subject} has {block.CellCount} cells; a piece holds at most {ShapeKey.MaxCells}.";
            }

            if (block.CellCount == 0)
            {
                return null;
            }

            RectInt bounds = LevelShapeThumbnail.GetBounds(LevelRecordCopy.GetCells(block));
            return bounds.width > ShapeKey.MaxExtent || bounds.height > ShapeKey.MaxExtent
                ? $"{subject} spans {bounds.width} x {bounds.height} cells; a piece fits inside {ShapeKey.MaxExtent} x " +
                  $"{ShapeKey.MaxExtent}."
                : null;
        }

        // Mirrors LevelData.ValidateDoors, but reports every broken rule of the door with its span.
        private static void AddDoorErrors(LevelData level, int index, List<LevelIssue> issues)
        {
            LevelDoorData door = level.GetDoorAt(index);
            if (door == null)
            {
                issues.Add(CreateError($"Door {index + 1} in the list is empty.", null, null));
                return;
            }

            string subject = GetDoorSubject(door);
            string entityId = GetEntityId(door.Id);
            string missing = JoinParts(
                HasText(door.Id) ? null : "an ID",
                HasText(door.ColorId) ? null : "a colour");
            if (missing != null)
            {
                issues.Add(CreateDoorIssue(LevelIssueSeverity.Error, $"{subject} needs {missing}.", door));
            }

            if (!LevelEditCommands.IsValidSide(door.Side))
            {
                issues.Add(CreateError($"{subject} is on an unknown edge.", entityId, null));
            }
            else if (door.Length < 1)
            {
                issues.Add(CreateError($"{subject} needs a length of at least one cell.", entityId, null));
            }
            else
            {
                // Same order as Validate, so sideLength - Start cannot overflow.
                int sideLength = LevelEditCommands.GetSideLength(level.Width, level.Height, door.Side);
                if (door.Start < 0 || door.Start >= sideLength || door.Length > sideLength - door.Start)
                {
                    issues.Add(CreateDoorIssue(LevelIssueSeverity.Error,
                        $"{subject} runs off the {sideLength}-cell {door.Side} edge.", door));
                }

                // Gates span 1 to 3 cells (user rule of 2026-09-25, enforced by Validate since schema v2).
                if (door.Length > LevelData.MaxDoorLength)
                {
                    issues.Add(CreateDoorIssue(LevelIssueSeverity.Error,
                        $"{subject} spans {door.Length} cells; gates span 1 to {LevelData.MaxDoorLength}.", door));
                }
            }

            if (HasText(door.ColorId) && !BoardLayout.TryParseDoorColor(door.ColorId, out _))
            {
                issues.Add(CreateDoorIssue(LevelIssueSeverity.Error,
                    $"{subject} uses '{door.ColorId}', which is not a gate colour.", door));
            }

            if (HasText(door.Id) && HasEarlierDoorId(level, index, door.Id))
            {
                issues.Add(CreateDoorIssue(LevelIssueSeverity.Error,
                    $"Another door already uses the ID {door.Id}.", door));
            }

            if (!LevelEditCommands.IsValidSide(door.Side) || door.Length < 1)
            {
                return;
            }

            for (int previous = 0; previous < index; previous++)
            {
                LevelDoorData other = level.GetDoorAt(previous);
                if (other == null || other.Side != door.Side || other.Length < 1)
                {
                    continue;
                }

                // Longs: a malformed door may sit near int.MaxValue; the shared span itself always fits an int.
                long start = Math.Max(other.Start, door.Start);
                long end = Math.Min((long)other.Start + other.Length, (long)door.Start + door.Length);
                if (start < end)
                {
                    issues.Add(LevelIssue.ForSegment(LevelIssueSeverity.Error,
                        $"Doors {GetLabel(other.Id)} and {GetLabel(door.Id)} overlap on the {door.Side} edge.",
                        entityId, door.Side, (int)start, (int)(end - start)));
                }
            }
        }

        private static void AddBlockWarning(LevelData level, int index, IReadOnlyList<ILevelBlockWarning> blockWarnings,
            List<LevelIssue> issues)
        {
            LevelBlockData block = level.GetBlockAt(index);
            if (block == null || !HasText(block.ColorId))
            {
                return;
            }

            string subject = GetBlockSubject(block);
            if (!BoardLayout.TryParseDoorColor(block.ColorId, out _))
            {
                issues.Add(LevelIssue.ForCells(LevelIssueSeverity.Warning,
                    $"{subject} uses '{block.ColorId}', which is not a gate colour.",
                    GetEntityId(block.Id), GetBoardCells(block)));
            }
            else if (!HasDoorColor(level, block.ColorId))
            {
                issues.Add(LevelIssue.ForCells(LevelIssueSeverity.Warning,
                    $"{subject} can never exit: no {block.ColorId} door is on the board.",
                    GetEntityId(block.Id), GetBoardCells(block)));
            }
            else
            {
                for (int warningIndex = 0; warningIndex < blockWarnings.Count; warningIndex++)
                {
                    string message = blockWarnings[warningIndex].GetWarning(level, block, subject);
                    if (message != null)
                    {
                        issues.Add(LevelIssue.ForCells(LevelIssueSeverity.Warning, message,
                            GetEntityId(block.Id), GetBoardCells(block)));
                    }
                }
            }
        }

        private static LevelIssue CreateError(string message, string entityId, Vector2Int[] cells)
        {
            return LevelIssue.ForCells(LevelIssueSeverity.Error, message, entityId, cells);
        }

        // A door on an unknown side or with no length has no span to draw, so its issue carries none.
        private static LevelIssue CreateDoorIssue(LevelIssueSeverity severity, string message, LevelDoorData door)
        {
            string entityId = GetEntityId(door.Id);
            return LevelEditCommands.IsValidSide(door.Side) && door.Length >= 1
                ? LevelIssue.ForSegment(severity, message, entityId, door.Side, door.Start, door.Length)
                : LevelIssue.ForCells(severity, message, entityId, null);
        }

        private static void AddOffBoardCells(LevelData level, LevelBlockData block, List<Vector2Int> found)
        {
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int relative = block.GetCellAt(index);
                // Longs, as in Validate: an origin near int.MaxValue must not wrap back onto the board.
                long x = (long)block.Origin.x + relative.x;
                long y = (long)block.Origin.y + relative.y;
                if (x < 0 || x >= level.Width || y < 0 || y >= level.Height)
                {
                    AddDistinct(found, block.Origin + relative);
                }
            }
        }

        private static void AddRepeatedCells(LevelBlockData block, List<Vector2Int> found)
        {
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int relative = block.GetCellAt(index);
                for (int earlier = 0; earlier < index; earlier++)
                {
                    if (relative == block.GetCellAt(earlier))
                    {
                        AddDistinct(found, block.Origin + relative);
                        break;
                    }
                }
            }
        }

        private static void AddSharedCells(LevelBlockData block, LevelBlockData other, List<Vector2Int> found)
        {
            for (int index = 0; index < block.CellCount; index++)
            {
                Vector2Int cell = block.Origin + block.GetCellAt(index);
                for (int otherIndex = 0; otherIndex < other.CellCount; otherIndex++)
                {
                    if (other.Origin + other.GetCellAt(otherIndex) == cell)
                    {
                        AddDistinct(found, cell);
                        break;
                    }
                }
            }
        }

        // The cells outside the block's largest edge-connected group; on a tie the group of the earliest cell stays.
        // Validate's rule for a block without repeated cells; a repeated cell counts as touching itself, so it is
        // reported once, as a repeat, not also as a split.
        private static void AddDetachedCells(LevelBlockData block, List<Vector2Int> found)
        {
            int count = block.CellCount;
            if (count < 2)
            {
                return;
            }

            int[] group = new int[count];
            int[] groupSize = new int[count];
            int[] queue = new int[count];
            for (int index = 0; index < count; index++)
            {
                group[index] = -1;
            }

            int groupCount = 0;
            for (int seed = 0; seed < count; seed++)
            {
                if (group[seed] >= 0)
                {
                    continue;
                }

                int head = 0;
                int tail = 0;
                queue[tail++] = seed;
                group[seed] = groupCount;
                while (head < tail)
                {
                    Vector2Int cell = block.GetCellAt(queue[head++]);
                    for (int other = 0; other < count; other++)
                    {
                        if (group[other] < 0 && IsTouching(cell, block.GetCellAt(other)))
                        {
                            group[other] = groupCount;
                            queue[tail++] = other;
                        }
                    }
                }

                groupSize[groupCount++] = tail;
            }

            int largest = 0;
            for (int index = 1; index < groupCount; index++)
            {
                if (groupSize[index] > groupSize[largest])
                {
                    largest = index;
                }
            }

            for (int index = 0; groupCount > 1 && index < count; index++)
            {
                if (group[index] != largest)
                {
                    AddDistinct(found, block.Origin + block.GetCellAt(index));
                }
            }
        }

        private static bool IsTouching(Vector2Int a, Vector2Int b)
        {
            long distance = Math.Abs((long)a.x - b.x) + Math.Abs((long)a.y - b.y);
            return distance <= 1;
        }

        private static void AddDistinct(List<Vector2Int> cells, Vector2Int cell)
        {
            if (!cells.Contains(cell))
            {
                cells.Add(cell);
            }
        }

        private static bool HasEarlierBlockId(LevelData level, int index, string id)
        {
            for (int previous = 0; previous < index; previous++)
            {
                LevelBlockData other = level.GetBlockAt(previous);
                if (other != null && string.Equals(other.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasEarlierDoorId(LevelData level, int index, string id)
        {
            for (int previous = 0; previous < index; previous++)
            {
                LevelDoorData other = level.GetDoorAt(previous);
                if (other != null && string.Equals(other.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasDoorColor(LevelData level, string colorId)
        {
            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                if (door != null && string.Equals(door.ColorId, colorId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2Int[] GetBoardCells(LevelBlockData block)
        {
            Vector2Int[] cells = new Vector2Int[block.CellCount];
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] = block.Origin + block.GetCellAt(index);
            }

            return cells;
        }

        private static bool HasText(string value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string GetEntityId(string id)
        {
            return HasText(id) ? id : null;
        }

        private static string GetLabel(string id)
        {
            return HasText(id) ? id : "(no ID)";
        }

        private static string GetBlockSubject(LevelBlockData block)
        {
            return HasText(block.Id) ? "Block " + block.Id : "A block";
        }

        private static string GetDoorSubject(LevelDoorData door)
        {
            if (HasText(door.Id))
            {
                return "Door " + door.Id;
            }

            return LevelEditCommands.IsValidSide(door.Side) ? $"A door on the {door.Side} edge" : "A door";
        }

        private static string FormatCell(Vector2Int cell)
        {
            return $"({cell.x},{cell.y})";
        }

        // "an ID", "a colour" or "an ID and a colour"; a null part is left out, both null gives null.
        private static string JoinParts(string first, string second)
        {
            if (first == null || second == null)
            {
                return first ?? second;
            }

            return first + " and " + second;
        }
    }
}
