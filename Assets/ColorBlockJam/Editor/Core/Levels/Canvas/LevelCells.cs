using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One shared cell-array copy, beside LevelRecordCopy's field-complete record copies.</summary>
    internal static class LevelCells
    {
        /// <summary>The cells copied into a new array, in order; an empty array for a null or empty list.</summary>
        public static Vector2Int[] Copy(IReadOnlyList<Vector2Int> cells)
        {
            Vector2Int[] copy = new Vector2Int[cells?.Count ?? 0];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = cells[index];
            }

            return copy;
        }
    }
}
