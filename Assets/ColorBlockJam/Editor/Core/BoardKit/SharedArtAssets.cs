using System;
using System.IO;
using ColorBlockJam.Runtime.Art;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>One opaque material, sampling the authored colour atlas, for the supplied board kit and the Blender booster meshes.</summary>
    public static class SharedArtAssets
    {
        public const string MaterialPath = BoardKitAssets.Root + "/Art/Materials/Shared/MAT_Gameplay.mat";
        public const string AtlasPath = BoardKitAssets.Root + "/Art/Textures/Atlases/T_GameplayPalette.png";
        private const string k_boosterFolder = BoardKitAssets.Root + "/Prefabs/Boosters/Models/";
        // The Vacuum is a baked sprite (Models/Vacuum.prefab is its sprite visual), so it takes no mesh material.
        private static readonly string[] s_boosters = { "Hammer", "RocketPurple" };

        [MenuItem("Color Block Jam/Art/Update Shared Art Material")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Update shared art outside Play Mode.");

            ValidateWritable(MaterialPath);
            ValidateWritable(AtlasPath);
            foreach (string name in s_boosters) ValidateWritable(k_boosterFolder + name + ".prefab");

            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(BoardKitAssets.PalettePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            var shader = Shader.Find("ColorBlockJam/Board Lit");
            if (palette == null || material == null || shader == null || ShaderUtil.ShaderHasError(shader))
                throw new InvalidOperationException("The palette, board material and compiled board shader must exist.");
            // The upper half is authored; the lower half mirrors Colours for the existing Blender UVs.
            ApplyAtlasImport();
            if (!palette.Validate(out string error)) throw new InvalidOperationException(error);
            UpdateBoosterAtlas(palette);
            material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath));
            // The Blender booster UVs read the bottom half; board tiles override this with their cell (DEC-202).
            material.SetVector("_PaletteUV", new Vector4(1f, .5f, 0f, 0f));
            material.SetFloat("_Smoothness", .28f);
            material.SetFloat("_Specular", .3f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);

            foreach (string name in s_boosters) SetMaterial(k_boosterFolder + name + ".prefab", material);
            BoardKitAssets.ExportPalette();
        }

        internal static Color32[] GetBoosterAtlasPixels(GamePalette palette)
        {
            if (!palette.Validate(out string error)) throw new InvalidOperationException(error);
            foreach (GamePalette.Entry entry in palette.Colors)
                if (entry.Cell.y < 2 || entry.LightCell.y < 2)
                    throw new InvalidOperationException("Colours must use the upper two atlas rows; the lower rows are generated for booster UVs.");
            if (palette.ArrowCell.y < 2)
                throw new InvalidOperationException("Arrow Cell must use the upper two atlas rows.");

            Color32[] pixels = palette.Atlas.GetPixels32();
            Color[] colors = BoardKitAssets.GetBoosterColors(palette);
            int size = PaletteAtlas.CellSize * 2;
            for (int i = 0; i < colors.Length; i++)
            {
                Color32 color = colors[i];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        pixels[y * PaletteAtlas.Width + i * size + x] = color;
            }
            return pixels;
        }

        private static void UpdateBoosterAtlas(GamePalette palette)
        {
            Color32[] pixels = GetBoosterAtlasPixels(palette);
            var atlas = new Texture2D(PaletteAtlas.Width, PaletteAtlas.Height, TextureFormat.RGB24, false);
            try
            {
                atlas.SetPixels32(pixels);
                atlas.Apply();
                File.WriteAllBytes(AtlasPath, atlas.EncodeToPNG());
                AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
            }
            finally { UnityEngine.Object.DestroyImmediate(atlas); }
        }

        // Readable, because GamePalette reads a cell's pixel for booster particles, editor canvases and the Blender
        // export (DEC-202); the 128 x 32 RGB24 copy costs 12 KB of CPU memory.
        private static void ApplyAtlasImport()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Point;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (string platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform);
                settings.overridden = true;
                settings.maxTextureSize = 128;
                settings.format = TextureImporterFormat.RGB24;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
        }

        private static void SetMaterial(string path, Material material)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                bool changed = false;
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    changed |= renderer.sharedMaterial != material || !renderer.receiveShadows;
                    renderer.sharedMaterial = material;
                    renderer.receiveShadows = true;
                }
                if (!changed) return;
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success) throw new IOException("Could not save shared material on " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ValidateWritable(string path)
        {
            BoardKitAssetFiles.ValidateWritable(path);
            BoardKitAssetFiles.ValidateNotInPrefabMode(path, "Close the open prefab before updating it: " + path);
        }
    }
}
