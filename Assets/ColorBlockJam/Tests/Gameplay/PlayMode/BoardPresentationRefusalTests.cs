#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the refusal paths of the non-throwing presentation calls that the board presenter uses during Playing
    /// (B-10, B-19, AC-16): BlockPiece.TrySetCells keeps the shown footprint for one its root cannot hold and grows
    /// the shared tile pool for one it can (DEC-184), BlockPieceFactory.CanCreate turns false once its root pool is
    /// full, BlockArrowMarkers.TryAttach returns false with nothing attached when a pool is exhausted, and the Ice
    /// look follows its block's recomposed tiles and leaves returned tiles unfrozen. The size refusals allocate nothing.
    /// </summary>
    public sealed class BoardPresentationRefusalTests
    {
        private const string k_iceMaterialPath = "Assets/ColorBlockJam/Art/Materials/Effects/MAT_Ice.mat";

        private static readonly Vector2Int[] s_triple = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_double = { new Vector2Int(0, 0), new Vector2Int(1, 0) };
        private static readonly Vector2Int[] s_single = { new Vector2Int(0, 0) };
        private static readonly Vector2Int[] s_empty = new Vector2Int[0];
        private static readonly Vector2Int[] s_four =
        {
            new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0), new Vector2Int(3, 0)
        };
        private static readonly Vector2Int[] s_gap = { new Vector2Int(0, 0), new Vector2Int(2, 0) };

        // Three cells like the line, but with a concave corner: an InnerCorner that no part of a line shows.
        private static readonly Vector2Int[] s_corner = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(0, 1) };

        private BoardArt m_art;
        private GameObject m_parent;

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            // The first measurement in a domain also measures the recorder's own setup.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [SetUp]
        public void SetUp()
        {
            m_art = PresenterScene.CreateArt();
            m_parent = new GameObject("Refusal fixture");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_parent);
        }

        [Test]
        public void TrySetCellsRefusesAFootprintItsRootCannotHoldAndGrowsTheSharedPoolForOneItCan()
        {
            BlockPieceFactory factory = new BlockPieceFactory(m_art, m_parent.transform);
            try
            {
                factory.Prepare(1, s_triple.Length);
                BlockPiece piece = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_triple));
                Assert.That(factory.Tiles.GetCreatedCount(BlockPieceKind.InnerCorner), Is.Zero, "Precondition: no part of a line shows one.");
                int shown = piece.View.ActivePieceCount;

                AssertRefused(piece, null, shown, "no footprint");
                AssertRefused(piece, s_empty, shown, "an empty footprint");
                AssertRefused(piece, s_four, shown, "more cells than prepared");
                AssertRefused(piece, s_gap, shown, "a disconnected footprint");
                Assert.That(() =>
                {
                    piece.TrySetCells(s_empty);
                    piece.TrySetCells(s_four);
                }, Is.Not.AllocatingGCMemory(), "size refusals");

                // A footprint that needs a tile kind nobody warmed grows the shared pool instead of being refused.
                Assert.That(piece.TrySetCells(s_corner), Is.True);
                Assert.That(factory.Tiles.GetCreatedCount(BlockPieceKind.InnerCorner), Is.EqualTo(1));

                // A footprint the root can show is accepted, then the line again, without allocating either.
                Assert.That(piece.TrySetCells(s_double), Is.True);
                Assert.That(piece.Cells, Is.EquivalentTo(s_double));
                Assert.That(piece.TrySetCells(s_triple), Is.True);
                Assert.That(() =>
                {
                    piece.TrySetCells(s_double);
                    piece.TrySetCells(s_triple);
                }, Is.Not.AllocatingGCMemory(), "an accepted recompose");
                Assert.That(piece.Cells, Is.EquivalentTo(s_triple));
                factory.Return(piece);
            }
            finally
            {
                factory.Dispose();
            }
        }

        [Test]
        public void CanCreateTurnsFalseOnceTheRootPoolIsFull()
        {
            BlockPieceFactory factory = new BlockPieceFactory(m_art, m_parent.transform);
            try
            {
                factory.Prepare(2, 1);
                Assert.That(factory.CanCreate(), Is.True);
                BlockPiece first = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_single));
                Assert.That(factory.CanCreate(), Is.True, "one warmed root is still free");
                BlockPiece second = factory.Create(new BlockPieceSpawnData(PaletteColor.Blue, s_single));

                Assert.That(factory.CanCreate(), Is.False, "the pool is exhausted");
                Assert.That(() => { factory.CanCreate(); }, Is.Not.AllocatingGCMemory());
                Assert.Throws<InvalidOperationException>(
                    () => factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_single)),
                    "Create still rejects exhaustion instead of growing");
                Assert.That(factory.PreparedCount, Is.EqualTo(2));
                Assert.That(factory.ActiveCount, Is.EqualTo(2));

                factory.Return(first);
                Assert.That(factory.CanCreate(), Is.True, "a returned root is free again");
                factory.Return(second);
            }
            finally
            {
                factory.Dispose();
            }

            Assert.That(factory.CanCreate(), Is.False, "a disposed factory creates nothing");
        }

        [Test]
        public void ArrowTryAttachRefusesAnExhaustedOrUnwarmedVariantWithNoMarker()
        {
            BlockArrowMarkers markers = new BlockArrowMarkers(m_art.Catalog, m_parent.transform);
            Transform first = CreateRoot("First root");
            Transform second = CreateRoot("Second root");
            try
            {
                markers.Prepare(new[] { 1, 0, 0 });
                Assert.That(markers.TryAttach(first, s_single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out BoardPieceView attached), Is.True);
                Assert.That(attached, Is.Not.Null);
                Assert.That(attached.transform.parent, Is.SameAs(first));

                Assert.That(markers.TryAttach(second, s_single, LevelEffectAxis.Vertical, AtlasColor.FromCell(Vector2Int.zero), out BoardPieceView none), Is.False,
                    "the only Arrow_1 is attached");
                Assert.That(none, Is.Null);
                Assert.That(markers.TryAttach(second, s_double, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out none), Is.False,
                    "no Arrow_2 was warmed");
                Assert.That(none, Is.Null);
                Assert.That(markers.TryAttach(null, s_single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out none), Is.False);
                Assert.That(markers.TryAttach(second, s_empty, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out none), Is.False,
                    "no cells give no marker");
                Assert.That(markers.AttachedCount, Is.EqualTo(1));
                Assert.That(second.childCount, Is.Zero, "nothing was attached to the refused root");
                Assert.That(() => { markers.TryAttach(second, s_single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero), out _); },
                    Is.Not.AllocatingGCMemory());
                Assert.Throws<InvalidOperationException>(
                    () => markers.Attach(second, s_single, LevelEffectAxis.Horizontal, AtlasColor.FromCell(Vector2Int.zero)),
                    "the Editor preview's Attach still throws at exhaustion");

                markers.Return(attached);
                Assert.That(markers.TryAttach(second, s_single, LevelEffectAxis.Vertical, AtlasColor.FromCell(Vector2Int.zero), out attached), Is.True,
                    "a returned marker is free again");
                markers.Return(attached);
            }
            finally
            {
                markers.Dispose();
            }
        }

        [Test]
        public void IceFollowsItsBlocksRecomposedTilesWithoutAllocatingAndReturnedTilesComeBackUnfrozen()
        {
            Material ice = LoadIce();
            BlockPieceFactory factory = new BlockPieceFactory(m_art, m_parent.transform);
            BlockIceOverlays overlays = new BlockIceOverlays(ice, 2);
            try
            {
                factory.Prepare(1, s_triple.Length);
                BlockPiece piece = factory.Create(new BlockPieceSpawnData(PaletteColor.Red, s_triple));
                int childrenBefore = piece.transform.childCount;
                overlays.Attach(0, piece.View, 2, 2);
                Assert.That(piece.transform.childCount, Is.EqualTo(childrenBefore), "Ice spawns no object (DEC-184).");
                AssertIced(piece.View, ice, "attached");

                Assert.That(piece.TrySetCells(s_double), Is.True);
                AssertIced(piece.View, ice, "a Rocket remainder keeps the ice");
                Assert.That(piece.TrySetCells(s_triple), Is.True);
                Assert.That(() =>
                {
                    piece.TrySetCells(s_double);
                    piece.TrySetCells(s_triple);
                }, Is.Not.AllocatingGCMemory(), "an iced recompose");
                AssertIced(piece.View, ice, "after the recomposes");

                factory.Return(piece);
                foreach (BoardPieceView tile in factory.Tiles.Root.GetComponentsInChildren<BoardPieceView>(true))
                    Assert.That(tile.Renderer.sharedMaterials, Has.Length.EqualTo(1), "a returned tile is unfrozen");
                Assert.Throws<InvalidOperationException>(() => overlays.Attach(0, piece.View, 2, 2),
                    "a slot that shows ice refuses a second block");
                overlays.Attach(1, piece.View, 0, 2);
                Assert.That(overlays.GetAt(1), Is.Null, "a thawed count attaches nothing");
            }
            finally
            {
                overlays.Dispose();
                factory.Dispose();
            }
        }

        private static void AssertRefused(BlockPiece piece, IReadOnlyList<Vector2Int> cells, int shown, string message)
        {
            Assert.That(piece.TrySetCells(cells), Is.False, message);
            Assert.That(piece.Cells, Is.EquivalentTo(s_triple), message + ": the footprint is unchanged");
            Assert.That(piece.View.ActivePieceCount, Is.EqualTo(shown), message + ": the shown tiles are unchanged");
        }

        private Transform CreateRoot(string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(m_parent.transform, false);
            return root;
        }

        private static void AssertIced(ModularBlockView view, Material ice, string message)
        {
            Assert.That(view.ActivePieceCount, Is.GreaterThan(0), message);
            for (int index = 0; index < view.ActivePieceCount; index++)
            {
                Material[] materials = view.GetShownPiece(index).Renderer.sharedMaterials;
                Assert.That(materials, Has.Length.EqualTo(2), message);
                Assert.That(materials[1], Is.SameAs(ice), message);
            }
        }

        private static Material LoadIce()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(k_iceMaterialPath);
            Assert.That(material, Is.Not.Null);
            return material;
        }
    }
}
#endif
