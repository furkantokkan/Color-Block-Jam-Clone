using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using UnityEngine;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>The palette colour, the block's own cells and the local pose that one BlockPieceFactory.Create uses (DEC-193).</summary>
    public readonly struct BlockPieceSpawnData
    {
        public PaletteColor Color { get; }
        /// <summary>Required. Borrowed until Create returns; the piece copies the cells into its own state.</summary>
        public IReadOnlyList<Vector2Int> Cells { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }

        public BlockPieceSpawnData(PaletteColor color, IReadOnlyList<Vector2Int> cells,
            Vector3 localPosition = default, Quaternion? localRotation = null)
        {
            Color = color;
            Cells = cells;
            LocalPosition = localPosition;
            LocalRotation = localRotation ?? Quaternion.identity;
        }
    }
}
