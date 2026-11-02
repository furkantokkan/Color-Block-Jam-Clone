using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Safely parses one CBJ level file into a CbjLevelSource: JObject-only reads, so "$type" keys are
    /// never resolved to a .NET type (DEC-101 safe parsing). It also collects every non-default mechanic
    /// field and non-empty mechanic array in one pass, since no matching project effect exists yet.
    /// </summary>
    internal static class CbjLevelReader
    {
        // Mechanic arrays the CBJ format carries; the project supports none of them yet, so a non-empty
        // one is always reported (contract item 7).
        private static readonly string[] s_mechanicArrayNames =
        {
            "IWMS", "GMS", "EMS", "CLMS", "CCMS", "GRM", "CBMS", "IBG", "CCLMS", "IMS"
        };

        internal static bool TryRead(
            string json, out CbjLevelSource source, out List<string> mechanicViolations, out string error)
        {
            source = null;
            mechanicViolations = new List<string>();
            error = null;

            JObject root;
            try
            {
                // JObject.Parse never resolves "$type"; TypeNameHandling only affects JsonSerializer reads,
                // which this importer never uses for CBJ data (DEC-101, AC-4).
                root = JObject.Parse(json);
            }
            catch (JsonException exception)
            {
                error = $"Invalid CBJ level JSON: {exception.Message}";
                return false;
            }

            if (!TryGetArray(root, "BMS", out JArray blockTokens)
                || !TryGetArray(root, "DMS", out JArray doorTokens)
                || !TryGetArray(root, "WMS", out JArray wallTokens)
                || !TryGetArray(root, "CMS", out JArray floorTokens)
                || !TryGetArray(root, "BSP", out JArray ringTokens))
            {
                error = "The CBJ file is missing BMS, DMS, WMS, CMS, or BSP.";
                return false;
            }

            for (int index = 0; index < s_mechanicArrayNames.Length; index++)
            {
                string arrayName = s_mechanicArrayNames[index];
                if (TryGetArray(root, arrayName, out JArray mechanicArray) && mechanicArray.Count > 0)
                {
                    mechanicViolations.Add($"array {arrayName} has {mechanicArray.Count} entries");
                }
            }

            if (ringTokens.Count != 1 || !TryReadCell(ringTokens[0], out Vector2Int ringOrigin))
            {
                error = "The CBJ file's BSP must name exactly one ring origin cell.";
                return false;
            }

            if (!TryReadNestedCells(floorTokens, "floor cell", out Vector2Int[] floorCells, out error)
                || !TryReadNestedCells(wallTokens, "wall cell", out Vector2Int[] wallCells, out error))
            {
                return false;
            }

            CbjDoor[] doors = new CbjDoor[doorTokens.Count];
            for (int index = 0; index < doorTokens.Count; index++)
            {
                if (!TryReadDoor(doorTokens[index], index, out doors[index], out error))
                {
                    return false;
                }
            }

            CbjBlock[] blocks = new CbjBlock[blockTokens.Count];
            for (int index = 0; index < blockTokens.Count; index++)
            {
                if (!TryReadBlock(blockTokens[index], index, mechanicViolations, out blocks[index], out error))
                {
                    return false;
                }
            }

            source = new CbjLevelSource
            {
                FloorCells = floorCells,
                WallCells = wallCells,
                Doors = doors,
                Blocks = blocks,
                RingOrigin = ringOrigin
            };
            return true;
        }

        private static bool TryReadDoor(JToken token, int index, out CbjDoor door, out string error)
        {
            door = null;
            if (!(token is JObject obj) || !TryGetInt(obj, "BCT", out int bct)
                || !TryGetArray(obj, "BPMS", out JArray cellTokens) || cellTokens.Count == 0)
            {
                error = $"CBJ door {index} is missing BCT or BPMS.";
                return false;
            }

            if (!TryReadCells(cellTokens, out Vector2Int[] cells, out error))
            {
                error = $"CBJ door {index} {error}";
                return false;
            }

            JToken ihToken = obj["IH"];
            if (ihToken == null || ihToken.Type != JTokenType.Boolean)
            {
                error = $"CBJ door {index} is missing IH.";
                return false;
            }

            door = new CbjDoor { Bct = bct, Cells = cells, IsHorizontal = ihToken.Value<bool>() };
            error = null;
            return true;
        }

        private static bool TryReadBlock(
            JToken token, int index, List<string> mechanicViolations, out CbjBlock block, out string error)
        {
            block = null;
            if (!(token is JObject obj) || !TryGetInt(obj, "BCT", out int bct)
                || !TryGetArray(obj, "BPMS", out JArray cellTokens) || cellTokens.Count == 0)
            {
                error = $"CBJ block {index} is missing BCT or BPMS.";
                return false;
            }

            if (!TryReadCells(cellTokens, out Vector2Int[] cells, out error))
            {
                error = $"CBJ block {index} {error}";
                return false;
            }

            string context = $"block b{index + 1}";
            CheckMechanicIntField(obj, "BIC", 0, context, mechanicViolations);
            CheckMechanicIntField(obj, "BAD", 0, context, mechanicViolations);
            CheckMechanicIntField(obj, "KID", 0, context, mechanicViolations);
            CheckMechanicIntField(obj, "LID", 0, context, mechanicViolations);
            CheckMechanicBoolField(obj, "BHS", false, context, mechanicViolations);
            CheckMechanicIntField(obj, "BD", 0, context, mechanicViolations);
            CheckMechanicBoolField(obj, "ILE", false, context, mechanicViolations);
            CheckMechanicIntField(obj, "LBCT", 0, context, mechanicViolations);
            CheckMechanicIntField(obj, "LIC", 0, context, mechanicViolations);
            CheckMechanicIntField(obj, "AD", 0, context, mechanicViolations);

            block = new CbjBlock { Bct = bct, Cells = cells };
            error = null;
            return true;
        }

        private static void CheckMechanicIntField(
            JObject obj, string key, int expected, string context, List<string> violations)
        {
            JToken token = obj[key];
            if (token != null && token.Type == JTokenType.Integer && token.Value<int>() != expected)
            {
                violations.Add($"{context} field {key} is {token.Value<int>()} (expected {expected})");
            }
        }

        private static void CheckMechanicBoolField(
            JObject obj, string key, bool expected, string context, List<string> violations)
        {
            JToken token = obj[key];
            if (token != null && token.Type == JTokenType.Boolean && token.Value<bool>() != expected)
            {
                violations.Add($"{context} field {key} is {token.Value<bool>()} (expected {expected})");
            }
        }

        // Blocks and doors: a direct array of {x, y} cells (BPMS).
        private static bool TryReadCells(JArray tokens, out Vector2Int[] cells, out string error)
        {
            cells = new Vector2Int[tokens.Count];
            for (int index = 0; index < tokens.Count; index++)
            {
                if (!TryReadCell(tokens[index], out cells[index]))
                {
                    error = $"has a malformed cell at index {index}.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // Floor (CMS) and walls (WMS): each entry nests its cell under "BPM".
        private static bool TryReadNestedCells(JArray tokens, string label, out Vector2Int[] cells, out string error)
        {
            cells = new Vector2Int[tokens.Count];
            for (int index = 0; index < tokens.Count; index++)
            {
                if (!(tokens[index] is JObject obj) || !(obj["BPM"] is JToken nested)
                    || !TryReadCell(nested, out cells[index]))
                {
                    error = $"CBJ {label} {index} is malformed.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool TryReadCell(JToken token, out Vector2Int cell)
        {
            cell = default;
            if (!(token is JObject obj) || !TryGetInt(obj, "x", out int x) || !TryGetInt(obj, "y", out int y))
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }

        private static bool TryGetInt(JObject obj, string key, out int value)
        {
            value = 0;
            JToken token = obj[key];
            if (token == null || token.Type != JTokenType.Integer)
            {
                return false;
            }

            value = token.Value<int>();
            return true;
        }

        private static bool TryGetArray(JObject obj, string key, out JArray array)
        {
            array = obj[key] as JArray;
            return array != null;
        }
    }
}
