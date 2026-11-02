#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.PlayMode
{
    /// <summary>
    /// Pooled block-root lifecycle: BlockPieceFactory/ModularBlockView Prepare, the shared tile pool's reuse,
    /// Rocket-split recompose and Dispose (DEC-120, DEC-123, DEC-184).
    /// </summary>
    public sealed class BoardKitLifecycleTests
    {
        private const string k_palettePath = "Assets/ColorBlockJam/Data/Art/GamePalette.asset";
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Board/BoardPieceCatalog.asset";
        private const string k_modularBlockPrefabPath = "Assets/ColorBlockJam/Prefabs/Board/ModularBlock.prefab";

        private static readonly Vector2Int[] s_double={Vector2Int.zero,Vector2Int.right};
        private static readonly Vector2Int[] s_triple={Vector2Int.zero,Vector2Int.right,new Vector2Int(2,0)};
        private static readonly Vector2Int[] s_square=
            {new Vector2Int(0,0),new Vector2Int(1,0),new Vector2Int(0,1),new Vector2Int(1,1)};

        [UnityTest]
        public IEnumerator PieceFactoryReturnsDisabledRootsAndDisposesOutstandingPieces()
        {
            BoardArt art=CreateArt();
            var parent=new GameObject("Piece factory lifecycle test");
            var factory=new BlockPieceFactory(art,parent.transform);
            try
            {
                factory.Prepare(2,3);
                var request=new BlockPieceSpawnData(PaletteColor.Red,s_double);
                var first=factory.Create(request);
                var second=factory.Create(request);
                first.SetCells(s_triple);
                int shown=first.View.ActivePieceCount;
                first.gameObject.SetActive(false);
                Assert.That(first.View.ActivePieceCount,Is.EqualTo(shown),"A root's hierarchy cannot change while it is deactivated.");
                first.gameObject.SetActive(true);
                Assert.That(first.View.ActivePieceCount,Is.EqualTo(shown),"Enable must not reconstruct or resume a piece.");
                factory.Return(first);
                Assert.That(first.Cells,Is.Empty);
                Assert.That(first.View.ActivePieceCount,Is.Zero,"Return gives the tiles back to the shared pool.");
                Assert.That(factory.Create(request),Is.SameAs(first));
                Assert.That(first.Cells.Count,Is.EqualTo(2));
                Assert.That(parent.transform.childCount,Is.EqualTo(3),"two roots and the shared tile pool object");
                yield return null;
                Assert.That(second.View.ActivePieceCount,Is.GreaterThan(0));
                factory.Dispose();
                Assert.That(first.View.IsPrepared,Is.False);
                Assert.That(second.View.IsPrepared,Is.False);
                yield return null;
                Assert.That(parent.transform.childCount,Is.Zero);
                Assert.That(factory.ActiveCount,Is.Zero);
            }
            finally { factory.Dispose(); if(parent!=null) UnityEngine.Object.DestroyImmediate(parent); }
        }

        [UnityTest]
        public IEnumerator DisableKeepsTheShownTilesAndReuseClearsClipping()
        {
            BoardArt art=CreateArt();
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(k_modularBlockPrefabPath));
            try
            {
                var view=root.GetComponent<ModularBlockView>();
                Assert.That(view.IsPrepared,Is.False);
                view.Prepare(art.Catalog,4);
                var cells=new[]{Vector2Int.zero,Vector2Int.right,Vector2Int.up};
                view.SetCells(cells,art.GetAtlasColor(PaletteColor.Purple));
                view.SetClipPlane(new Vector4(1,0,0,0),true);
                int children=root.transform.childCount;
                int shown=view.ActivePieceCount;
                root.SetActive(false);
                Assert.That(view.ActivePieceCount,Is.EqualTo(shown),"A root's hierarchy cannot change while it is deactivated.");
                root.SetActive(true);
                Assert.That(view.ActivePieceCount,Is.EqualTo(shown));
                view.SetCells(cells,art.GetAtlasColor(PaletteColor.LightBlue));
                yield return null;
                Assert.That(root.transform.childCount,Is.EqualTo(children));
                var properties=new MaterialPropertyBlock();
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.GetPropertyBlock(properties);
                    Assert.That(properties.GetFloat("_ClipEnabled"),Is.Zero);
                }
                view.Release();
                yield return null;
                Assert.That(root.transform.childCount,Is.Zero);
            }
            finally { if(root!=null) UnityEngine.Object.DestroyImmediate(root); }
        }

        // AC-3: the owner's colour is the only property-block write; a pool release clears clipping in memory only.
        [UnityTest]
        public IEnumerator PooledTileTakesItsOwnersColourAndReleaseMakesNoPropertyBlockWrite()
        {
            BoardArt art=CreateArt();
            var root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(k_modularBlockPrefabPath));
            try
            {
                var view=root.GetComponent<ModularBlockView>();
                view.Prepare(art.Catalog,1);
                var single=new[]{Vector2Int.zero};
                view.SetCells(single,art.GetAtlasColor(PaletteColor.Red));
                view.SetClipPlane(new Vector4(1,0,0,-2),true);
                var tiles=new List<BoardPieceView>(root.GetComponentsInChildren<BoardPieceView>());
                Assert.That(tiles.Count,Is.EqualTo(4));

                view.Clear();
                var properties=new MaterialPropertyBlock();
                foreach(var tile in tiles)
                {
                    tile.Renderer.GetPropertyBlock(properties);
                    Assert.That(properties.GetVector("_PaletteUV"),Is.EqualTo(art.GetAtlasColor(PaletteColor.Red).UV),"Release writes no property block.");
                    Assert.That(properties.GetFloat("_ClipEnabled"),Is.EqualTo(1f),"Release writes no property block.");
                }

                view.SetCells(single,art.GetAtlasColor(PaletteColor.Blue));
                yield return null;
                var reused=root.GetComponentsInChildren<BoardPieceView>();
                Assert.That(reused.Length,Is.EqualTo(4));
                foreach(var tile in reused)
                {
                    Assert.That(tiles.Contains(tile),Is.True,"The pool hands out the same tiles again.");
                    tile.Renderer.GetPropertyBlock(properties);
                    Assert.That(properties.GetVector("_PaletteUV"),Is.EqualTo(art.GetAtlasColor(PaletteColor.Blue).UV));
                    Assert.That(properties.GetFloat("_ClipEnabled"),Is.Zero,"Release reset the clip state.");
                }
                view.Release();
            }
            finally { if(root!=null) UnityEngine.Object.DestroyImmediate(root); }
        }

        // AC-4: the factory resolves the palette colour, recomposes the first Rocket removal of a freshly
        // prepared block without allocating and instantiates nothing after Prepare. JIT is warmed on a
        // different root, so a tile whose cache is still created during the recompose fails the test.
        [UnityTest]
        public IEnumerator FactoryTintsWithTheArtPaletteRecomposesWithoutAllocatingAndPoolsEverything()
        {
            BoardArt art=CreateArt();
            var parent=new GameObject("Board art factory test");
            var factory=new BlockPieceFactory(art,parent.transform);
            try
            {
                factory.Prepare(2,4);
                // Home's budget: both Squares' tiles plus one L remainder spare per root, since both split here (DEC-184).
                var tiles=new int[BlockPieceCapacity.KindCount];
                var spare=new int[BlockPieceCapacity.KindCount];
                BlockPieceCapacity.AddTileBudget(s_square,tiles,spare);
                BlockPieceCapacity.AddTileBudget(s_square,tiles,spare);
                for(int kind=0;kind<tiles.Length;kind++) tiles[kind]+=2*spare[kind];
                factory.WarmTiles(tiles);
                int prepared=CountTransforms(parent.transform);
                var request=new BlockPieceSpawnData(PaletteColor.LightBlue,s_square);
                BlockPiece warm=factory.Create(request);
                BlockPiece piece=factory.Create(request);
                AtlasColor lightBlue=art.GetAtlasColor(PaletteColor.LightBlue);
                AssertTint(piece,lightBlue);

                // A precomputed Rocket split part (DEC-146): the domain splits the block and passes each
                // part's own cells, so recompose goes through TrySetCells, never a removal rule.
                var remainder=new List<Vector2Int>(s_square);
                remainder.RemoveAt(0);
                BlockPiece target=warm;
                bool removed=false;
                TestDelegate recomposeFirstSplit=()=>{ removed=target.TrySetCells(remainder); };
                // The warm pass runs the same delegate through the same constraint on the other root, so
                // one-time JIT work of the delegate, the recompose and the constraint stays out of the count.
                new AllocatingGCMemoryConstraint().ApplyTo(recomposeFirstSplit);
                Assert.That(removed,Is.True);
                target=piece;
                removed=false;
                Assert.That(recomposeFirstSplit,
                    Is.Not.AllocatingGCMemory(),
                    "The first Rocket split recompose of a prepared block allocates nothing.");
                Assert.That(removed,Is.True);
                Assert.That(piece.Cells.Count,Is.EqualTo(3));
                AssertTint(piece,lightBlue);

                factory.Return(warm);
                factory.Return(piece);
                yield return null;
                Assert.That(CountTransforms(parent.transform),Is.EqualTo(prepared),"Nothing is instantiated after Prepare.");
                Assert.That(factory.ActiveCount,Is.Zero);
                factory.Dispose();
                yield return null;
                Assert.That(parent.transform.childCount,Is.Zero,"Dispose releases every root and tile.");
            }
            finally { factory.Dispose(); if(parent!=null) UnityEngine.Object.DestroyImmediate(parent); }
        }

        private static BoardArt CreateArt()
        {
            return new BoardArt(AssetDatabase.LoadAssetAtPath<GamePalette>(k_palettePath),
                AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(k_catalogPath));
        }

        private static void AssertTint(BlockPiece piece, AtlasColor color)
        {
            var properties=new MaterialPropertyBlock();
            var renderers=piece.GetComponentsInChildren<MeshRenderer>();
            Assert.That(renderers.Length,Is.GreaterThan(0));
            foreach(var renderer in renderers)
            {
                renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_PaletteUV"),Is.EqualTo(color.UV));
            }
        }

        private static int CountTransforms(Transform root)
        {
            int count=1;
            for(int i=0;i<root.childCount;i++) count+=CountTransforms(root.GetChild(i));
            return count;
        }
    }
}
#endif
