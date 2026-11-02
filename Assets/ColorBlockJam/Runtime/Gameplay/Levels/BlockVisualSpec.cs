using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Levels
{
    /// <summary>
    /// Everything the board presenter needs to spawn one block root: the colour, the footprint cells relative to
    /// the origin and the authored origin cell. Every root is the same bare prefab showing these cells (DEC-193).
    /// </summary>
    public readonly struct BlockVisualSpec
    {
        public BlockVisualSpec(PaletteColor color, IReadOnlyList<Vector2Int> cells, Vector2Int origin)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            Vector2Int[] copy = new Vector2Int[cells.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = cells[i];
            }

            Color = color;
            Cells = Array.AsReadOnly(copy);
            Origin = origin;
        }

        public PaletteColor Color { get; }

        /// <summary>Footprint cells relative to the origin; a read-only copy, so the authored data stays untouched.</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }

        public Vector2Int Origin { get; }
    }
}
