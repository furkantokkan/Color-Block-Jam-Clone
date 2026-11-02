using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure Level Editor Barrier commands (catalog OBJ-05) and the barrier rule for blocks: paint, erase and Starts open
    /// edits each return a new level or a designer message, and a block may sit on an open barrier cell but not on a
    /// closed one. Refusals reuse the level validation's own messages. They never change the level they are given
    /// (docs/Level-Editor-Design.md, Obstacles).
    /// </summary>
    internal static class LevelBarrierCommands
    {
        internal const string k_typeId = LevelEffectTypes.Barrier;
        internal const string k_startsOpenKey = "startsOpen";
        private const string k_idPrefix = "barrier-";
        private static readonly Vector2Int[] s_neighbours =
        {
            Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down
        };
        private static readonly Vector2Int[] s_oneCell = { Vector2Int.zero };

        /// <summary>True for a barrier board object, whatever its Starts open value.</summary>
        public static bool IsBarrier(LevelCellObjectData item)
        {
            return item != null && string.Equals(item.Effect?.Type, k_typeId, StringComparison.Ordinal);
        }

        /// <summary>True for a barrier whose startsOpen reads true; a missing or unreadable value starts closed.</summary>
        public static bool StartsOpen(LevelCellObjectData item)
        {
            return IsBarrier(item) && item.TryGetEffect(out BarrierObject barrier) && barrier.StartsOpen;
        }

        /// <summary>A barrier that starts closed: the board object no block cell may share.</summary>
        public static bool IsClosedBarrier(LevelCellObjectData item)
        {
            return IsBarrier(item) && !StartsOpen(item);
        }

        /// <summary>Index of the first closed barrier at this cell, or -1.</summary>
        public static int GetBlockingObjectIndex(LevelData level, Vector2Int cell)
        {
            for (int index = 0; level != null && index < level.CellObjectCount; index++)
            {
                LevelCellObjectData item = level.GetCellObjectAt(index);
                if (IsClosedBarrier(item) && item.ContainsCell(cell))
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// Why a cell of block blockId may not go on this cell because a closed barrier covers it, in the validation's
        /// words, or null when none does. The block place, move and cell commands use it.
        /// </summary>
        public static string GetBlockCellIssue(LevelData level, string blockId, Vector2Int cell)
        {
            int index = GetBlockingObjectIndex(level, cell);
            if (index < 0)
            {
                return null;
            }

            LevelCellObjectData item = level.GetCellObjectAt(index);
            LevelBlockData probeBlock = new LevelBlockData(blockId, null, cell, s_oneCell);
            return GetValidationIssue(level, new[] { probeBlock }, item)
                   ?? $"Board object {item.Id} covers the cell ({cell.x},{cell.y}).";
        }

        /// <summary>
        /// Why the Barrier brush may not paint this cell whatever its Starts open state, or null: the cell must be board
        /// floor with no other board object on it. A closed barrier also refuses a block cell (<see cref="IsUnderBlock"/>).
        /// </summary>
        public static string GetCellIssue(LevelData level, Vector2Int cell)
        {
            if (level == null || !level.IsFloor(cell))
            {
                return $"Cell ({cell.x},{cell.y}) is not board floor.";
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData item = level.GetCellObjectAt(index);
                if (item != null && !IsBarrier(item) && item.ContainsCell(cell))
                {
                    return $"Obstacle {item.Id} occupies that cell.";
                }
            }

            return null;
        }

        /// <summary>True when a block cell covers this board cell.</summary>
        public static bool IsUnderBlock(LevelData level, Vector2Int cell)
        {
            for (int index = 0; level != null && index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int part = 0; block != null && part < block.CellCount; part++)
                {
                    if (block.Origin + block.GetCellAt(part) == cell)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// One Barrier brush stroke as one result. Paint adds the stroke's free floor cells: each connected group of new
        /// cells becomes one new barrier with no params, so it starts closed. A group that covers or touches existing
        /// barriers joins them; the first-listed barrier's id and effect win, its startsOpen and every other param
        /// included, and the others are removed. Cells on voids or other board objects are skipped and named in issue.
        /// A closed result over a block cell refuses the whole stroke with the validation's message. Erase removes the
        /// stroke's barrier cells: a barrier that falls apart splits into connected barriers with the same effect, the
        /// first keeping its id, and an empty one is removed. False, with no result, when nothing changes.
        /// </summary>
        public static bool TryApplyStroke(LevelData level, IReadOnlyList<Vector2Int> stroke, bool paint,
            out LevelData result, out string issue)
        {
            result = null;
            issue = null;
            if (level == null)
            {
                issue = "Select a level first.";
                return false;
            }

            if (stroke == null || stroke.Count == 0)
            {
                return false;
            }

            List<LevelCellObjectData> objects = new List<LevelCellObjectData>(LevelRecordCopy.GetCellObjects(level));
            bool changed = paint
                ? Paint(level, objects, stroke, ref issue)
                : Erase(objects, stroke);
            if (changed)
            {
                result = LevelRecordCopy.WithCellObjects(level, objects.ToArray());
            }

            return changed;
        }

        /// <summary>
        /// Sets one barrier's startsOpen param; its id, cells and other params stay. Closing a barrier over a block cell
        /// is refused with the validation's message. False, with no issue, when the barrier already has that state.
        /// The Level Editor has no control for it; the Effect Editor edits barrier params (user decision 2026-09-25).
        /// </summary>
        public static bool TrySetStartsOpen(LevelData level, int objectIndex, bool startsOpen, out LevelData result,
            out string issue)
        {
            result = null;
            issue = null;
            if (level == null)
            {
                issue = "Select a level first.";
                return false;
            }

            LevelCellObjectData item = objectIndex >= 0 && objectIndex < level.CellObjectCount
                ? level.GetCellObjectAt(objectIndex)
                : null;
            if (!IsBarrier(item))
            {
                issue = "Select a barrier first.";
                return false;
            }

            if (StartsOpen(item) == startsOpen)
            {
                return false;
            }

            LevelCellObjectData changed = new LevelCellObjectData(item.Id, LevelRecordCopy.GetCells(item),
                WithStartsOpen(item.Effect, startsOpen));
            issue = GetClosedIssue(level, changed);
            if (issue != null)
            {
                return false;
            }

            LevelCellObjectData[] objects = LevelRecordCopy.GetCellObjects(level);
            objects[objectIndex] = changed;
            result = LevelRecordCopy.WithCellObjects(level, objects);
            return true;
        }

        /// <summary>
        /// The runtime-support message Save Level shows while barrier gameplay is not implemented (decision 3(a) of
        /// TASK-20260925-102907480-9e5cbf34), or null when the level has no barrier or the rule is implemented. It asks
        /// LevelData.ValidateRuntimeSupport about the level's barriers alone, so other effects save as before.
        /// </summary>
        public static string GetSaveIssue(LevelData level)
        {
            List<LevelCellObjectData> barriers = null;
            for (int index = 0; level != null && index < level.CellObjectCount; index++)
            {
                LevelCellObjectData item = level.GetCellObjectAt(index);
                if (IsBarrier(item))
                {
                    barriers = barriers ?? new List<LevelCellObjectData>();
                    barriers.Add(item);
                }
            }

            if (barriers == null)
            {
                return null;
            }

            LevelData probe = new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                Array.Empty<Vector2Int>(), Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>(), barriers.ToArray());
            return probe.ValidateRuntimeSupport(out string error) ? null : error;
        }

        /// <summary>A new barrier's effect: no params, so startsOpen takes its default, false (closed).</summary>
        public static LevelEffectData CreateEffect()
        {
            return new LevelEffectData(k_typeId, Array.Empty<LevelEffectParamData>());
        }

        private static bool Paint(LevelData level, List<LevelCellObjectData> objects, IReadOnlyList<Vector2Int> stroke,
            ref string issue)
        {
            int width = level.Width;
            int height = level.Height;
            int[] owner = CreateOwnerGrid(level, objects);
            bool[] isNew = new bool[width * height];
            List<Vector2Int> added = CollectNewCells(level, owner, stroke, isNew, ref issue);
            if (added.Count == 0)
            {
                return false;
            }

            // Each connected group of new and touched barrier cells becomes one barrier.
            int[] group = new int[width * height];
            List<Vector2Int> open = new List<Vector2Int>();
            List<int> touched = new List<int>();
            List<int> absorbed = new List<int>();
            List<LevelCellObjectData> created = new List<LevelCellObjectData>();
            int groupCount = 0;
            for (int start = 0; start < added.Count; start++)
            {
                Vector2Int seed = added[start];
                if (group[GetKey(seed, width)] != 0)
                {
                    continue;
                }

                groupCount++;
                touched.Clear();
                open.Clear();
                FloodGroup(level, seed, groupCount, owner, isNew, group, open, touched);

                // The merge rule: the first-listed touched barrier's id and whole effect win, applied next.
                LevelCellObjectData barrierObject =
                    CreateMergedBarrier(objects, touched, added, group, groupCount, width, created);
                string closed = GetClosedIssue(level, barrierObject);
                if (closed != null)
                {
                    issue = closed;
                    return false;
                }

                if (touched.Count == 0)
                {
                    created.Add(barrierObject);
                    continue;
                }

                objects[touched[0]] = barrierObject;
                for (int index = 1; index < touched.Count; index++)
                {
                    absorbed.Add(touched[index]);
                }
            }

            absorbed.Sort();
            for (int index = absorbed.Count - 1; index >= 0; index--)
            {
                objects.RemoveAt(absorbed[index]);
            }

            objects.AddRange(created);
            return true;
        }

        // The barrier index at each on-board cell (the stroke's owner grid), or -1.
        private static int[] CreateOwnerGrid(LevelData level, List<LevelCellObjectData> objects)
        {
            int[] owner = new int[level.Width * level.Height];
            for (int index = 0; index < owner.Length; index++)
            {
                owner[index] = -1;
            }

            for (int index = 0; index < objects.Count; index++)
            {
                LevelCellObjectData item = objects[index];
                if (!IsBarrier(item))
                {
                    continue;
                }

                for (int part = 0; part < item.CellCount; part++)
                {
                    Vector2Int cell = item.GetCellAt(part);
                    if (IsOnBoard(level, cell))
                    {
                        owner[GetKey(cell, level.Width)] = index;
                    }
                }
            }

            return owner;
        }

        // The stroke's free floor cells, marking each into isNew; the first refusal, if any, is written into issue.
        private static List<Vector2Int> CollectNewCells(LevelData level, int[] owner, IReadOnlyList<Vector2Int> stroke,
            bool[] isNew, ref string issue)
        {
            List<Vector2Int> added = new List<Vector2Int>();
            for (int index = 0; index < stroke.Count; index++)
            {
                Vector2Int cell = stroke[index];
                if (!IsOnBoard(level, cell))
                {
                    continue;
                }

                int key = GetKey(cell, level.Width);
                if (owner[key] >= 0 || isNew[key])
                {
                    continue;
                }

                string refusal = GetCellIssue(level, cell);
                if (refusal != null)
                {
                    issue = issue ?? refusal;
                    continue;
                }

                isNew[key] = true;
                added.Add(cell);
            }

            return added;
        }

        // Flood-fills one connected group of new and touched barrier cells from seed into group under groupId,
        // collecting every touched barrier index into touched; open is the caller's cleared work list.
        private static void FloodGroup(LevelData level, Vector2Int seed, int groupId, int[] owner, bool[] isNew,
            int[] group, List<Vector2Int> open, List<int> touched)
        {
            int width = level.Width;
            open.Add(seed);
            group[GetKey(seed, width)] = groupId;
            for (int read = 0; read < open.Count; read++)
            {
                Vector2Int cell = open[read];
                int barrier = owner[GetKey(cell, width)];
                if (barrier >= 0 && !touched.Contains(barrier))
                {
                    touched.Add(barrier);
                }

                for (int direction = 0; direction < s_neighbours.Length; direction++)
                {
                    Vector2Int next = cell + s_neighbours[direction];
                    if (!IsOnBoard(level, next))
                    {
                        continue;
                    }

                    int key = GetKey(next, width);
                    if (group[key] == 0 && (isNew[key] || owner[key] >= 0))
                    {
                        group[key] = groupId;
                        open.Add(next);
                    }
                }
            }
        }

        // The merge rule: the first-listed touched barrier's id and whole effect win, or a new object with the
        // lowest free id when none touched.
        private static LevelCellObjectData CreateMergedBarrier(List<LevelCellObjectData> objects, List<int> touched,
            List<Vector2Int> added, int[] group, int groupId, int width, List<LevelCellObjectData> created)
        {
            touched.Sort();
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int index = 0; index < touched.Count; index++)
            {
                cells.AddRange(LevelRecordCopy.GetCells(objects[touched[index]]));
            }

            for (int index = 0; index < added.Count; index++)
            {
                Vector2Int cell = added[index];
                if (group[GetKey(cell, width)] == groupId)
                {
                    cells.Add(cell);
                }
            }

            return touched.Count > 0
                ? new LevelCellObjectData(objects[touched[0]].Id, cells.ToArray(), objects[touched[0]].Effect)
                : new LevelCellObjectData(GetNewId(objects, created), cells.ToArray(), CreateEffect());
        }

        // The flat index of a cell on a board of this width, matching every array above.
        private static int GetKey(Vector2Int cell, int width)
        {
            return cell.y * width + cell.x;
        }

        private static bool Erase(List<LevelCellObjectData> objects, IReadOnlyList<Vector2Int> stroke)
        {
            bool changed = false;
            List<int> emptied = new List<int>();
            List<LevelCellObjectData> pieces = new List<LevelCellObjectData>();
            for (int index = 0; index < objects.Count; index++)
            {
                LevelCellObjectData item = objects[index];
                if (!IsBarrier(item))
                {
                    continue;
                }

                List<Vector2Int> remaining = new List<Vector2Int>(item.CellCount);
                for (int part = 0; part < item.CellCount; part++)
                {
                    Vector2Int cell = item.GetCellAt(part);
                    if (!Contains(stroke, cell))
                    {
                        remaining.Add(cell);
                    }
                }

                if (remaining.Count == item.CellCount)
                {
                    continue;
                }

                changed = true;
                if (remaining.Count == 0)
                {
                    emptied.Add(index);
                    continue;
                }

                List<Vector2Int[]> parts = SplitConnected(remaining);
                objects[index] = new LevelCellObjectData(item.Id, parts[0], item.Effect);
                for (int part = 1; part < parts.Count; part++)
                {
                    pieces.Add(new LevelCellObjectData(GetNewId(objects, pieces), parts[part], item.Effect));
                }
            }

            for (int index = emptied.Count - 1; index >= 0; index--)
            {
                objects.RemoveAt(emptied[index]);
            }

            objects.AddRange(pieces);
            return changed;
        }

        // The cells' four-connected groups, each in the cells' order; the group of the first cell comes first.
        private static List<Vector2Int[]> SplitConnected(List<Vector2Int> cells)
        {
            List<Vector2Int[]> parts = new List<Vector2Int[]>();
            bool[] used = new bool[cells.Count];
            List<int> part = new List<int>();
            for (int start = 0; start < cells.Count; start++)
            {
                if (used[start])
                {
                    continue;
                }

                part.Clear();
                part.Add(start);
                used[start] = true;
                for (int read = 0; read < part.Count; read++)
                {
                    Vector2Int cell = cells[part[read]];
                    for (int other = 0; other < cells.Count; other++)
                    {
                        Vector2Int step = cells[other] - cell;
                        if (!used[other] && Mathf.Abs(step.x) + Mathf.Abs(step.y) == 1)
                        {
                            used[other] = true;
                            part.Add(other);
                        }
                    }
                }

                part.Sort();
                Vector2Int[] group = new Vector2Int[part.Count];
                for (int index = 0; index < group.Length; index++)
                {
                    group[index] = cells[part[index]];
                }

                parts.Add(group);
            }

            return parts;
        }

        // The validation's message when this barrier starts closed over a block cell, else null.
        private static string GetClosedIssue(LevelData level, LevelCellObjectData barrier)
        {
            if (StartsOpen(barrier))
            {
                return null;
            }

            bool covered = false;
            for (int part = 0; part < barrier.CellCount && !covered; part++)
            {
                covered = IsUnderBlock(level, barrier.GetCellAt(part));
            }

            if (!covered)
            {
                return null;
            }

            // Effects stay off the probe blocks, so only this barrier's own check can speak.
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount];
            for (int index = 0; index < blocks.Length; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                blocks[index] = block == null
                    ? new LevelBlockData(null, null, Vector2Int.zero, Array.Empty<Vector2Int>())
                    : new LevelBlockData(block.Id, block.ColorId, block.Origin, LevelRecordCopy.GetCells(block));
            }

            return GetValidationIssue(level, blocks, barrier)
                   ?? $"Barrier {barrier.Id} starts closed on a block.";
        }

        // LevelEffectValidator's message for one board object against these blocks, with no doors or other objects.
        private static string GetValidationIssue(LevelData level, LevelBlockData[] blocks, LevelCellObjectData item)
        {
            LevelData probe = new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                Array.Empty<Vector2Int>(), Array.Empty<LevelDoorData>(), blocks, new[] { item });
            return LevelEffectValidator.Validate(probe, out string error) ? null : error;
        }

        // The effect with startsOpen set, every other param kept in order.
        private static LevelEffectData WithStartsOpen(LevelEffectData effect, bool startsOpen)
        {
            return LevelEffectCommands.WithParam(effect, k_startsOpenKey, startsOpen ? "true" : "false");
        }

        // The lowest "barrier-{n}" no listed or new board object uses.
        private static string GetNewId(List<LevelCellObjectData> objects, List<LevelCellObjectData> created)
        {
            for (int number = 1; ; number++)
            {
                string id = k_idPrefix + number;
                if (!HasId(objects, id) && !HasId(created, id))
                {
                    return id;
                }
            }
        }

        private static bool HasId(List<LevelCellObjectData> objects, string id)
        {
            for (int index = 0; index < objects.Count; index++)
            {
                if (objects[index] != null && string.Equals(objects[index].Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(IReadOnlyList<Vector2Int> cells, Vector2Int cell)
        {
            for (int index = 0; index < cells.Count; index++)
            {
                if (cells[index] == cell)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsOnBoard(LevelData level, Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < level.Width && cell.y >= 0 && cell.y < level.Height;
        }
    }
}
