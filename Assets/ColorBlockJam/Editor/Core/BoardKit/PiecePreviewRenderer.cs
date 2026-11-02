using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Board;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Renders the Piece Editor's 3D preview of one footprint from the original kit pieces in an isolated preview
    /// scene, and owns that scene and the last rendered texture until it is disposed.
    /// </summary>
    internal sealed class PiecePreviewRenderer : IDisposable
    {
        private const int k_maxCells = PieceGridDraft.MaxSize * PieceGridDraft.MaxSize;
        private const float k_textureSize = 400f;
        private static readonly Vector3 s_viewOffset = new Vector3(1.8f, 8f, -10f);
        private static readonly Color s_backgroundColor = new Color(.09f, .115f, .17f);
        private PreviewRenderUtility m_preview;
        private ModularBlockView m_view;
        private Texture2D m_texture;
        private int m_preparedCapacity;

        /// <summary>
        /// Renders <paramref name="cells"/> in <paramref name="color"/> and returns the texture, which this renderer
        /// destroys on the next render or on Dispose. An empty, duplicate or disconnected footprint throws an
        /// <see cref="ArgumentException"/> before anything renders.
        /// </summary>
        internal Texture Render(IReadOnlyList<Vector2Int> cells, AtlasColor color)
        {
            // Validation only: Compose throws for an empty, duplicate or disconnected footprint, so the caller can say
            // why before anything renders. The placements are not used.
            new BlockPieceComposer(k_maxCells).Compose(cells,
                new List<BlockPiecePlacement>(k_maxCells * BlockPieceComposer.MaxPlacementsPerCell));
            if (m_preview == null)
            {
                m_preview = new PreviewRenderUtility();
                var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BoardKitAssets.PrefabPath));
                m_preview.AddSingleGO(root);
                m_view = root.GetComponent<ModularBlockView>();
            }
            if (m_preparedCapacity < cells.Count)
            {
                m_view.Release();
                m_preparedCapacity = Mathf.NextPowerOfTwo(cells.Count);
                m_view.Prepare(AssetDatabase.LoadAssetAtPath<BoardPieceCatalog>(BoardKitAssets.CatalogPath), m_preparedCapacity);
            }
            m_view.SetCells(cells, color);
            Bounds bounds = BoardKitPreviewStage.GetRendererBounds(m_view.gameObject);
            Camera camera = m_preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(.85f, Mathf.Max(bounds.size.x, bounds.size.z) * .72f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.transform.position = bounds.center + s_viewOffset;
            camera.transform.LookAt(bounds.center);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = s_backgroundColor;
            BoardKitPreviewStage.ApplyLighting(m_preview, Color.gray);
            m_preview.BeginStaticPreview(new Rect(0, 0, k_textureSize, k_textureSize));
            m_preview.Render(true, true);
            if (m_texture != null) Object.DestroyImmediate(m_texture);
            m_texture = m_preview.EndStaticPreview();
            return m_texture;
        }

        /// <summary>Returns the pooled pieces, releases the preview scene and destroys the last texture.</summary>
        public void Dispose()
        {
            if (m_view != null) m_view.Release();
            m_preview?.Cleanup(); m_preview = null; m_view = null; m_preparedCapacity = 0;
            if (m_texture != null) Object.DestroyImmediate(m_texture);
            m_texture = null;
        }
    }
}
