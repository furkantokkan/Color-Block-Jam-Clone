using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure Pen edits of a library template in the open level: a stroke redraws the edited block and re-stamps every
    /// other block of the template (the edit's recorded blocks that still carry its key, DEC-193) in its own
    /// orientation, Discard puts them back as the edit found them, and Overlay shows pending footprints in a piece
    /// catalog. Each returns a new level or refuses as a whole, and never changes the level or catalog it is given
    /// (docs/Level-Editor-Design.md, Pen template edits).
    /// </summary>
    internal static class LevelTemplateCommands
    {
        /// <summary>
        /// Gives the block at <paramref name="blockIndex"/>, a block of the template, the board cells
        /// <paramref name="newBoardCells"/> (its id, colour and effects stay) and re-stamps the other blocks of the
        /// template with the new footprint in their own rotation or mirror and bounding-box corner.
        /// <paramref name="current"/> is the template's footprint before the stroke in library orientation (the pending
        /// footprint, else the library's); <paramref name="memberIds"/> names the template's blocks
        /// (LevelTemplateEdit.GetMemberIds, or at the edit's first stroke its blocks keyed like the template), so only a
        /// listed block with the key of <paramref name="current"/> follows and a block that merely shares that key stays
        /// (DEC-193). <paramref name="libraryCells"/> is the new footprint in library orientation. Refused as a
        /// whole, naming the block, when the edited block is not a block of the template, the new cells break a piece
        /// limit, the edited block would leave the board or cover a void, a board object or another block, or any other
        /// block of the template would leave the board or cover a void or a board object. Other re-stamped blocks may
        /// overlap blocks (contract D3-B).
        /// </summary>
        public static bool TryEdit(LevelData level, int blockIndex, IReadOnlyList<Vector2Int> newBoardCells,
            string templateName, IReadOnlyList<Vector2Int> current, IReadOnlyList<string> memberIds,
            out LevelData result, out Vector2Int[] libraryCells, out string issue)
        {
            result = null;
            libraryCells = null;
            LevelBlockData block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount
                ? level.GetBlockAt(blockIndex)
                : null;
            if (block == null || block.CellCount == 0 || !ShapeKey.TryCreate(current, out ShapeKey currentKey))
            {
                issue = "Nothing to edit.";
                return false;
            }

            if (block.Key != currentKey)
            {
                issue = $"{block.Id} is not a block of {templateName}.";
                return false;
            }

            if (!LevelCustomPiece.TryValidate(newBoardCells, out string footprintIssue))
            {
                issue = $"{block.Id} changed nothing. {footprintIssue}";
                return false;
            }

            Vector2Int[] mapped = LevelTemplateEdit.ToLibraryOrientation(current, GetBoardCells(block), newBoardCells);
            if (mapped == null)
            {
                issue = $"{block.Id} no longer matches {templateName}; Re-stamp it first.";
                return false;
            }

            Vector2Int origin = LevelShapeThumbnail.GetBounds(newBoardCells).min;
            Vector2Int[] footprint = LevelCustomPiece.Normalize(newBoardCells);
            if (!LevelShapeRestamp.TryRedrawBlock(level, blockIndex, footprint, origin, block.ColorId,
                    out LevelData redrawn, out string redrawIssue))
            {
                issue = $"{block.Id}: {redrawIssue}";
                return false;
            }

            // The redrawn block has the new footprint's key, so only the template's other listed blocks move.
            if (!LevelShapeRestamp.TryRestamp(redrawn, currentKey, current, mapped, templateName,
                    out LevelData restamped, out _, out issue, onlyBlockIds: memberIds ?? Array.Empty<string>()))
            {
                return false;
            }

            // The stroke's own record is kept exactly as it was drawn.
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(restamped);
            blocks[blockIndex] = redrawn.GetBlockAt(blockIndex);
            result = LevelRecordCopy.WithBlocks(restamped, blocks);
            libraryCells = mapped;
            issue = null;
            return true;
        }

        /// <summary>
        /// Gives every block of the template (the edit's members, LevelTemplateEdit.GetMemberIds) that still has the
        /// pending footprint's key the base footprint again; a block that merely shares that key is left alone
        /// (DEC-193). A block the edit began with goes back exactly: its recorded origin and cells, while its colour
        /// and effects stay as they are now (defect D-001: a re-stamp anchors at the grown bounding-box corner and could
        /// move blocks onto others). A block that joined during the edit is re-stamped from the pending footprint in its
        /// own rotation or mirror and bounding-box corner; that re-stamp is refused as a whole, naming the blocks, when
        /// one would leave the board or cover a void or a board object. The block <paramref name="keepBlockId"/> names,
        /// when given, keeps its cells (Make a separate piece). A level without such blocks is returned as given.
        /// </summary>
        public static bool TryDiscard(LevelData level, LevelTemplateEdit edit, out LevelData result, out string issue,
            string keepBlockId = null)
        {
            result = null;
            if (level == null || edit == null || edit.Cells.Count == 0 || edit.BaseCells.Count == 0
                || !edit.Key.IsValid)
            {
                issue = "Nothing to discard.";
                return false;
            }

            // Each block is handled on its own, so a block that goes back exactly never takes part in a re-stamp that
            // could refuse or move it.
            ShapeKey pendingKey = edit.Key;
            List<string> members = edit.GetMemberIds();
            LevelData current = level;
            List<string> refused = null;
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = current.GetBlockAt(index);
                if (block == null || block.CellCount == 0 || block.Key != pendingKey
                    || !LevelBlockQueries.ContainsId(members, block.Id)
                    || (keepBlockId != null && string.Equals(block.Id, keepBlockId, StringComparison.Ordinal)))
                {
                    continue;
                }

                LevelTemplateBlockOrigin original = FindOriginal(edit, block.Id);
                if (original != null && original.Cells.Count > 0)
                {
                    LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(current);
                    blocks[index] = Restore(block, original);
                    current = LevelRecordCopy.WithBlocks(current, blocks);
                    continue;
                }

                if (!LevelShapeRestamp.TryRestampBlock(current, index, edit.Cells, edit.BaseCells, out LevelData next,
                        out string blockIssue))
                {
                    // The rest is still tried so the status can list every block in the way.
                    refused ??= new List<string>();
                    refused.Add(blockIssue);
                    continue;
                }

                current = next;
            }

            if (refused != null)
            {
                issue = $"{refused.Count} block(s) of {edit.DisplayName} would not fit: " + string.Join("; ", refused);
                return false;
            }

            result = current;
            issue = null;
            return true;
        }

        // The block with its recorded origin and cells; its id, colour and effects stay as they are now.
        private static LevelBlockData Restore(LevelBlockData block, LevelTemplateBlockOrigin original)
        {
            Vector2Int[] cells = LevelCells.Copy(original.Cells);
            return LevelRecordCopy.WithOrigin(LevelRecordCopy.WithCells(block, cells), original.Origin);
        }

        private static LevelTemplateBlockOrigin FindOriginal(LevelTemplateEdit edit, string blockId)
        {
            for (int index = 0; blockId != null && index < edit.Originals.Count; index++)
            {
                if (string.Equals(edit.Originals[index].BlockId, blockId, StringComparison.Ordinal))
                {
                    return edit.Originals[index];
                }
            }

            return null;
        }

        /// <summary>
        /// The catalog with each edit's pending cells in place of its template's cells, at the same index; the
        /// template's name and library key stay, so the overlaid choice's own key is the pending one. The template is
        /// found by the edit's key, else by its name (LevelShapeCatalog.IndexOfTemplate); an edit whose template the
        /// catalog lacks is ignored and a later edit of the same template wins. Returns the given catalog when there is
        /// nothing to overlay: no edits, no matching template, or a catalog that failed to load.
        /// </summary>
        public static LevelShapeCatalog Overlay(LevelShapeCatalog catalog, IReadOnlyList<LevelTemplateEdit> edits)
        {
            if (catalog == null || catalog.Error != null || edits == null || edits.Count == 0)
            {
                return catalog;
            }

            LevelShapeChoice[] choices = null;
            for (int index = 0; index < edits.Count; index++)
            {
                LevelTemplateEdit edit = edits[index];
                int position = edit != null && edit.Cells.Count > 0
                    ? catalog.IndexOfTemplate(edit.TemplateKey, edit.DisplayName)
                    : -1;
                if (position < 0)
                {
                    continue;
                }

                if (choices == null)
                {
                    choices = new LevelShapeChoice[catalog.Count];
                    for (int copy = 0; copy < choices.Length; copy++)
                    {
                        choices[copy] = catalog.Get(copy);
                    }
                }

                LevelShapeChoice original = catalog.Get(position);
                choices[position] = new LevelShapeChoice(original.DisplayName, edit.Cells, original.TemplateKey);
            }

            return choices == null ? catalog : LevelShapeCatalog.Create(choices);
        }

        // A block's cells on the board: its origin plus each relative cell.
        private static Vector2Int[] GetBoardCells(LevelBlockData block)
        {
            Vector2Int[] cells = LevelRecordCopy.GetCells(block);
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] += block.Origin;
            }

            return cells;
        }
    }
}
