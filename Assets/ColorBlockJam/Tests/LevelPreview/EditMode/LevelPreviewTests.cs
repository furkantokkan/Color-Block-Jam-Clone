using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.LevelPreview;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using Onity.Pooling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.LevelPreview
{
    /// <summary>
    /// Verifies the level-to-block mapping (AC-7) and the preview host lifecycle in the
    /// test runner's temporary scene, which is never saved. Every block maps from its own cells onto the catalog's
    /// one block root (DEC-193), so the library-shape, Custom_ id, learned-shape and shape-prefab cases were
    /// removed.
    /// </summary>
    public sealed class LevelPreviewTests
    {
        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };

        private BoardArt m_art;

        [SetUp]
        public void SetUp()
        {
            m_art = LevelPreviewTestSupport.CreateBoardArt();
        }

        [Test]
        public void LevelOneBlocksMapToShapeColourCellsAndFloorCellPositions()
        {
            LevelData level = CreateLevelOne();
            BoardLayout layout = CreateLayout(level);
            List<LevelPreviewBlock> blocks = new List<LevelPreviewBlock>();

            Assert.That(LevelPreviewBlockMapper.TryMap(level, layout, m_art.Catalog, blocks, out string error), Is.True, error);

            Assert.That(blocks.Count, Is.EqualTo(2));
            AssertBlock(blocks[0], "b1", PaletteColor.LightBlue, new Vector3(2f, 0f, 2f));
            AssertBlock(blocks[1], "b2", PaletteColor.Pink, new Vector3(0f, 0f, 1f));
            Assert.That(blocks[0].LocalPosition, Is.EqualTo(layout.CellToLocal(new Vector2Int(2, 2))));
        }

        [TestCase("Magenta", "unknown colour 'Magenta'")]
        [TestCase("Wall", "unknown colour 'Wall'")]
        [TestCase("7", "unknown colour '7'")]
        public void UnknownColourIsRejectedWithTheBlockId(string colorId, string reason)
        {
            LevelData level = CreateLevel("level-x", 4, 5,
                Block("b1", "Pink", new Vector2Int(0, 1)),
                new LevelBlockData("b7", colorId, new Vector2Int(2, 2), s_square));
            List<LevelPreviewBlock> blocks = new List<LevelPreviewBlock>();

            Assert.That(LevelPreviewBlockMapper.TryMap(level, CreateLayout(level), m_art.Catalog, blocks, out string error), Is.False);

            Assert.That(error, Does.Contain("Block b7"));
            Assert.That(error, Does.Contain(reason));
            Assert.That(blocks, Is.Empty, "A rejected level maps no block.");
        }

        [Test]
        public void HostBuildsRebuildsWithBoundedPoolsKeepsInvalidDraftsAndLeavesNothingOnDispose()
        {
            // The test runner's own untitled scene; it is discarded after the run, never saved.
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            int poolsBefore = CountPreviewPools();
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, m_art);

                Assert.That(host.TryShow(CreateLevelOne(), out string error), Is.True, error);
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1), "One host root lives in the target scene.");
                Assert.That(host.Root.scene, Is.EqualTo(scene));
                Assert.That(host.LevelId, Is.EqualTo("level-01"));
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                Assert.That(host.ActiveBlocks[0].Cells, Is.EquivalentTo(s_square));
                AssertBlockSitsOnFloorCell(host, host.ActiveBlocks[0], new Vector3(2f, 0f, 2f));
                AssertBlockSitsOnFloorCell(host, host.ActiveBlocks[1], new Vector3(0f, 0f, 1f));
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);

                Assert.That(host.TryShow(CreateOtherLevel(), out error), Is.True, error);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(3));
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);
                int pooledObjects = LevelPreviewTestSupport.CountObjects(host.Root);

                Assert.That(host.TryShow(CreateLevelOne(), out error), Is.True, error);
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(pooledObjects), "Reselecting reuses the pools.");
                Assert.That(host.TryShow(CreateOtherLevel(), out error), Is.True, error);
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(pooledObjects), "Reselecting reuses the pools.");

                LevelData invalid = new LevelData("level-bad", "Bad", 0, 5, 60,
                    Array.Empty<LevelDoorData>(), new[] { Block("b1", "Pink", Vector2Int.zero) });
                Assert.That(host.TryShow(invalid, out error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(host.LevelId, Is.EqualTo("level-other"), "An invalid draft keeps the last valid preview.");
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(3));

                host.Dispose();
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "Dispose leaves no preview object in the scene.");
                Assert.That(CountPreviewPools(), Is.EqualTo(poolsBefore), "Dispose releases every preview pool.");
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void PreviewBlocksHoldOnlyThePiecesTheirFootprintShows()
        {
            Scene scene = SceneManager.GetActiveScene();
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, m_art);

                Assert.That(host.TryShow(CreateLevelOne(), out string error), Is.True, error);
                foreach (BlockPiece block in host.ActiveBlocks)
                {
                    // DEC-120: no hidden authored copy; DEC-184: no spare tiles under a block, they wait in the shared pool.
                    Assert.That(block.transform.Find("GeneratedShape"), Is.Null);
                    Assert.That(block.GetComponentsInChildren<BoardPieceView>(true).Length,
                        Is.EqualTo(CountComposedPieces(block.Cells)));
                    Assert.That(block.View.ActivePieceCount, Is.EqualTo(CountComposedPieces(block.Cells)));
                }
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void FootprintAboveTheEnumerationLimitShowsWithoutAWarning()
        {
            var cells = new List<Vector2Int>();
            // 15 cells inside the 5 x 5 piece limit (DEC-193), above the 12-cell enumeration limit.
            for (int y = 0; y < 3; y++)
                for (int x = 0; x < 5; x++)
                    cells.Add(new Vector2Int(x, y));
            Assert.That(cells.Count, Is.GreaterThan(BlockPieceCapacity.MaxEnumeratedCells), "Precondition.");
            LevelData level = new LevelData("level-large", "Large", 8, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 2, "Pink") },
                new[] { new LevelBlockData("b1", "Pink", Vector2Int.zero, cells.ToArray()) });
            Scene scene = SceneManager.GetActiveScene();
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, m_art);

                Assert.That(host.TryShow(level, out string error), Is.True, error);
                Assert.That(host.TryShow(CreateLevelOne(), out error), Is.True, error);
                Assert.That(host.TryShow(level, out error), Is.True, error);
                LogAssert.NoUnexpectedReceived();

                BlockPiece block = host.ActiveBlocks[0];
                Assert.That(block.Cells.Count, Is.EqualTo(cells.Count));
                Assert.That(block.View.ActivePieceCount, Is.EqualTo(CountComposedPieces(cells)));
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void FailedRootCreationReturnsFalseLeavesNothingAndCanBeRetried()
        {
            Scene scene = SceneManager.GetActiveScene();
            // A scene object without EnvironmentView stands in for a broken environment prefab.
            GameObject broken = new GameObject("Broken Environment");
            int rootsBefore = scene.rootCount;
            LevelPreviewHost host = null;
            try
            {
                host = new LevelPreviewHost(scene, broken,
                    AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(LevelPreviewTestSupport.DatabasePath),
                    m_art);

                for (int attempt = 0; attempt < 2; attempt++)
                {
                    Assert.That(host.TryShow(CreateLevelOne(), out string error), Is.False);
                    Assert.That(error, Does.Contain("EnvironmentView"), "A retry rebuilds the root instead of reusing a half-built one.");
                    Assert.That(host.Root, Is.Null);
                    Assert.That(host.LevelId, Is.Null);
                    Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "A failed build leaves no preview object.");
                }
            }
            finally
            {
                host?.Dispose();
                Object.DestroyImmediate(broken);
            }
        }

        [Test]
        public void FailedRebuildShowsTheLastValidLevelAgain()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            int poolsBefore = CountPreviewPools();
            Transform destroyed = new GameObject("Destroyed Blocks Root").transform;
            Object.DestroyImmediate(destroyed.gameObject);
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, m_art);
                Assert.That(host.TryShow(CreateLevelOne(), out string error), Is.True, error);

                // Fault injection: the other level needs a new shape pool, and a new
                // factory under a destroyed parent throws after the shown blocks were returned.
                LevelPreviewTestSupport.SetBlocksRoot(host, destroyed);

                Assert.That(host.TryShow(CreateOtherLevel(), out error), Is.False);
                Assert.That(error, Is.Not.Empty);
                Assert.That(host.LevelId, Is.EqualTo("level-01"), "The last valid level is shown again.");
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                AssertBlockSitsOnFloorCell(host, host.ActiveBlocks[0], new Vector3(2f, 0f, 2f));
                AssertBlockSitsOnFloorCell(host, host.ActiveBlocks[1], new Vector3(0f, 0f, 1f));
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1), "The failed build's root was replaced, not kept.");

                Assert.That(host.TryShow(CreateOtherLevel(), out error), Is.True, error);
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(3));

                host.Dispose();
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore));
                Assert.That(CountPreviewPools(), Is.EqualTo(poolsBefore), "The failed build leaked no pool.");
            }
            finally
            {
                host?.Dispose();
            }
        }

        // AC-4 of the barrier task: barriers show in their start state and each republished draft
        // (startsOpen or cells changed) rebuilds them, all in Edit Mode without PrimeTween.
        [Test]
        public void BarriersShowInTheirStartStateAndFollowDraftEdits()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, m_art);
                Vector2Int[] run = { new Vector2Int(1, 3), new Vector2Int(2, 3), new Vector2Int(3, 3) };
                Vector2Int[] single = { new Vector2Int(4, 1) };

                Assert.That(host.TryShow(CreateBarrierLevel(false, run, true, single), out string error), Is.True, error);
                Assert.That(ReadBarrierHeights(host, run, 12), Is.All.EqualTo(0f), "closed barrier stands on the floor");
                Assert.That(ReadBarrierHeights(host, single, 4), Is.All.EqualTo(-0.8f).Within(1e-4f), "open barrier is sunk");
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1), "Edit Mode adds no tween manager object.");
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);

                // The draft flips startsOpen on both barriers.
                Assert.That(host.TryShow(CreateBarrierLevel(true, run, false, single), out error), Is.True, error);
                Assert.That(ReadBarrierHeights(host, run, 12), Is.All.EqualTo(-0.8f).Within(1e-4f));
                Assert.That(ReadBarrierHeights(host, single, 4), Is.All.EqualTo(0f));

                // The draft moves the run one row down and shortens it.
                Vector2Int[] moved = { new Vector2Int(1, 2), new Vector2Int(2, 2) };
                Assert.That(host.TryShow(CreateBarrierLevel(false, moved, false, single), out error), Is.True, error);
                Assert.That(ReadBarrierHeights(host, run, 0), Is.Empty, "No piece stays on the old cells.");
                Assert.That(ReadBarrierHeights(host, moved, 8), Is.All.EqualTo(0f));
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore + 1));
            }
            finally
            {
                host?.Dispose();
            }

            Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "Dispose leaves nothing behind.");
        }

        // A 6 x 5 board with two barriers; the block sits in the top-right corner, off both.
        private static LevelData CreateBarrierLevel(bool runOpen, Vector2Int[] run, bool singleOpen, Vector2Int[] single)
        {
            return new LevelData("level-barriers", "Barriers", 6, 5, 60, Array.Empty<Vector2Int>(),
                new[] { new LevelDoorData("d1", BoardSide.Top, 4, 2, "Pink") },
                new[] { Block("b1", "Pink", new Vector2Int(4, 3)) },
                new[] { Barrier("run", runOpen, run), Barrier("single", singleOpen, single) });
        }

        private static LevelCellObjectData Barrier(string id, bool startsOpen, Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, new LevelEffectData("barrier",
                new[] { new LevelEffectParamData("startsOpen", startsOpen ? "true" : "false") }));
        }

        // Heights of the wall and corner pieces standing on the given floor cells: those are barrier
        // pieces, since fixed walls stand only on void cells and the ring.
        private static List<float> ReadBarrierHeights(LevelPreviewHost host, Vector2Int[] cells, int expectedCount)
        {
            List<float> heights = new List<float>();
            foreach (BoardPieceView piece in host.BlocksRoot.parent.GetComponentsInChildren<BoardPieceView>(false))
            {
                Vector3 position = piece.transform.localPosition;
                Vector2Int cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
                if (piece.transform.parent == host.BlocksRoot.parent && Array.IndexOf(cells, cell) >= 0
                    && (piece.name.StartsWith("Wall", StringComparison.Ordinal)
                        || piece.name.StartsWith("Corner_1", StringComparison.Ordinal)))
                {
                    heights.Add(position.y);
                }
            }

            Assert.That(heights.Count, Is.EqualTo(expectedCount), "barrier pieces on the cells");
            return heights;
        }

        private static void AssertBlock(LevelPreviewBlock block, string id, PaletteColor color, Vector3 localPosition)
        {
            Assert.That(block.BlockId, Is.EqualTo(id));
            Assert.That(block.Color, Is.EqualTo(color), id);
            Assert.That(block.Cells, Is.EqualTo(s_square), id);
            Assert.That(block.LocalPosition, Is.EqualTo(localPosition), id);
        }

        // The block root and the floor tile of its origin cell share a world position,
        // so the block's cells sit exactly on the floor cells the environment built.
        private static void AssertBlockSitsOnFloorCell(LevelPreviewHost host, BlockPiece block, Vector3 cellLocal)
        {
            Transform boardRoot = host.BlocksRoot.parent;
            foreach (BoardPieceView piece in boardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (piece.name.StartsWith("Grid", StringComparison.Ordinal)
                    && piece.transform.localPosition == cellLocal)
                {
                    Assert.That(Vector3.Distance(block.transform.position, piece.transform.position), Is.LessThan(1e-4f));
                    return;
                }
            }

            Assert.Fail($"No floor tile at {cellLocal}.");
        }

        private static int CountComposedPieces(IReadOnlyList<Vector2Int> cells)
        {
            var placements = new List<BlockPiecePlacement>(cells.Count * BlockPieceComposer.MaxPlacementsPerCell);
            new BlockPieceComposer(cells.Count).Compose(cells, placements);
            return placements.Count;
        }

        private static int CountPreviewPools()
        {
            List<OnityPoolDiagnosticsSnapshot> snapshots = new List<OnityPoolDiagnosticsSnapshot>();
            OnityPoolDiagnosticsRegistry.GetSnapshots(snapshots);
            int count = 0;
            foreach (OnityPoolDiagnosticsSnapshot snapshot in snapshots)
            {
                if (snapshot.PoolName.StartsWith("Environment ", StringComparison.Ordinal)
                    || snapshot.PoolName.StartsWith("Block roots ", StringComparison.Ordinal)
                    || snapshot.PoolName.StartsWith("Board ", StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        // Mirrors level-01 of the shipped database, Data/Json/Levels/Main (DEC-196).
        private static LevelData CreateLevelOne()
        {
            return new LevelData("level-01", "Level 1", 4, 5, 300,
                new[]
                {
                    new LevelDoorData("d1", BoardSide.Top, 1, 3, "LightBlue"),
                    new LevelDoorData("d2", BoardSide.Bottom, 0, 3, "Pink")
                },
                new[]
                {
                    Block("b1", "LightBlue", new Vector2Int(2, 2)),
                    Block("b2", "Pink", new Vector2Int(0, 1))
                });
        }

        private static LevelData CreateOtherLevel()
        {
            return new LevelData("level-other", "Other", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 2, "LightGreen") },
                new[]
                {
                    new LevelBlockData("b1", "LightGreen", Vector2Int.zero,
                        new[] { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) }),
                    Block("b2", "Orange", new Vector2Int(0, 2)),
                    Block("b3", "Pink", new Vector2Int(3, 2))
                });
        }

        private static LevelData CreateLevel(string id, int width, int height, params LevelBlockData[] blocks)
        {
            return new LevelData(id, id, width, height, 60, Array.Empty<LevelDoorData>(), blocks);
        }

        private static LevelBlockData Block(string id, string colorId, Vector2Int origin)
        {
            return new LevelBlockData(id, colorId, origin, s_square);
        }

        private static BoardLayout CreateLayout(LevelData level)
        {
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            return layout;
        }
    }
}
