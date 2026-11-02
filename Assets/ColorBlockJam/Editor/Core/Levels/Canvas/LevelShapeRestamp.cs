using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Pure whole-level shape edits: Re-stamp gives the blocks of one piece (the blocks whose cells form one ShapeKey, or
    /// the recorded blocks of the piece an edit changes, DEC-193) a new footprint in their own rotation or mirror,
    /// and a redraw gives one block new cells. Each returns a
    /// new level or refuses as a whole, never a partial edit (contract D2-A, D3-A). A re-stamp may leave blocks
    /// overlapping one another; the canvas marks them and Save Level waits until they are apart (D3-B).
    /// </summary>
    internal static class LevelShapeRestamp
    {
        /// <summary>
        /// Re-stamps every block whose cells form <paramref name="fromKey"/> with <paramref name="newFootprint"/>, or,
        /// when <paramref name="onlyBlockIds"/> is given, only those of them it lists. An edit in progress passes the
        /// blocks of the piece it edits, so a block that merely shares a key the piece passes through never moves
        /// (DEC-193); the banner's Re-stamp passes none, since it maps blocks at rest by key. Each block keeps the
        /// rotation or mirror that maps <paramref name="oldFootprint"/> onto its cells and the bottom-left corner of
        /// its bounding box; when the old footprint is unknown or does not map, the transform whose cells overlap the
        /// block most is kept. A new footprint of the same key changes nothing, so the blocks keep their own
        /// orientation. Refused as a whole, with every refused block listed under <paramref name="pieceName"/>, when
        /// any block would leave the board or cover a void or a board object; a block that only overlaps other blocks
        /// is re-stamped (D3-B). The ids of the changed blocks are added to <paramref name="changedIds"/> when given.
        /// </summary>
        public static bool TryRestamp(LevelData level, ShapeKey fromKey, IReadOnlyList<Vector2Int> oldFootprint,
            IReadOnlyList<Vector2Int> newFootprint, string pieceName, out LevelData result, out int count,
            out string issue, List<string> changedIds = null, IReadOnlyList<string> onlyBlockIds = null)
        {
            result = null;
            count = 0;
            issue = null;
            if (level == null || !fromKey.IsValid || newFootprint == null || newFootprint.Count == 0)
            {
                issue = "Nothing to re-stamp.";
                return false;
            }

            if (ShapeKey.TryCreate(newFootprint, out ShapeKey newKey) && newKey == fromKey)
            {
                result = level;
                return true;
            }

            LevelData current = level;
            List<string> refused = null;
            List<string> changed = new List<string>();
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = current.GetBlockAt(index);
                if (block == null || block.CellCount == 0 || block.Key != fromKey
                    || (onlyBlockIds != null && !LevelBlockQueries.ContainsId(onlyBlockIds, block.Id)))
                {
                    continue;
                }

                if (TryRestampAt(current, index, oldFootprint, newFootprint, out LevelData next, out string blockIssue))
                {
                    current = next;
                    changed.Add(block.Id);
                    continue;
                }

                // The rest is still tried so the banner can list every block in the way.
                refused ??= new List<string>();
                refused.Add($"{block.Id}: {blockIssue}");
            }

            if (refused != null)
            {
                issue = $"{refused.Count} block(s) of {pieceName} would not fit: " + string.Join("; ", refused);
                return false;
            }

            count = changed.Count;
            changedIds?.AddRange(changed);
            result = current;
            return true;
        }

        /// <summary>
        /// Re-stamps one block with <paramref name="newFootprint"/> in the rotation or mirror that maps
        /// <paramref name="oldFootprint"/> onto it. Refused, with the block named, when it would leave the board or
        /// cover a void or a board object; it may overlap other blocks (D3-B).
        /// </summary>
        public static bool TryRestampBlock(LevelData level, int blockIndex, IReadOnlyList<Vector2Int> oldFootprint,
            IReadOnlyList<Vector2Int> newFootprint, out LevelData result, out string issue)
        {
            result = null;
            LevelBlockData block = level != null && blockIndex >= 0 && blockIndex < level.BlockCount
                ? level.GetBlockAt(blockIndex)
                : null;
            if (block == null || block.CellCount == 0 || newFootprint == null || newFootprint.Count == 0)
            {
                issue = "Nothing to re-stamp.";
                return false;
            }

            if (TryRestampAt(level, blockIndex, oldFootprint, newFootprint, out result, out issue))
            {
                return true;
            }

            issue = $"{block.Id}: {issue}";
            return false;
        }

        /// <summary>
        /// Redraws one block with a new footprint at a new origin in the given colour: it keeps its id, list position
        /// and effects. Refused, like placing, when a cell leaves the board, covers a void or overlaps another block,
        /// and when it covers a board object.
        /// </summary>
        public static bool TryRedrawBlock(LevelData level, int blockIndex, IReadOnlyList<Vector2Int> footprint,
            Vector2Int origin, string colorId, out LevelData result, out string issue)
        {
            result = null;
            if (!LevelEditCommands.TryRemoveBlock(level, blockIndex, out LevelData without, out issue))
            {
                return false;
            }

            issue = GetBoardObjectIssue(level, origin, footprint);
            if (issue != null
                || !LevelEditCommands.TryPlaceBlock(without, footprint, colorId, origin, out _, out issue))
            {
                return false;
            }

            LevelBlockData old = level.GetBlockAt(blockIndex);
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(level);
            blocks[blockIndex] = new LevelBlockData(old.Id, colorId, origin, LevelCells.Copy(footprint),
                LevelRecordCopy.GetEffects(old));
            result = LevelRecordCopy.WithBlocks(level, blocks);
            return true;
        }

        /// <summary>
        /// Why a block with these relative cells at this origin would sit on a board object, naming the first covered
        /// cell and the object, or null when none is covered. The general place and move rule is not changed here.
        /// </summary>
        public static string GetBoardObjectIssue(LevelData level, Vector2Int origin, IReadOnlyList<Vector2Int> cells)
        {
            for (int index = 0; level != null && cells != null && index < cells.Count; index++)
            {
                Vector2Int cell = origin + cells[index];
                for (int objectIndex = 0; objectIndex < level.CellObjectCount; objectIndex++)
                {
                    LevelCellObjectData cellObject = level.GetCellObjectAt(objectIndex);
                    if (cellObject != null && cellObject.ContainsCell(cell))
                    {
                        return $"Cell ({cell.x},{cell.y}) is under board object {cellObject.Id}.";
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// The new footprint in the block's orientation, placed at the corner of the block's bounding box.
        /// </summary>
        internal static Vector2Int[] GetRestampedCells(IReadOnlyList<Vector2Int> blockCells,
            IReadOnlyList<Vector2Int> oldFootprint, IReadOnlyList<Vector2Int> newFootprint)
        {
            int transform = oldFootprint != null ? LevelCustomPiece.FindTransform(oldFootprint, blockCells) : -1;
            if (transform < 0)
            {
                transform = GetBestOverlap(blockCells, newFootprint);
            }

            Vector2Int corner = LevelShapeThumbnail.GetBounds(blockCells).min;
            Vector2Int[] cells = LevelCustomPiece.Transform(newFootprint, transform);
            for (int index = 0; index < cells.Length; index++)
            {
                cells[index] += corner;
            }

            return cells;
        }

        // One block's re-stamp: board objects first (the replace command does not check them), then the board and
        // void rules of TryReplaceBlockCells, which lets the block overlap other blocks (D3-B).
        private static bool TryRestampAt(LevelData level, int index, IReadOnlyList<Vector2Int> oldFootprint,
            IReadOnlyList<Vector2Int> newFootprint, out LevelData result, out string issue)
        {
            LevelBlockData block = level.GetBlockAt(index);
            Vector2Int[] cells = GetRestampedCells(LevelRecordCopy.GetCells(block), oldFootprint, newFootprint);
            issue = GetBoardObjectIssue(level, block.Origin, cells);
            if (issue != null)
            {
                result = null;
                return false;
            }

            return LevelEditCommands.TryReplaceBlockCells(level, index, cells, true, out result, out issue);
        }

        // The transform of the new footprint that shares the most cells with the block, both from their corner; the
        // lowest transform wins a tie, so an unchanged orientation is kept when nothing better fits.
        private static int GetBestOverlap(IReadOnlyList<Vector2Int> blockCells, IReadOnlyList<Vector2Int> newFootprint)
        {
            Vector2Int[] block = LevelCustomPiece.Normalize(blockCells);
            int best = 0;
            int bestShared = -1;
            for (int transform = 0; transform < LevelCustomPiece.k_transformCount; transform++)
            {
                Vector2Int[] candidate = LevelCustomPiece.Transform(newFootprint, transform);
                int shared = 0;
                for (int index = 0; index < candidate.Length; index++)
                {
                    if (Array.IndexOf(block, candidate[index]) >= 0)
                    {
                        shared++;
                    }
                }

                if (shared > bestShared)
                {
                    best = transform;
                    bestShared = shared;
                }
            }

            return best;
        }
    }
}
