#if UNITY_EDITOR
using ColorBlockJam.Runtime.Art;
using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using Onity.Pooling;
using UnityEngine;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Shared asset paths and pool/property-block readbacks for the Environment PlayMode suites, so a
    /// prefab move or pool-name change needs one edit instead of several (Tests-Environment-Tests-Hub-F5).
    /// </summary>
    internal static class EnvironmentViewTestSupport
    {
        internal const string PrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";
        internal const string DatabasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";

        private static readonly int s_paletteUVId = Shader.PropertyToID("_PaletteUV");

        internal static int CountActivePoolItems()
        {
            List<OnityPoolDiagnosticsSnapshot> snapshots = new List<OnityPoolDiagnosticsSnapshot>();
            OnityPoolDiagnosticsRegistry.GetSnapshots(snapshots);
            int active = 0;
            foreach (OnityPoolDiagnosticsSnapshot snapshot in snapshots)
            {
                if (snapshot.PoolName.StartsWith("Environment ", StringComparison.Ordinal))
                {
                    active += snapshot.CountActive;
                }
            }

            return active;
        }

        /// <summary>The atlas cell UV the piece samples (DEC-202).</summary>
        internal static Vector4 ReadColor(BoardPieceView piece)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            piece.Renderer.GetPropertyBlock(properties);
            return properties.GetVector(s_paletteUVId);
        }

        internal static float ColorDistance(Vector4 left, AtlasColor right)
        {
            return (left - right.UV).magnitude;
        }
    }
}
#endif
