using System;
using System.IO;
using ColorBlockJam.Presentation.Board;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorBlockJam.Environment.Editor
{
    /// <summary>
    /// Creates the flat interior tile from existing art and binds it as the
    /// EnvironmentDatabase's BlockFill piece; does not edit meshes or scenes.
    /// </summary>
    public static class EnvironmentSolidAssets
    {
        public const string FillPath = "Assets/ColorBlockJam/Prefabs/Environment/BlockFill.prefab";
        private const string k_databasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";
        private const string k_wallPath = "Assets/ColorBlockJam/Prefabs/Board/Pieces/Wall.prefab";
        private const float k_joinEdgeToleranceFraction = 0.005f;

        [MenuItem("Color Block Jam/Art/Prepare Barrier Fill")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Prepare obstacle art outside Play Mode.");

            CheckWritable(FillPath);
            CheckWritable(k_databasePath);
            var database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(k_databasePath);
            if (database == null || database.FloorBasePiece == null || database.RuleSet == null)
                throw new InvalidOperationException("The environment database needs its existing floor base and wall rules.");

            BoardPieceView floorBase = database.FloorBasePiece;
            float surfaceHeight = GetFillHeight();
            // The floor base already uses this quad on XZ. Copy its complete mesh and
            // rotation, move only the top, and retain the board's one-material contract.
            MeshFilter source = floorBase.Filter;
            if (source == null || source.sharedMesh == null || floorBase.Renderer == null)
                throw new InvalidOperationException("The floor-base prefab has no configured mesh and renderer.");

            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(FillPath) != null;
            var previewScene = exists ? default : EditorSceneManager.NewPreviewScene();
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(FillPath) : new GameObject("BlockFill");
            try
            {
                if (!exists) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, previewScene);
                BoardPieceView view = root.GetComponent<BoardPieceView>();
                if (exists && (view == null || root.transform.childCount != 1 || root.transform.GetChild(0).name != "Visual"))
                    throw new InvalidOperationException("BlockFill contains custom content; refusing to overwrite it.");
                if (view == null) view = root.AddComponent<BoardPieceView>();
                var visual = exists ? root.transform.GetChild(0).gameObject : new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = new Vector3(0f, surfaceHeight, 0f);
                visual.transform.localRotation = source.transform.localRotation;
                visual.transform.localScale = Vector3.one;
                var filter = visual.GetComponent<MeshFilter>();
                if (filter == null) filter = visual.AddComponent<MeshFilter>();
                filter.sharedMesh = source.sharedMesh;
                var renderer = visual.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = visual.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = floorBase.Renderer.sharedMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                var data = new SerializedObject(view);
                data.FindProperty("m_filter").objectReferenceValue = filter;
                data.FindProperty("m_renderer").objectReferenceValue = renderer;
                data.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FillPath, out bool success);
                if (!success) throw new IOException("Could not save " + FillPath);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(previewScene);
            }

            var databaseData = new SerializedObject(database);
            databaseData.FindProperty("m_blockFillPiece").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BoardPieceView>(FillPath);
            // ApplyModifiedProperties already records its own Undo step; naming that
            // group avoids a second, redundant Undo.RecordObject call.
            Undo.SetCurrentGroupName("Assign barrier fill");
            databaseData.ApplyModifiedProperties();
            AssetDatabase.SaveAssetIfDirty(database);
        }

        private static float GetFillHeight()
        {
            // Wall is bevelled on both sides. Its joining edge is below its crest;
            // placing a quad at bounds.max.y leaves a visible slit beneath the fill.
            var wall = AssetDatabase.LoadAssetAtPath<BoardPieceView>(k_wallPath);
            if (wall == null || wall.Filter == null)
                throw new InvalidOperationException("The calibrated Wall prefab is missing.");
            Vector3[] vertices = wall.Filter.sharedMesh.vertices;
            float edge = wall.Renderer.bounds.min.x;
            float tolerance = wall.Renderer.bounds.size.x * k_joinEdgeToleranceFraction;
            float height = float.NegativeInfinity;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = wall.Filter.transform.TransformPoint(vertex);
                if (point.x <= edge + tolerance) height = Mathf.Max(height, point.y);
            }
            if (float.IsNegativeInfinity(height))
                throw new InvalidOperationException("The Wall mesh has no vertices on its joining edge.");
            return height;
        }

        private static void CheckWritable(string path)
        {
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new IOException("Asset is read-only: " + path);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
                throw new InvalidOperationException("Close the prefab before updating it: " + path);
        }
    }
}
