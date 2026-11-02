using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The compact cell text of level JSON formatVersion 3 (DEC-193): one cell is "x,y" in invariant digits and a
    /// list is cells separated by single spaces, "0,0 1,0 0,1". Parsing is strict: a malformed cell or list is
    /// refused with a problem the reader prefixes with its level and entity. Load and save time only; it allocates.
    /// </summary>
    public static class CellText
    {
        private const char k_axisSeparator = ',';
        private const char k_cellSeparator = ' ';

        /// <summary>One cell as "x,y".</summary>
        public static string Format(Vector2Int cell)
        {
            StringBuilder text = new StringBuilder();
            Append(text, cell);
            return text.ToString();
        }

        /// <summary>The cells as "x,y" separated by single spaces; an empty list gives an empty string.</summary>
        public static string Format(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null)
            {
                throw new ArgumentNullException(nameof(cells));
            }

            StringBuilder text = new StringBuilder(cells.Count * 4);
            for (int index = 0; index < cells.Count; index++)
            {
                if (index > 0)
                {
                    text.Append(k_cellSeparator);
                }

                Append(text, cells[index]);
            }

            return text.ToString();
        }

        /// <summary>Reads one "x,y" cell; otherwise false and a problem such as "'1,' is not an x,y cell".</summary>
        public static bool TryParseCell(string text, out Vector2Int cell, out string problem)
        {
            if (text != null && TryParseCell(text, 0, text.Length, out cell))
            {
                problem = null;
                return true;
            }

            cell = default;
            problem = text == null ? "the text is missing" : $"'{text}' is not an x,y cell";
            return false;
        }

        /// <summary>
        /// Reads cells separated by single spaces; an empty string is an empty list. Otherwise false and a problem
        /// such as "'1,a' is not an x,y cell" or "'0,0  1,0' has an empty cell; separate cells with one space".
        /// </summary>
        public static bool TryParseCells(string text, out Vector2Int[] cells, out string problem)
        {
            cells = null;
            if (text == null)
            {
                problem = "the text is missing";
                return false;
            }

            if (text.Length == 0)
            {
                cells = Array.Empty<Vector2Int>();
                problem = null;
                return true;
            }

            int count = 1;
            for (int index = 0; index < text.Length; index++)
            {
                if (text[index] == k_cellSeparator)
                {
                    count++;
                }
            }

            Vector2Int[] parsed = new Vector2Int[count];
            int start = 0;
            for (int cellIndex = 0; cellIndex < count; cellIndex++)
            {
                int end = text.IndexOf(k_cellSeparator, start);
                if (end < 0)
                {
                    end = text.Length;
                }

                if (end == start)
                {
                    problem = $"'{text}' has an empty cell; separate cells with one space";
                    return false;
                }

                if (!TryParseCell(text, start, end, out parsed[cellIndex]))
                {
                    problem = $"'{text.Substring(start, end - start)}' is not an x,y cell";
                    return false;
                }

                start = end + 1;
            }

            cells = parsed;
            problem = null;
            return true;
        }

        private static void Append(StringBuilder text, Vector2Int cell)
        {
            text.Append(cell.x.ToString(CultureInfo.InvariantCulture))
                .Append(k_axisSeparator)
                .Append(cell.y.ToString(CultureInfo.InvariantCulture));
        }

        // Reads "x,y" from text[start, end): two integers, each an optional '-' and digits, joined by one comma.
        private static bool TryParseCell(string text, int start, int end, out Vector2Int cell)
        {
            cell = default;
            int comma = text.IndexOf(k_axisSeparator, start, end - start);
            if (comma < 0 || !TryParseNumber(text, start, comma, out int x) || !TryParseNumber(text, comma + 1, end, out int y))
            {
                return false;
            }

            cell = new Vector2Int(x, y);
            return true;
        }

        // Reads an optional '-' and one or more digits from text[start, end) into an int; false for anything else,
        // including a value an int cannot hold.
        private static bool TryParseNumber(string text, int start, int end, out int value)
        {
            value = 0;
            bool negative = start < end && text[start] == '-';
            int first = negative ? start + 1 : start;
            if (first == end)
            {
                return false;
            }

            long magnitude = 0;
            for (int index = first; index < end; index++)
            {
                char digit = text[index];
                if (digit < '0' || digit > '9')
                {
                    return false;
                }

                magnitude = magnitude * 10 + (digit - '0');
                if (magnitude > (long)int.MaxValue + 1)
                {
                    return false;
                }
            }

            long signed = negative ? -magnitude : magnitude;
            if (signed > int.MaxValue)
            {
                return false;
            }

            value = (int)signed;
            return true;
        }
    }
}
