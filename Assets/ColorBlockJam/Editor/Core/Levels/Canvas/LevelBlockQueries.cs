using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The one query "blocks of a level (or levels) with this piece", by the ShapeKey of their cells (DEC-193), shared by
    /// every piece-save, template-save and delete plan so a future rule change is made once instead of in each copy.
    /// </summary>
    internal static class LevelBlockQueries
    {
        /// <summary>The blocks of this level whose cells form this key; none for the default key.</summary>
        public static int CountBlocks(LevelData level, ShapeKey key)
        {
            int count = 0;
            for (int index = 0; key.IsValid && level != null && index < level.BlockCount; index++)
            {
                if (level.GetBlockAt(index)?.Key == key)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>The blocks of these levels whose cells form this key.</summary>
        public static int CountBlocks(IReadOnlyList<LevelData> levels, ShapeKey key)
        {
            int count = 0;
            for (int index = 0; levels != null && index < levels.Count; index++)
            {
                count += CountBlocks(levels[index], key);
            }

            return count;
        }

        /// <summary>True when a block of the level has cells that form this key.</summary>
        public static bool HasBlock(LevelData level, ShapeKey key)
        {
            for (int index = 0; key.IsValid && level != null && index < level.BlockCount; index++)
            {
                if (level.GetBlockAt(index)?.Key == key)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The index of the first block whose cells form this key, or -1.</summary>
        public static int FindBlock(LevelData level, ShapeKey key)
        {
            for (int index = 0; key.IsValid && level != null && index < level.BlockCount; index++)
            {
                if (level.GetBlockAt(index)?.Key == key)
                {
                    return index;
                }
            }

            return -1;
        }

        /// <summary>
        /// The ids of the blocks whose cells form this key, in block order: the blocks of a piece when an edit of it
        /// starts, before any stroke can give the key to a block of another piece (DEC-193).
        /// </summary>
        public static List<string> GetBlockIds(LevelData level, ShapeKey key)
        {
            List<string> ids = new List<string>();
            for (int index = 0; key.IsValid && level != null && index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block != null && block.Key == key && !string.IsNullOrEmpty(block.Id))
                {
                    ids.Add(block.Id);
                }
            }

            return ids;
        }

        /// <summary>True when the list names this block id, compared ordinally.</summary>
        public static bool ContainsId(IReadOnlyList<string> blockIds, string blockId)
        {
            for (int index = 0; blockIds != null && blockId != null && index < blockIds.Count; index++)
            {
                if (string.Equals(blockIds[index], blockId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
