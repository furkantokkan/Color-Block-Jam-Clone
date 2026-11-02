using System;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Presentation.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies ModularBlockView and BoardPieceView pooling: reusing prepared piece roots and materials across
    /// repeated footprint changes, and MaterialPropertyBlock tint/clip state surviving a pool release (DEC-123).
    /// </summary>
    public sealed class BoardKitTests
    {
        private static AtlasColor Red => AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath).GetAtlasColor(PaletteColor.Red);

        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_l = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [Test]
        public void RepeatedFootprintChangesReusePreparedObjectsAndMaterials()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.PrefabPath));
            try
            {
                var view = root.GetComponent<ModularBlockView>();
                Assert.That(view.IsPrepared, Is.False);
                view.Prepare(AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath), 8);
                // The first footprint creates the view's own tile pool object and its tiles (DEC-184).
                view.SetCells(s_single, Red);
                int count = root.transform.childCount;
                var material = AssetDatabase.LoadAssetAtPath<Material>(BoardKitAssets.Root + "/Art/Materials/Shared/MAT_Gameplay.mat");
                for (int i = 0; i < 10; i++)
                {
                    view.SetCells(s_l, Red); view.SetClipPlane(new Vector4(1, 0, 0, 0), true);
                    view.SetCells(s_single, Red);
                }
                Assert.That(root.transform.childCount, Is.EqualTo(count));
                Assert.That(view.ActivePieceCount, Is.EqualTo(4));
                var properties = new MaterialPropertyBlock();
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    Assert.That(renderer.sharedMaterial, Is.SameAs(material));
                    renderer.GetPropertyBlock(properties);
                    Assert.That(properties.GetFloat("_ClipEnabled"), Is.Zero);
                }
                Assert.Throws<ArgumentException>(() => view.SetCells(new[] { Vector2Int.zero, Vector2Int.zero }, Red));
                Assert.That(view.ActivePieceCount, Is.EqualTo(4));
                view.Clear();
                Assert.That(view.ActivePieceCount, Is.Zero);
                view.Release();
                Assert.That(root.transform.childCount, Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void SharedStateRetainsTintWhileClippingAndResetsOnReturn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.Root + "/Prefabs/Board/Pieces/Door_1x1.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var piece = root.GetComponent<BoardPieceView>();
                var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
                AtlasColor green = palette.GetAtlasColor(PaletteColor.DarkGreen);
                piece.SetColor(green);
                piece.SetClipPlane(new Vector4(1, 0, 0, -2), true);
                var properties = new MaterialPropertyBlock();
                piece.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(green.UV));
                Assert.That(properties.GetFloat("_ClipEnabled"), Is.EqualTo(1));
                // Release makes no GPU write (DEC-123); the owner's next colour writes the cleared clip state.
                piece.OnPoolRelease();
                piece.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(green.UV));
                piece.SetColor(palette.GetAtlasColor(PaletteColor.Orange));
                piece.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_ClipEnabled"), Is.Zero);
                Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(palette.GetAtlasColor(PaletteColor.Orange).UV));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void PiecePrefabNestedAndDisabledChildrenSurvivePooling()
        {
            var catalog = Object.Instantiate(AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath));
            var source = Object.Instantiate(catalog.Get(BlockPieceKind.OuterCorner).gameObject);
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.PrefabPath));
            try
            {
                source.SetActive(false);
                var original = source.transform.GetChild(0);
                var extra = Object.Instantiate(original.gameObject, source.transform);
                extra.name = "Added child";
                original.gameObject.SetActive(false);
                var catalogData = new SerializedObject(catalog);
                catalogData.FindProperty("m_pieces").GetArrayElementAtIndex((int)BlockPieceKind.OuterCorner)
                    .FindPropertyRelative("m_prefab").objectReferenceValue = source.GetComponent<BoardPieceView>();
                catalogData.ApplyModifiedPropertiesWithoutUndo();
                var view = root.GetComponent<ModularBlockView>();
                view.Prepare(catalog, 1); view.SetCells(s_single, Red);
                foreach (var piece in root.GetComponentsInChildren<BoardPieceView>())
                {
                    Assert.That(piece.transform.childCount, Is.EqualTo(2));
                    Assert.That(piece.transform.GetChild(0).gameObject.activeSelf, Is.False);
                    Assert.That(piece.transform.Find("Added child").gameObject.activeSelf, Is.True);
                }
                view.Release();
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(source); Object.DestroyImmediate(catalog); }
        }
    }
}
