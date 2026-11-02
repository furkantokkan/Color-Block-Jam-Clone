using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the shared opaque art material and its palette atlas (DEC-96): every board/environment/booster
    /// renderer shares one instanced material and shader passes, booster UV colours reuse the authored palette,
    /// and MaterialPropertyBlock colour selection survives a pool release.
    /// </summary>
    public sealed class SharedArtTests
    {
        [Test]
        public void BoardEnvironmentAndBoosterMeshesUseOneOpaqueMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SharedArtAssets.MaterialPath);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.enableInstancing, Is.True);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            var paths = AssetDatabase.FindAssets("t:Prefab", new[]
            {
                "Assets/ColorBlockJam/Prefabs"
            });
            int renderers = 0;
            foreach (string guid in paths)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1), path);
                    Assert.That(renderer.sharedMaterial, Is.SameAs(material), path);
                    renderers++;
                }
            }
            Assert.That(renderers, Is.GreaterThan(20));
            foreach (string pass in new[] { "ForwardLit", "ShadowCaster", "DepthOnly", "DepthNormals" })
                Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0), pass);
        }

        [Test]
        public void AtlasIsReadableAndThePaletteNamesItsCells()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SharedArtAssets.MaterialPath);
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(SharedArtAssets.AtlasPath);
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(atlas));
            Assert.That(material.GetVector("_PaletteUV"), Is.EqualTo(new Vector4(1, .5f, 0, 0)));
            // GamePalette reads cell pixels for particles, editor canvases and the Blender export.
            Assert.That(atlas.isReadable, Is.True);
            Assert.That(palette.Validate(out string error), Is.True, error);
            var copy = new Texture2D(2, 2);
            try
            {
                copy.LoadImage(System.IO.File.ReadAllBytes(SharedArtAssets.AtlasPath));
                Assert.That(copy.width, Is.EqualTo(128));
                Assert.That(copy.height, Is.EqualTo(32));
                for (int i = 0; i < 8; i++)
                {
                    // Each swatch fills its 16-pixel square, which the Blender UVs read.
                    Assert.That((Color32)copy.GetPixel(i * 16 + 4, 4),
                        Is.EqualTo((Color32)BoardKitAssets.GetBoosterColors(palette)[i]));
                    Assert.That(copy.GetPixel(i * 16 + 12, 12), Is.EqualTo(copy.GetPixel(i * 16 + 4, 4)));
                }
            }
            finally { Object.DestroyImmediate(copy); }
            var importer = (TextureImporter)AssetImporter.GetAtPath(SharedArtAssets.AtlasPath);
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            foreach (string platform in new[] { "Android", "iPhone" })
                Assert.That(importer.GetPlatformTextureSettings(platform).format, Is.EqualTo(TextureImporterFormat.RGB24));
        }

        [Test]
        public void BoosterAtlasUsesColoursAndPreservesAuthoredPixels()
        {
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            Color32[] original = palette.Atlas.GetPixels32();
            Color32[] pixels = SharedArtAssets.GetBoosterAtlasPixels(palette);
            PaletteColor[] types =
            {
                PaletteColor.LightTurquoise, PaletteColor.Blue, PaletteColor.Purple, PaletteColor.Yellow,
                PaletteColor.Orange, PaletteColor.LightGreen, PaletteColor.DarkGreen,
            };
            for (int i = 0; i < 8; i++)
            {
                Color32 expected = i < types.Length ? palette.GetColor(types[i])
                    : palette.GetCellColor(palette.GetLightAtlasColor(PaletteColor.LightTurquoise).Cell);
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                        Assert.That(pixels[y * PaletteAtlas.Width + i * 16 + x], Is.EqualTo(expected));
            }
            for (int i = PaletteAtlas.Width * 16; i < pixels.Length; i++)
                Assert.That(pixels[i], Is.EqualTo(original[i]), "Authored cell changed at pixel " + i);
            Assert.That(palette.Atlas.GetPixels32(), Is.EqualTo(original), "Generating pixels must not mutate the source texture.");
        }

        [Test]
        public void BoosterAtlasRefusesToOverwriteAnAuthoredColour()
        {
            var palette = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath));
            try
            {
                var data = new SerializedObject(palette);
                data.FindProperty("m_colors").GetArrayElementAtIndex(0).FindPropertyRelative("m_cell").vector2IntValue = Vector2Int.zero;
                data.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<System.InvalidOperationException>(() => SharedArtAssets.GetBoosterAtlasPixels(palette));
            }
            finally { Object.DestroyImmediate(palette); }
        }

        [Test]
        public void AtlasCellSurvivesRecolorClippingAndPoolReturn()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ColorBlockJam/Prefabs/Board/Pieces/Door_1x1.prefab");
            var root = Object.Instantiate(prefab);
            try
            {
                var view = root.GetComponent<BoardPieceView>();
                var properties = new MaterialPropertyBlock();
                AtlasColor cyan = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath).GetAtlasColor(PaletteColor.LightTurquoise);
                view.SetColor(cyan);
                view.SetClipPlane(new Vector4(1, 0, 0, -2), true);
                view.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(cyan.UV));
                Assert.That(properties.GetFloat("_ClipEnabled"), Is.EqualTo(1));
                // Release makes no GPU write (DEC-123); the owner's next colour writes the cleared clip state.
                view.OnPoolRelease();
                view.SetColor(cyan);
                view.Renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_PaletteUV"), Is.EqualTo(cyan.UV));
                Assert.That(properties.GetFloat("_ClipEnabled"), Is.Zero);
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
