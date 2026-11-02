using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Counts how many Rocket parts one authored footprint can show at the same time, so Home warms enough roots,
    /// Ice shells and Arrow markers and Playing never instantiates (B-19, AC-16). Every part of a block holds a
    /// connected subset of the authored footprint in the block's own cell space, and two parts are never 4-adjacent
    /// there, because a split separates them by the removed cell. Any nonempty subset is reachable by removing the
    /// other cells farthest first, so the most simultaneous parts is the maximum independent set of the footprint's
    /// 4-neighbour graph. Plain C# for preparation on Home only: it allocates.
    /// </summary>
    public static class FragmentCapacity
    {
        /// <summary>Largest footprint whose subsets are enumerated exactly (at most 4095 subsets), as ModularBlockView does.</summary>
        public const int MaxEnumeratedCells = 12;

        /// <summary>
        /// The most parts the footprint can be split into at once: the maximum independent set of its 4-neighbour
        /// graph, exact up to MaxEnumeratedCells cells, else the cell count minus one (a connected footprint of two
        /// or more cells always keeps two neighbours in one part).
        /// </summary>
        public static int GetMaxFragments(IReadOnlyList<Vector2Int> cells)
        {
            int count = ValidateCount(cells);
            if (count > MaxEnumeratedCells)
            {
                return count - 1;
            }

            int[] neighbours = CreateNeighbours(cells);
            int full = (1 << count) - 1;
            int best = 1;
            for (int mask = 1; mask <= full; mask++)
            {
                if (IsIndependent(mask, neighbours))
                {
                    best = Math.Max(best, CountBits(mask));
                }
            }

            return best;
        }

        /// <summary>
        /// Raises counts[N - 1] by the most Arrow_N markers the parts of this Arrow block can show at once: for every
        /// nonempty subset of the footprint, its parts' ArrowMarkerRun variants along the axis are counted, and the
        /// largest count per variant is added. A part is never longer along the axis than its block, so variants
        /// above the block's own get nothing. Above MaxEnumeratedCells cells every variant up to the block's own gets
        /// the cell count minus one, the GetMaxFragments bound.
        /// </summary>
        public static void AddArrowMarkerCapacity(IReadOnlyList<Vector2Int> cells, LevelEffectAxis axis, int[] counts)
        {
            int count = ValidateCount(cells);
            if (counts == null || counts.Length != ArrowMarkerRun.MaxVariant)
            {
                throw new ArgumentException("Pass one marker count per Arrow variant.", nameof(counts));
            }

            int own = ArrowMarkerRun.Create(cells, axis).Variant;
            if (own == 0)
            {
                throw new ArgumentException("Pass a Horizontal or Vertical axis.", nameof(axis));
            }

            if (count > MaxEnumeratedCells)
            {
                for (int variant = 1; variant <= own; variant++)
                {
                    counts[variant - 1] += count - 1;
                }

                return;
            }

            int[] neighbours = CreateNeighbours(cells);
            int[] best = new int[ArrowMarkerRun.MaxVariant];
            int[] shown = new int[ArrowMarkerRun.MaxVariant];
            int full = (1 << count) - 1;
            for (int mask = 1; mask <= full; mask++)
            {
                Array.Clear(shown, 0, shown.Length);
                int left = mask;
                // Owner: this call. Stops: each pass removes one whole part from left, so it ends when none is left.
                while (left != 0)
                {
                    int part = GetComponent(left & -left, left, neighbours);
                    left &= ~part;
                    shown[GetVariant(cells, part, axis) - 1]++;
                }

                for (int variant = 0; variant < best.Length; variant++)
                {
                    best[variant] = Math.Max(best[variant], shown[variant]);
                }
            }

            for (int variant = 0; variant < best.Length; variant++)
            {
                counts[variant] += best[variant];
            }
        }

        private static int ValidateCount(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                throw new ArgumentException("Pass a nonempty footprint.", nameof(cells));
            }

            return cells.Count;
        }

        private static int[] CreateNeighbours(IReadOnlyList<Vector2Int> cells)
        {
            int count = cells.Count;
            int[] neighbours = new int[count];
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < count; j++)
                {
                    Vector2Int offset = cells[i] - cells[j];
                    if (Math.Abs(offset.x) + Math.Abs(offset.y) == 1)
                    {
                        neighbours[i] |= 1 << j;
                    }
                }
            }

            return neighbours;
        }

        private static bool IsIndependent(int mask, int[] neighbours)
        {
            for (int i = 0, bits = mask; bits != 0; i++, bits >>= 1)
            {
                if ((bits & 1) != 0 && (neighbours[i] & mask) != 0)
                {
                    return false;
                }
            }

            return true;
        }

        // The cells of mask 4-connected to seed, found by repeated neighbour expansion.
        private static int GetComponent(int seed, int mask, int[] neighbours)
        {
            int reached = seed;
            // Owner: this call. Stops: when an expansion reaches no new cell, after at most one pass per cell.
            while (true)
            {
                int next = reached;
                for (int i = 0, bits = reached; bits != 0; i++, bits >>= 1)
                {
                    if ((bits & 1) != 0)
                    {
                        next |= neighbours[i];
                    }
                }

                next &= mask;
                if (next == reached)
                {
                    return reached;
                }

                reached = next;
            }
        }

        // ArrowMarkerRun's variant for the part: its bounding-box extent along the axis, clamped to 1-3.
        private static int GetVariant(IReadOnlyList<Vector2Int> cells, int part, LevelEffectAxis axis)
        {
            int min = int.MaxValue;
            int max = int.MinValue;
            for (int i = 0, bits = part; bits != 0; i++, bits >>= 1)
            {
                if ((bits & 1) == 0)
                {
                    continue;
                }

                int along = axis == LevelEffectAxis.Horizontal ? cells[i].x : cells[i].y;
                min = Math.Min(min, along);
                max = Math.Max(max, along);
            }

            return Math.Min(max - min + 1, ArrowMarkerRun.MaxVariant);
        }

        private static int CountBits(int mask)
        {
            int count = 0;
            for (int bits = mask; bits != 0; bits &= bits - 1)
            {
                count++;
            }

            return count;
        }
    }
}
