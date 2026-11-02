using System;
using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam.Runtime.Board
{
    /// <summary>Plain C# tile budget math (DEC-184): how many tiles of each kind a board's shared tile pool warms.</summary>
    public static class BlockPieceCapacity
    {
        /// <summary>Number of piece kinds; a tile budget has one count per BlockPieceKind.</summary>
        public const int KindCount = 4;

        /// <summary>Largest footprint whose sub-footprints are enumerated (at most 4095 subsets).</summary>
        public const int MaxEnumeratedCells = 12;

        /// <summary>
        /// Adds one block to a board's shared tile budget (DEC-184): shown[kind] grows by the tiles its footprint
        /// shows, and spare[kind] rises to the most extra tiles of that kind the block can need after Rocket removals
        /// (GetSplitPieces). A board warms shown plus spare, so any one block can be split without instantiating; a
        /// larger need grows the pool instead of being refused. A footprint above MaxEnumeratedCells adds no spare.
        /// Preparation only: it allocates.
        /// </summary>
        public static void AddTileBudget(IReadOnlyList<Vector2Int> footprint, int[] shown, int[] spare)
        {
            if (footprint == null || footprint.Count == 0) throw new ArgumentException("Pass a non-empty footprint.", nameof(footprint));
            if (shown == null || shown.Length != KindCount) throw new ArgumentException("Pass one count per piece kind.", nameof(shown));
            if (spare == null || spare.Length != KindCount) throw new ArgumentException("Pass one count per piece kind.", nameof(spare));
            var composer = new BlockPieceComposer(footprint.Count);
            var placements = new List<BlockPiecePlacement>(footprint.Count * BlockPieceComposer.MaxPlacementsPerCell);
            var own = new int[KindCount];
            // Rejects duplicate or disconnected footprints before any enumeration.
            AddPieces(composer, footprint, placements, own);
            for (int kind = 0; kind < KindCount; kind++) shown[kind] += own[kind];
            if (footprint.Count > MaxEnumeratedCells) return;
            int[] split = GetSplitPieces(footprint);
            for (int kind = 0; kind < KindCount; kind++) spare[kind] = Math.Max(spare[kind], split[kind] - own[kind]);
        }

        /// <summary>
        /// The most pieces of each kind that any nonempty set of the footprint's cells shows, each 4-connected part
        /// composed on its own. Rocket removes one cell at a time and may split a block into parts (DEC-146), so
        /// every set Rocket can leave is one of these. Footprints up to MaxEnumeratedCells; preparation only.
        /// </summary>
        public static int[] GetSplitPieces(IReadOnlyList<Vector2Int> footprint)
        {
            if (footprint == null || footprint.Count == 0) throw new ArgumentException("Pass a non-empty footprint.", nameof(footprint));
            int count = footprint.Count;
            if (count > MaxEnumeratedCells) throw new ArgumentOutOfRangeException(nameof(footprint), "Too many cells to enumerate.");
            var neighbours = new int[count];
            for (int i = 0; i < count; i++)
                for (int j = 0; j < count; j++)
                {
                    Vector2Int offset = footprint[i] - footprint[j];
                    if (Math.Abs(offset.x) + Math.Abs(offset.y) == 1) neighbours[i] |= 1 << j;
                }
            var composer = new BlockPieceComposer(count);
            var placements = new List<BlockPiecePlacement>(count * BlockPieceComposer.MaxPlacementsPerCell);
            var part = new List<Vector2Int>(count);
            var counts = new int[KindCount];
            var most = new int[KindCount];
            int full = (1 << count) - 1;
            for (int mask = 1; mask <= full; mask++)
            {
                Array.Clear(counts, 0, counts.Length);
                for (int left = mask; left != 0;)
                {
                    int component = GetComponent(left, neighbours);
                    left &= ~component;
                    part.Clear();
                    for (int i = 0; i < count; i++)
                        if ((component & (1 << i)) != 0) part.Add(footprint[i]);
                    AddPieces(composer, part, placements, counts);
                }
                for (int kind = 0; kind < KindCount; kind++) most[kind] = Math.Max(most[kind], counts[kind]);
            }
            return most;
        }

        private static void AddPieces(BlockPieceComposer composer, IReadOnlyList<Vector2Int> cells,
            List<BlockPiecePlacement> placements, int[] counts)
        {
            composer.Compose(cells, placements);
            for (int i = 0; i < placements.Count; i++) counts[(int)placements[i].Kind]++;
        }

        // The 4-connected part of mask that holds its lowest cell.
        private static int GetComponent(int mask, int[] neighbours)
        {
            int reached = mask & -mask;
            while (true)
            {
                int next = reached;
                for (int i = 0, bits = reached; bits != 0; i++, bits >>= 1)
                    if ((bits & 1) != 0) next |= neighbours[i];
                next &= mask;
                if (next == reached) return reached;
                reached = next;
            }
        }
    }
}
