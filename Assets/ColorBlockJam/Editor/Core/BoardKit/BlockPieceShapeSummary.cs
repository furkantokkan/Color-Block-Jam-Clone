using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Writes the line under the BlockPiece Inspector's shape grid: the drawn footprint's size, whether it is a piece
    /// library template or a level piece, and its key, so a block in the scene can be matched to its level JSON
    /// (DEC-193).
    /// </summary>
    internal static class BlockPieceShapeSummary
    {
        /// <param name="cells">The drawn footprint, never empty: a scene root's current cells.</param>
        /// <param name="findTemplateName">The name of the piece library template with a key, or null when the library
        /// has none (a level piece).</param>
        public static string Describe(IReadOnlyList<Vector2Int> cells, Func<ShapeKey, string> findTemplateName)
        {
            RectInt bounds = LevelShapeThumbnail.GetBounds(cells);
            string size = $"{cells.Count} {(cells.Count == 1 ? "cell" : "cells")}, {bounds.width} x {bounds.height}";
            // Level data refuses a block without a key, so only broken data reaches this.
            if (!ShapeKey.TryCreate(cells, out ShapeKey key))
            {
                ShapeKey.TryValidate(cells, out string issue);
                return $"{size} · no key: {issue}";
            }

            // The key ignores turns and mirrors, so a turned template reads as that template.
            string template = findTemplateName?.Invoke(key);
            string origin = string.IsNullOrEmpty(template) ? "level piece" : "template " + template;
            return $"{size} · {origin} · key {key}";
        }
    }
}
