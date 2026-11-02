using System;
using System.Globalization;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;

namespace ColorBlockJam.Runtime.Levels
{
    public enum LevelEffectTarget
    {
        Block = 0,
        Door = 1,
        CellObject = 2
    }

    /// <summary>How a board object's cells take part in movement (catalog section 5).</summary>
    public enum LevelCellObjectRole
    {
        None = 0,
        Wall = 1,
        ColorGate = 2,
        Floor = 3,
        Movable = 4,
        WallOrFloor = 5
    }

    public enum LevelEffectValueKind
    {
        Enum = 0,
        Integer = 1,
        Bool = 2,
        PaletteColor = 3,
        BlockId = 4,
        DoorId = 5,
        CellObjectId = 6,
        Cell = 7
    }

    /// <summary>
    /// Checks a converted effect against the level it belongs to. Returns null when the effect fits,
    /// otherwise the message tail after "Level {id} {entity} effect {type} ".
    /// </summary>
    public delegate string LevelEffectCheck(LevelData level, LevelEffectOwner owner, LevelEffectParameters parameters);

    /// <summary>The entity an effect is attached to; exactly one field is set.</summary>
    public readonly struct LevelEffectOwner
    {
        public LevelEffectOwner(LevelBlockData block, LevelDoorData door, LevelCellObjectData cellObject)
        {
            Block = block;
            Door = door;
            CellObject = cellObject;
        }

        public LevelBlockData Block { get; }
        public LevelDoorData Door { get; }
        public LevelCellObjectData CellObject { get; }

        /// <summary>The effect at index on whichever entity this owner holds.</summary>
        public LevelEffectData GetEffectAt(int index)
        {
            if (Block != null)
            {
                return Block.GetEffectAt(index);
            }

            if (Door != null)
            {
                return Door.GetEffectAt(index);
            }

            return CellObject.Effect;
        }

        /// <summary>The validation-message entity name, for example "block b1".</summary>
        public string EntityName
        {
            get
            {
                if (Block != null)
                {
                    return "block " + Block.Id;
                }

                if (Door != null)
                {
                    return "door " + Door.Id;
                }

                return "cell object " + CellObject.Id;
            }
        }
    }

    /// <summary>
    /// One parameter key of an effect definition and the rule its string value must satisfy. A list key
    /// keeps its items in the one string value, separated by commas, for example "Red,Blue"; a cell is
    /// written "x:y" (user decision 2026-09-25, docs/Level-Data-Format.md).
    /// </summary>
    public sealed class LevelEffectKey
    {
        public const char ListSeparator = ',';
        public const char CellSeparator = ':';

        private readonly string[] m_enumValues;

        private LevelEffectKey(string name, bool isRequired, LevelEffectValueKind kind, int min, int max,
            string[] enumValues, bool isList = false, int minItems = 1, int maxItems = 1)
        {
            Name = name;
            IsRequired = isRequired;
            Kind = kind;
            Min = min;
            Max = max;
            m_enumValues = enumValues ?? Array.Empty<string>();
            IsList = isList;
            MinItems = minItems;
            MaxItems = maxItems;
        }

        public string Name { get; }
        public bool IsRequired { get; }

        /// <summary>The rule of the value, or of each item of a list value.</summary>
        public LevelEffectValueKind Kind { get; }

        public int Min { get; }
        public int Max { get; }
        public bool IsList { get; }
        public int MinItems { get; }
        public int MaxItems { get; }
        public int EnumValueCount => m_enumValues.Length;

        public string GetEnumValueAt(int index)
        {
            return m_enumValues[index];
        }

