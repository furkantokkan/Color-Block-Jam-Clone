using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One-time conversions of the JSON written before DEC-193: a level database of formatVersion 1 or 2 becomes
    /// version 3 (shapeId dropped, a level piece's designer-given id kept as its name in the level's pieces table),
    /// and a piece library of formatVersion 1 becomes version 2 ({name, cells}). A conversion writes the whole converted
    /// file or nothing, and says why. Editor-only; only its two menu items and explicit calls run it.
    /// </summary>
    internal static class LevelJsonMigration
    {
        private const string k_levelMenuPath = "Color Block Jam/Data/Migrate Level JSON to v3";
        private const string k_libraryMenuPath = "Color Block Jam/Data/Migrate Piece Library JSON to v2";
        private const string k_levelDialogTitle = "Migrate Level JSON to v3";
        private const string k_libraryDialogTitle = "Migrate Piece Library JSON to v2";
        private const string k_formatVersionProperty = "formatVersion";
        private const int k_legacyLevelVersion = 1;
        private const int k_previousLevelVersion = 2;
        private const int k_currentLevelVersion = 3;
        private const int k_previousLibraryVersion = 1;
        private const int k_currentLibraryVersion = 2;
        private const string k_libraryWhat = "piece JSON";
        // Older levels gave an unnamed level piece the id "Custom_" plus its footprint code, which names nothing a
        // designer chose, so it becomes no piece name.
        private const string k_generatedIdPrefix = "Custom_";

        private static readonly JsonSerializerSettings s_levelSettings = new JsonSerializerSettings
        {
            MissingMemberHandling = MissingMemberHandling.Error,
            TypeNameHandling = TypeNameHandling.None,
            // Parameter values are read as raw tokens; a date-like string must stay a string.
            DateParseHandling = DateParseHandling.None
        };

        /// <summary>
        /// Converts a level database of formatVersion 1 or 2 to version 3. Blocks keep their id, colour, origin, cells
        /// and effects and lose shapeId, because a block's piece is the ShapeKey of its cells. A block whose shapeId is
        /// neither a library piece's id nor a generated Custom_ id, and whose key no library piece has, gives its level a
        /// pieces entry with that id as the name (the first such block per key wins), so designers keep the names they
        /// gave their level pieces. The result is written and read back through LevelDatabaseJson; any problem returns
        /// false with the reason and no JSON.
        /// </summary>
        /// <param name="libraryPieces">
        /// The piece library's cells by the id older levels used as shapeId: a formatVersion 1 library's type names
        /// (TryReadLibraryPieces).
        /// </param>
        internal static bool TryMigrateLevels(string json, IReadOnlyDictionary<string, Vector2Int[]> libraryPieces,
            out string migratedJson, out string error)
        {
            if (libraryPieces == null) throw new ArgumentNullException(nameof(libraryPieces));

            migratedJson = null;
            if (!TryReadLevels(json, libraryPieces, out LevelData[] levels, out error)
                || !LevelDatabaseJson.TryWrite(levels, out string written, out error)
                || !LevelDatabaseJson.TryRead(written, out _, out error))
            {
                return false;
            }

            migratedJson = written;
            return true;
        }

        /// <summary>
        /// Converts a piece library of formatVersion 1 ({id, type, name, cells: [{x, y}]}) to version 2: each record
        /// keeps its name (the old display name) and cells, written as CellText; the id and type go, since a template's
        /// key is derived from its cells. A record the version 2 rules refuse (a footprint beyond 5 x 5, two records of
        /// one shape, a repeated name) fails the whole conversion with the reason.
        /// </summary>
        internal static bool TryMigrateLibrary(string json, out string migratedJson, out string error)
        {
            migratedJson = null;
            try
            {
                JToken token = StrictJson.Parse(json ?? string.Empty);
                long version = GetVersion(token);
                if (version == k_currentLibraryVersion)
                {
                    error = $"The file is already formatVersion {k_currentLibraryVersion}.";
                    return false;
                }

                if (version != k_previousLibraryVersion)
                {
                    error = $"Expected piece JSON formatVersion {k_previousLibraryVersion}.";
                    return false;
                }

                List<(string TypeName, string Name, Vector2Int[] Cells)> records = ReadLibraryRecords(token);
                var shapes = new BlockShapeLibrary.Shape[records.Count];
                for (int index = 0; index < shapes.Length; index++)
                {
                    shapes[index] = new BlockShapeLibrary.Shape(records[index].Name, records[index].Cells);
                }

                string written = BlockShapeLibraryJson.Write(shapes);
                BlockShapeLibraryJson.Read(written);
                migratedJson = written;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is JsonException
                                              || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// The pieces of a piece library JSON by the id older levels used as shapeId: a formatVersion 1 library's type
        /// names, or a version 2 library's names. The level migration also compares keys, so a library piece is
        /// recognized by its cells either way.
        /// </summary>
        internal static bool TryReadLibraryPieces(string libraryJson, out Dictionary<string, Vector2Int[]> pieces,
            out string error)
        {
            pieces = null;
            try
            {
                JToken token = StrictJson.Parse(libraryJson ?? string.Empty);
                long version = GetVersion(token);
                var result = new Dictionary<string, Vector2Int[]>(StringComparer.Ordinal);
                if (version == k_previousLibraryVersion)
                {
                    foreach (var (typeName, _, cells) in ReadLibraryRecords(token))
                    {
                        result.Add(typeName, cells);
                    }
                }
                else if (version == k_currentLibraryVersion)
                {
                    foreach (BlockShapeLibrary.Shape shape in BlockShapeLibraryJson.Read(libraryJson))
                    {
                        result.Add(shape.DisplayName, ToArray(shape.Cells));
                    }
                }
                else
                {
                    error = $"Expected piece JSON formatVersion {k_previousLibraryVersion} or {k_currentLibraryVersion}.";
                    return false;
                }

                pieces = result;
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is JsonException
                                              || exception is OverflowException)
            {
                error = exception.Message;
                return false;
            }
        }

        /// <summary>
        /// Converts the level JSON file at <paramref name="levelPath"/> to formatVersion 3 with the piece library JSON at
        /// <paramref name="libraryPath"/> (TryMigrateLevels) and replaces the file in one step, only when every level
        /// converts and the file did not change meanwhile; a file under Assets is reimported. The report says what
        /// happened either way.
        /// </summary>
        internal static bool TryMigrateLevelFile(string levelPath, string libraryPath, out string report)
        {
            string levelName = Path.GetFileName(levelPath);
            if (!TryReadText(libraryPath, out string libraryJson, out string error)
                || !TryReadLibraryPieces(libraryJson, out Dictionary<string, Vector2Int[]> pieces, out error))
            {
                report = $"The piece library {Path.GetFileName(libraryPath)} cannot be read: {error} Nothing was written.";
                return false;
            }

            if (!TryReadText(levelPath, out string json, out error)
                || !TryMigrateLevels(json, pieces, out string migrated, out error)
                || !TryReplace(levelPath, json, migrated, out error))
            {
                report = $"{levelName}: {error} Nothing was written.";
                return false;
            }

            Reimport(levelPath);
            LevelDatabaseJson.TryRead(migrated, out LevelData[] levels, out _);
            int names = 0;
            for (int index = 0; index < levels.Length; index++)
            {
                names += levels[index].PieceNameCount;
            }

            report = $"Migrated {levelName} to formatVersion {k_currentLevelVersion}: {levels.Length} "
                + $"{(levels.Length == 1 ? "level" : "levels")}, {names} level piece {(names == 1 ? "name" : "names")}.";
            return true;
        }

        /// <summary>
        /// Converts the piece library JSON file at <paramref name="libraryPath"/> to formatVersion 2 (TryMigrateLibrary)
        /// and replaces it in one step, only when every record converts and the file did not change meanwhile; a file
        /// under Assets is reimported. The report says what happened either way.
        /// </summary>
        internal static bool TryMigrateLibraryFile(string libraryPath, out string report)
        {
            string libraryName = Path.GetFileName(libraryPath);
            if (!TryReadText(libraryPath, out string json, out string error)
                || !TryMigrateLibrary(json, out string migrated, out error)
                || !TryReplace(libraryPath, json, migrated, out error))
            {
                report = $"{libraryName}: {error} Nothing was written.";
                return false;
            }

            Reimport(libraryPath);
            int count = BlockShapeLibraryJson.Read(migrated).Length;
            report = $"Migrated {libraryName} to formatVersion {k_currentLibraryVersion}: {count} "
                + $"{(count == 1 ? "template" : "templates")}.";
            return true;
        }

        [MenuItem(k_levelMenuPath)]
        private static void MigrateLevelsFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Level JSON to migrate", LevelDatabaseAddressables.DatabaseDirectory,
                "json");
            if (string.IsNullOrEmpty(path)
                || !EditorUtility.DisplayDialog(k_levelDialogTitle,
                    $"Replace {Path.GetFileName(path)} with its formatVersion {k_currentLevelVersion} version? The piece "
                    + $"library {BoardKitAssets.ShapesJsonPath} tells library pieces from level pieces. Nothing is written "
                    + "unless every level converts.", "Migrate", "Cancel"))
            {
                return;
            }

            ShowReport(k_levelDialogTitle, TryMigrateLevelFile(path, BoardKitAssets.ShapesJsonPath, out string report),
                report);
        }

        [MenuItem(k_libraryMenuPath)]
        private static void MigrateLibraryFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Piece library JSON to migrate", BoardKitAssets.PiecesDirectory,
                "json");
            if (string.IsNullOrEmpty(path)
                || !EditorUtility.DisplayDialog(k_libraryDialogTitle,
                    $"Replace {Path.GetFileName(path)} with its formatVersion {k_currentLibraryVersion} version? Nothing is "
                    + "written unless every piece converts.", "Migrate", "Cancel"))
            {
                return;
            }

            ShowReport(k_libraryDialogTitle, TryMigrateLibraryFile(path, out string report), report);
        }

        private static void ShowReport(string title, bool migrated, string report)
        {
            if (migrated)
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogWarning(report);
            }

            EditorUtility.DisplayDialog(title, report, "OK");
        }

        // The older reader's parse, refusal for refusal, into version 3 records; a level's piece names come from its
        // blocks' shapeIds (TryGetPieceName).
        private static bool TryReadLevels(string json, IReadOnlyDictionary<string, Vector2Int[]> libraryPieces,
            out LevelData[] levels, out string error)
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
                JToken token = StrictJson.Parse(json);
                if (!(token is JObject) || ContainsMetadata(token))
                {
                    error = "The JSON must be a database object without type metadata.";
                    return false;
                }

                long version = GetVersion(token);
                if (version == k_currentLevelVersion)
                {
                    error = $"The file is already formatVersion {k_currentLevelVersion}.";
                    return false;
                }

                if (version != k_legacyLevelVersion && version != k_previousLevelVersion)
                {
                    error = $"Expected level database formatVersion {k_legacyLevelVersion} or {k_previousLevelVersion} "
                        + "and a levels array.";
                    return false;
                }

                document = JsonConvert.DeserializeObject<DatabaseDto>(json, s_levelSettings);
            }
            catch (JsonException exception)
            {
                error = $"Invalid level JSON: {exception.Message}";
                return false;
            }

            if (document?.levels == null)
            {
                error = $"Expected level database formatVersion {k_legacyLevelVersion} or {k_previousLevelVersion} "
                    + "and a levels array.";
                return false;
            }

            HashSet<ShapeKey> libraryKeys = GetKeys(libraryPieces);
            bool isLegacy = document.formatVersion == k_legacyLevelVersion;
            var parsed = new LevelData[document.levels.Length];
            for (int index = 0; index < parsed.Length; index++)
            {
                if (!TryReadLevel(document.levels[index], index, isLegacy, libraryPieces, libraryKeys, out parsed[index],
                        out error))
                {
                    return false;
                }
            }

            levels = parsed;
            error = null;
            return true;
        }

        private static bool TryReadLevel(LevelDto source, int index, bool isLegacy,
            IReadOnlyDictionary<string, Vector2Int[]> libraryPieces, HashSet<ShapeKey> libraryKeys, out LevelData level,
            out string error)
        {
            level = null;
            if (source == null || source.board == null || source.doors == null
                || source.blocks == null || source.cellObjects == null)
            {
                error = $"Level {index} is missing board, doors, blocks, or cellObjects.";
                return false;
            }

            // Version 1 predates void cells, effects and cell objects.
            if (isLegacy && (source.board.voidCells != null || source.cellObjects.Length != 0 || HasEffects(source)))
            {
                error = $"Level {source.id} uses voidCells, effects or cellObjects, which need formatVersion 2.";
                return false;
            }

            Vector2Int[] voidCells = Array.Empty<Vector2Int>();
            if (source.board.voidCells != null
                && !TryReadCells(source.board.voidCells, $"Level {source.id} has an empty void cell.", out voidCells,
                    out error))
            {
                return false;
            }

            var pieceNames = new List<LevelPieceName>();
            if (!TryReadDoors(source, out LevelDoorData[] doors, out error)
                || !TryReadBlocks(source, libraryPieces, libraryKeys, pieceNames, out LevelBlockData[] blocks, out error)
                || !TryReadCellObjects(source, out LevelCellObjectData[] cellObjects, out error))
            {
                return false;
            }

            level = new LevelData(source.id, source.name, source.board.width, source.board.height,
                source.timerSeconds, voidCells, doors, blocks, cellObjects, pieceNames.ToArray());
            error = null;
            return true;
        }

        private static bool HasEffects(LevelDto source)
        {
            for (int index = 0; index < source.doors.Length; index++)
            {
                if (source.doors[index]?.effects != null && source.doors[index].effects.Length != 0)
                {
                    return true;
                }
            }

            for (int index = 0; index < source.blocks.Length; index++)
            {
                if (source.blocks[index]?.effects != null && source.blocks[index].effects.Length != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryReadDoors(LevelDto source, out LevelDoorData[] doors, out string error)
        {
            doors = new LevelDoorData[source.doors.Length];
            for (int doorIndex = 0; doorIndex < doors.Length; doorIndex++)
            {
                DoorDto door = source.doors[doorIndex];
                if (door == null || door.effects == null || !TryReadSide(door.side, out BoardSide side))
                {
                    error = $"Level {source.id} door {doorIndex} has a missing effect list or an invalid side.";
                    return false;
                }

                if (!TryReadEffects(door.effects, $"Level {source.id} door {door.id}", out LevelEffectData[] effects,
                        out error))
                {
                    return false;
                }

                doors[doorIndex] = new LevelDoorData(door.id, side, door.start, door.length, door.colorId, effects);
            }

            error = null;
            return true;
        }

        private static bool TryReadBlocks(LevelDto source, IReadOnlyDictionary<string, Vector2Int[]> libraryPieces,
            HashSet<ShapeKey> libraryKeys, List<LevelPieceName> pieceNames, out LevelBlockData[] blocks,
            out string error)
        {
            blocks = new LevelBlockData[source.blocks.Length];
            for (int blockIndex = 0; blockIndex < blocks.Length; blockIndex++)
            {
                BlockDto block = source.blocks[blockIndex];
                if (block == null || block.origin == null || block.cells == null || block.effects == null)
                {
                    error = $"Level {source.id} block {blockIndex} has missing cells or effects.";
                    return false;
                }

                if (!TryReadCells(block.cells, $"Level {source.id} block {block.id} has an empty cell.",
                        out Vector2Int[] cells, out error)
                    || !TryReadEffects(block.effects, $"Level {source.id} block {block.id}",
                        out LevelEffectData[] effects, out error))
                {
                    return false;
                }

                if (TryGetPieceName(block.shapeId, cells, libraryPieces, libraryKeys, pieceNames,
                        out LevelPieceName pieceName))
                {
                    pieceNames.Add(pieceName);
                }

                blocks[blockIndex] = new LevelBlockData(block.id, block.colorId,
                    new Vector2Int(block.origin.x, block.origin.y), cells, effects);
            }

            error = null;
            return true;
        }

        // A designer's name for a level piece: a shapeId that is neither a library piece's id nor a generated Custom_
        // id, on cells whose key no library piece has and no earlier block of the level already named.
        private static bool TryGetPieceName(string shapeId, Vector2Int[] cells,
            IReadOnlyDictionary<string, Vector2Int[]> libraryPieces, HashSet<ShapeKey> libraryKeys,
            List<LevelPieceName> named, out LevelPieceName pieceName)
        {
            pieceName = null;
            if (string.IsNullOrWhiteSpace(shapeId) || libraryPieces.ContainsKey(shapeId)
                || shapeId.StartsWith(k_generatedIdPrefix, StringComparison.Ordinal)
                || !ShapeKey.TryCreate(cells, out ShapeKey key) || libraryKeys.Contains(key))
            {
                return false;
            }

            for (int index = 0; index < named.Count; index++)
            {
                if (named[index].Key == key)
                {
                    return false;
                }
            }

            pieceName = new LevelPieceName(key, shapeId);
            return true;
        }

        private static bool TryReadCellObjects(LevelDto source, out LevelCellObjectData[] cellObjects,
            out string error)
        {
            cellObjects = new LevelCellObjectData[source.cellObjects.Length];
            for (int objectIndex = 0; objectIndex < cellObjects.Length; objectIndex++)
            {
                CellObjectDto cellObject = source.cellObjects[objectIndex];
                if (cellObject == null || cellObject.cells == null || cellObject.effect == null)
                {
                    error = $"Level {source.id} cell object {objectIndex} has missing cells or effect.";
                    return false;
                }

                if (!TryReadCells(cellObject.cells, $"Level {source.id} cell object {cellObject.id} has an empty cell.",
                        out Vector2Int[] cells, out error)
                    || !TryReadEffect(cellObject.effect, $"Level {source.id} cell object {cellObject.id}",
                        out LevelEffectData effect, out error))
                {
                    return false;
                }

                cellObjects[objectIndex] = new LevelCellObjectData(cellObject.id, cells, effect);
            }

            error = null;
            return true;
        }

        private static bool TryReadCells(PositionDto[] source, string emptyError, out Vector2Int[] cells,
            out string error)
        {
            cells = new Vector2Int[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                PositionDto cell = source[index];
                if (cell == null)
                {
                    error = emptyError;
                    return false;
                }

                cells[index] = new Vector2Int(cell.x, cell.y);
            }

            error = null;
            return true;
        }

        private static bool TryReadEffects(EffectDto[] source, string entity, out LevelEffectData[] effects,
            out string error)
        {
            effects = new LevelEffectData[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                if (!TryReadEffect(source[index], entity, out effects[index], out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Only the record shape is read here; LevelData.Validate checks types, keys and values when the result is written.
        private static bool TryReadEffect(EffectDto source, string entity, out LevelEffectData effect, out string error)
        {
            effect = null;
            if (source == null || source.type == null || source.@params == null)
            {
                error = $"{entity} has an effect without type or params.";
                return false;
            }

            var parameters = new LevelEffectParamData[source.@params.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                ParamDto param = source.@params[index];
                if (param == null)
                {
                    error = $"{entity} effect {source.type} has an empty parameter.";
                    return false;
                }

                // Values are strings (DEC-MECH-1); a number or bool token would not round-trip unchanged.
                if (!(param.key is string key) || !(param.value is string value))
                {
                    error = $"{entity} effect {source.type} parameter {param.key} needs a JSON string key and value.";
                    return false;
                }

                parameters[index] = new LevelEffectParamData(key, value);
            }

            effect = new LevelEffectData(source.type, parameters);
            error = null;
            return true;
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

        // The records of a formatVersion 1 piece library, checked field by field as its reader did. Throws
        // ArgumentException, or OverflowException for a coordinate an int cannot hold.
        private static List<(string TypeName, string Name, Vector2Int[] Cells)> ReadLibraryRecords(JToken token)
        {
            StrictJson.RequireOnlyProperties(token, k_libraryWhat, k_formatVersionProperty, "shapes");
            StrictJson.RequireType(token["shapes"], JTokenType.Array, k_libraryWhat);
            var records = new List<(string TypeName, string Name, Vector2Int[] Cells)>();
            var typeNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (JToken shape in token["shapes"])
            {
                StrictJson.RequireOnlyProperties(shape, k_libraryWhat, "id", "type", "name", "cells");
                StrictJson.RequireType(shape["id"], JTokenType.Integer, k_libraryWhat);
                StrictJson.RequireType(shape["type"], JTokenType.String, k_libraryWhat);
                StrictJson.RequireType(shape["name"], JTokenType.String, k_libraryWhat);
                StrictJson.RequireType(shape["cells"], JTokenType.Array, k_libraryWhat);
                var cells = new List<Vector2Int>();
                foreach (JToken cell in shape["cells"])
                {
                    StrictJson.RequireOnlyProperties(cell, k_libraryWhat, "x", "y");
                    StrictJson.RequireType(cell["x"], JTokenType.Integer, k_libraryWhat);
                    StrictJson.RequireType(cell["y"], JTokenType.Integer, k_libraryWhat);
                    cells.Add(new Vector2Int((int)cell["x"], (int)cell["y"]));
                }

                string typeName = (string)shape["type"];
                if (!typeNames.Add(typeName))
                {
                    throw new ArgumentException($"Two pieces use the type name {typeName}.");
                }

                records.Add((typeName, (string)shape["name"], cells.ToArray()));
            }

            return records;
        }

        private static HashSet<ShapeKey> GetKeys(IReadOnlyDictionary<string, Vector2Int[]> libraryPieces)
        {
            var keys = new HashSet<ShapeKey>();
            foreach (KeyValuePair<string, Vector2Int[]> piece in libraryPieces)
            {
                if (ShapeKey.TryCreate(piece.Value, out ShapeKey key))
                {
                    keys.Add(key);
                }
            }

            return keys;
        }

        // The integer formatVersion of a JSON object; -1 when it is missing, not an integer or not an object.
        private static long GetVersion(JToken token)
        {
            return token is JObject root && root.TryGetValue(k_formatVersionProperty, out JToken version)
                && version is JValue value && value.Value is long number
                ? number
                : -1;
        }

        private static bool ContainsMetadata(JToken token)
        {
            if (token is JObject objectToken)
            {
                foreach (JProperty property in objectToken.Properties())
                {
                    if (property.Name.StartsWith("$", StringComparison.Ordinal) || ContainsMetadata(property.Value))
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

        private static Vector2Int[] ToArray(IReadOnlyList<Vector2Int> cells)
        {
            var result = new Vector2Int[cells.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = cells[index];
            }

            return result;
        }

        private static bool TryReadText(string path, out string text, out string error)
        {
            text = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = $"The file is missing: {path}.";
                return false;
            }

            try
            {
                text = File.ReadAllText(path);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not read {path}: {exception.Message}";
                return false;
            }
        }

        // Replaces the file with next only while it still holds expected, through a hidden temporary file beside it,
        // so an interrupted write never leaves half a file and Unity never imports the temporary one.
        private static bool TryReplace(string path, string expected, string next, out string error)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty,
                "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(temporary, next, new UTF8Encoding(false));
                if (!string.Equals(File.ReadAllText(path), expected, StringComparison.Ordinal))
                {
                    error = "The file changed on disk while it was converted.";
                    return false;
                }

                File.Replace(temporary, path, null);
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                error = $"Could not write {path}: {exception.Message}";
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    // A leftover hidden temporary file does not replace the source.
                }
            }
        }

        // A converted file under Assets is imported again, so its TextAsset shows the new text.
        private static void Reimport(string path)
        {
            string full = Path.GetFullPath(path);
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.ImportAsset("Assets/" + full.Substring(assets.Length).Replace('\\', '/'),
                    ImportAssetOptions.ForceUpdate);
            }
        }

        // The formatVersion 1 and 2 level records, exactly as the older reader declared them.

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
            [JsonProperty("cellObjects", Required = Required.Always)] public CellObjectDto[] cellObjects;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class BoardDto
        {
            [JsonProperty("width", Required = Required.Always)] public int width;
            [JsonProperty("height", Required = Required.Always)] public int height;
            // Absent or null means no void cells; version 1 files never carry it.
            [JsonProperty("voidCells", Required = Required.Default)] public PositionDto[] voidCells;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class DoorDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("side", Required = Required.Always)] public string side;
            [JsonProperty("start", Required = Required.Always)] public int start;
            [JsonProperty("length", Required = Required.Always)] public int length;
            [JsonProperty("colorId", Required = Required.Always)] public string colorId;
            [JsonProperty("effects", Required = Required.Always)] public EffectDto[] effects;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class BlockDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("colorId", Required = Required.Always)] public string colorId;
            [JsonProperty("shapeId", Required = Required.Always)] public string shapeId;
            [JsonProperty("origin", Required = Required.Always)] public PositionDto origin;
            [JsonProperty("cells", Required = Required.Always)] public PositionDto[] cells;
            [JsonProperty("effects", Required = Required.Always)] public EffectDto[] effects;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class CellObjectDto
        {
            [JsonProperty("id", Required = Required.Always)] public string id;
            [JsonProperty("cells", Required = Required.Always)] public PositionDto[] cells;
            [JsonProperty("effect", Required = Required.Always)] public EffectDto effect;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class EffectDto
        {
            [JsonProperty("type", Required = Required.Default)] public string type;
            [JsonProperty("params", Required = Required.Default)] public ParamDto[] @params;
        }

        // Untyped members, so TryReadEffect can refuse a number, bool, null or missing member with the level, entity
        // and key. Object is the one member type for which Newtonsoft keeps "" as "".
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class ParamDto
        {
            [JsonProperty("key", Required = Required.Default)] public object key;
            [JsonProperty("value", Required = Required.Default)] public object value;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class PositionDto
        {
            [JsonProperty("x", Required = Required.Always)] public int x;
            [JsonProperty("y", Required = Required.Always)] public int y;
        }
    }
}
