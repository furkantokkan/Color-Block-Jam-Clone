using System;
using System.IO;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Board;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>Calibrates the supplied arrow meshes; does not rebuild the rest of the board kit.</summary>
    public static class BoardArrowAssets
    {
        private const string k_arrowFolder = BoardKitAssets.Root + "/Prefabs/Board/Pieces/Arrow_";
        private const float k_surfaceClearance = .025f;

        [MenuItem("Color Block Jam/Art/Prepare Arrow Prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Prepare arrow assets outside Play Mode.");
            var catalog = AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath);
            if (catalog == null)
                throw new InvalidOperationException("Build the board catalog first.");

            var prefabs = new BoardPieceView[3];
            for (int i = 0; i < prefabs.Length; i++)
            {
                string path = k_arrowFolder + (i + 1) + ".prefab";
                ValidateWritable(path);
                prefabs[i] = AssetDatabase.LoadAssetAtPath<BoardPieceView>(path);
                if (prefabs[i] == null || prefabs[i].Filter == null || prefabs[i].Filter.sharedMesh == null)
                    throw new InvalidOperationException("Missing original arrow prefab: " + path);
                if (AssetDatabase.GetAssetPath(prefabs[i].Filter.sharedMesh) != BoardKitAssets.Root + "/Art/Models/Supplied/Arrows.fbx")
                    throw new InvalidOperationException("Arrows must use the supplied Arrows.fbx meshes.");
            }

            float surfaceHeight = 0f;
            for (int i = 0; i < BlockPieceCapacity.KindCount; i++)
            {
                var tile = catalog.Get((BlockPieceKind)i);
                foreach (var renderer in tile.GetComponentsInChildren<MeshRenderer>(true))
                    surfaceHeight = Mathf.Max(surfaceHeight, renderer.bounds.max.y);
            }
            for (int i = 0; i < prefabs.Length; i++)
                Calibrate(k_arrowFolder + (i + 1) + ".prefab", surfaceHeight);
        }

        private static void Calibrate(string path, float surfaceHeight)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<BoardPieceView>();
                var visual = view.Filter.transform;
                Quaternion rotation = Quaternion.Euler(90f, 0f, 0f);
                Vector3 scale = Vector3.one * .48f;
                Bounds bounds = TransformBounds(view.Filter.sharedMesh.bounds, rotation, scale);
                if (bounds.size.x > bounds.size.z)
                {
                    rotation = Quaternion.Euler(0f, 90f, 0f) * rotation;
                    bounds = TransformBounds(view.Filter.sharedMesh.bounds, rotation, scale);
                }
                visual.SetLocalPositionAndRotation(new Vector3(-bounds.center.x,
                    surfaceHeight + k_surfaceClearance - bounds.min.y, -bounds.center.z), rotation);
                visual.localScale = scale;
                view.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Arrows hold no palette; their owner tints them with BoardArt.ArrowColor (DEC-123).
                var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (!success || saved == null)
                    throw new IOException("Could not save arrow prefab: " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Bounds TransformBounds(Bounds source, Quaternion rotation, Vector3 scale)
        {
            var result = new Bounds(rotation * Vector3.Scale(source.min, scale), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? source.min.x : source.max.x,
                    (i & 2) == 0 ? source.min.y : source.max.y, (i & 4) == 0 ? source.min.z : source.max.z);
                result.Encapsulate(rotation * Vector3.Scale(corner, scale));
            }
            return result;
        }

        private static void ValidateWritable(string path)
        {
            BoardKitAssetFiles.ValidateWritable(path);
            BoardKitAssetFiles.ValidateNotInPrefabMode(path, "Close Prefab Mode before rebuilding this arrow: " + path);
        }
    }
}