        public static LevelEffectKey Enum(string name, bool isRequired, params string[] values)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.Enum, 0, 0, values);
        }

        public static LevelEffectKey Integer(string name, bool isRequired, int min, int max)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.Integer, min, max, null);
        }

        public static LevelEffectKey Bool(string name, bool isRequired)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.Bool, 0, 0, null);
        }

        public static LevelEffectKey Color(string name, bool isRequired)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.PaletteColor, 0, 0, null);
        }

        public static LevelEffectKey Reference(string name, bool isRequired, LevelEffectValueKind kind)
        {
            return new LevelEffectKey(name, isRequired, kind, 0, 0, null);
        }

        /// <summary>One board cell written "x:y", for example "2:3".</summary>
        public static LevelEffectKey Cell(string name, bool isRequired)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.Cell, 0, 0, null);
        }

        public static LevelEffectKey ColorList(string name, bool isRequired, int minItems, int maxItems)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.PaletteColor, 0, 0, null,
                true, minItems, maxItems);
        }

        public static LevelEffectKey IntegerList(string name, bool isRequired, int min, int max, int minItems,
            int maxItems)
        {
            return new LevelEffectKey(name, isRequired, LevelEffectValueKind.Integer, min, max, null,
                true, minItems, maxItems);
        }

        public static LevelEffectKey ReferenceList(string name, bool isRequired, LevelEffectValueKind kind,
            int minItems, int maxItems)
        {
            return new LevelEffectKey(name, isRequired, kind, 0, 0, null, true, minItems, maxItems);
        }

        /// <summary>
        /// Null when the value follows its rule, otherwise the message tail after
        /// "Level {id} {entity} effect {type} ". Only the canonical spelling passes, so values round-trip.
        /// </summary>
        public string FindProblem(string value)
        {
            if (value == null || !IsList)
            {
                return value != null && IsValidItem(value)
                    ? null
                    : $"key {Name} has value '{value}'; expected {DescribeRule()}.";
            }

            string[] items = SplitItems(value);
            if (items.Length < MinItems || items.Length > MaxItems)
            {
                string count = items.Length == 1 ? "1 item" : $"{items.Length} items";
                return $"key {Name} has {count} in '{value}'; expected {DescribeItemCount()}.";
            }

            for (int index = 0; index < items.Length; index++)
            {
                if (!IsValidItem(items[index]))
                {
                    return $"key {Name} item '{items[index]}' in '{value}' is not {DescribeItem()}.";
                }
            }

            return null;
        }

        /// <summary>The rule text used after "expected" in validation messages.</summary>
        public string DescribeRule()
        {
            return IsList
                ? $"{DescribeItemCount()} separated by '{ListSeparator}', each {DescribeItem()}"
                : DescribeItem();
        }

        /// <summary>The rule of one value, or of one item of a list value.</summary>
        public string DescribeItem()
        {
            switch (Kind)
            {
                case LevelEffectValueKind.Enum:
                    return "one of " + string.Join(", ", m_enumValues);
                case LevelEffectValueKind.Integer:
                    return Max == int.MaxValue
                        ? $"an integer of {Min} or more"
                        : $"an integer from {Min} to {Max}";
                case LevelEffectValueKind.Bool:
                    return "true or false";
                case LevelEffectValueKind.PaletteColor:
                    return "a gate colour name";
                case LevelEffectValueKind.BlockId:
                    return "a block ID";
                case LevelEffectValueKind.DoorId:
                    return "a door ID";
                case LevelEffectValueKind.Cell:
                    return "a cell written x:y";
                default:
                    return "a cell object ID";
            }
        }

        private string DescribeItemCount()
        {
            return MaxItems == int.MaxValue
                ? $"{MinItems} or more items"
                : $"{MinItems} to {MaxItems} items";
        }

        private bool IsValidItem(string value)
        {
            switch (Kind)
            {
                case LevelEffectValueKind.Enum:
                    return FindEnumIndex(value) >= 0;
                case LevelEffectValueKind.Integer:
                    return TryParseInteger(value, out int number) && number >= Min && number <= Max;
                case LevelEffectValueKind.Bool:
                    return TryParseBool(value, out _);
                case LevelEffectValueKind.PaletteColor:
                    return BoardLayout.TryParseDoorColor(value, out _);
                case LevelEffectValueKind.Cell:
                    return TryParseCell(value, out _);
                default:
                    return !string.IsNullOrWhiteSpace(value);
            }
        }

        // An empty value has no items; "Red,,Blue" keeps its empty middle item so it is refused.
        internal static string[] SplitItems(string value)
        {
            return value.Length == 0 ? Array.Empty<string>() : value.Split(ListSeparator);
        }

        // Canonical "x:y" only ("2:3", not "2: 3" or "02:3"), so a cell round-trips unchanged.
        internal static bool TryParseCell(string value, out UnityEngine.Vector2Int cell)
        {
            cell = default;
            int separator = value.IndexOf(CellSeparator);
            if (separator < 0
                || !TryParseInteger(value.Substring(0, separator), out int x)
                || !TryParseInteger(value.Substring(separator + 1), out int y))
            {
                return false;
            }

            cell = new UnityEngine.Vector2Int(x, y);
            return true;
        }

        internal int FindEnumIndex(string value)
        {
            for (int index = 0; index < m_enumValues.Length; index++)
            {
                if (string.Equals(m_enumValues[index], value, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        // Only the canonical invariant spelling is accepted ("7", "-2"), so a value round-trips unchanged.
        internal static bool TryParseInteger(string value, out int number)
        {
            return int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out number)
                && string.Equals(number.ToString(CultureInfo.InvariantCulture), value, StringComparison.Ordinal);
        }

        internal static bool TryParseBool(string value, out bool result)
        {
            switch (value)
            {
                case "true": result = true; return true;
                case "false": result = false; return true;
                default: result = false; return false;
            }
        }
    }

    /// <summary>
    /// The validated values of one effect in definition key order, read by that definition's converter.
    /// </summary>
    public sealed class LevelEffectValues
    {
        private readonly LevelEffectDefinition m_definition;
        private readonly string[] m_values;

        internal LevelEffectValues(LevelEffectDefinition definition, string[] values)
        {
            m_definition = definition;
            m_values = values;
        }

        public string GetText(string key)
        {
            return m_values[m_definition.IndexOfKey(key)];
        }

        public int GetInteger(string key, int missingValue)
        {
            string value = GetText(key);
            return value != null && LevelEffectKey.TryParseInteger(value, out int number) ? number : missingValue;
        }

        public bool GetBool(string key, bool missingValue)
        {
            string value = GetText(key);
            return value != null && LevelEffectKey.TryParseBool(value, out bool result) ? result : missingValue;
        }

        public int GetEnumIndex(string key)
        {
            int keyIndex = m_definition.IndexOfKey(key);
            return m_definition.GetKeyAt(keyIndex).FindEnumIndex(m_values[keyIndex]);
        }

        public PaletteColor GetColor(string key)
        {
            BoardLayout.TryParseDoorColor(GetText(key), out PaletteColor color);
            return color;
        }

        public UnityEngine.Vector2Int GetCell(string key)
        {
            LevelEffectKey.TryParseCell(GetText(key), out UnityEngine.Vector2Int cell);
            return cell;
        }

        /// <summary>The items of a list value in authored order; empty when the key is absent.</summary>
        public string[] GetTexts(string key)
        {
            string value = GetText(key);
            return value == null ? Array.Empty<string>() : LevelEffectKey.SplitItems(value);
        }

        public int[] GetIntegers(string key)
        {
            string[] items = GetTexts(key);
            int[] numbers = new int[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                LevelEffectKey.TryParseInteger(items[index], out numbers[index]);
            }

            return numbers;
        }

        public PaletteColor[] GetColors(string key)
        {
            string[] items = GetTexts(key);
            PaletteColor[] colors = new PaletteColor[items.Length];
            for (int index = 0; index < items.Length; index++)
            {
                BoardLayout.TryParseDoorColor(items[index], out colors[index]);
            }

            return colors;
        }
    }

    /// <summary>
    /// One registered catalog type: target, occupancy role, key rules, the typed converter, the optional
    /// level check and whether gameplay implements it. Instances are immutable.
    /// </summary>
    public sealed class LevelEffectDefinition
    {
        private readonly LevelEffectKey[] m_keys;
        private readonly Func<LevelEffectValues, LevelEffectParameters> m_convert;
        private readonly LevelEffectCheck m_check;

        public LevelEffectDefinition(
            string typeId,
            string catalogId,
            LevelEffectTarget target,
            LevelCellObjectRole role,
            bool isRuntimeImplemented,
            LevelEffectKey[] keys,
            Func<LevelEffectValues, LevelEffectParameters> convert,
            LevelEffectCheck check = null)
        {
            TypeId = typeId;
            CatalogId = catalogId;
            Target = target;
            Role = role;
            IsRuntimeImplemented = isRuntimeImplemented;
            m_keys = keys ?? Array.Empty<LevelEffectKey>();
            m_convert = convert;
            m_check = check;
        }

        public string TypeId { get; }
        public string CatalogId { get; }
        public LevelEffectTarget Target { get; }
        public LevelCellObjectRole Role { get; }

        /// <summary>False until the gameplay rule lands; project scope then refuses levels that use it.</summary>
        public bool IsRuntimeImplemented { get; }

        public int KeyCount => m_keys.Length;

        public LevelEffectKey GetKeyAt(int index)
        {
            return m_keys[index];
        }

        public int IndexOfKey(string name)
        {
            for (int index = 0; index < m_keys.Length; index++)
            {
                if (string.Equals(m_keys[index].Name, name, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        internal LevelEffectParameters Convert(string[] values)
        {
            return m_convert(new LevelEffectValues(this, values));
        }

        internal string Check(LevelData level, LevelEffectOwner owner, LevelEffectParameters parameters)
        {
            return m_check?.Invoke(level, owner, parameters);
        }
    }
}
