using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>How strictly a level database is read.</summary>
    public enum LevelReadMode
    {
        /// <summary>
        /// The game's pack load only (LevelCatalogLoader.LoadPackAsync): authoring rules plus the runtime
        /// support gate of every effect.
        /// </summary>
        Runtime = 0,

        /// <summary>
        /// The default read (Level Editor, importers, tests): every catalog type, implemented in
        /// gameplay or not.
        /// </summary>
        Authoring = 1
    }

    /// <summary>
    /// Reads and writes the canonical level JSON, formatVersion 3 (DEC-193), without file or asset access.
    /// Every pack of a level database is one such document (DEC-196): the game reads the pack of the level it
    /// plays; the editor writes the packs a save changes after validation. Cells, origins and void cells
    /// are CellText strings, effect params are one object written in definition key order, empty effect, cell
    /// object and void cell lists are omitted, and a level's optional pieces table names its non-library pieces.
    /// Only version 3 is read (docs/Level-Data-Format.md).
    /// </summary>
    public static class LevelDatabaseJson
    {
        private const int k_formatVersion = 3;
        private const string k_formatVersionProperty = "formatVersion";

        private static readonly JsonSerializerSettings s_jsonSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            // Parameter values are read as raw tokens; a date-like string must stay a string.
            DateParseHandling = DateParseHandling.None
        };

        private static readonly string s_versionError =
            $"Expected level database formatVersion {k_formatVersion} and a levels array.";

        /// <summary>
        /// Authoring read, so every Level Editor path accepts levels whose effects gameplay does not
        /// implement yet. Only the game's project-scope load passes LevelReadMode.Runtime.
        /// </summary>
        public static bool TryRead(string json, out LevelData[] levels, out string error)
        {
            return TryRead(json, LevelReadMode.Authoring, out levels, out error);
        }

        public static bool TryRead(string json, LevelReadMode mode, out LevelData[] levels, out string error)
        {
            levels = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The JSON file is empty.";
                return false;
            }

            DatabaseDto document;
            try
            {
                JToken token = JToken.Parse(json, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
                });
                if (!(token is JObject root) || ContainsMetadata(token))
                {
                    error = "The JSON must be a database object without type metadata.";
                    return false;
                }

                // An older file is refused before its fields are read, so the message names the version.
                if (!IsCurrentVersion(root))
                {
                    error = s_versionError;
                    return false;
                }

                document = JsonConvert.DeserializeObject<DatabaseDto>(json, s_jsonSettings);
            }
            catch (JsonException exception)
            {
                error = $"Invalid level JSON: {exception.Message}";
                return false;
            }

            if (document == null || document.levels == null)
            {
                error = s_versionError;
                return false;
            }

            LevelData[] parsed = new LevelData[document.levels.Length];
            for (int index = 0; index < document.levels.Length; index++)
            {
                if (!TryReadLevel(document.levels[index], index, out parsed[index], out error))
                {
                    return false;
                }
            }

            // Each level already ran LevelData.Validate inside TryReadLevel; only the cross-level
            // duplicate-ID check is still needed here (Runtime-Core-2-F6).
            if (!TryCheckUniqueIds(parsed, out error))
            {
                return false;
            }

            for (int index = 0; mode == LevelReadMode.Runtime && index < parsed.Length; index++)
            {
                if (!parsed[index].ValidateRuntimeSupport(out error))
                {
                    return false;
                }
            }

            levels = parsed;
            error = null;
            return true;
        }

        public static bool TryWrite(IReadOnlyList<LevelData> levels, out string json, out string error)
        {
            json = null;
            if (levels == null)
            {
                error = "Level data is missing.";
                return false;
            }

            if (!ValidateEntries(levels, out error))
            {
                return false;
            }

            DatabaseDto document = new DatabaseDto
            {
                formatVersion = k_formatVersion,
                levels = new LevelDto[levels.Count]
            };
            for (int index = 0; index < levels.Count; index++)
            {
                document.levels[index] = WriteLevel(levels[index]);
            }

            json = JsonConvert.SerializeObject(document, Formatting.Indented, s_jsonSettings);
            error = null;
            return true;
        }

        // A formatVersion of exactly 3; a large integer, a float or a string is another version.
        private static bool IsCurrentVersion(JObject root)
        {
            return root.TryGetValue(k_formatVersionProperty, out JToken version)
                && version is JValue value && value.Value is long number && number == k_formatVersion;
        }

        private static bool TryReadLevel(LevelDto source, int index, out LevelData level, out string error)
        {
            level = null;
            if (source == null || source.board == null || source.doors == null || source.blocks == null)
            {
                error = $"Level {index} is missing board, doors or blocks.";
                return false;
            }

            Vector2Int[] voidCells = Array.Empty<Vector2Int>();
            if (source.board.voidCells != null
                && !CellText.TryParseCells(source.board.voidCells, out voidCells, out string problem))
            {
                error = $"Level {source.id} void cells: {problem}.";
                return false;
            }

            if (!TryReadDoors(source, out LevelDoorData[] doors, out error)
                || !TryReadBlocks(source, out LevelBlockData[] blocks, out error)
                || !TryReadCellObjects(source, out LevelCellObjectData[] cellObjects, out error)
                || !TryReadPieceNames(source, out LevelPieceName[] pieceNames, out error))
            {
                return false;
            }

            level = new LevelData(
                source.id,
                source.name,
                source.board.width,
                source.board.height,
                source.timerSeconds,
                voidCells,
                doors,
                blocks,
                cellObjects,
                pieceNames);
            if (!level.Validate(out error))
            {
                return false;
            }

            return true;
        }

        private static bool TryReadDoors(LevelDto source, out LevelDoorData[] doors, out string error)
        {
            doors = new LevelDoorData[source.doors.Length];
            for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
            {
                DoorDto door = source.doors[doorIndex];
                if (door == null || !TryReadSide(door.side, out BoardSide side))
                {
                    error = $"Level {source.id} door {doorIndex} is empty or has an invalid side.";
                    return false;
                }

                if (!TryReadEffects(door.effects, $"Level {source.id} door {door.id}",
                        out LevelEffectData[] effects, out error))
                {
                    return false;
                }

                doors[doorIndex] = new LevelDoorData(
                    door.id, side, door.start, door.length, door.colorId, effects);
            }

            error = null;
            return true;
        }

        private static bool TryReadBlocks(LevelDto source, out LevelBlockData[] blocks, out string error)
        {
            blocks = new LevelBlockData[source.blocks.Length];
            for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
            {
                BlockDto block = source.blocks[blockIndex];
                if (block == null)
                {
                    error = $"Level {source.id} block {blockIndex} is empty.";
                    return false;
                }

                string entity = $"Level {source.id} block {block.id}";
                if (!CellText.TryParseCell(block.origin, out Vector2Int origin, out string problem))
                {
                    error = $"{entity} origin: {problem}.";
                    return false;
                }

                if (!CellText.TryParseCells(block.cells, out Vector2Int[] cells, out problem))
                {
                    error = $"{entity} cells: {problem}.";
                    return false;
                }

                if (!TryReadEffects(block.effects, entity, out LevelEffectData[] effects, out error))
                {
                    return false;
                }

                blocks[blockIndex] = new LevelBlockData(block.id, block.colorId, origin, cells, effects);
            }

            error = null;
            return true;
        }

        private static bool TryReadCellObjects(
            LevelDto source, out LevelCellObjectData[] cellObjects, out string error)
        {
            int count = source.cellObjects?.Length ?? 0;
            cellObjects = new LevelCellObjectData[count];
            for (int objectIndex = 0; objectIndex < count; objectIndex++)
            {
                CellObjectDto cellObject = source.cellObjects[objectIndex];
                if (cellObject == null || cellObject.effect == null)
                {
                    error = $"Level {source.id} cell object {objectIndex} has missing cells or effect.";
                    return false;
                }

                string entity = $"Level {source.id} cell object {cellObject.id}";
                if (!CellText.TryParseCells(cellObject.cells, out Vector2Int[] cells, out string problem))
                {
                    error = $"{entity} cells: {problem}.";
                    return false;
                }

                if (!TryReadEffect(cellObject.effect, entity, out LevelEffectData effect, out error))
                {
                    return false;
                }

                cellObjects[objectIndex] = new LevelCellObjectData(cellObject.id, cells, effect);
            }

            error = null;
            return true;
        }

        // A piece key must be the canonical text of a piece; LevelData.Validate checks names and repeats.
        private static bool TryReadPieceNames(LevelDto source, out LevelPieceName[] pieceNames, out string error)
        {
            int count = source.pieces?.Length ?? 0;
            pieceNames = new LevelPieceName[count];
            for (int pieceIndex = 0; pieceIndex < count; pieceIndex++)
            {
                PieceDto piece = source.pieces[pieceIndex];
                if (piece == null || !ShapeKey.TryParse(piece.key, out ShapeKey key))
                {
                    error = $"Level {source.id} piece {pieceIndex} has key '{piece?.key}', which is not the "
                            + $"{ShapeKey.TextLength}-digit key of a piece.";
                    return false;
                }

                pieceNames[pieceIndex] = new LevelPieceName(key, piece.name);
            }

            error = null;
            return true;
        }

        // An omitted list is empty.
        private static bool TryReadEffects(EffectDto[] source, string entity, out LevelEffectData[] effects,
            out string error)
        {
            int count = source?.Length ?? 0;
            effects = new LevelEffectData[count];
            for (int index = 0; index < count; index++)
            {
                if (!TryReadEffect(source[index], entity, out effects[index], out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Only the record shape is read here; LevelData.Validate checks types, keys and values.
        private static bool TryReadEffect(EffectDto source, string entity, out LevelEffectData effect,
            out string error)
        {
            effect = null;
            if (source == null || source.type == null || source.@params == null)
            {
                error = $"{entity} has an effect without type or params.";
                return false;
            }

            LevelEffectParamData[] parameters = new LevelEffectParamData[source.@params.Count];
            int next = 0;
            foreach (JProperty param in source.@params.Properties())
            {
                // Values are strings (DEC-MECH-1); a number, bool, null, object or array would not round-trip
                // unchanged. The first parse already refused a key listed twice.
                if (param.Value.Type != JTokenType.String)
                {
                    error = $"{entity} effect {source.type} parameter {param.Name} needs a JSON string value.";
                    return false;
                }

                // An empty key or value reaches LevelData.Validate ("has an empty key", "has value ''").
                parameters[next++] = new LevelEffectParamData(param.Name, (string)param.Value);
            }

            effect = new LevelEffectData(source.type, parameters);
            error = null;
            return true;
        }

        private static LevelDto WriteLevel(LevelData source)
        {
            LevelDto result = new LevelDto
            {
                id = source.Id,
                name = source.Name,
                board = new BoardDto
                {
                    width = source.Width,
                    height = source.Height,
                    voidCells = source.VoidCellCount == 0
                        ? null
                        : WriteCells(source.VoidCellCount, source.GetVoidCellAt)
                },
                timerSeconds = source.TimerSeconds,
                doors = new DoorDto[source.DoorCount],
                blocks = new BlockDto[source.BlockCount],
                cellObjects = source.CellObjectCount == 0 ? null : new CellObjectDto[source.CellObjectCount],
                pieces = source.PieceNameCount == 0 ? null : new PieceDto[source.PieceNameCount]
            };

            for (int index = 0; index < source.DoorCount; index++)
            {
                result.doors[index] = WriteDoor(source.GetDoorAt(index));
            }

            for (int index = 0; index < source.BlockCount; index++)
            {
                result.blocks[index] = WriteBlock(source.GetBlockAt(index));
            }

            for (int index = 0; index < source.CellObjectCount; index++)
            {
                result.cellObjects[index] = WriteCellObject(source.GetCellObjectAt(index));
            }

            for (int index = 0; index < source.PieceNameCount; index++)
            {
                LevelPieceName piece = source.GetPieceNameAt(index);
                result.pieces[index] = new PieceDto { key = piece.Key.ToString(), name = piece.Name };
            }

            return result;
        }

        private static DoorDto WriteDoor(LevelDoorData door)
        {
            return new DoorDto
            {
                id = door.Id,
                side = door.Side.ToString(),
                start = door.Start,
                length = door.Length,
                colorId = door.ColorId,
                effects = WriteEffects(door.EffectCount, door.GetEffectAt)
            };
        }

        private static BlockDto WriteBlock(LevelBlockData block)
        {
            return new BlockDto
            {
                id = block.Id,
                colorId = block.ColorId,
                origin = CellText.Format(block.Origin),
                cells = WriteCells(block.CellCount, block.GetCellAt),
                effects = WriteEffects(block.EffectCount, block.GetEffectAt)
            };
        }

        private static CellObjectDto WriteCellObject(LevelCellObjectData cellObject)
        {
            return new CellObjectDto
            {
                id = cellObject.Id,
                cells = WriteCells(cellObject.CellCount, cellObject.GetCellAt),
                effect = WriteEffect(cellObject.Effect)
            };
        }

        // Save time only: the records expose cells by index, and CellText formats a list.
        private static string WriteCells(int count, Func<int, Vector2Int> getCellAt)
        {
            Vector2Int[] cells = new Vector2Int[count];
            for (int index = 0; index < count; index++)
            {
                cells[index] = getCellAt(index);
            }

            return CellText.Format(cells);
        }

        // Null for no effects, so the writer omits the list.
        private static EffectDto[] WriteEffects(int count, Func<int, LevelEffectData> getEffectAt)
        {
            if (count == 0)
            {
                return null;
            }

            EffectDto[] effects = new EffectDto[count];
            for (int index = 0; index < count; index++)
            {
                effects[index] = WriteEffect(getEffectAt(index));
            }

            return effects;
        }

        // Canonical form: params in the definition's key order. The level was validated first,
        // so every type is registered and every key is known and listed once.
        private static EffectDto WriteEffect(LevelEffectData effect)
        {
            LevelEffectRegistry.TryFind(effect.Type, out LevelEffectDefinition definition);
            JObject parameters = new JObject();
            for (int keyIndex = 0; keyIndex < definition.KeyCount; keyIndex++)
            {
                string key = definition.GetKeyAt(keyIndex).Name;
                string value = effect.FindValue(key);
                if (value != null)
                {
                    parameters.Add(key, value);
                }
            }

            return new EffectDto { type = effect.Type, @params = parameters };
        }

        // Used only by TryWrite, whose levels were not already validated by TryReadLevel.
        private static bool ValidateEntries(IReadOnlyList<LevelData> levels, out string error)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                LevelData level = levels[index];
                if (level == null)
                {
                    error = $"Level {index} is empty.";
                    return false;
                }

                if (!level.Validate(out error))
                {
                    return false;
                }
            }

            return TryCheckUniqueIds(levels, out error);
        }

        private static bool TryCheckUniqueIds(IReadOnlyList<LevelData> levels, out string error)
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < levels.Count; index++)
            {
                if (!ids.Add(levels[index].Id))
                {
                    error = $"Level ID {levels[index].Id} appears more than once.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Shared with LevelManifestJson: any "$" property anywhere is Newtonsoft type or reference metadata.
        internal static bool ContainsMetadata(JToken token)
        {
            if (token is JObject objectToken)
            {
                foreach (JProperty property in objectToken.Properties())
                {
                    if (property.Name.StartsWith("$", StringComparison.Ordinal)
                        || ContainsMetadata(property.Value))
                    {
                        return true;
                    }
                }
            }
            else if (token is JArray arrayToken)
            {
                foreach (JToken element in arrayToken)
                {
                    if (ContainsMetadata(element))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryReadSide(string text, out BoardSide side)
        {
            switch (text)
            {
                case "Top": side = BoardSide.Top; return true;
                case "Bottom": side = BoardSide.Bottom; return true;
                case "Left": side = BoardSide.Left; return true;
                case "Right": side = BoardSide.Right; return true;
                default: side = default; return false;
            }
        }

        // Optional lists are omitted when empty (NullValueHandling.Ignore on write); an explicit JSON null is
        // refused (Required.DisallowNull), so absence is the only spelling of "none".

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class DatabaseDto
        {
            [JsonProperty("formatVersion", Required = Required.Always)] public int formatVersion;
            [JsonProperty("levels", Required = Required.Always)] public LevelDto[] levels;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class LevelDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("name", Required = Required.Always)] public string name;
            [JsonProperty("board", Required = Required.Always)] public BoardDto board;
            [JsonProperty("timerSeconds", Required = Required.Always)] public int timerSeconds;
            [JsonProperty("doors", Required = Required.Always)] public DoorDto[] doors;
            [JsonProperty("blocks", Required = Required.Always)] public BlockDto[] blocks;

            [JsonProperty("cellObjects", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public CellObjectDto[] cellObjects;

            // Names of the level's non-library pieces (DEC-193).
            [JsonProperty("pieces", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public PieceDto[] pieces;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class BoardDto
        {
            [JsonProperty("width", Required = Required.Always)] public int width;
            [JsonProperty("height", Required = Required.Always)] public int height;

            [JsonProperty("voidCells", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public string voidCells;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class DoorDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("side", Required = Required.Always)] public string side;
            [JsonProperty("start", Required = Required.Always)] public int start;
            [JsonProperty("length", Required = Required.Always)] public int length;
            [JsonProperty("colorId", Required = Required.Always)] public string colorId;

            [JsonProperty("effects", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public EffectDto[] effects;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class BlockDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("colorId", Required = Required.Always)] public string colorId;
            // "x,y" and "x,y x,y" (CellText); the cells are relative to the origin and name the piece (DEC-193).
            [JsonProperty("origin", Required = Required.Always)] public string origin;
            [JsonProperty("cells", Required = Required.Always)] public string cells;

            [JsonProperty("effects", Required = Required.DisallowNull, NullValueHandling = NullValueHandling.Ignore)]
            public EffectDto[] effects;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class CellObjectDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("cells", Required = Required.Always)] public string cells;
            [JsonProperty("effect", Required = Required.Always)] public EffectDto effect;
        }

        // Type and params are checked in TryReadEffect so the message names the level and entity. Params is one
        // object of key-value strings; each value is checked there too, so a non-string value names its key.
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class EffectDto
        {
            [JsonProperty("type", Required = Required.Default)] public string type;
            [JsonProperty("params", Required = Required.Default)] public JObject @params;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class PieceDto
        {
            [JsonProperty("key", Required = Required.Always)] public string key;
            [JsonProperty("name", Required = Required.Always)] public string name;
        }
    }
}
