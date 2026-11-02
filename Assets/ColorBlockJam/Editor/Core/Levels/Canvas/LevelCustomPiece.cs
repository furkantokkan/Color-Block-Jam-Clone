using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Designer words and footprint helpers over ShapeKey, the single authority for the piece limits (at most 25
    /// edge-connected cells inside a 5 x 5 box) and for a piece's identity (DEC-193): the limit check as a sentence the
    /// Level Editor and the Piece Editor show, and the turns and mirrors the designer's R and F keys apply.
    /// </summary>
    internal static class LevelCustomPiece
    {
        /// <summary>The most cells a piece holds (ShapeKey.MaxCells).</summary>
        public const int k_maxCells = ShapeKey.MaxCells;

        /// <summary>The widest and the tallest a piece can be, in cells (ShapeKey.MaxExtent).</summary>
        public const int k_maxExtent = ShapeKey.MaxExtent;

        /// <summary>Transforms 0-3 turn clockwise 0-3 times; 4-7 mirror horizontally first, then turn.</summary>
        public const int k_transformCount = ShapeKey.TransformCount;

        // ShapeKey's issues call the piece "it" or "its" ("it has 26 cells; a piece holds at most 25"), so the
        // designer sentence names the piece in their place.
        private const string k_subject = "it ";
        private const string k_possessive = "its ";

        private static readonly Comparison<Vector2Int> s_rowMajor = CompareCells;

        /// <summary>
        /// True when the footprint is a piece within the ShapeKey limits; otherwise a designer sentence, for example
        /// "The piece spans 6 x 1 cells; a piece fits inside 5 x 5."
        /// </summary>
        public static bool TryValidate(IReadOnlyList<Vector2Int> cells, out string issue)
        {
            if (ShapeKey.TryValidate(cells, out string reason))
            {
                issue = null;
                return true;
            }

            issue = ToSentence(reason);
            return false;
        }

        /// <summary>
        /// Turns and mirrors a footprint through the canvas Rotate and Mirror helpers, so a transform index means the
        /// same thing as the designer's R and F keys. The result starts at cell (0, 0).
        /// </summary>
        public static Vector2Int[] Transform(IReadOnlyList<Vector2Int> cells, int transform)
        {
            if (transform < 0 || transform >= k_transformCount)
            {
                throw new ArgumentOutOfRangeException(nameof(transform));
            }

            Vector2Int[] result = transform >= k_transformCount / 2
                ? LevelPieceTools.FlipHorizontal(cells)
                : Normalize(cells);
            for (int turn = 0; turn < transform % (k_transformCount / 2); turn++)
            {
                result = LevelPieceTools.RotateClockwise(result);
            }

            return result;
        }

        /// <summary>The lowest transform that maps <paramref name="from"/> onto <paramref name="to"/>; -1 when none.</summary>
        public static int FindTransform(IReadOnlyList<Vector2Int> from, IReadOnlyList<Vector2Int> to)
        {
            if (from == null || to == null || from.Count == 0 || from.Count != to.Count)
            {
                return -1;
            }

            Vector2Int[] target = Normalize(to);
            Array.Sort(target, s_rowMajor);
            for (int transform = 0; transform < k_transformCount; transform++)
            {
                Vector2Int[] candidate = Transform(from, transform);
                Array.Sort(candidate, s_rowMajor);
                if (CompareLists(candidate, target) == 0)
                {
                    return transform;
                }
            }

            return -1;
        }

        /// <summary>The cells shifted so their bounding box starts at (0, 0).</summary>
        public static Vector2Int[] Normalize(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("A footprint needs at least one cell.", nameof(cells));
            }

            RectInt bounds = LevelShapeThumbnail.GetBounds(cells);
            Vector2Int[] result = new Vector2Int[cells.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = cells[index] - bounds.min;
            }

            return result;
        }

        /// <summary>True when every cell reaches every other through edge-sharing steps; an empty list is not connected.</summary>
        public static bool IsConnected(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return false;
            }

            bool[] reached = new bool[cells.Count];
            int[] queue = new int[cells.Count];
            int head = 0;
            int tail = 0;
            queue[tail++] = 0;
            reached[0] = true;
            while (head < tail)
            {
                Vector2Int cell = cells[queue[head++]];
                for (int other = 0; other < cells.Count; other++)
                {
                    Vector2Int delta = cells[other] - cell;
                    if (!reached[other] && Mathf.Abs(delta.x) + Mathf.Abs(delta.y) == 1)
                    {
                        reached[other] = true;
                        queue[tail++] = other;
                    }
                }
            }

            return tail == cells.Count;
        }

        // "it has no cells" becomes "The piece has no cells."; "its cells do not all touch" becomes "The piece's cells ...".
        private static string ToSentence(string reason)
        {
            if (reason.StartsWith(k_possessive, StringComparison.Ordinal))
            {
                return "The piece's " + reason.Substring(k_possessive.Length) + ".";
            }

            return reason.StartsWith(k_subject, StringComparison.Ordinal)
                ? "The piece " + reason.Substring(k_subject.Length) + "."
                : "The piece is not valid: " + reason + ".";
        }

        private static int CompareCells(Vector2Int a, Vector2Int b)
        {
            return a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
        }

        private static int CompareLists(Vector2Int[] a, Vector2Int[] b)
        {
            int count = Mathf.Min(a.Length, b.Length);
            for (int index = 0; index < count; index++)
            {
                int order = CompareCells(a[index], b[index]);
                if (order != 0)
                {
                    return order;
                }
            }

            return a.Length.CompareTo(b.Length);
        }
    }
}
