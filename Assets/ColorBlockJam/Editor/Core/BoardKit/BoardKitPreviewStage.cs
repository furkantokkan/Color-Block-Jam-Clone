using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Shared set-up of the board kit preview scenes (the Piece Editor preview and the contact sheets): the bounds a
    /// preview camera frames and the two-light look. Each caller keeps its own camera size, view offset, background
    /// and ambient colour, so every rendered image stays as it was.
    /// </summary>
    internal static class BoardKitPreviewStage
    {
        private const float k_keyLightIntensity = 1.3f;
        private const float k_fillLightIntensity = .4f;
        private static readonly Quaternion s_keyLightRotation = Quaternion.Euler(45f, -30f, 0f);

        /// <summary>
        /// Returns the combined bounds of the mesh renderers under <paramref name="root"/>, or a unit box at the root
        /// when it has none.
        /// </summary>
        internal static Bounds GetRendererBounds(GameObject root)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        /// <summary>Sets the preview's key and fill lights and the given ambient colour.</summary>
        internal static void ApplyLighting(PreviewRenderUtility utility, Color ambient)
        {
            utility.ambientColor = ambient;
            utility.lights[0].intensity = k_keyLightIntensity;
            utility.lights[0].transform.rotation = s_keyLightRotation;
            utility.lights[1].intensity = k_fillLightIntensity;
        }
    }
}
