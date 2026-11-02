using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Maps the blocks of a validated level to preview spawn records: the block's own cells, colour from ColorId and
    /// the origin cell in the board layout's cell coordinates. Every block is a copy of the catalog's bare block root
    /// showing its cells, as in the game (DEC-193), so the mapper checks that root once. It rejects the whole level,
    /// naming the block, when one cannot map.
    /// </summary>
    internal static class LevelPreviewBlockMapper
    {
        public static bool TryMap(
            LevelData level,
            IBoardLayout layout,
            BoardPieceCatalog catalog,
            List<LevelPreviewBlock> output,
            out string error)
        {
            if (level == null) throw new ArgumentNullException(nameof(level));
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (output == null) throw new ArgumentNullException(nameof(output));

            output.Clear();
            // BlockPieceFactory.Prepare rejects an unusable root mid-build; reject it here, before the scene changes,
            // so the shown preview stays.
            if (!BlockPieceFactory.TryGetRoot(catalog, out _, out error))
            {
                return false;
            }

            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                Vector2Int[] cells = new Vector2Int[block.CellCount];
                for (int cell = 0; cell < cells.Length; cell++)
                {
                    cells[cell] = block.GetCellAt(cell);
                }

                // Blocks and gates share the eleven gate colours; Wall, Ground and
                // numeric names are rejected, as they are for doors.
                if (!BoardLayout.TryParseDoorColor(block.ColorId, out PaletteColor color))
                {
                    error = $"Block {block.Id} uses unknown colour '{block.ColorId}'.";
                    output.Clear();
                    return false;
                }

                // ModularBlockView centres cell (x, y) at local (x, 0, y), as the
                // environment centres floor cell (x, y) at CellToLocal, so the piece
                // root sits on its origin cell under the same board root. Effect
                // visuals use the same relative cells, so they sit in the root.
                output.Add(new LevelPreviewBlock(block.Id, color, cells, layout.CellToLocal(block.Origin)));
            }

            error = null;
            return true;
        }
    }
}
