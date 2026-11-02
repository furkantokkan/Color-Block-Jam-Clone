using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the shared board tile pool (DEC-184): a root holds only the tiles it shows, the Home budget is every
    /// block's own tiles plus one Rocket remainder's spare (checked against an independent reference that walks every
    /// Rocket removal sequence), and a need beyond it grows the pool instead of being refused.
    /// </summary>
    public sealed class ModularBlockViewCapacityTests
    {
        // The library Square's cells, in the library's order.
        private static readonly Vector2Int[] s_square =
            { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1) };

        private BlockShapeLibrary m_library;
        private BoardArt m_art;

        [SetUp]
        public void LoadLibrary()
        {
            m_library = BlockShapeLibrary.LoadProject();
            Assert.That(m_library, Is.Not.Null);
            m_art = new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
        }

        [Test]
        public void SplitPiecesOfEveryLibraryShapeAndLevelFootprintMatchTheRocketReference()
        {
            List<IReadOnlyList<Vector2Int>> footprints = GetLibraryAndLevelFootprints();
            Assert.That(footprints.Count, Is.GreaterThan(m_library.Shapes.Count), "Level footprints are included.");
            foreach (IReadOnlyList<Vector2Int> footprint in footprints)
                Assert.That(BlockPieceCapacity.GetSplitPieces(footprint), Is.EqualTo(GetReferenceCapacity(footprint)), Describe(footprint));
        }

        [Test]
        public void SplittingALineNeedsMoreCornersThanTheLineOrAnyConnectedPartOfIt()
        {
            // Rocket takes the middle of a line of three and leaves two one-cell parts: eight outer corners, not four.
            var line = new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
            Assert.That(BlockPieceCapacity.GetSplitPieces(line)[(int)BlockPieceKind.OuterCorner], Is.EqualTo(8));
        }

        [Test]
        public void TileBudgetAddsEveryBlocksOwnTilesAndTheLargestRocketRemainderSpare()
        {
            List<IReadOnlyList<Vector2Int>> footprints = GetLibraryAndLevelFootprints();
            var shown = new int[BlockPieceCapacity.KindCount];
            var spare = new int[BlockPieceCapacity.KindCount];
            var expectedShown = new int[BlockPieceCapacity.KindCount];
            var expectedSpare = new int[BlockPieceCapacity.KindCount];
            foreach (IReadOnlyList<Vector2Int> footprint in footprints)
            {
                BlockPieceCapacity.AddTileBudget(footprint, shown, spare);
                int[] own = GetComposedCounts(footprint);
                int[] reachable = GetReferenceCapacity(footprint);
                for (int kind = 0; kind < own.Length; kind++)
                {
                    expectedShown[kind] += own[kind];
                    expectedSpare[kind] = Math.Max(expectedSpare[kind], reachable[kind] - own[kind]);
                }
            }
            Assert.That(shown, Is.EqualTo(expectedShown));
            Assert.That(spare, Is.EqualTo(expectedSpare));
        }

        [Test]
        public void FootprintAboveTheEnumerationLimitAddsItsTilesButNoSpare()
        {
            var line = new Vector2Int[BlockPieceCapacity.MaxEnumeratedCells + 1];
            for (int i = 0; i < line.Length; i++) line[i] = new Vector2Int(i, 0);
            var shown = new int[BlockPieceCapacity.KindCount];
            var spare = new int[BlockPieceCapacity.KindCount];
            BlockPieceCapacity.AddTileBudget(line, shown, spare);
            Assert.That(shown, Is.EqualTo(GetComposedCounts(line)));
            Assert.That(spare, Is.EqualTo(new int[BlockPieceCapacity.KindCount]));
        }

        [Test]
        public void EveryRootAndEveryRocketRemainderHoldsOnlyTheTilesItShows()
        {
            int checkedShapes = 0;
            var tilesParent = new GameObject("Shared tiles");
            var tiles = new BoardTilePool(m_art.Catalog, tilesParent.transform);
            try
            {
                foreach (BlockShapeLibrary.Shape shape in m_library.Shapes)
                {
                    GameObject root = Object.Instantiate(m_art.Catalog.BlockRoot.gameObject);
                    try
                    {
                        BlockPiece piece = root.GetComponent<BlockPiece>();
                        piece.Prepare(tiles, shape.Cells.Count);
                        piece.SetCells(shape.Cells);
                        Assert.That(root.transform.childCount, Is.EqualTo(Sum(GetComposedCounts(shape.Cells))), shape.DisplayName);
                        foreach (List<Vector2Int> remainder in GetRocketRemainders(shape.Cells))
                        {
                            Assert.That(piece.TrySetCells(remainder), Is.True);
                            Assert.That(root.transform.childCount, Is.EqualTo(Sum(GetComposedCounts(remainder))),
                                shape.DisplayName + " for " + Describe(remainder));
                        }
                        piece.Release();
                        Assert.That(root.transform.childCount, Is.Zero, shape.DisplayName + ": released tiles go back to the shared pool.");
                        checkedShapes++;
                    }
                    finally { Object.DestroyImmediate(root); }
                }
            }
            finally
            {
                tiles.Dispose();
                Object.DestroyImmediate(tilesParent);
            }
            Assert.That(checkedShapes, Is.GreaterThan(0));
        }

        [Test]
        public void PooledRootsHoldNoTilesAndOneSharedPoolServesEveryRoot()
        {
            var parent = new GameObject("Shared pool factory test");
            var factory = new BlockPieceFactory(m_art, parent.transform);
            try
            {
                factory.Prepare(3, s_square.Length);
                foreach (BlockPiece root in parent.GetComponentsInChildren<BlockPiece>(true))
                    Assert.That(root.transform.childCount, Is.Zero, "A waiting root holds no spare tiles.");
                BlockPiece first = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_square));
                BlockPiece second = factory.Create(new BlockPieceSpawnData(PaletteColor.Pink, s_square));
                int shown = Sum(GetComposedCounts(s_square));
                Assert.That(first.transform.childCount, Is.EqualTo(shown));
                Assert.That(second.transform.childCount, Is.EqualTo(shown));
                factory.Return(first);
                factory.Return(second);
                Assert.That(factory.Tiles.GetWaitingCount(BlockPieceKind.Center), Is.EqualTo(factory.Tiles.GetCreatedCount(BlockPieceKind.Center)));
            }
            finally
            {
                factory.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RocketRemainderBeyondTheWarmedBudgetGrowsTheSharedPoolAndKeepsTheNewTiles()
        {
            var parent = new GameObject("Growth factory test");
            var factory = new BlockPieceFactory(m_art, parent.transform);
            try
            {
                // Exactly the Square's own tiles: its L-shaped remainders show one more outer corner and an inner corner.
                int[] squareOnly = GetComposedCounts(s_square);
                factory.Prepare(1, s_square.Length);
                factory.WarmTiles(squareOnly);
                BlockPiece piece = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_square));
                var remainder = new List<Vector2Int>(s_square);
                remainder.RemoveAt(0);
                int[] needs = GetComposedCounts(remainder);
                Assert.That(needs[(int)BlockPieceKind.OuterCorner], Is.GreaterThan(squareOnly[(int)BlockPieceKind.OuterCorner]), "Precondition.");

                Assert.That(piece.TrySetCells(remainder), Is.True);

                Assert.That(piece.Cells, Is.EquivalentTo(remainder));
                Assert.That(piece.transform.childCount, Is.EqualTo(Sum(needs)));
                int grownOuter = factory.Tiles.GetCreatedCount(BlockPieceKind.OuterCorner);
                Assert.That(grownOuter, Is.EqualTo(needs[(int)BlockPieceKind.OuterCorner]));
                factory.Return(piece);
                Assert.That(factory.Tiles.GetWaitingCount(BlockPieceKind.OuterCorner), Is.EqualTo(grownOuter),
                    "A tile that grew the pool stays in it.");
            }
            finally
            {
                factory.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void DisconnectedFootprintIsRefusedWithoutChangingTheBlock()
        {
            var parent = new GameObject("Refusal factory test");
            var factory = new BlockPieceFactory(m_art, parent.transform);
            try
            {
                factory.Prepare(1, s_square.Length);
                BlockPiece piece = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_square));
                int shown = piece.View.ActivePieceCount;

                Assert.That(piece.TrySetCells(new[] { Vector2Int.zero, new Vector2Int(3, 0) }), Is.False);

                Assert.That(piece.Cells, Is.EquivalentTo(s_square));
                Assert.That(piece.View.ActivePieceCount, Is.EqualTo(shown));
                factory.Return(piece);
            }
            finally
            {
                factory.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void RocketSplitRecomposeWithinTheWarmedBudgetAllocatesNothing()
        {
            var parent = new GameObject("Allocation factory test");
            var factory = new BlockPieceFactory(m_art, parent.transform);
            try
            {
                var shown = new int[BlockPieceCapacity.KindCount];
                var spare = new int[BlockPieceCapacity.KindCount];
                BlockPieceCapacity.AddTileBudget(s_square, shown, spare);
                for (int kind = 0; kind < shown.Length; kind++) shown[kind] += spare[kind];
                factory.Prepare(1, s_square.Length);
                factory.WarmTiles(shown);
                BlockPiece piece = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_square));
                var remainder = new List<Vector2Int>(s_square);
                remainder.RemoveAt(0);
                // Warm once, so one-time JIT work is excluded; the Recorder-based constraint measures the rest.
                RecomposeAndRestore(piece, remainder, s_square);
                Assert.That(() => RecomposeAndRestore(piece, remainder, s_square), Is.Not.AllocatingGCMemory());
                for (int kind = 0; kind < shown.Length; kind++)
                    Assert.That(factory.Tiles.GetCreatedCount((BlockPieceKind)kind), Is.EqualTo(shown[kind]), "The budget covered the remainder.");
                factory.Return(piece);
            }
            finally
            {
                factory.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void FactoryPlacesAnyResolvedFootprintOfTheShapeSize()
        {
            // BoardPresenter's path: a root shows any footprint of up to its prepared cell capacity.
            var line = new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(3, 0) };
            var t4 = new[] { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0), new Vector2Int(1, 1) };
            var parent = new GameObject("Resolved footprint factory test");
            var factory = new BlockPieceFactory(m_art, parent.transform);
            try
            {
                factory.Prepare(2, s_square.Length);
                BlockPiece first = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, line));
                BlockPiece second = factory.Create(new BlockPieceSpawnData(PaletteColor.Pink, t4));
                Assert.That(first.Cells, Is.EquivalentTo(line));
                Assert.That(second.Cells, Is.EquivalentTo(t4));
                Assert.That(first.View.ActivePieceCount, Is.EqualTo(Sum(GetComposedCounts(line))));
                Assert.That(second.View.ActivePieceCount, Is.EqualTo(Sum(GetComposedCounts(t4))));
                factory.Return(first);
                factory.Return(second);
            }
            finally
            {
                factory.Dispose();
                Object.DestroyImmediate(parent);
            }
        }

        private static void RecomposeAndRestore(BlockPiece piece, IReadOnlyList<Vector2Int> remainder, IReadOnlyList<Vector2Int> whole)
        {
            for (int i = 0; i < 50; i++)
            {
                piece.TrySetCells(remainder);
                piece.TrySetCells(whole);
            }
        }

        // Every footprint Rocket can leave: remove one cell at a time, keeping a nonempty connected remainder.
        private static List<List<Vector2Int>> GetRocketRemainders(IReadOnlyList<Vector2Int> footprint)
        {
            var seen = new HashSet<string>();
            var result = new List<List<Vector2Int>>();
            var queue = new Queue<List<Vector2Int>>();
            var start = new List<Vector2Int>(footprint);
            seen.Add(Key(start));
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                List<Vector2Int> current = queue.Dequeue();
                result.Add(current);
                for (int i = 0; i < current.Count && current.Count > 1; i++)
                {
                    var next = new List<Vector2Int>(current);
                    next.RemoveAt(i);
                    if (IsConnected(next) && seen.Add(Key(next))) queue.Enqueue(next);
                }
            }
            return result;
        }

        // The most pieces per kind over every cell set Rocket can leave, walking removals that may split the block
        // (DEC-146) but never take a part's last cell; each part is composed on its own.
        private static int[] GetReferenceCapacity(IReadOnlyList<Vector2Int> footprint)
        {
            var capacity = new int[BlockPieceCapacity.KindCount];
            if (footprint.Count > BlockPieceCapacity.MaxEnumeratedCells) return GetComposedCounts(footprint);
            var seen = new HashSet<string>();
            var queue = new Queue<List<Vector2Int>>();
            var start = new List<Vector2Int>(footprint);
            seen.Add(Key(start));
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                List<Vector2Int> current = queue.Dequeue();
                var counts = new int[BlockPieceCapacity.KindCount];
                foreach (List<Vector2Int> part in GetParts(current))
                {
                    int[] partCounts = GetComposedCounts(part);
                    for (int kind = 0; kind < counts.Length; kind++) counts[kind] += partCounts[kind];
                }
                for (int kind = 0; kind < capacity.Length; kind++) capacity[kind] = Math.Max(capacity[kind], counts[kind]);
                foreach (List<Vector2Int> part in GetParts(current))
                {
                    if (part.Count < 2) continue;
                    foreach (Vector2Int cell in part)
                    {
                        var next = new List<Vector2Int>(current);
                        next.Remove(cell);
                        if (seen.Add(Key(next))) queue.Enqueue(next);
                    }
                }
            }
            return capacity;
        }

        private static List<List<Vector2Int>> GetParts(List<Vector2Int> cells)
        {
            var parts = new List<List<Vector2Int>>();
            var left = new HashSet<Vector2Int>(cells);
            while (left.Count > 0)
            {
                var enumerator = left.GetEnumerator();
                enumerator.MoveNext();
                var part = new List<Vector2Int>();
                var queue = new Queue<Vector2Int>();
                queue.Enqueue(enumerator.Current);
                left.Remove(enumerator.Current);
                while (queue.Count > 0)
                {
                    Vector2Int cell = queue.Dequeue();
                    part.Add(cell);
                    foreach (Vector2Int step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                        if (left.Remove(cell + step)) queue.Enqueue(cell + step);
                }
                parts.Add(part);
            }
            return parts;
        }

        private static int[] GetComposedCounts(IReadOnlyList<Vector2Int> cells)
        {
            var placements = new List<BlockPiecePlacement>(cells.Count * 9);
            new BlockPieceComposer(cells.Count).Compose(cells, placements);
            var counts = new int[BlockPieceCapacity.KindCount];
            foreach (BlockPiecePlacement placement in placements) counts[(int)placement.Kind]++;
            return counts;
        }

        private static bool IsConnected(List<Vector2Int> cells)
        {
            var reached = new HashSet<Vector2Int> { cells[0] };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(cells[0]);
            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                foreach (Vector2Int other in cells)
                {
                    Vector2Int offset = other - cell;
                    if (Math.Abs(offset.x) + Math.Abs(offset.y) == 1 && reached.Add(other)) queue.Enqueue(other);
                }
            }
            return reached.Count == cells.Count;
        }

        private List<IReadOnlyList<Vector2Int>> GetLibraryAndLevelFootprints()
        {
            var footprints = new List<IReadOnlyList<Vector2Int>>();
            foreach (BlockShapeLibrary.Shape shape in m_library.Shapes) footprints.Add(shape.Cells);
            LevelData[] levels = ShippedLevels.Read();
            foreach (LevelData level in levels)
                for (int block = 0; block < level.BlockCount; block++)
                {
                    LevelBlockData data = level.GetBlockAt(block);
                    var cells = new Vector2Int[data.CellCount];
                    for (int cell = 0; cell < cells.Length; cell++) cells[cell] = data.GetCellAt(cell);
                    footprints.Add(cells);
                }
            return footprints;
        }

        private static string Key(List<Vector2Int> cells)
        {
            var sorted = new List<Vector2Int>(cells);
            sorted.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var key = new StringBuilder();
            foreach (Vector2Int cell in sorted) key.Append(cell.x).Append(',').Append(cell.y).Append(';');
            return key.ToString();
        }

        private static string Describe(IReadOnlyList<Vector2Int> cells)
        {
            var text = new StringBuilder();
            foreach (Vector2Int cell in cells) text.Append('(').Append(cell.x).Append(',').Append(cell.y).Append(')');
            return text.ToString();
        }

        private static int Sum(int[] values)
        {
            int total = 0;
            foreach (int value in values) total += value;
            return total;
        }
    }
}
