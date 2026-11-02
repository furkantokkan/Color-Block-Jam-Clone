using System;
using System.Collections.Generic;
using ColorBlockJam.Editor.BoardKit;
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

namespace ColorBlockJam.Tests.LevelPreview
{
    /// <summary>
    /// Verifies the LevelEditor scene preview's Arrow markers
    /// (TASK-20260926-212320271-98b25efa-level-editor-arrow-markers AC-3, revision 1, catalog BLK-01) in the test
    /// runner's temporary scene, which is never saved: the Arrow_N variant from the board piece catalog, parent,
    /// bounding-box-centre position, axis turn and tint of each marker placed by the shared BlockArrowMarkers, their
    /// return to the pools on rebuild and dispose, an invalid draft keeping them, a catalog without the needed prefab
    /// rejecting the level before the scene changes, and a marker deleted in the Hierarchy being replaced on the next
    /// publish. Since TASK-20260926-230921278-a3ffdc40 the markers come from the host's ArrowPreviewEffect adapter
    /// (seam-only port).
    /// </summary>
    public sealed class PreviewArrowMarkerTests
    {
        private const string k_markerPoolPrefix = "Arrow markers ";
        private const string k_arrowPrefabPathPrefix = BoardKitAssets.Root + "/Prefabs/Board/Pieces/Arrow_";
        private static readonly int s_colorId = Shader.PropertyToID("_PaletteUV");

