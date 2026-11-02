using System;
using System.Globalization;
using System.Text;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Turns level identifiers into designer words for the check's reasons: colours ("Light Blue"), block labels
    /// ("B1"), pieces (the caller's name for a block's ShapeKey, otherwise "4-cell"), board cells in the Issues list's
    /// "(x,y)" form, mechanic names, budget limits and solution counts. No ShapeKey reaches a reason.
    /// </summary>
    internal static class LevelDesignerText
    {
        /// <summary>
        /// "the {colour} {label} block at (x,y)": the label is the block's canvas label ("B1"), so the reason points at
        /// what the designer sees on the canvas; a block without an id falls back to its piece name (GetShapeName). The
        /// cell is the block's lowest, then leftmost, board cell.
        /// </summary>
        public static string DescribeBlock(LevelData level, int blockIndex)
        {
            return DescribeBlock(level, blockIndex, null);
        }

        /// <summary>DescribeBlock with an id-less block's piece named through <paramref name="shapeNames"/>.</summary>
        public static string DescribeBlock(LevelData level, int blockIndex, Func<ShapeKey, string> shapeNames)
        {
            LevelBlockData block = level.GetBlockAt(blockIndex);
            Vector2Int cell = GetNamingCell(block);
            string label = string.IsNullOrWhiteSpace(block.Id)
                ? GetShapeName(block, shapeNames)
                : block.Id.ToUpperInvariant();
            return "the " + GetColourName(block.ColorId) + " " + label + " block at " + FormatCell(cell.x, cell.y);
        }

        /// <summary>
        /// The blocks of a mask (bit block) in block order, joined as "A", "A and B" or "A, B and C"; past three, the rest
        /// are counted ("A, B, C and 2 other blocks") so the sentence stays readable (DEC-176).
        /// </summary>
        public static string DescribeBlocks(LevelData level, int blockMask, LevelBlockDescriber describe)
        {
            const int named = 3;
            string[] names = new string[named];
            int total = 0;
            for (int block = 0; block < level.BlockCount; block++)
            {
                if ((blockMask & (1 << block)) == 0)
                {
                    continue;
                }

                if (total < named)
                {
                    names[total] = describe(level, block);
                }

                total++;
            }

            if (total <= 1)
            {
                return total == 1 ? names[0] : string.Empty;
            }

            if (total <= named)
            {
                return total == 2 ? names[0] + " and " + names[1] : names[0] + ", " + names[1] + " and " + names[2];
            }

            return names[0] + ", " + names[1] + ", " + names[2] + " and "
                + Count(total - named, "other block", "other blocks");
        }

        /// <summary>A PaletteColor name split into words: "LightBlue" becomes "Light Blue".</summary>
        public static string GetColourName(string colorId)
        {
            return string.IsNullOrWhiteSpace(colorId) ? "unknown colour" : SplitWords(colorId);
        }

        /// <summary>
        /// The block's piece in designer words: the name <paramref name="shapeNames"/> gives for the block's ShapeKey,
        /// such as a piece library template or the level's name for its own piece (the Level Editor supplies it,
        /// DEC-193); otherwise "{n}-cell", for example "4-cell". The lookup is never asked about a block without a key.
        /// </summary>
        public static string GetShapeName(LevelBlockData block, Func<ShapeKey, string> shapeNames)
        {
            ShapeKey key = block.Key;
            string name = key.IsValid && shapeNames != null ? shapeNames(key) : null;
            return string.IsNullOrWhiteSpace(name)
                ? block.CellCount.ToString(CultureInfo.InvariantCulture) + "-cell"
                : name;
        }

        /// <summary>The Issues list's cell form: zero-based, x then y, no space.</summary>
        public static string FormatCell(int x, int y)
        {
            return "(" + x.ToString(CultureInfo.InvariantCulture) + "," + y.ToString(CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>"1 block" or "3 blocks".</summary>
        public static string Count(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }

        /// <summary>"250,000 positions".</summary>
        public static string FormatPositions(int positions)
        {
            return positions.ToString("N0", CultureInfo.InvariantCulture) + (positions == 1 ? " position" : " positions");
        }

        /// <summary>"3 seconds", "0.5 seconds" or "1 second".</summary>
        public static string FormatSeconds(double seconds)
        {
            return seconds.ToString("0.###", CultureInfo.InvariantCulture) + (seconds == 1.0 ? " second" : " seconds");
        }

        /// <summary>
        /// A listing's count: "1 solution" or "12 solutions" when exhausted, "20+ solutions" when one more exit order
        /// exists, "at least 3 solutions" when a budget or a cancel stopped it; empty when nothing was listed.
        /// </summary>
        public static string FormatSolutionCount(int count, LevelSolutionListState state)
        {
            switch (state)
            {
                case LevelSolutionListState.Complete:
                    return Count(count, "solution", "solutions");
                case LevelSolutionListState.More:
                    return count.ToString(CultureInfo.InvariantCulture) + "+ solutions";
                case LevelSolutionListState.Partial:
                    return "at least " + Count(count, "solution", "solutions");
                default:
                    return string.Empty;
            }
        }

        /// <summary>The sentence without its final period, a middle dot and the count: "Solvable in 5 moves · 12 solutions".</summary>
        public static string FormatSummary(string sentence, string solutionCount)
        {
            string head = sentence.EndsWith(".", StringComparison.Ordinal) ? sentence.Substring(0, sentence.Length - 1) : sentence;
            return head + " · " + solutionCount;
        }

        /// <summary>
        /// A plural designer name for an effect type on its owner: block effects read "Arrow blocks", door
        /// effects "iced gates" and board objects "barriers". The type id's words become the name, with
        /// "color" spelled "colour" as the rest of the editor does.
        /// </summary>
        public static string GetMechanicName(string typeId, LevelEffectTarget target)
        {
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return target == LevelEffectTarget.CellObject ? "board objects without a type" : "effects without a type";
            }

            string[] words = typeId.Split('-');
            for (int i = 0; i < words.Length; i++)
            {
                words[i] = words[i].Replace("color", "colour");
            }

            switch (target)
            {
                case LevelEffectTarget.Block:
                    for (int i = 0; i < words.Length; i++)
                    {
                        words[i] = Capitalize(words[i]);
                    }

                    return string.Join(" ", words) + " blocks";

                case LevelEffectTarget.Door:
                    if (words[words.Length - 1] == "door")
                    {
                        words[words.Length - 1] = "gates";
                        return string.Join(" ", words);
                    }

                    return string.Join(" ", words) + " gates";

                default:
                    words[words.Length - 1] = Pluralize(words[words.Length - 1]);
                    return string.Join(" ", words);
            }
        }

        /// <summary>Splits PascalCase: "TripleHorizontal" gives "Triple Horizontal", "LTypeB1" gives "L Type B1".</summary>
        public static string SplitWords(string text)
        {
            StringBuilder builder = new StringBuilder(text.Length + 4);
            for (int i = 0; i < text.Length; i++)
            {
                char current = text[i];
                if (i > 0 && char.IsUpper(current))
                {
                    char previous = text[i - 1];
                    bool nextIsLower = i + 1 < text.Length && char.IsLower(text[i + 1]);
                    if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                    {
                        builder.Append(' ');
                    }
                }

                builder.Append(current == '_' ? ' ' : current);
            }

            return builder.ToString();
        }

        private static Vector2Int GetNamingCell(LevelBlockData block)
        {
            Vector2Int best = block.Origin;
            bool found = false;
            for (int i = 0; i < block.CellCount; i++)
            {
                Vector2Int cell = block.Origin + block.GetCellAt(i);
                if (!found || cell.y < best.y || (cell.y == best.y && cell.x < best.x))
                {
                    best = cell;
                    found = true;
                }
            }

            return best;
        }

        private static string Capitalize(string word)
        {
            return word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word.Substring(1);
        }

        // "barrier" gives "barriers", "ivy" stays "ivy" (a mass noun), "path" gives "paths".
        private static string Pluralize(string word)
        {
            if (word.Length == 0 || word.EndsWith("s", StringComparison.Ordinal) || word.EndsWith("y", StringComparison.Ordinal))
            {
                return word;
            }

            return word + "s";
        }
    }
}
