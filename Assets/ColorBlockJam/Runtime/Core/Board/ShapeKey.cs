using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ColorBlockJam.Runtime.Board
{
    /// <summary>
    /// A block type's identity, derived from its cells and stored nowhere (DEC-193): the smallest 25-bit row-major
    /// mask (bit y * 5 + x) among the 8 rotations and mirrors of the footprint shifted into a 5 x 5 box, written as 7
    /// uppercase hex digits. The same shape is therefore the same piece in every level, database and client without
    /// a registry. This type is the single authority for the piece limits (at most 25 edge-connected cells inside a
    /// 5 x 5 box) and for the code; the default value is invalid and names no piece.
    /// </summary>
    public readonly struct ShapeKey : IEquatable<ShapeKey>
    {
        /// <summary>The most cells a piece holds.</summary>
        public const int MaxCells = 25;

        /// <summary>The widest and the tallest a piece can be, in cells.</summary>
        public const int MaxExtent = 5;

        /// <summary>Transforms 0-3 turn clockwise 0-3 times; 4-7 mirror across the vertical axis first, then turn.</summary>
        public const int TransformCount = 8;

        /// <summary>The length of a key's text: 7 uppercase hex digits.</summary>
        public const int TextLength = 7;

        private const int k_boxCells = MaxExtent * MaxExtent;
        private const int k_fullMask = (1 << k_boxCells) - 1;
        // Cells with x == 0 and x == 4 in the 5 x 5 box, so a horizontal shift cannot wrap a row.
        private const int k_firstColumn = 1 | 1 << 5 | 1 << 10 | 1 << 15 | 1 << 20;
        private const int k_lastColumn = k_firstColumn << (MaxExtent - 1);
        private const string k_textFormat = "X7";
        private const string k_invalidText = "invalid";

        // Why cells are or are not a piece, in the order a designer fixes them.
        private enum Problem
        {
            None,
            Empty,
            Repeated,
            TooManyCells,
            TooWide,
            Disconnected
        }

        private ShapeKey(int code)
        {
            Code = code;
        }

        /// <summary>The canonical footprint code; 0 for the default, invalid key.</summary>
        public int Code { get; }

        /// <summary>False for the default key, which names no piece.</summary>
        public bool IsValid => Code != 0;

        /// <summary>
        /// Checks the piece limits in the order a designer fixes them; the first broken rule gives the issue, a
        /// sentence the caller prefixes with the block or piece it names. Allocation-free for valid cells.
        /// </summary>
        public static bool TryValidate(IReadOnlyList<Vector2Int> cells, out string issue)
        {
            switch (Check(cells))
            {
                case Problem.None:
                    issue = null;
                    return true;
                case Problem.Empty:
                    issue = "it has no cells";
                    return false;
                case Problem.Repeated:
                    issue = "it lists a cell more than once";
                    return false;
                case Problem.TooManyCells:
                    issue = $"it has {cells.Count} cells; a piece holds at most {MaxCells}";
                    return false;
                case Problem.TooWide:
                    GetBounds(cells, out Vector2Int min, out Vector2Int max);
                    issue = $"it spans {max.x - min.x + 1} x {max.y - min.y + 1} cells; a piece fits inside {MaxExtent} x {MaxExtent}";
                    return false;
                default:
                    issue = "its cells do not all touch edge to edge";
                    return false;
            }
        }

        /// <summary>The key of cells within the piece limits, wherever they sit; false and the default key otherwise. Allocation-free.</summary>
        public static bool TryCreate(IReadOnlyList<Vector2Int> cells, out ShapeKey key)
        {
            if (Check(cells) != Problem.None)
            {
                key = default;
                return false;
            }

            key = new ShapeKey(Canonicalize(GetMask(cells)));
            return true;
        }

        /// <summary>
        /// The key a canonical code names; false for any other code, such as another rotation's mask of the same
        /// piece or disconnected bits. Allocation-free.
        /// </summary>
        public static bool TryFromCode(int code, out ShapeKey key)
        {
            bool isKey = code > 0 && code <= k_fullMask && IsConnected(code) && Canonicalize(code) == code;
            key = isKey ? new ShapeKey(code) : default;
            return isKey;
        }

        /// <summary>Reads a key's 7 uppercase hex digits; false for any other text and for a non-canonical code.</summary>
        public static bool TryParse(string text, out ShapeKey key)
        {
            key = default;
            if (text == null || text.Length != TextLength)
            {
                return false;
            }

            int code = 0;
            for (int index = 0; index < TextLength; index++)
            {
                char digit = text[index];
                int value = digit >= '0' && digit <= '9' ? digit - '0'
                    : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10
                    : -1;
                if (value < 0)
                {
                    return false;
                }

                code = (code << 4) | value;
            }

            return TryFromCode(code, out key);
        }

        /// <summary>The key's canonical cells in row-major order from (0, 0); empty for the default key. It allocates.</summary>
        public Vector2Int[] Decode()
        {
            Vector2Int[] cells = new Vector2Int[CountBits(Code)];
            int next = 0;
            for (int bit = 0; bit < k_boxCells; bit++)
            {
                if ((Code & (1 << bit)) != 0)
                {
                    cells[next++] = new Vector2Int(bit % MaxExtent, bit / MaxExtent);
                }
            }

            return cells;
        }

        /// <summary>
        /// True when one footprint is a rotation or mirror of the other, for footprints of any size. Editor and load
        /// time only: it allocates.
        /// </summary>
        public static bool AreSame(IReadOnlyList<Vector2Int> left, IReadOnlyList<Vector2Int> right)
        {
            return FindTransform(left, right, TransformCount) >= 0;
        }

        /// <summary>True when one footprint is a rotation of the other, without a mirror. It allocates.</summary>
        public static bool IsRotationOf(IReadOnlyList<Vector2Int> left, IReadOnlyList<Vector2Int> right)
        {
            return FindTransform(left, right, TransformCount / 2) >= 0;
        }

        /// <summary>The 7 uppercase hex digits of a valid key, as level JSON writes it; "invalid" for the default key.</summary>
        public override string ToString()
        {
            return IsValid ? Code.ToString(k_textFormat, CultureInfo.InvariantCulture) : k_invalidText;
        }

        public bool Equals(ShapeKey other)
        {
            return Code == other.Code;
        }

        public override bool Equals(object obj)
        {
            return obj is ShapeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Code;
        }

        public static bool operator ==(ShapeKey left, ShapeKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ShapeKey left, ShapeKey right)
        {
            return !left.Equals(right);
        }

        private static Problem Check(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return Problem.Empty;
            }

            for (int index = 1; index < cells.Count; index++)
            {
                for (int earlier = 0; earlier < index; earlier++)
                {
                    if (cells[index] == cells[earlier])
                    {
                        return Problem.Repeated;
                    }
                }
            }

            if (cells.Count > MaxCells)
            {
                return Problem.TooManyCells;
            }

            GetBounds(cells, out Vector2Int min, out Vector2Int max);
            if (max.x - min.x + 1 > MaxExtent || max.y - min.y + 1 > MaxExtent)
            {
                return Problem.TooWide;
            }

            return IsConnected(GetMask(cells)) ? Problem.None : Problem.Disconnected;
        }

        // The cells shifted to (0, 0), as a mask in the 5 x 5 box. Call on cells within the limits.
        private static int GetMask(IReadOnlyList<Vector2Int> cells)
        {
            GetBounds(cells, out Vector2Int min, out _);
            int mask = 0;
            for (int index = 0; index < cells.Count; index++)
            {
                mask |= 1 << ((cells[index].y - min.y) * MaxExtent + cells[index].x - min.x);
            }

            return mask;
        }

        // The smallest of the mask's 8 transforms in row-major list order. Two sorted cell lists of equal length first
        // differ at the lowest bit only one of them holds, and the list holding it is the smaller. Allocation-free.
        private static int Canonicalize(int mask)
        {
            int best = Transform(mask, 0);
            for (int transform = 1; transform < TransformCount; transform++)
            {
                int candidate = Transform(mask, transform);
                int difference = candidate ^ best;
                if (difference != 0 && (candidate & difference & -difference) != 0)
                {
                    best = candidate;
                }
            }

            return best;
        }

        // The mask's cells after the transform, shifted to (0, 0) in the 5 x 5 box.
        private static int Transform(int mask, int transform)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int bit = 0; bit < k_boxCells; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    Vector2Int cell = Apply(new Vector2Int(bit % MaxExtent, bit / MaxExtent), transform);
                    minX = Math.Min(minX, cell.x);
                    minY = Math.Min(minY, cell.y);
                }
            }

            int result = 0;
            for (int bit = 0; bit < k_boxCells; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                {
                    Vector2Int cell = Apply(new Vector2Int(bit % MaxExtent, bit / MaxExtent), transform);
                    result |= 1 << ((cell.y - minY) * MaxExtent + cell.x - minX);
                }
            }

            return result;
        }

        // Transforms 0-3 turn 0-3 quarter turns clockwise; 4-7 mirror across the vertical axis first. Not normalized.
        private static Vector2Int Apply(Vector2Int cell, int transform)
        {
            int x = transform >= TransformCount / 2 ? -cell.x : cell.x;
            int y = cell.y;
            switch (transform % (TransformCount / 2))
            {
                case 1: return new Vector2Int(y, -x);
                case 2: return new Vector2Int(-x, -y);
                case 3: return new Vector2Int(-y, x);
                default: return new Vector2Int(x, y);
            }
        }

        private static bool IsConnected(int mask)
        {
            int reached = mask & -mask;
            while (true)
            {
                int grown = reached
                    | ((reached << 1) & ~k_firstColumn)
                    | ((reached >> 1) & ~k_lastColumn)
                    | (reached << MaxExtent)
                    | (reached >> MaxExtent);
                grown &= mask;
                if (grown == reached)
                {
                    return reached == mask;
                }

                reached = grown;
            }
        }

        private static int FindTransform(IReadOnlyList<Vector2Int> from, IReadOnlyList<Vector2Int> to, int transforms)
        {
            if (from == null || to == null || from.Count == 0 || from.Count != to.Count)
            {
                return -1;
            }

            Vector2Int[] target = Normalize(to, 0);
            Array.Sort(target, CompareRowMajor);
            for (int transform = 0; transform < transforms; transform++)
            {
                Vector2Int[] candidate = Normalize(from, transform);
                Array.Sort(candidate, CompareRowMajor);
                bool equal = true;
                for (int index = 0; index < candidate.Length && equal; index++)
                {
                    equal = candidate[index] == target[index];
                }

                if (equal)
                {
                    return transform;
                }
            }

            return -1;
        }

        private static Vector2Int[] Normalize(IReadOnlyList<Vector2Int> cells, int transform)
        {
            Vector2Int[] result = new Vector2Int[cells.Count];
            Vector2Int min = new Vector2Int(int.MaxValue, int.MaxValue);
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = Apply(cells[index], transform);
                min = Vector2Int.Min(min, result[index]);
            }

            for (int index = 0; index < result.Length; index++)
            {
                result[index] -= min;
            }

            return result;
        }

        private static int CompareRowMajor(Vector2Int a, Vector2Int b)
        {
            return a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x);
        }

        private static void GetBounds(IReadOnlyList<Vector2Int> cells, out Vector2Int min, out Vector2Int max)
        {
            min = cells[0];
            max = cells[0];
            for (int index = 1; index < cells.Count; index++)
            {
                min = Vector2Int.Min(min, cells[index]);
                max = Vector2Int.Max(max, cells[index]);
            }
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
            {
                count++;
            }

            return count;
        }
    }
}
