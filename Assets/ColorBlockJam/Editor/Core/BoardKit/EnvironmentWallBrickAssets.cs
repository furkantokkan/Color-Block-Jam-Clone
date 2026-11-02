using System;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Imports the Blender-authored WallBrick.fbx, builds Brick_End.prefab with Wall.prefab's calibration,
    /// Corner_End.prefab and Corner_Join.prefab with Corner_1.prefab's and Door_End.prefab with Door_1x1.prefab's,
    /// and binds them in the EnvironmentDatabase (DEC-139). It writes only those six assets: the full kit rebuilds
    /// (BoardKitAssets.Build, SharedArtAssets) regenerate unrelated assets and stay out of it.
    /// </summary>
    public static class EnvironmentWallBrickAssets
    {
        public const string ModelPath = BoardKitAssets.Root + "/Art/Models/BoardKit/WallBrick.fbx";
        public const string PrefabPath = BoardKitAssets.Root + "/Prefabs/Board/Pieces/WallBrick/Brick_End.prefab";
        public const string CornerPrefabPath = BoardKitAssets.Root + "/Prefabs/Board/Pieces/WallBrick/Corner_End.prefab";
        public const string CornerJoinPrefabPath = BoardKitAssets.Root + "/Prefabs/Board/Pieces/WallBrick/Corner_Join.prefab";
        public const string DoorPrefabPath = BoardKitAssets.Root + "/Prefabs/Board/Pieces/WallBrick/Door_End.prefab";
        private const string k_kitModelPath = BoardKitAssets.Root + "/Art/Models/Supplied/WallAndDoor.fbx";
        private const string k_wallPrefabPath = BoardKitAssets.EnvironmentPrefabPath + "/Wall.prefab";
        private const string k_cornerPrefabPath = BoardKitAssets.EnvironmentPrefabPath + "/Corner_1.prefab";
        private const string k_doorPrefabPath = BoardKitAssets.EnvironmentPrefabPath + "/Door_1x1.prefab";
        private const string k_databasePath = BoardKitAssets.EnvironmentDatabasePath;
        // Each piece is authored inside its source piece's exact mesh bounds (ArtSource/BoardKit/WallBrick.blend).
        private const float k_boundsTolerance = 0.001f;

        [MenuItem("Color Block Jam/Art/Build Wall Brick")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Build the wall brick outside Play Mode.");
            }

            ConfigureImporter();
            // Every mesh is checked before any prefab is written, so a bad export changes nothing.
            Mesh brickMesh = FindCheckedMesh("Brick_End", "Wall");
            Mesh cornerMesh = FindCheckedMesh("Corner_End", "Corner_1");
            Mesh cornerJoinMesh = FindCheckedMesh("Corner_Join", "Corner_1");
            Mesh doorMesh = FindCheckedMesh("Door_End", "Door_1x1");
            BoardPieceView brick = CreatePiece(brickMesh, k_wallPrefabPath, PrefabPath);
            BoardPieceView corner = CreatePiece(cornerMesh, k_cornerPrefabPath, CornerPrefabPath);
            BoardPieceView cornerJoin = CreatePiece(cornerJoinMesh, k_cornerPrefabPath, CornerJoinPrefabPath);
            BoardPieceView door = CreatePiece(doorMesh, k_doorPrefabPath, DoorPrefabPath);
            BindDatabase(brick, corner, cornerJoin, door);
            AssetDatabase.SaveAssets();
            Debug.Log($"Wall brick built: {PrefabPath}, {CornerPrefabPath}, {CornerJoinPrefabPath} and {DoorPrefabPath} bound in {k_databasePath}.");
        }

        // The supplied kit's mesh-space import (WallAndDoor.fbx) with materials, animation and Read/Write off.
        private static void ConfigureImporter()
        {
            ModelImporter source = (ModelImporter)AssetImporter.GetAtPath(k_kitModelPath);
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (source == null || importer == null)
            {
                throw new InvalidOperationException($"Import {ModelPath} and {k_kitModelPath} first.");
            }

            importer.globalScale = source.globalScale;
            importer.useFileScale = source.useFileScale;
            importer.bakeAxisConversion = source.bakeAxisConversion;
            importer.importNormals = source.importNormals;
            importer.normalSmoothingAngle = source.normalSmoothingAngle;
            importer.importTangents = source.importTangents;
            importer.weldVertices = source.weldVertices;
            importer.keepQuads = source.keepQuads;
            importer.meshCompression = source.meshCompression;
            importer.indexFormat = source.indexFormat;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.isReadable = false;
            importer.addCollider = false;
            importer.SaveAndReimport();
        }

        // Equal bounds prove the new mesh shares the source mesh's space, so the source prefab's calibration fits it.
        private static Mesh FindCheckedMesh(string meshName, string sourceMeshName)
        {
            Mesh mesh = BoardKitAssetFiles.FindMesh(ModelPath, meshName);
            Mesh source = BoardKitAssetFiles.FindMesh(k_kitModelPath, sourceMeshName);
            if (!IsClose(mesh.bounds.min, source.bounds.min) || !IsClose(mesh.bounds.max, source.bounds.max))
            {
                throw new InvalidOperationException(
                    $"{meshName} bounds {mesh.bounds} differ from {sourceMeshName} bounds {source.bounds}; re-export WallBrick.fbx.");
            }

            return mesh;
        }

        // The source prefab's root, Visual child, X180 turn, height and stretch, shared material and view fields,
        // with only the mesh replaced.
        private static BoardPieceView CreatePiece(Mesh mesh, string sourcePrefabPath, string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(sourcePrefabPath);
            try
            {
                root.name = mesh.name;
                BoardPieceView view = root.GetComponent<BoardPieceView>();
                if (view == null || view.Filter == null)
                {
                    throw new InvalidOperationException($"{sourcePrefabPath} has no BoardPieceView with a mesh filter.");
                }

                view.Filter.sharedMesh = mesh;
                return PrefabUtility.SaveAsPrefabAsset(root, prefabPath).GetComponent<BoardPieceView>();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void BindDatabase(BoardPieceView brick, BoardPieceView corner, BoardPieceView cornerJoin,
            BoardPieceView door)
        {
            EnvironmentDatabase database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(k_databasePath);
            if (database == null)
            {
                throw new InvalidOperationException($"No environment database at {k_databasePath}.");
            }

            SerializedObject data = new SerializedObject(database);
            data.FindProperty("m_brickEndPiece").objectReferenceValue = brick;
            data.FindProperty("m_cornerEndPiece").objectReferenceValue = corner;
            data.FindProperty("m_cornerJoinPiece").objectReferenceValue = cornerJoin;
            data.FindProperty("m_doorEndPiece").objectReferenceValue = door;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssetIfDirty(database);
        }

        private static bool IsClose(Vector3 a, Vector3 b)
        {
            return Mathf.Abs(a.x - b.x) <= k_boundsTolerance && Mathf.Abs(a.y - b.y) <= k_boundsTolerance
                && Mathf.Abs(a.z - b.z) <= k_boundsTolerance;
        }
    }
}
