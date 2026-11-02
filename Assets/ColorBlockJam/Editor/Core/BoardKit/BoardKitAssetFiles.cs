using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>Generic asset-file checks and mesh lookups the board kit's authoring tools share.</summary>
    internal static class BoardKitAssetFiles
    {
        internal static void ValidateWritable(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0)
                throw new IOException("The authoring file is read-only: " + path);

            // Opening without writing also checks ACLs and exclusive file locks.
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) { }
        }

        /// <summary>Throws <paramref name="message"/> when <paramref name="path"/> is the asset open in Prefab Mode.</summary>
        internal static void ValidateNotInPrefabMode(string path, string message)
        {
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && string.Equals(stage.assetPath, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(message);
        }

        internal static Mesh FindMesh(string path, string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Mesh mesh && mesh.name == name) return mesh;
            throw new InvalidOperationException("Missing mesh " + name + " in " + path);
        }
    }
}
