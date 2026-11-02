using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Board;
using UnityEditor;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Rewrites the board kit prefabs in the current serialization format, so the palette, catalog and
    /// library fields the views no longer declare leave the files (DEC-123). It reserializes in place:
    /// GUIDs and fileIDs stay, and nothing is regenerated. ForceReserializeAssets writes nothing for an
    /// unchanged prefab in Unity 2022.3, so each prefab's objects are marked dirty and saved instead.
    /// </summary>
    public static class BoardKitMigration
    {
        private const string k_floorBasePath = BoardKitAssets.Root + "/Prefabs/Environment/FloorBase.prefab";

        // BlockPieceKind's names (the block piece prefabs) followed by BoardKitAssets.EnvironmentPieceNames.
        private static readonly string[] s_pieceNames = BuildPieceNames();

        [MenuItem("Color Block Jam/Art/Reserialize Board Kit Prefabs")]
        public static void Reserialize()
        {
            List<string> paths = GetPaths();
            Validate(paths);
            foreach (string path in paths)
            {
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset != null) EditorUtility.SetDirty(asset);
                }

                AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
            }

            UnityEngine.Debug.Log($"Reserialized {paths.Count} board kit prefabs in place.");
        }

        /// <summary>The 14 piece prefabs, the block root and the floor base; no prefab carries a shape any more (DEC-193).</summary>
        public static List<string> GetPaths()
        {
            var paths = new List<string>();
            foreach (string name in s_pieceNames)
            {
                paths.Add(BoardKitAssets.EnvironmentPrefabPath + "/" + name + ".prefab");
            }

            paths.Add(BoardKitAssets.PrefabPath);
            paths.Add(k_floorBasePath);
            return paths;
        }

        private static string[] BuildPieceNames()
        {
            string[] blockKinds = Enum.GetNames(typeof(BlockPieceKind));
            var names = new string[blockKinds.Length + BoardKitAssets.EnvironmentPieceNames.Length];
            Array.Copy(blockKinds, names, blockKinds.Length);
            Array.Copy(BoardKitAssets.EnvironmentPieceNames, 0, names, blockKinds.Length, BoardKitAssets.EnvironmentPieceNames.Length);
            return names;
        }

        private static void Validate(List<string> paths)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                throw new InvalidOperationException("Reserialize the board kit outside Play Mode and after scripts compile.");
            }

            foreach (string path in paths)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new FileNotFoundException("A board kit prefab is missing: " + path);
                }

                BoardKitAssetFiles.ValidateNotInPrefabMode(path, "Close Prefab Mode before reserializing: " + path);
                BoardKitAssetFiles.ValidateWritable(path);
            }
        }
    }
}
