using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// One level block resolved for the preview: its colour, footprint cells relative to its origin and its position
    /// under the board root. The cells are the whole piece (DEC-193). Effect adapters read the block's effects from the
    /// level itself.
    /// </summary>
    internal readonly struct LevelPreviewBlock
    {
        public string BlockId { get; }
        public PaletteColor Color { get; }
        public IReadOnlyList<Vector2Int> Cells { get; }
        public Vector3 LocalPosition { get; }

        public LevelPreviewBlock(string blockId, PaletteColor color, IReadOnlyList<Vector2Int> cells,
            Vector3 localPosition)
        {
            BlockId = blockId;
            Color = color;
            Cells = cells;
            LocalPosition = localPosition;
        }
    }
}
