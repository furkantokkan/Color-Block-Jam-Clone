using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.LevelPreview
{
    /// <summary>
    /// Static authored Ice uses the runtime Ice look and label owners without starting an attempt: the Ice material
    /// is shown on each frozen block's own tiles (DEC-184).
    /// </summary>
    internal sealed class IcePreviewEffect : ILevelPreviewEffect, ILevelPreviewEffectCheck
    {
        private const string k_commonStyleSheetPath = "Assets/ColorBlockJam/UI/Common/Common.uss";
        private const string k_missingAssetsMessage =
            "Ice preview requires the shared Ice material and overlay PanelSettings.";

        private readonly Material m_material;
        private readonly PanelSettings m_panel;
        private BlockIceOverlays m_shells;
        private IceCounterLabels m_labels;
        private GameObject m_documentRoot;
        private int[] m_counts;

        public IcePreviewEffect(Material material, PanelSettings panel) { m_material = material; m_panel = panel; }

        // Refuses a level with an Ice block when the material or panel is missing, before the host changes the
        // scene; Prepare below keeps the same check as a guard.
        public bool TryCheck(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, out string error)
        {
            if (HasIceBlock(level, blocks.Count) && (m_material == null || m_panel == null))
            {
                error = k_missingAssetsMessage;
                return false;
            }

            error = null;
            return true;
        }

        public void Prepare(LevelData level, IReadOnlyList<LevelPreviewBlock> blocks, BoardArt art, Transform storage, Camera camera)
        {
            Dispose();
            m_counts = new int[blocks.Count];
            var cells = new IReadOnlyList<Vector2Int>[blocks.Count];
            bool any = false;
            for (int block = 0; block < blocks.Count; block++)
            {
                cells[block] = blocks[block].Cells;
                if (!level.GetBlockAt(block).TryGetEffect(out IceEffect effect)) continue;
                m_counts[block] = effect.ClearsToThaw;
                any = true;
            }
            if (!any) return;
            if (m_material == null || m_panel == null)
                throw new System.InvalidOperationException(k_missingAssetsMessage);
            try
            {
                m_shells = new BlockIceOverlays(m_material, blocks.Count);
                m_documentRoot = new GameObject("Ice preview counters") { hideFlags = HideFlags.DontSave };
                m_documentRoot.transform.SetParent(storage, false);
                UIDocument document = m_documentRoot.AddComponent<UIDocument>();
                document.panelSettings = m_panel;
                document.sortingOrder = 20;
                StyleSheet commonStyles = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_commonStyleSheetPath);
                if (commonStyles == null)
                    throw new System.InvalidOperationException($"Ice preview style sheet is missing: {k_commonStyleSheetPath}");
                document.rootVisualElement.styleSheets.Add(commonStyles);
                m_labels = new IceCounterLabels(camera, document.rootVisualElement, cells);
            }
            catch { Dispose(); throw; }
        }

        public void Attach(int block, Transform root)
        {
            if (m_counts[block] <= 0) return;
            if (root.TryGetComponent(out ModularBlockView view)) m_shells.Attach(block, view, m_counts[block], m_counts[block]);
            m_labels.Attach(block, root, m_counts[block]);
        }

        public void Reproject() { m_labels?.Reproject(); }
        public void ReturnAll() { m_labels?.ReturnAll(); m_shells?.ReturnAll(); }
        public void Dispose()
        {
            m_labels?.Dispose();
            m_labels = null;
            m_shells?.Dispose();
            m_shells = null;
            if (m_documentRoot != null) Object.DestroyImmediate(m_documentRoot);
            m_documentRoot = null;
        }

        private static bool HasIceBlock(LevelData level, int blockCount)
        {
            for (int block = 0; block < blockCount; block++)
            {
                if (level.GetBlockAt(block).TryGetEffect(out IceEffect _)) return true;
            }

            return false;
        }
    }
}
