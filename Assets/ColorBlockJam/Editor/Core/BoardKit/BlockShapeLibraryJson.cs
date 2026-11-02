using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Reads and writes the piece library JSON, formatVersion 2 (DEC-193): an ordered list of templates, each a name
    /// and its cells as CellText, for example { "name": "Square", "cells": "0,0 1,0 0,1 1,1" }. A template's key is
    /// derived from its cells and never stored. Only version 2 is read; Color Block Jam > Data > Migrate Piece Library
    /// JSON to v2 converts an older file.
    /// </summary>
    public static class BlockShapeLibraryJson
    {
        private const int k_formatVersion = 2;
        private const int k_maxShapes = 512;
        private const int k_maxJsonCharacters = 2 * 1024 * 1024;
        private const string k_what = "piece JSON";
        private static readonly JsonSerializerSettings s_settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.None,
            MissingMemberHandling = MissingMemberHandling.Error,
            MaxDepth = 16
        };

        /// <summary>The templates of a version 2 piece JSON, in file order; throws ArgumentException naming the first problem.</summary>
        public static BlockShapeLibrary.Shape[] Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > k_maxJsonCharacters)
                throw new ArgumentException($"Piece JSON must be nonempty and at most {k_maxJsonCharacters / (1024 * 1024)} MiB.");

            try
            {
                ValidateTokenTypes(StrictJson.Parse(json));
                var document = JsonConvert.DeserializeObject<Document>(json, s_settings);
                if (document.shapes.Length > k_maxShapes)
                    throw new ArgumentException($"Expected at most {k_maxShapes} piece records.");

                var shapes = new BlockShapeLibrary.Shape[document.shapes.Length];
                for (int i = 0; i < shapes.Length; i++)
                    shapes[i] = ReadShape(document.shapes[i]);

                Validate(shapes);
                return shapes;
            }
            catch (JsonException error)
            {
                throw new ArgumentException("Invalid piece JSON: " + error.Message, error);
            }
        }

        private static BlockShapeLibrary.Shape ReadShape(ShapeDto input)
        {
            if (!CellText.TryParseCells(input.cells, out Vector2Int[] cells, out string problem))
                throw new ArgumentException($"Piece {input.name} cells: {problem}.");

            return new BlockShapeLibrary.Shape(input.name, cells);
        }

        /// <summary>The canonical version 2 text of the templates, after the same checks Read applies.</summary>
        public static string Write(IReadOnlyList<BlockShapeLibrary.Shape> shapes)
        {
            Validate(shapes);
            var document = new Document { formatVersion = k_formatVersion, shapes = new ShapeDto[shapes.Count] };
            for (int i = 0; i < shapes.Count; i++)
                document.shapes[i] = new ShapeDto { name = shapes[i].DisplayName, cells = CellText.Format(shapes[i].Cells) };

            return JsonConvert.SerializeObject(document, Formatting.Indented, s_settings) + "\n";
        }

        /// <summary>
        /// The current templates followed by the incoming ones they lack. A template with an incoming one's key or name
        /// must equal it; otherwise the merge is refused and nothing changes.
        /// </summary>
        public static BlockShapeLibrary.Shape[] Merge(IReadOnlyList<BlockShapeLibrary.Shape> current,
            IReadOnlyList<BlockShapeLibrary.Shape> incoming)
        {
            Validate(current);
            Validate(incoming);
            var result = new List<BlockShapeLibrary.Shape>(current);
            foreach (var shape in incoming)
            {
                var existing = FindIdentity(current, shape);
                if (existing == null)
                    result.Add(shape);
                else if (!Equal(existing, shape))
                    throw new ArgumentException("Merge conflict for piece " + shape.DisplayName + ". Existing data was preserved.");
            }
            Validate(result);
            return result.ToArray();
        }

        // The template that is the candidate's piece (same key) or carries its name.
        private static BlockShapeLibrary.Shape FindIdentity(IReadOnlyList<BlockShapeLibrary.Shape> shapes,
            BlockShapeLibrary.Shape candidate)
        {
            foreach (var shape in shapes)
            {
                if (shape.Key == candidate.Key ||
                    string.Equals(shape.DisplayName, candidate.DisplayName, StringComparison.OrdinalIgnoreCase))
                    return shape;
            }
            return null;
        }

        /// <summary>True when both templates have the same name and the same cells in the same orientation.</summary>
        public static bool Equal(BlockShapeLibrary.Shape left, BlockShapeLibrary.Shape right)
        {
            if (left.Key != right.Key || left.DisplayName != right.DisplayName || left.Cells.Count != right.Cells.Count)
                return false;

            // Cell order has no meaning. Merge compares the occupied set.
            foreach (var cell in left.Cells)
            {
                if (!Contains(right.Cells, cell))
                    return false;
            }
            return true;
        }

        private static bool Contains(IReadOnlyList<Vector2Int> cells, Vector2Int cell)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                if (cells[i] == cell)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The library rules beyond one record's (which BlockShapeLibrary.Shape enforces): at most 512 templates, one
        /// template per key, unique names ignoring case, and cells that start at (0, 0). Throws ArgumentException.
        /// </summary>
        public static void Validate(IReadOnlyList<BlockShapeLibrary.Shape> shapes)
        {
            if (shapes == null || shapes.Count > k_maxShapes)
                throw new ArgumentException($"Expected at most {k_maxShapes} piece records.");

            var names = new Dictionary<string, BlockShapeLibrary.Shape>(StringComparer.OrdinalIgnoreCase);
            var keys = new Dictionary<ShapeKey, BlockShapeLibrary.Shape>();
            foreach (var shape in shapes)
            {
                if (shape == null)
                    throw new ArgumentException("A piece record is empty.");
                if (names.ContainsKey(shape.DisplayName))
                    throw new ArgumentException($"Two pieces are named {shape.DisplayName}; piece names must be unique.");
                if (keys.TryGetValue(shape.Key, out BlockShapeLibrary.Shape twin))
                    throw new ArgumentException($"{twin.DisplayName} and {shape.DisplayName} are the same shape, counting "
                        + "turns and mirrors; keep one of them.");

                names.Add(shape.DisplayName, shape);
                keys.Add(shape.Key, shape);
                ValidateMargins(shape);
            }
        }

        private static void ValidateMargins(BlockShapeLibrary.Shape shape)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            foreach (var cell in shape.Cells)
            {
                minX = Math.Min(minX, cell.x);
                minY = Math.Min(minY, cell.y);
            }
            if (minX != 0 || minY != 0)
                throw new ArgumentException($"Piece {shape.DisplayName}: remove the empty left and bottom margins from its cells.");
        }

        // The version is checked before the records, so an older file names its version instead of an unknown field.
        private static void ValidateTokenTypes(JToken token)
        {
            StrictJson.RequireOnlyProperties(token, k_what, "formatVersion", "shapes");
            StrictJson.RequireType(token["formatVersion"], JTokenType.Integer, k_what);
            if (!(token["formatVersion"] is JValue version && version.Value is long number && number == k_formatVersion))
                throw new ArgumentException($"Expected piece JSON formatVersion {k_formatVersion}. Convert an older file "
                    + "with Color Block Jam > Data > Migrate Piece Library JSON to v2.");

            StrictJson.RequireType(token["shapes"], JTokenType.Array, k_what);
            foreach (var shape in token["shapes"])
            {
                StrictJson.RequireOnlyProperties(shape, k_what, "name", "cells");
                StrictJson.RequireType(shape["name"], JTokenType.String, k_what);
                StrictJson.RequireType(shape["cells"], JTokenType.String, k_what);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private sealed class Document
        {
            [JsonProperty(Required = Required.Always)] public int formatVersion;
            [JsonProperty(Required = Required.Always)] public ShapeDto[] shapes;
        }

        // Cells are CellText relative to (0, 0): "0,0 1,0 0,1 1,1".
        [JsonObject(MemberSerialization.OptIn)]
        private sealed class ShapeDto
        {
            [JsonProperty(Required = Required.Always)] public string name;
            [JsonProperty(Required = Required.Always)] public string cells;
        }
    }
}