        private static readonly Vector2Int[] s_square =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1)
        };
        private static readonly Vector2Int[] s_triple = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_verticalDouble = { new Vector2Int(0, 0), new Vector2Int(0, 1) };
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };
        private static readonly Vector2Int[] s_verticalI4 =
        {
            new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(0, 3)
        };

        private BoardArt m_art;

        [SetUp]
        public void SetUp()
        {
            m_art = LevelPreviewTestSupport.CreateBoardArt();
        }

        [Test]
        public void MapperGivesEachArrowBlockItsMarkerInItsOwnCells()
        {
            LevelData level = CreateArrowLevel("Horizontal", "Vertical");
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            List<LevelPreviewBlock> blocks = new List<LevelPreviewBlock>();

            Assert.That(LevelPreviewBlockMapper.TryMap(level, layout, m_art.Catalog, blocks, out error), Is.True, error);

            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 0).Center, Is.EqualTo(new Vector2(1f, 0f)));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 0).Variant, Is.EqualTo(3));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 1).Center, Is.EqualTo(new Vector2(0f, 0.5f)));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 1).Variant, Is.EqualTo(2));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 2).Variant, Is.EqualTo(1));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 3).Center, Is.EqualTo(new Vector2(0f, 1.5f)));
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 3).Variant, Is.EqualTo(3),
                "A line of four reuses Arrow_3.");
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 4).HasMarker, Is.False,
                "A block without Arrow maps no marker.");
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 5).Center, Is.EqualTo(new Vector2(0.5f, 0.5f)),
                "A 2 x 2 centres between its columns.");
            Assert.That(ArrowPreviewEffect.GetMarker(level, blocks, 5).Variant, Is.EqualTo(2));
        }

        [Test]
        public void EachArrowBlockGetsItsVariantOnItsRootAtTheBoxCentreTurnedForItsAxisAndTinted()
        {
            Scene scene = SceneManager.GetActiveScene();
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);

                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out string error), Is.True, error);

                Assert.That(Markers(host).ActiveMarkers.Count, Is.EqualTo(5),
                    "One marker per Arrow block; the plain square has none.");
                AssertMarker(host, 0, 0, 3, new Vector3(1f, 0f, 0f), LevelEffectAxis.Horizontal);
                AssertMarker(host, 1, 1, 2, new Vector3(0f, 0f, 0.5f), LevelEffectAxis.Vertical);
                AssertMarker(host, 2, 2, 1, Vector3.zero, LevelEffectAxis.Vertical);
                AssertMarker(host, 3, 3, 3, new Vector3(0f, 0f, 1.5f), LevelEffectAxis.Vertical);
                Assert.That(CountMarkersUnder(host.ActiveBlocks[4].transform), Is.Zero);
                // The user's b2 case: a Vertical 2 x 2 is centred on the line between its columns, not on one of them.
                AssertMarker(host, 4, 5, 2, new Vector3(0.5f, 0f, 0.5f), LevelEffectAxis.Vertical);
                AssertCentredOnBlock(host, 4, 5);
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);

                // The Effect Editor flips both axes: the next publish turns the markers and picks the new extents.
                Assert.That(host.TryShow(CreateArrowLevel("Vertical", "Horizontal"), out error), Is.True, error);

                AssertMarker(host, 0, 0, 1, new Vector3(1f, 0f, 0f), LevelEffectAxis.Vertical);
                AssertMarker(host, 1, 1, 1, new Vector3(0f, 0f, 0.5f), LevelEffectAxis.Horizontal);
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void RebuildsReturnEveryMarkerAndReuseThePoolsAndDisposeLeavesNone()
        {
            Scene scene = SceneManager.GetActiveScene();
            int rootsBefore = scene.rootCount;
            int poolsBefore = CountMarkerPools();
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);
                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out string error), Is.True, error);
                Assert.That(CountMarkerPools(), Is.EqualTo(poolsBefore + 3), "One pool per variant the level uses.");
                Assert.That(Markers(host).PreparedMarkerCount, Is.EqualTo(5), "The pools warm exactly the level's markers.");

                // A level without Arrow blocks: every marker returns and none stays on a block root.
                Assert.That(host.TryShow(CreatePlainLevel(), out error), Is.True, error);
                Assert.That(Markers(host).ActiveMarkers, Is.Empty);
                foreach (BlockPiece block in host.ActiveBlocks)
                {
                    Assert.That(CountMarkersUnder(block.transform), Is.Zero, "No marker rides on a reused root.");
                }

                Assert.That(CountActiveMarkers(), Is.Zero, "Every marker is back in its pool.");

                // Showing both again reuses every pool: nothing new is instantiated. The first pass over both
                // levels warms each environment pool to the larger of their counts, because the two doors split
                // the wall runs differently between Wall and Brick_End (DEC-139), so count after it.
                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out error), Is.True, error);
                int objects = LevelPreviewTestSupport.CountObjects(host.Root);
                Assert.That(host.TryShow(CreatePlainLevel(), out error), Is.True, error);
                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out error), Is.True, error);
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(objects), "Reselecting reuses the pools.");
                Assert.That(Markers(host).PreparedMarkerCount, Is.EqualTo(5), "No rebuild warms another marker.");
                Assert.That(Markers(host).ActiveMarkers.Count, Is.EqualTo(5));
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);

                host.Dispose();
                Assert.That(scene.rootCount, Is.EqualTo(rootsBefore), "Dispose leaves no preview object in the scene.");
                Assert.That(CountMarkerPools(), Is.EqualTo(poolsBefore), "Dispose releases every marker pool.");
                Assert.That(CountMarkersInScene(scene), Is.Zero, "No marker is left in the scene.");
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void AnInvalidDraftKeepsTheLastValidPreviewWithItsMarkers()
        {
            Scene scene = SceneManager.GetActiveScene();
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);
                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out string error), Is.True, error);
                BoardPieceView first = Markers(host).ActiveMarkers[0];

                // An Arrow without a valid axis fails level validation, before the scene changes.
                Assert.That(host.TryShow(CreateArrowLevel("Diagonal", "Vertical"), out error), Is.False);

                Assert.That(error, Is.Not.Empty);
                Assert.That(host.LevelId, Is.EqualTo("level-arrows"));
                Assert.That(Markers(host).ActiveMarkers.Count, Is.EqualTo(5));
                Assert.That(Markers(host).ActiveMarkers[0], Is.SameAs(first));
                AssertMarker(host, 0, 0, 3, new Vector3(1f, 0f, 0f), LevelEffectAxis.Horizontal);
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void ALevelWhoseCatalogLacksAnArrowPrefabIsRejectedBeforeTheSceneChanges()
        {
            Scene scene = SceneManager.GetActiveScene();
            BoardPieceCatalog catalog = LevelPreviewTestSupport.CreateCatalogWithoutArrows(m_art.Catalog);
            LevelPreviewHost host = null;
            try
            {
                host = LevelPreviewTestSupport.CreateHost(scene, new BoardArt(m_art.Palette, catalog), CreateEffects);
                Assert.That(host.TryShow(CreatePlainLevel(), out string error), Is.True, error);
                BlockPiece first = host.ActiveBlocks[0];
                int objects = LevelPreviewTestSupport.CountObjects(host.Root);

                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out error), Is.False);

                Assert.That(error, Does.Contain("b1").And.Contain("Arrow_3"), "The error names the block and its prefab.");
                Assert.That(host.LevelId, Is.EqualTo("level-plain"), "The shown level stays.");
                Assert.That(host.ActiveBlocks.Count, Is.EqualTo(2));
                Assert.That(host.ActiveBlocks[0], Is.SameAs(first));
                Assert.That(Markers(host).ActiveMarkers, Is.Empty);
                Assert.That(Markers(host).PreparedMarkerCount, Is.Zero);
                Assert.That(LevelPreviewTestSupport.CountObjects(host.Root), Is.EqualTo(objects), "The scene did not change.");
            }
            finally
            {
                host?.Dispose();
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AMarkerDeletedFromTheHierarchyIsReplacedOnTheNextPublish()
        {
            Scene scene = SceneManager.GetActiveScene();
            LevelPreviewHost host = null;
            try
            {
                host = CreateHost(scene);
                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out string error), Is.True, error);
                BoardPieceView deleted = Markers(host).ActiveMarkers[1];

                // The user deletes b2's Arrow_2 marker in the Hierarchy; its block root stays.
                UnityEngine.Object.DestroyImmediate(deleted.gameObject);

                Assert.That(host.TryShow(CreateArrowLevel("Horizontal", "Vertical"), out error), Is.True, error);

                Assert.That(Markers(host).ActiveMarkers.Count, Is.EqualTo(5));
                Assert.That(Markers(host).ActiveMarkers[1] != null, Is.True, "b2 shows a live marker again.");
                AssertMarker(host, 1, 1, 2, new Vector3(0f, 0f, 0.5f), LevelEffectAxis.Vertical);
                AssertMarker(host, 4, 5, 2, new Vector3(0.5f, 0f, 0.5f), LevelEffectAxis.Vertical);
                Assert.That(Markers(host).PreparedMarkerCount, Is.EqualTo(5));
                Assert.That(CountMarkersUnder(host.BlocksRoot), Is.EqualTo(5), "No untracked marker was instantiated.");
                LevelPreviewTestSupport.AssertEverythingIsDontSave(host.Root);
            }
            finally
            {
                host?.Dispose();
            }
        }

        [Test]
        public void TheCatalogGivesEachArrowPrefabFacingLocalZSoVerticalKeepsItsTurnAndHorizontalTurnsNinety()
        {
            // The rotation rule depends on the calibrated prefabs: centred, long along the root's local Z.
            for (int variant = 1; variant <= ArrowMarkerRun.MaxVariant; variant++)
            {
                Assert.That(m_art.Catalog.TryGetArrow(variant, out BoardPieceView prefab), Is.True, "Arrow_" + variant);
                Assert.That(AssetDatabase.GetAssetPath(prefab), Is.EqualTo(k_arrowPrefabPathPrefix + variant + ".prefab"));
                Bounds bounds = GetRootBounds(prefab);
                Assert.That(bounds.size.z, Is.GreaterThan(bounds.size.x), "Arrow_" + variant + " is long along local Z.");
                Assert.That(bounds.center.x, Is.EqualTo(0f).Within(1e-3f), "Arrow_" + variant + " is centred.");
                Assert.That(bounds.center.z, Is.EqualTo(0f).Within(1e-3f), "Arrow_" + variant + " is centred.");
            }

            Assert.That(m_art.Catalog.TryGetArrow(0, out _), Is.False);
            Assert.That(m_art.Catalog.TryGetArrow(ArrowMarkerRun.MaxVariant + 1, out _), Is.False);
        }

        // The marker of blocks[blockIndex]: its variant's mesh, on that block root, at the bounding-box centre in the
        // root's cells, turned so its long local Z follows the axis in board space, and tinted with the palette's Arrow
        // colour.
        private void AssertMarker(LevelPreviewHost host, int markerIndex, int blockIndex, int variant,
            Vector3 localPosition, LevelEffectAxis axis)
        {
            BoardPieceView marker = Markers(host).ActiveMarkers[markerIndex];
            BlockPiece block = host.ActiveBlocks[blockIndex];
            BoardPieceView prefab = AssetDatabase.LoadAssetAtPath<BoardPieceView>(k_arrowPrefabPathPrefix + variant + ".prefab");
            string name = $"Marker {markerIndex}";

            Assert.That(marker.gameObject.activeSelf, Is.True, name);
            Assert.That(marker.Filter.sharedMesh, Is.SameAs(prefab.Filter.sharedMesh), name + " uses Arrow_" + variant);
            Assert.That(marker.Renderer.sharedMaterial, Is.SameAs(prefab.Renderer.sharedMaterial), name + " shares the material");
            Assert.That(marker.transform.parent, Is.SameAs(block.transform), name + " rides on its block root");
            Assert.That(Vector3.Distance(marker.transform.localPosition, localPosition), Is.LessThan(1e-4f),
                name + " sits at the bounding-box centre");
            Assert.That(marker.transform.localScale, Is.EqualTo(Vector3.one), name + " is not stretched");

            Transform boardRoot = host.BlocksRoot.parent;
            Vector3 along = boardRoot.InverseTransformDirection(marker.transform.forward);
            Vector3 expected = axis == LevelEffectAxis.Horizontal ? Vector3.right : Vector3.forward;
            Assert.That(Mathf.Abs(Vector3.Dot(along, expected)), Is.EqualTo(1f).Within(1e-4f),
                name + " points along its axis on the board");

            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            marker.Renderer.GetPropertyBlock(properties);
            Vector4 tint = properties.GetVector(s_colorId);
            Assert.That(tint, Is.EqualTo(m_art.ArrowColor.UV), name + " takes the palette's Arrow colour");
        }

        // The marker's mesh and the block's shown tiles share one centre on the board plane.
        private static void AssertCentredOnBlock(LevelPreviewHost host, int markerIndex, int blockIndex)
        {
            BoardPieceView marker = Markers(host).ActiveMarkers[markerIndex];
            Bounds block = default;
            bool first = true;
            foreach (MeshRenderer tile in host.ActiveBlocks[blockIndex].GetComponentsInChildren<MeshRenderer>(false))
            {
                if (tile.GetComponentInParent<BoardPieceView>() == marker)
                {
                    continue;
                }

                if (first)
                {
                    block = tile.bounds;
                    first = false;
                }
                else
                {
                    block.Encapsulate(tile.bounds);
                }
            }

            Assert.That(first, Is.False, "The block shows its tiles.");
            Bounds arrow = marker.Renderer.bounds;
            Assert.That(arrow.center.x, Is.EqualTo(block.center.x).Within(1e-3f), "Centred across the block.");
            Assert.That(arrow.center.z, Is.EqualTo(block.center.z).Within(1e-3f), "Centred along the block.");
        }

        private LevelPreviewHost CreateHost(Scene scene)
        {
            return LevelPreviewTestSupport.CreateHost(scene, m_art, CreateEffects);
        }

        // The Arrow adapter LevelPreviewOwner registers; the Ice adapter has its own tests.
        private static ILevelPreviewEffect[] CreateEffects()
        {
            return new ILevelPreviewEffect[] { new ArrowPreviewEffect() };
        }

        private static ArrowPreviewEffect Markers(LevelPreviewHost host)
        {
            return host.GetEffect<ArrowPreviewEffect>();
        }

        // A 6 x 6 board: b1 a horizontal triple at (0, 0) and b2 a vertical double at (4, 0) with the given Arrow
        // axes, b3 a single at (5, 5) and b4 a vertical line of four at (0, 2), both Arrow Vertical, b5 a
        // square without effects at (2, 3) and b6 a square at (4, 2) with a Vertical Arrow, like the user's b2.
        private static LevelData CreateArrowLevel(string tripleAxis, string doubleAxis)
        {
            return new LevelData("level-arrows", "Arrows", 6, 6, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 2, 2, "Pink") },
                new[]
                {
                    new LevelBlockData("b1", "Pink", new Vector2Int(0, 0), s_triple,
                        new[] { Arrow(tripleAxis) }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(4, 0), s_verticalDouble,
                        new[] { Arrow(doubleAxis) }),
                    new LevelBlockData("b3", "Orange", new Vector2Int(5, 5), s_single, new[] { Arrow("Vertical") }),
                    new LevelBlockData("b4", "LightGreen", new Vector2Int(0, 2), s_verticalI4,
                        new[] { Arrow("Vertical") }),
                    new LevelBlockData("b5", "Purple", new Vector2Int(2, 3), s_square),
                    new LevelBlockData("b6", "Red", new Vector2Int(4, 2), s_square, new[] { Arrow("Vertical") })
                });
        }

        private static LevelData CreatePlainLevel()
        {
            return new LevelData("level-plain", "Plain", 6, 6, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 2, "Pink") },
                new[]
                {
                    new LevelBlockData("b1", "Pink", new Vector2Int(0, 0), s_triple),
                    new LevelBlockData("b2", "Purple", new Vector2Int(2, 3), s_square)
                });
        }

        private static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        // The prefab's mesh bounds in its root's space, from the visual child's local transform.
        private static Bounds GetRootBounds(BoardPieceView prefab)
        {
            Matrix4x4 toRoot = prefab.transform.worldToLocalMatrix * prefab.Filter.transform.localToWorldMatrix;
            Bounds mesh = prefab.Filter.sharedMesh.bounds;
            Bounds bounds = new Bounds(toRoot.MultiplyPoint3x4(mesh.min), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                bounds.Encapsulate(toRoot.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? mesh.min.x : mesh.max.x,
                    (corner & 2) == 0 ? mesh.min.y : mesh.max.y,
                    (corner & 4) == 0 ? mesh.min.z : mesh.max.z)));
            }

            return bounds;
        }

        private static bool IsMarker(BoardPieceView view)
        {
            Mesh mesh = view.Filter != null ? view.Filter.sharedMesh : null;
            return mesh != null && mesh.name.StartsWith("Arrow_", StringComparison.Ordinal);
        }

        private static int CountMarkersUnder(Transform root)
        {
            int count = 0;
            foreach (BoardPieceView view in root.GetComponentsInChildren<BoardPieceView>(true))
            {
                count += IsMarker(view) ? 1 : 0;
            }

            return count;
        }

        private static int CountMarkersInScene(Scene scene)
        {
            int count = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                count += CountMarkersUnder(root.transform);
            }

            return count;
        }

        private static int CountMarkerPools()
        {
            List<OnityPoolDiagnosticsSnapshot> snapshots = new List<OnityPoolDiagnosticsSnapshot>();
            OnityPoolDiagnosticsRegistry.GetSnapshots(snapshots);
            int count = 0;
            foreach (OnityPoolDiagnosticsSnapshot snapshot in snapshots)
            {
                count += snapshot.PoolName.StartsWith(k_markerPoolPrefix, StringComparison.Ordinal) ? 1 : 0;
            }

            return count;
        }

        private static int CountActiveMarkers()
        {
            List<OnityPoolDiagnosticsSnapshot> snapshots = new List<OnityPoolDiagnosticsSnapshot>();
            OnityPoolDiagnosticsRegistry.GetSnapshots(snapshots);
            int count = 0;
            foreach (OnityPoolDiagnosticsSnapshot snapshot in snapshots)
            {
                count += snapshot.PoolName.StartsWith(k_markerPoolPrefix, StringComparison.Ordinal) ? snapshot.CountActive : 0;
            }

            return count;
        }
    }
}
