using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that BlockPieceFactory and BlockPieceState reuse warmed roots without allocating and that a root
    /// without its view is refused. No prefab carries a shape any more (DEC-193): every root is the catalog's one
    /// block root, so the derived shape prefab cases of this fixture were removed.
    /// </summary>
    public sealed class ShapePrefabTests
    {
        private static readonly Vector2Int[] s_double = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_triple = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };

        [Test]
        public void PreparedFootprintChangesAllocateNoManagedMemory()
        {
            var state = new BlockPieceState(3);
            var twoCells = new[] { Vector2Int.zero, Vector2Int.right };
            var oneCell = new[] { new Vector2Int(2, 0) };
            state.SetCells(s_triple);
            state.SetCells(twoCells);
            state.SetCells(oneCell);
            // AD-27: GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor; the warmed Recorder
            // constraint gives a real allocation check.
            Assert.That(() =>
            {
                state.SetCells(s_triple);
                state.SetCells(twoCells);
                state.SetCells(oneCell);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void OnityFactoryReusesRootsResetsStateAndRejectsExhaustedForeignOrDuplicateReturns()
        {
            var parent = new GameObject("Isolated piece factory");
            var factory = new BlockPieceFactory(CreateArt(), parent.transform);
            try
            {
                factory.Prepare(1, 3);
                var request = new BlockPieceSpawnData(PaletteColor.LightBlue, s_double);
                Assert.That(factory.PreparedCount, Is.EqualTo(1));
                Assert.That(factory.ActiveCount, Is.Zero);
                var piece = factory.Create(request);
                int children = piece.transform.childCount;
                piece.SetCells(s_triple);
                piece.View.SetClipPlane(new Vector4(1, 0, 0, 1), true);
                piece.transform.localScale = Vector3.one * 2;
                Assert.Throws<InvalidOperationException>(() => factory.Create(request));
                using (var other = new BlockPieceFactory(CreateArt(), parent.transform))
                    Assert.Throws<ArgumentException>(() => other.Return(piece));
                factory.Return(piece);
                Assert.That(piece.Cells, Is.Empty);
                Assert.That(piece.View.ActivePieceCount, Is.Zero);
                Assert.That(piece.IsPrepared, Is.True, "Returning retains warmed inner pools.");
                Assert.Throws<ArgumentException>(() => factory.Return(piece));
                Assert.That(factory.Create(request), Is.SameAs(piece));
                Assert.That(piece.Cells.Count, Is.EqualTo(2));
                Assert.That(piece.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(piece.transform.childCount, Is.EqualTo(children));
                var properties = new MaterialPropertyBlock();
                var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
                foreach (var renderer in piece.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.GetPropertyBlock(properties);
                    Assert.That(properties.GetFloat("_ClipEnabled"), Is.Zero);
                    Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(palette.GetAtlasColor(PaletteColor.LightBlue).UV));
                }
                factory.Return(piece);
                Assert.Throws<ArgumentException>(() => factory.Create(new BlockPieceSpawnData(
                    PaletteColor.Red, new[] { Vector2Int.zero, new Vector2Int(8, 0) })));
                Assert.That(factory.ActiveCount, Is.Zero);
                Assert.That(factory.Create(request), Is.SameAs(piece));
                factory.Return(piece);
                for (int i = 0; i < 5; i++) factory.Return(factory.Create(request)); // Warm before measuring.
                // AD-27: GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor; the warmed Recorder
                // constraint gives a real allocation check.
                Assert.That(() => { for (int i = 0; i < 50; i++) factory.Return(factory.Create(request)); },
                    Is.Not.AllocatingGCMemory(), "Warmed factory checkout/return does not allocate managed memory.");
                factory.Create(request); // Teardown also returns outstanding roots.
                factory.Dispose();
                Assert.That(parent.transform.childCount, Is.Zero);
                Assert.Throws<ObjectDisposedException>(() => factory.Create(request));
            }
            finally { factory.Dispose(); Object.DestroyImmediate(parent); }
        }

        [Test]
        public void MissingViewRejectsPreparationWithoutLeavingPreparedState()
        {
            BoardArt art = CreateArt();
            var root = Object.Instantiate(art.Catalog.BlockRoot.gameObject);
            var tilesParent = new GameObject("Missing view tiles");
            var tiles = new BoardTilePool(art.Catalog, tilesParent.transform);
            try
            {
                var piece = root.GetComponent<BlockPiece>();
                var view = piece.View;
                var data = new SerializedObject(piece);
                data.FindProperty("m_view").objectReferenceValue = null;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => piece.Prepare(tiles, 1));
                Assert.That(piece.IsPrepared, Is.False);
                Assert.That(view.IsPrepared, Is.False);
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally
            {
                tiles.Dispose();
                Object.DestroyImmediate(tilesParent);
                Object.DestroyImmediate(root);
            }
        }

        // The shared palette and catalog; the catalog supplies the one block root.
        private static BoardArt CreateArt()
        {
            return new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
        }
    }
}
