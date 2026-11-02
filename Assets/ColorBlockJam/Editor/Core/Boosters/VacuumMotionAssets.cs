using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Presentation.Boosters;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Boosters
{
    // Imports the Blender-baked Vacuum suction sheet (DEC-152): configures the sprite sheet importer,
    // repairs Unity 2022's sprite identity tables, then writes the frames into VacuumEffect.prefab and
    // the first frame plus the Intake anchor into the shared Prefabs/Boosters/Models/Vacuum.prefab.
    // Source data: docs/art/boosters/motion/vacuum-bake.json.
    public static class VacuumMotionAssets
    {
        private const string k_bakePath = "docs/art/boosters/motion/vacuum-bake.json";
        private const string k_texturePath = "Assets/ColorBlockJam/Art/Textures/Atlases/VacuumSuctionAtlas.png";
        private const string k_effectPrefabPath = "Assets/ColorBlockJam/Prefabs/Boosters/Effects/VacuumEffect.prefab";
        private const string k_modelPrefabPath = "Assets/ColorBlockJam/Prefabs/Boosters/Models/Vacuum.prefab";
        private const string k_frameNamePrefix = "VacuumSuction_";

        // A sprite with this GUID, or no GUID, is the legacy spritesheet setter's unassigned marker.
        private const string k_nullSpriteId = "00000000000000000800000000000000";

        // Base local file ID for a frame that has no prior identity to keep; offset by frame index.
        private const long k_spriteLocalIdBase = 21300000L;

        private const int k_maxTextureSize = 2048;

        [Serializable]
        private sealed class Bake
        {
            public int frames;
            public int cell_pixels;
            public int atlas_width;
            public int atlas_height;
            public float pixels_per_unit;
            public float[] intake_local;
        }

        [MenuItem("Color Block Jam/Art/Import Vacuum Motion Bake")]
        public static void Import()
        {
            Bake bake = JsonUtility.FromJson<Bake>(File.ReadAllText(k_bakePath));
            SpriteMetaData[] rects = ApplyImporterSettings(bake, out TextureImporter importer);
            RepairSpriteIdentities(importer, rects);
            Sprite[] sprites = LoadFrames(bake);
            SaveEffectPrefab(sprites);
            SaveModelPrefab(bake, sprites[0]);
            Debug.Log($"Imported Vacuum turn-in, suction and turn-out: {bake.frames} frames, one renderer, 512 KiB mobile ASTC payload.");
        }

        private static SpriteMetaData[] ApplyImporterSettings(Bake bake, out TextureImporter importer)
        {
            AssetDatabase.ImportAsset(k_texturePath, ImportAssetOptions.ForceSynchronousImport);
            importer = (TextureImporter)AssetImporter.GetAtPath(k_texturePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = bake.pixels_per_unit;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = k_maxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);

            int columns = bake.atlas_width / bake.cell_pixels;
            int rows = bake.atlas_height / bake.cell_pixels;
            SpriteMetaData[] rects = new SpriteMetaData[bake.frames];
            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = new SpriteMetaData
                {
                    name = k_frameNamePrefix + i.ToString("D2"),
                    alignment = 0,
                    pivot = new Vector2(0.5f, 0.5f),
                    rect = new Rect(
                        (i % columns) * bake.cell_pixels,
                        (rows - 1 - i / columns) * bake.cell_pixels,
                        bake.cell_pixels,
                        bake.cell_pixels)
                };
            }
            importer.spritesheet = rects;

            foreach (string platform in new[] { "Android", "iPhone" })
            {
                TextureImporterPlatformSettings config = importer.GetPlatformTextureSettings(platform);
                config.name = platform;
                config.overridden = true;
                config.maxTextureSize = k_maxTextureSize;
                config.format = TextureImporterFormat.ASTC_6x6;
                config.compressionQuality = 100;
                importer.SetPlatformTextureSettings(config);
            }

            return rects;
        }

        // Unity 2022's legacy spritesheet setter leaves new entries with duplicate sprite GUIDs and zero
        // local IDs. Persist both identity tables before importing, preserving every existing nonzero ID.
        private static void RepairSpriteIdentities(TextureImporter importer, SpriteMetaData[] rects)
        {
            SerializedObject importerData = new SerializedObject(importer);
            SerializedProperty sheet = importerData.FindProperty("m_SpriteSheet.m_Sprites");
            SerializedProperty names = importerData.FindProperty("m_SpriteSheet.m_NameFileIdTable");

            Dictionary<string, long> existing = new Dictionary<string, long>();
            for (int i = 0; i < names.arraySize; i++)
            {
                SerializedProperty pair = names.GetArrayElementAtIndex(i);
                existing[pair.FindPropertyRelative("first").stringValue] = pair.FindPropertyRelative("second").longValue;
            }

            names.arraySize = rects.Length;
            HashSet<string> guids = new HashSet<string>();
            for (int i = 0; i < rects.Length; i++)
            {
                SerializedProperty item = sheet.GetArrayElementAtIndex(i);
                SerializedProperty guid = item.FindPropertyRelative("m_SpriteID");
                if (string.IsNullOrEmpty(guid.stringValue) || guid.stringValue == k_nullSpriteId || !guids.Add(guid.stringValue))
                {
                    guid.stringValue = GUID.Generate().ToString();
                    guids.Add(guid.stringValue);
                }

                if (!existing.TryGetValue(rects[i].name, out long localId) || localId == 0)
                {
                    localId = k_spriteLocalIdBase + i;
                }
                item.FindPropertyRelative("m_InternalID").longValue = localId;

                SerializedProperty pair = names.GetArrayElementAtIndex(i);
                pair.FindPropertyRelative("first").stringValue = rects[i].name;
                pair.FindPropertyRelative("second").longValue = localId;
            }
            importerData.ApplyModifiedPropertiesWithoutUndo();

            // Match Unity 2022 Sprite Editor's Apply/re-serialize/import sequence;
            // SaveAndReimport alone can lose native sprite identity table edits.
            AssetDatabase.ForceReserializeAssets(new[] { k_texturePath }, ForceReserializeAssetsOptions.ReserializeMetadata);
            AssetDatabase.ImportAsset(k_texturePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        }

        private static Sprite[] LoadFrames(Bake bake)
        {
            Sprite[] sprites = new Sprite[bake.frames];
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(k_texturePath))
            {
                if (asset is Sprite sprite)
                {
                    sprites[int.Parse(sprite.name.Substring(sprite.name.Length - 2))] = sprite;
                }
            }
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null)
                {
                    throw new InvalidOperationException("Missing Vacuum frame.");
                }
            }
            return sprites;
        }

        private static void SaveEffectPrefab(Sprite[] sprites)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_effectPrefabPath);
            try
            {
                VacuumEffectView view = root.GetComponent<VacuumEffectView>();
                SerializedObject data = new SerializedObject(view);
                SerializedProperty frames = data.FindProperty("m_frames");
                frames.arraySize = sprites.Length;
                for (int i = 0; i < sprites.Length; i++)
                {
                    frames.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_effectPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // Vacuum.prefab owns the SpriteRenderer and Intake anchor; VacuumEffect.prefab nests it as
        // "Visual". Writing here instead of as an instance override keeps the model prefab current.
        private static void SaveModelPrefab(Bake bake, Sprite firstFrame)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_modelPrefabPath);
            try
            {
                root.GetComponent<SpriteRenderer>().sprite = firstFrame;
                root.transform.Find("Intake").localPosition = new Vector3(bake.intake_local[0], bake.intake_local[1], bake.intake_local[2]);
                PrefabUtility.SaveAsPrefabAsset(root, k_modelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
