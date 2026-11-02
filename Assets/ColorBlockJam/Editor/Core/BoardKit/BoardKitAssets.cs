using System;
using System.IO;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Presentation.Board;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Builds the board kit's palette, piece library seed, shared material, piece prefabs, bare block root and catalog
    /// from the supplied FBX files.
    /// </summary>
    public static class BoardKitAssets
    {
        public const string Root = "Assets/ColorBlockJam";
        public const string PalettePath = Root + "/Data/Art/GamePalette.asset";
        public const string PiecesDirectory = Root + "/Data/Json/Pieces";
        /// <summary>The piece library: one fixed JSON file with no wrapper asset (DEC-195).</summary>
        public const string ShapesJsonPath = PiecesDirectory + "/BlockShapeLibrary.json";
        public const string CatalogPath = Root + "/Data/Board/BoardPieceCatalog.asset";
        public const string PrefabPath = Root + "/Prefabs/Board/ModularBlock.prefab";
        public const string EnvironmentPrefabPath = Root + "/Prefabs/Board/Pieces";
        public const string EnvironmentDatabasePath = Root + "/Data/Environment/EnvironmentDatabase.asset";
        private const string k_materialPath = Root + "/Art/Materials/Shared/MAT_Gameplay.mat";
        // Seam-closing calibration of the supplied wall kit (play-area-environment D-001).
        private const float k_cornerOffsetX = -0.0083f;
        private const float k_cornerStretchX = 1.0166f;
        private const float k_wallStretchX = 1.01f;
        private const float k_doorStretchZ = 1.02f;
        // The block piece visual's vertical origin: the ModularBlock mesh's Y extent (0.6556866)
        // times the piece's local Z scale (.35) under the X90 rotation, lifting the mesh's
        // underside onto the floor instead of clipping through it (docs/art/board-kit/README.md).
        private const float k_studHeight = .6556866f * .35f;
        // Calibrated wall/gate height of the board kit (docs/art/board-kit/README.md).
        private const float k_wallHeight = .72f;

        /// <summary>The environment pieces the kit calibrates, shared with the contact-sheet preview, the prefab reserializer and the wall-brick tool.</summary>
        public static readonly string[] EnvironmentPieceNames =
        {
            "Wall", "Corner_1", "Door_1x1", "Door_Arrow", "Grid", "Arrow_1", "Arrow_2", "Arrow_3"
        };

        // Seeds a missing BlockShapeLibrary.json only; the JSON is authoritative once it exists (DEC-193).
        private static readonly (string name, Vector2Int[] cells)[] s_seedShapes =
        {
            ("Single", new[] { new Vector2Int(0,0) }),
            ("Square", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1) }),
            ("Double", new[] { new Vector2Int(0,0), new Vector2Int(1,0) }),
            ("Triple", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0) }),
            ("Corner", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(0,1) }),
            ("L Shape", new[] { new Vector2Int(0,0), new Vector2Int(0,1), new Vector2Int(0,2), new Vector2Int(1,2) }),
            ("T Shape", new[] { new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }),
            ("Cross", new[] { new Vector2Int(1,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1), new Vector2Int(1,2) }),
            ("Z Shape", new[] { new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1) }),
            ("Rectangle", new[] { new Vector2Int(0,0), new Vector2Int(1,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) }),
            ("C Shape", new[] { new Vector2Int(0,0), new Vector2Int(2,0), new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(2,1) })
        };

        [MenuItem("Color Block Jam/Art/Build Board Kit Assets")]
        public static void Build()
        {
            Folder(Root + "/Data/Art"); Folder(Root + "/Data/Board");
            Folder(EnvironmentPrefabPath); Folder(Root + "/Art/Materials/Shared");
            GamePalette palette = CreatePaletteIfMissing();
            BlockShapeLibrary shapes = CreateShapeLibraryIfMissing();
            shapes.Load();
            Material material = CreateMaterialIfMissing();
            BoardPieceView[] pieces = CreateBlockPieces(material);
            BoardPieceView[] environment = CreateEnvironmentPieces(material);
            BlockPiece blockRoot = CreateBlockTemplate();
            BindCatalog(pieces, environment, blockRoot);
            BoardArrowAssets.Build();
            ExportPalette();
            Debug.Log($"Board kit imported: {shapes.Shapes.Count} piece templates, " +
                $"{pieces.Length + environment.Length} reusable original-mesh pieces, the block root, shared palette and material.");
        }

        // A new palette names the shipped atlas layout (DEC-202): row 2 holds each PaletteColor then the arrow, row 3
        // each PaletteColor blended toward white. Booster UV colours are derived from these entries.
        private static GamePalette CreatePaletteIfMissing()
        {
            GamePalette palette = AssetDatabase.LoadAssetAtPath<GamePalette>(PalettePath);
            if (palette != null) return palette;
            palette = ScriptableObject.CreateInstance<GamePalette>();
            var data = new SerializedObject(palette);
            data.FindProperty("m_atlas").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(SharedArtAssets.AtlasPath);
            var colors = data.FindProperty("m_colors");
            int colorCount = Enum.GetValues(typeof(PaletteColor)).Length;
            colors.arraySize = colorCount;
            for (int i = 0; i < colorCount; i++)
            {
                var entry = colors.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("m_type").intValue = i;
                entry.FindPropertyRelative("m_cell").vector2IntValue = new Vector2Int(i, 2);
                entry.FindPropertyRelative("m_lightCell").vector2IntValue = new Vector2Int(i, 3);
            }
            data.FindProperty("m_arrowCell").vector2IntValue = new Vector2Int(colorCount, 2);
            data.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(palette, PalettePath);
            return palette;
        }

        private static BlockShapeLibrary CreateShapeLibraryIfMissing()
        {
            Folder(PiecesDirectory);
            if (!File.Exists(ShapesJsonPath))
            {
                var records = new BlockShapeLibrary.Shape[s_seedShapes.Length];
                for (int i = 0; i < records.Length; i++)
                    records[i] = new BlockShapeLibrary.Shape(s_seedShapes[i].name, s_seedShapes[i].cells);
                ShapeLibraryFile.Write(ShapesJsonPath, null, BlockShapeLibraryJson.Write(records));
                AssetDatabase.ImportAsset(ShapesJsonPath, ImportAssetOptions.ForceUpdate);
            }
            return BlockShapeLibrary.LoadProject();
        }

        private static Material CreateMaterialIfMissing()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(k_materialPath);
            if (material != null) return material;
            var shader = Shader.Find("ColorBlockJam/Board Lit");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Board shader is not ready.");
            material = new Material(shader) { enableInstancing = true };
            AssetDatabase.CreateAsset(material, k_materialPath);
            return material;
        }

        private static BoardPieceView[] CreateBlockPieces(Material material)
        {
            string model = Root + "/Art/Models/Supplied/BlockParts.fbx";
            var pieces = new BoardPieceView[BlockPieceCapacity.KindCount];
            for (int i = 0; i < pieces.Length; i++)
            {
                string name = ((BlockPieceKind)i).ToString();
                pieces[i] = CreateBlockPiece((BlockPieceKind)i, BoardKitAssetFiles.FindMesh(model, "ModularBlock_" + name), material);
            }
            return pieces;
        }

        private static BoardPieceView[] CreateEnvironmentPieces(Material material)
        {
            string wallModel = Root + "/Art/Models/Supplied/WallAndDoor.fbx";
            var environment = new BoardPieceView[EnvironmentPieceNames.Length];
            for (int i = 0; i < environment.Length; i++)
            {
                string name = EnvironmentPieceNames[i];
                string path = name == "Grid" ? Root + "/Art/Models/Supplied/GroundGrid.fbx" :
                    name.StartsWith("Arrow_", StringComparison.Ordinal) ? Root + "/Art/Models/Supplied/Arrows.fbx" : wallModel;
                Mesh mesh = BoardKitAssetFiles.FindMesh(path, name);
                (Vector3 position, Quaternion rotation, Vector3 scale) = GetEnvironmentTransform(name, mesh);
                environment[i] = CreatePiece(name, mesh, material, position, rotation, scale);
            }
            return environment;
        }

        // Each environment piece's measured lattice placement (play-area-environment D-001).
        private static (Vector3 position, Quaternion rotation, Vector3 scale) GetEnvironmentTransform(string name, Mesh mesh)
        {
            Vector3 position = Vector3.zero, scale = Vector3.one;
            Quaternion rotation = Quaternion.identity;
            if (name == "Grid") scale = new Vector3(1f / mesh.bounds.size.x, 1, 1f / mesh.bounds.size.z);
            else if (name.StartsWith("Arrow_", StringComparison.Ordinal))
            {
                rotation = Quaternion.Euler(90, 0, 0);
                scale = Vector3.one * .48f;
                position.y = .76f;
            }
            else if (name == "Door_Arrow")
            {
                rotation = Quaternion.Euler(180, 0, 0);
                position = new Vector3(-mesh.bounds.center.x, .74f + mesh.bounds.max.y * .2f, mesh.bounds.center.z);
                scale = new Vector3(1, .2f, 1);
            }
            else
            {
                float sx = name == "Door_1x1" ? 1f / mesh.bounds.size.x : 1f;
                // The supplied closed, rounded top faces local -Y. The open
                // +Y side is the underside, not a roof requiring new geometry.
                rotation = Quaternion.Euler(180, 0, 0);
                scale = new Vector3(sx, k_wallHeight / mesh.bounds.size.y, 1);
                position.y = mesh.bounds.max.y * scale.y;
                // Wall/corner origins remain at their measured lattice junctions.
                if (name == "Wall" || name == "Door_1x1")
                    position.x = -mesh.bounds.center.x * sx;
                // The supplied corner arm, wall and door stop just short of the cell edge, which
                // showed hairline seams at their joins at 1080 x 1920. These small, user-approved
                // stretches make neighbours overlap instead (play-area-environment D-001).
                if (name == "Corner_1")
                {
                    position.x = k_cornerOffsetX;
                    scale.x = k_cornerStretchX;
                }
                else if (name == "Wall")
                    scale.x *= k_wallStretchX;
                else if (name == "Door_1x1")
                    scale.z = k_doorStretchZ;
            }
            return (position, rotation, scale);
        }

        private static void BindCatalog(BoardPieceView[] pieces, BoardPieceView[] environment, BlockPiece blockRoot)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BoardPieceCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var catalogData = new SerializedObject(catalog);
            var entries = catalogData.FindProperty("m_pieces");
            entries.arraySize = pieces.Length;
            for (int i = 0; i < pieces.Length; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("m_kind").enumValueIndex = i;
                entry.FindPropertyRelative("m_prefab").objectReferenceValue = pieces[i];
            }
            var surroundings = catalogData.FindProperty("m_environment");
            surroundings.arraySize = environment.Length;
            for (int i = 0; i < environment.Length; i++) surroundings.GetArrayElementAtIndex(i).objectReferenceValue = environment[i];
            catalogData.FindProperty("m_blockRoot").objectReferenceValue = blockRoot;
            catalogData.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssetIfDirty(catalog);
        }

        // The one bare block root every block is pooled from (DEC-193): a BlockPiece with its ModularBlockView on the
        // root and no child, which BlockPieceFactory.TryGetRoot requires. It holds no ScriptableObject reference; owners
        // pass the board art (DEC-123).
        private static BlockPiece CreateBlockTemplate()
        {
            var root = new GameObject("ModularBlock");
            try
            {
                var view = root.AddComponent<ModularBlockView>();
                var piece = root.AddComponent<BlockPiece>();
                var data = new SerializedObject(piece);
                data.FindProperty("m_view").objectReferenceValue = view;
                data.ApplyModifiedPropertiesWithoutUndo();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool saved);
                if (!saved || prefab == null) throw new IOException("Could not save the block root prefab: " + PrefabPath);
                return prefab.GetComponent<BlockPiece>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [MenuItem("Color Block Jam/Art/Export Shared Palette for Blender")]
        public static void ExportPalette()
        {
            var palette = AssetDatabase.LoadAssetAtPath<GamePalette>(PalettePath);
            if (palette == null) throw new InvalidOperationException("Create the authored palette first.");
            if (!palette.Validate(out string error)) throw new InvalidOperationException(error);
            // Keep the Blender UV-role order; Colours is the only authored colour mapping.
            string[] roles = { "cyan", "blue", "purple", "gold", "orange", "green", "dark-green", "light-cyan" };
            Color[] colors = GetBoosterColors(palette);
            var export = new BlenderPaletteExport { m_boosterSwatches = new BlenderSwatch[colors.Length] };
            for (int i = 0; i < export.m_boosterSwatches.Length; i++)
            {
                export.m_boosterSwatches[i] = new BlenderSwatch { m_id = roles[i], m_color = colors[i] };
            }
            Directory.CreateDirectory("ArtSource/BoardKit");
            File.WriteAllText("ArtSource/BoardKit/palette-export.json", JsonUtility.ToJson(export, true));
        }

        // The fixed model UV roles reuse Colours, including its light cyan for the highlight role.
        internal static Color[] GetBoosterColors(GamePalette palette) => new[]
        {
            palette.GetColor(PaletteColor.LightTurquoise),
            palette.GetColor(PaletteColor.Blue),
            palette.GetColor(PaletteColor.Purple),
            palette.GetColor(PaletteColor.Yellow),
            palette.GetColor(PaletteColor.Orange),
            palette.GetColor(PaletteColor.LightGreen),
            palette.GetColor(PaletteColor.DarkGreen),
            palette.GetCellColor(palette.GetLightAtlasColor(PaletteColor.LightTurquoise).Cell),
        };

        [Serializable]
        private sealed class BlenderPaletteExport
        {
            public BlenderSwatch[] m_boosterSwatches;
        }

        [Serializable]
        private struct BlenderSwatch
        {
            public string m_id;
            public Color m_color;
        }

        /// <summary>Creates <paramref name="path"/> and any missing parent folders as assets.</summary>
        internal static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            if (Directory.Exists(path))
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            Folder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static BoardPieceView CreateBlockPiece(BlockPieceKind kind, Mesh mesh, Material material)
        {
            string name = kind.ToString();
            var root = new GameObject(name);
            try
            {
                root.SetActive(false);
                int count = kind == BlockPieceKind.Center ? 4 : kind == BlockPieceKind.Edge ? 2 : 1;
                MeshFilter first = null;
                MeshRenderer firstRenderer = null;
                for (int i = 0; i < count; i++)
                {
                    var visual = new GameObject("Source_" + i);
                    visual.transform.SetParent(root.transform, false);
                    Vector3 position = new Vector3(0, k_studHeight, 0);
                    Quaternion rotation = Quaternion.Euler(90, 0, 0);
                    if (kind == BlockPieceKind.Center)
                    {
                        position.x = (i % 2 - 1) * .5f;
                        position.z = (i / 2 - 1) * .5f;
                    }
                    else if (kind == BlockPieceKind.Edge)
                    {
                        position.x = (i - 1) * .5f; position.z = -.5f;
                    }
                    else
                    {
                        rotation = Quaternion.Euler(0, -90, 0) * rotation;
                        if (kind == BlockPieceKind.OuterCorner) { position.x = .5f; position.z = -.5f; }
                    }
                    visual.transform.SetLocalPositionAndRotation(position, rotation);
                    visual.transform.localScale = new Vector3(.5f, .5f, .35f);
                    var filter = visual.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
                    var renderer = visual.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                    if (first == null) { first = filter; firstRenderer = renderer; }
                }
                var piece = root.AddComponent<BoardPieceView>();
                var data = new SerializedObject(piece);
                data.FindProperty("m_filter").objectReferenceValue = first;
                data.FindProperty("m_renderer").objectReferenceValue = firstRenderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefabPath + "/" + name + ".prefab").GetComponent<BoardPieceView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static BoardPieceView CreatePiece(string name, Mesh mesh,
            Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var root = new GameObject(name);
            try
            {
                root.SetActive(false);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.SetLocalPositionAndRotation(position, rotation);
                visual.transform.localScale = scale;
                var filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                var renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                var piece = root.AddComponent<BoardPieceView>();
                var data = new SerializedObject(piece);
                data.FindProperty("m_filter").objectReferenceValue = filter;
                data.FindProperty("m_renderer").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                root.SetActive(true);
                return PrefabUtility.SaveAsPrefabAsset(root, EnvironmentPrefabPath + "/" + name + ".prefab").GetComponent<BoardPieceView>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
