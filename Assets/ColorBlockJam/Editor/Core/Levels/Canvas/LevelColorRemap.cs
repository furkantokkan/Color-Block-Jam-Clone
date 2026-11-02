using System;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Recolours a whole level when its colour order changes: block and door colours and every effect parameter the
    /// effect registry types as a palette colour, on blocks, doors and board objects. Only the 11 gate colours are
    /// mapped; other values and effects of an unknown type stay. A change of order is a one-to-one map, so every block
    /// keeps matching its gates and validation gives the same result (TASK-20260926-145804549-da16f02f).
    /// </summary>
    internal static class LevelColorRemap
    {
        /// <summary>The level recoloured from one colour order to another, position by position.</summary>
        public static LevelData Apply(LevelData level, LevelColorOrder from, LevelColorOrder to)
        {
            return Recolor(level, color => from.MapTo(to, color));
        }

        /// <summary>
        /// Marks each gate colour the level uses (blocks, doors and effect colour parameters) by its int value. It walks
        /// the same fields as <see cref="Apply"/>, so the two never disagree about where colours are.
        /// </summary>
        public static bool[] GetUsedColors(LevelData level)
        {
            bool[] used = new bool[LevelColorOrder.k_count];
            Recolor(level, color =>
            {
                used[(int)color] = true;
                return color;
            });
            return used;
        }

        /// <summary>
        /// A random new order that recolours at least one colour the level uses; with no colour in use, any random order.
        /// A shuffle that keeps every used colour where it was moves the first used colour to another position.
        /// </summary>
        public static LevelColorOrder Shuffle(LevelData level, LevelColorOrder from, Random random)
        {
            LevelColorOrder to = from.Shuffle(random);
            bool[] used = GetUsedColors(level);
            int firstUsed = -1;
            for (int value = 0; value < used.Length; value++)
            {
                if (!used[value])
                {
                    continue;
                }

                if (from.MapTo(to, (PaletteColor)value) != (PaletteColor)value)
                {
                    return to;
                }

                if (firstUsed < 0)
                {
                    firstUsed = value;
                }
            }

            if (firstUsed < 0)
            {
                return to;
            }

            // The used colour kept its position; trading it with any other position recolours it.
            PaletteColor kept = (PaletteColor)firstUsed;
            int other = (to.IndexOf(kept) + 1 + random.Next(LevelColorOrder.k_count - 1)) % LevelColorOrder.k_count;
            return to.Swap(kept, to.GetColorAt(other));
        }

        /// <summary>
        /// Color Switch's partner for <paramref name="color"/>: a random other gate colour the level does not use, so
        /// the switch visibly changes only that colour; when the level uses every other colour, any other colour
        /// (user order of 2026-09-26, TASK-20260926-145804549-da16f02f fifth revision).
        /// </summary>
        public static PaletteColor PickSwitchPartner(LevelData level, PaletteColor color, Random random)
        {
            bool[] used = GetUsedColors(level);
            int unusedCount = 0;
            for (int value = 0; value < used.Length; value++)
            {
                if (value != (int)color && !used[value])
                {
                    unusedCount++;
                }
            }

            bool onlyUnused = unusedCount > 0;
            int pick = random.Next(onlyUnused ? unusedCount : LevelColorOrder.k_count - 1);
            for (int value = 0; value < used.Length; value++)
            {
                if (value == (int)color || (onlyUnused && used[value]))
                {
                    continue;
                }

                if (pick-- == 0)
                {
                    return (PaletteColor)value;
                }
            }

            throw new InvalidOperationException("No partner colour exists.");
        }

        private static LevelData Recolor(LevelData level, Func<PaletteColor, PaletteColor> map)
        {
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
            for (int index = 0; index < blocks.Length; index++)
            {
                LevelBlockData block = blocks[index];
                blocks[index] = LevelRecordCopy.WithEffects(LevelRecordCopy.WithColor(block, MapId(block.ColorId, map)),
                    MapEffects(LevelRecordCopy.GetEffects(block), map));
            }

            LevelDoorData[] doors = LevelRecordCopy.GetDoors(level);
            for (int index = 0; index < doors.Length; index++)
            {
                LevelDoorData door = doors[index];
                doors[index] = LevelRecordCopy.WithEffects(LevelRecordCopy.WithColor(door, MapId(door.ColorId, map)),
                    MapEffects(LevelRecordCopy.GetEffects(door), map));
            }

            LevelCellObjectData[] cellObjects = LevelRecordCopy.GetCellObjects(level);
            for (int index = 0; index < cellObjects.Length; index++)
            {
                LevelCellObjectData cellObject = cellObjects[index];
                cellObjects[index] = LevelRecordCopy.WithEffect(cellObject, MapEffect(cellObject.Effect, map));
            }

            return LevelRecordCopy.WithBoard(level, level.Width, level.Height, LevelRecordCopy.GetVoidCells(level), doors,
                blocks, cellObjects);
        }

        private static LevelEffectData[] MapEffects(LevelEffectData[] effects, Func<PaletteColor, PaletteColor> map)
        {
            for (int index = 0; index < effects.Length; index++)
            {
                effects[index] = MapEffect(effects[index], map);
            }

            return effects;
        }

        // An effect of an unknown type is kept as it is; only its registry's palette colour keys are mapped.
        private static LevelEffectData MapEffect(LevelEffectData effect, Func<PaletteColor, PaletteColor> map)
        {
            if (effect == null || !LevelEffectRegistry.TryFind(effect.Type, out LevelEffectDefinition definition))
            {
                return effect;
            }

            LevelEffectParamData[] parameters = new LevelEffectParamData[effect.ParamCount];
            bool changed = false;
            for (int index = 0; index < parameters.Length; index++)
            {
                LevelEffectParamData parameter = effect.GetParamAt(index);
                if (parameter == null)
                {
                    continue;
                }

                LevelEffectKey key = FindKey(definition, parameter.Key);
                string value = parameter.Value;
                if (key != null && key.Kind == LevelEffectValueKind.PaletteColor)
                {
                    value = key.IsList ? MapList(value, map) : MapId(value, map);
                }

                changed |= !string.Equals(value, parameter.Value, StringComparison.Ordinal);
                parameters[index] = new LevelEffectParamData(parameter.Key, value);
            }

            return changed ? new LevelEffectData(effect.Type, parameters) : effect;
        }

        private static LevelEffectKey FindKey(LevelEffectDefinition definition, string name)
        {
            for (int index = 0; index < definition.KeyCount; index++)
            {
                LevelEffectKey key = definition.GetKeyAt(index);
                if (string.Equals(key.Name, name, StringComparison.Ordinal))
                {
                    return key;
                }
            }

            return null;
        }

        // A list value keeps its items in one string ("Red,Blue"); each item is mapped on its own.
        private static string MapList(string value, Func<PaletteColor, PaletteColor> map)
        {
            if (value == null)
            {
                return null;
            }

            string[] items = value.Split(LevelEffectKey.ListSeparator);
            for (int index = 0; index < items.Length; index++)
            {
                items[index] = MapId(items[index], map);
            }

            return string.Join(LevelEffectKey.ListSeparator.ToString(), items);
        }

        // A gate colour id is mapped; any other value (Wall, neutral, unknown text) stays.
        private static string MapId(string colorId, Func<PaletteColor, PaletteColor> map)
        {
            return BoardLayout.TryParseDoorColor(colorId, out PaletteColor color) ? map(color).ToString() : colorId;
        }
    }
}
