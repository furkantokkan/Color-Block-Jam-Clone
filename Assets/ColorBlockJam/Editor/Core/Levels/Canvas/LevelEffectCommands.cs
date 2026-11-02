using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure Effect Editor commands the canvas commands lack: add or remove an effect of a block or gate, set or remove
    /// one param of a block, gate or board-object effect, and change a gate's span. Each returns a new level with one
    /// edit, copied through <see cref="LevelRecordCopy"/> so every other field survives, or refuses with a designer
    /// message; effect refusals are the level validation's own messages. They never change the level they are given
    /// (docs/Level-Editor-Design.md, Effect Editor).
    /// </summary>
    internal static class LevelEffectCommands
    {
        internal const string k_arrowType = LevelEffectTypes.Arrow;
        internal const string k_iceType = LevelEffectTypes.Ice;
        internal const string k_objectEffectIssue =
            "A board object keeps exactly one effect; erase the object on the canvas to remove it.";

        // The block effects the case scope authors (docs/Color-Block-Jam-Mechanics-Guide.md). Gate effects are outside
        // the case scope, and the Barrier is the one board object the Level Editor paints.
        private static readonly string[] s_blockEffectTypes = { k_arrowType, k_iceType };

        public static int BlockEffectTypeCount => s_blockEffectTypes.Length;

        public static string GetBlockEffectType(int index)
        {
            return s_blockEffectTypes[index];
        }

        /// <summary>Whether the Effect Editor authors this type on this target: Arrow and Ice, and the Barrier.</summary>
        public static bool IsEditable(LevelEffectTarget target, string typeId)
        {
            switch (target)
            {
                case LevelEffectTarget.Block:
                    return Array.IndexOf(s_blockEffectTypes, typeId) >= 0;
                case LevelEffectTarget.CellObject:
                    return string.Equals(typeId, LevelBarrierCommands.k_typeId, StringComparison.Ordinal);
                default:
                    return false;
            }
        }

        /// <summary>
        /// A new effect of this type with each required key at <see cref="GetDefaultValue"/>, and no optional key. An
        /// unknown type gets no params, so the validation names it.
        /// </summary>
        public static LevelEffectData CreateEffect(string typeId)
        {
            if (!LevelEffectRegistry.TryFind(typeId, out LevelEffectDefinition definition))
            {
                return new LevelEffectData(typeId, Array.Empty<LevelEffectParamData>());
            }

            List<LevelEffectParamData> pairs = new List<LevelEffectParamData>(definition.KeyCount);
            for (int index = 0; index < definition.KeyCount; index++)
            {
                LevelEffectKey key = definition.GetKeyAt(index);
                if (key.IsRequired)
                {
                    pairs.Add(new LevelEffectParamData(key.Name, GetDefaultValue(key)));
                }
            }

            return new LevelEffectData(typeId, pairs.ToArray());
        }

        /// <summary>The value a newly added key starts with: its first enum value, its minimum, or false.</summary>
        public static string GetDefaultValue(LevelEffectKey key)
        {
            switch (key.Kind)
            {
                case LevelEffectValueKind.Enum:
                    return key.EnumValueCount > 0 ? key.GetEnumValueAt(0) : string.Empty;
                case LevelEffectValueKind.Integer:
                    return key.Min.ToString(CultureInfo.InvariantCulture);
                case LevelEffectValueKind.Bool:
                    return "false";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Appends a new effect of this type (<see cref="CreateEffect"/>) to a block or gate; its other effects keep
        /// their order. Refused for a board object, and with the validation's message for an unknown, doubled or
        /// conflicting type.
        /// </summary>
        public static bool TryAddEffect(LevelData level, LevelEffectTarget target, int index, string typeId,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetEffects(level, target, index, out LevelEffectData[] effects, out issue))
            {
                return false;
            }

            if (target == LevelEffectTarget.CellObject)
            {
                issue = k_objectEffectIssue;
                return false;
            }

            Array.Resize(ref effects, effects.Length + 1);
            effects[effects.Length - 1] = CreateEffect(typeId);
            return TryReplaceEffects(level, target, index, effects, out result, out issue);
        }

        /// <summary>Removes one effect of a block or gate; the others keep their order. A board object refuses.</summary>
        public static bool TryRemoveEffect(LevelData level, LevelEffectTarget target, int index, int effectIndex,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetEffect(level, target, index, effectIndex, out LevelEffectData[] effects, out issue))
            {
                return false;
            }

            if (target == LevelEffectTarget.CellObject)
            {
                issue = k_objectEffectIssue;
                return false;
            }

            LevelEffectData[] remaining = new LevelEffectData[effects.Length - 1];
            for (int source = 0, kept = 0; source < effects.Length; source++)
            {
                if (source != effectIndex)
                {
                    remaining[kept++] = effects[source];
                }
            }

            // Removing an effect cannot break the ones that stay on this entity, so no validation runs.
            result = WithEffects(level, target, index, remaining);
            return true;
        }

        /// <summary>
        /// Sets one param of an effect: the key's value is replaced in place, or the key is appended when the effect
        /// lacks it; every other param keeps its order. Refused with the validation's message when the value breaks
        /// its rule or the entity's check (a closed barrier over a block, for example). False with no issue when the
        /// effect already holds that value. A Barrier's startsOpen goes through the Barrier's own command
        /// (<see cref="LevelBarrierCommands.TrySetStartsOpen"/>), which also treats the default as already held.
        /// </summary>
        public static bool TrySetParam(LevelData level, LevelEffectTarget target, int index, int effectIndex,
            string key, string value, out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetEffect(level, target, index, effectIndex, out LevelEffectData[] effects, out issue))
            {
                return false;
            }

            LevelEffectData effect = effects[effectIndex];
            if (target == LevelEffectTarget.CellObject
                && string.Equals(effect.Type, LevelBarrierCommands.k_typeId, StringComparison.Ordinal)
                && string.Equals(key, LevelBarrierCommands.k_startsOpenKey, StringComparison.Ordinal)
                && (value == "true" || value == "false"))
            {
                return LevelBarrierCommands.TrySetStartsOpen(level, index, value == "true", out result, out issue);
            }

            if (string.Equals(effect.FindValue(key), value, StringComparison.Ordinal))
            {
                return false;
            }

            effects[effectIndex] = WithParam(effect, key, value);
            return TryReplaceEffects(level, target, index, effects, out result, out issue);
        }

        /// <summary>
        /// Removes one param, so the effect falls back to the key's default (a barrier without startsOpen starts
        /// closed); the other params keep their order. Refused with the validation's message for a required key, or
        /// when the default breaks the entity's check. False with no issue when the effect lacks the key.
        /// </summary>
        public static bool TryRemoveParam(LevelData level, LevelEffectTarget target, int index, int effectIndex,
            string key, out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetEffect(level, target, index, effectIndex, out LevelEffectData[] effects, out issue))
            {
                return false;
            }

            LevelEffectData effect = effects[effectIndex];
            List<LevelEffectParamData> pairs = new List<LevelEffectParamData>(effect.ParamCount);
            for (int param = 0; param < effect.ParamCount; param++)
            {
                LevelEffectParamData pair = effect.GetParamAt(param);
                if (pair == null || !string.Equals(pair.Key, key, StringComparison.Ordinal))
                {
                    pairs.Add(pair);
                }
            }

            if (pairs.Count == effect.ParamCount)
            {
                return false;
            }

            effects[effectIndex] = new LevelEffectData(effect.Type, pairs.ToArray());
            return TryReplaceEffects(level, target, index, effects, out result, out issue);
        }

        /// <summary>
        /// Moves a gate to another first segment and length along the same side; its id, side, colour and effects stay.
        /// Refused, with the canvas's gate messages, off the edge, outside 1 to <see cref="LevelData.MaxDoorLength"/>
        /// segments, onto a void or onto a segment another gate holds. False with no issue when the span is unchanged.
        /// </summary>
        public static bool TrySetDoorSpan(LevelData level, int doorIndex, int start, int length,
            out LevelData result, out string issue)
        {
            result = null;
            if (!TryGetDoor(level, doorIndex, out LevelDoorData door, out issue))
            {
                return false;
            }

            if (start == door.Start && length == door.Length)
            {
                return false;
            }

            if (length < 1 || length > LevelData.MaxDoorLength)
            {
                issue = $"Gate {door.Id} would span {length} cells; gates span 1 to {LevelData.MaxDoorLength}.";
                return false;
            }

            BoardSide side = door.Side;
            int sideLength = side == BoardSide.Top || side == BoardSide.Bottom ? level.Width : level.Height;
            for (int segment = start; segment < start + length; segment++)
            {
                if (segment < 0 || segment >= sideLength)
                {
                    issue = $"Segment {segment} is off the {side} edge.";
                    return false;
                }

                Vector2Int behind = LevelEditCommands.GetEdgeCell(level, side, segment);
                if (!level.IsFloor(behind))
                {
                    issue = $"Cell ({behind.x},{behind.y}) behind the {side} edge at {segment} is a void.";
                    return false;
                }

                for (int other = 0; other < level.DoorCount; other++)
                {
                    LevelDoorData holder = level.GetDoorAt(other);
                    if (other != doorIndex && holder != null && holder.Side == side
                        && segment >= holder.Start && segment < holder.Start + holder.Length)
                    {
                        issue = $"The {side} edge at {segment} already holds door {holder.Id}.";
                        return false;
                    }
                }
            }

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            doors[doorIndex] = LevelRecordCopy.WithSpan(door, start, length);
            result = LevelRecordCopy.WithDoors(level, doors);
            return true;
        }

        /// <summary>
        /// The effect with this param's value replaced in place, or appended when the effect lacks the key; every
        /// other param keeps its order. Shared with <see cref="LevelBarrierCommands"/>'s own startsOpen param.
        /// </summary>
        internal static LevelEffectData WithParam(LevelEffectData effect, string key, string value)
        {
            List<LevelEffectParamData> pairs = new List<LevelEffectParamData>(effect.ParamCount + 1);
            bool found = false;
            for (int param = 0; param < effect.ParamCount; param++)
            {
                LevelEffectParamData pair = effect.GetParamAt(param);
                bool isKey = pair != null && string.Equals(pair.Key, key, StringComparison.Ordinal);
                pairs.Add(isKey ? new LevelEffectParamData(key, value) : pair);
                found |= isKey;
            }

            if (!found)
            {
                pairs.Add(new LevelEffectParamData(key, value));
            }

            return new LevelEffectData(effect.Type, pairs.ToArray());
        }

        // The entity's effects as a new array; a board object's one effect is a one-element array.
        private static bool TryGetEffects(LevelData level, LevelEffectTarget target, int index,
            out LevelEffectData[] effects, out string issue)
        {
            effects = null;
            issue = null;
            if (level == null)
            {
                issue = "Select a level first.";
                return false;
            }

            switch (target)
            {
                case LevelEffectTarget.Block:
                    LevelBlockData block = index >= 0 && index < level.BlockCount ? level.GetBlockAt(index) : null;
                    effects = block != null ? LevelRecordCopy.GetEffects(block) : null;
                    issue = block != null ? null : "Select a block first.";
                    break;
                case LevelEffectTarget.Door:
                    LevelDoorData door = index >= 0 && index < level.DoorCount ? level.GetDoorAt(index) : null;
                    effects = door != null ? LevelRecordCopy.GetEffects(door) : null;
                    issue = door != null ? null : "Select a door first.";
                    break;
                default:
                    LevelCellObjectData item = index >= 0 && index < level.CellObjectCount
                        ? level.GetCellObjectAt(index)
                        : null;
                    effects = item?.Effect != null ? new[] { item.Effect } : null;
                    issue = item?.Effect != null ? null : "Select a board object first.";
                    break;
            }

            return effects != null;
        }

        private static bool TryGetEffect(LevelData level, LevelEffectTarget target, int index, int effectIndex,
            out LevelEffectData[] effects, out string issue)
        {
            if (!TryGetEffects(level, target, index, out effects, out issue))
            {
                return false;
            }

            if (effectIndex < 0 || effectIndex >= effects.Length || effects[effectIndex] == null)
            {
                issue = "Select an effect first.";
                return false;
            }

            return true;
        }

        private static bool TryGetDoor(LevelData level, int doorIndex, out LevelDoorData door, out string issue)
        {
            door = level != null && doorIndex >= 0 && doorIndex < level.DoorCount ? level.GetDoorAt(doorIndex) : null;
            issue = level == null ? "Select a level first." : door == null ? "Select a door first." : null;
            return door != null;
        }

        // The level with the entity's effects replaced, when the validation accepts this entity's effects.
        private static bool TryReplaceEffects(LevelData level, LevelEffectTarget target, int index,
            LevelEffectData[] effects, out LevelData result, out string issue)
        {
            LevelData changed = WithEffects(level, target, index, effects);
            issue = GetEffectIssue(changed, target, index);
            result = issue == null ? changed : null;
            return issue == null;
        }

        private static LevelData WithEffects(LevelData level, LevelEffectTarget target, int index,
            LevelEffectData[] effects)
        {
            switch (target)
            {
                case LevelEffectTarget.Block:
                    LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
                    blocks[index] = LevelRecordCopy.WithEffects(blocks[index], effects);
                    return LevelRecordCopy.WithBlocks(level, blocks);
                case LevelEffectTarget.Door:
                    LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
                    doors[index] = LevelRecordCopy.WithEffects(doors[index], effects);
                    return LevelRecordCopy.WithDoors(level, doors);
                default:
                    LevelCellObjectData[] objects = LevelRecordCopy.GetCellObjects(level);
                    objects[index] = LevelRecordCopy.WithEffect(objects[index], effects[0]);
                    return LevelRecordCopy.WithCellObjects(level, objects);
            }
        }

        // The validation's message for this entity's effects alone, or null. The probe keeps the board and every block
        // and gate with its cells, but only this entity's effects and only this board object, so an unrelated authored
        // effect never blocks the edit; the Level Editor's issues list and Save Level still check the whole level.
        private static string GetEffectIssue(LevelData level, LevelEffectTarget target, int index)
        {
            LevelBlockData[] blocks = new LevelBlockData[level.BlockCount];
            for (int block = 0; block < blocks.Length; block++)
            {
                LevelBlockData record = level.GetBlockAt(block);
                blocks[block] = target == LevelEffectTarget.Block && block == index ? record
                    : record == null ? new LevelBlockData(null, null, Vector2Int.zero, Array.Empty<Vector2Int>())
                    : new LevelBlockData(record.Id, record.ColorId, record.Origin, LevelRecordCopy.GetCells(record));
            }

            LevelDoorData[] doors = new LevelDoorData[level.DoorCount];
            for (int door = 0; door < doors.Length; door++)
            {
                LevelDoorData record = level.GetDoorAt(door);
                doors[door] = target == LevelEffectTarget.Door && door == index ? record
                    : record == null ? new LevelDoorData(null, BoardSide.Top, 0, 0, null)
                    : new LevelDoorData(record.Id, record.Side, record.Start, record.Length, record.ColorId);
            }

            LevelCellObjectData[] objects = target == LevelEffectTarget.CellObject
                ? new[] { level.GetCellObjectAt(index) }
                : Array.Empty<LevelCellObjectData>();
            LevelData probe = new LevelData(level.Id, level.Name, level.Width, level.Height, level.TimerSeconds,
                LevelRecordCopy.GetVoidCells(level), doors, blocks, objects);
            return LevelEffectValidator.Validate(probe, out string error) ? null : error;
        }
    }
}
