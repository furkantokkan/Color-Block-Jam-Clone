using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Shows each frozen block's remaining exit count as a UI Toolkit label projected over its footprint. Owned,
    /// pointer-transparent labels; only count, attachment, footprint and layout changes update them.
    /// </summary>
    public sealed class IceCounterLabels : IDisposable
    {
        private const float k_fontCellRatio = .75f;
        private const float k_twoDigitFontCellRatio = .65f;
        private const float k_outlineCellRatio = .04f;
        private const float k_fillStrokeCellRatio = .025f;
        // The counter anchors at the board kit's 0.72 top height (docs/art/board-kit/README.md), above every block body.
        private const float k_labelHeight = 0.72f;
        private static readonly string[] s_digits =
            { "", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10",
              "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };
        private static readonly int s_maxCount = s_digits.Length - 1;
        // A cyan inner fill gives the numeral its heavy rounded face while a wider navy outer stroke keeps a
        // consistent outline; the fill reuses the same face colour as its own outline (see CreateFillLabel).
        private static readonly Color s_faceColor = new Color(163f / 255f, 241f / 255f, 253f / 255f, 1f);
        private static readonly Color s_outlineColor = new Color(25f / 255f, 66f / 255f, 98f / 255f, 1f);
        private readonly Camera m_camera;
        private readonly VisualElement m_layer;
        private readonly Label[] m_labels;
        private readonly Label[] m_fillLabels;
        private readonly Transform[] m_targets;
        private readonly Vector3[] m_offsets;
        private readonly int[] m_counts;
        private bool m_disposed;

        /// <summary>One label per block, anchored on each block's footprint; the Editor preview and fixed boards use it.</summary>
        public IceCounterLabels(Camera camera, VisualElement parent, IReadOnlyList<Vector2Int>[] cells)
            : this(camera, parent, cells, cells?.Length ?? 0)
        {
        }

        /// <summary>
        /// Labels for capacity block IDs (LevelModel.TotalCellCount), so every Rocket part of a frozen block has one
        /// (B-10). The first cells.Length slots are anchored on the authored footprints; a part's slot gets its anchor
        /// from SetFootprint before it is attached. Every label is built here, outside Playing.
        /// </summary>
        public IceCounterLabels(Camera camera, VisualElement parent, IReadOnlyList<Vector2Int>[] cells, int capacity)
        {
            m_camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (cells == null) throw new ArgumentNullException(nameof(cells));
            if (capacity < cells.Length)
                throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must hold every authored block.");
            m_labels = new Label[capacity];
            m_fillLabels = new Label[capacity];
            m_targets = new Transform[capacity];
            m_offsets = new Vector3[capacity];
            m_counts = new int[capacity];
            m_layer = new VisualElement { name = "ice-counter-layer", pickingMode = PickingMode.Ignore };
            m_layer.AddToClassList("ice-counter-label-layer");
            for (int block = 0; block < capacity; block++)
            {
                if (block < cells.Length) m_offsets[block] = GetLabelOffset(cells[block]);
                Label label = CreateCountLabel(block);
                Label fill = CreateFillLabel(s_faceColor);
                label.Add(fill);
                m_fillLabels[block] = fill;
                m_labels[block] = label;
                m_layer.Add(label);
            }
            m_layer.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_layer.RegisterCallback<AttachToPanelEvent>(OnAttached);
            parent.Add(m_layer);
        }

        // Every inline property Reproject writes exists from here on, so a Rocket part's first projection during
        // Playing replaces values instead of growing the label's inline style list (B-10, AC-16).
        private static Label CreateCountLabel(int block)
        {
            Label label = new Label
            {
                name = "ice-count-" + block,
                pickingMode = PickingMode.Ignore,
                enableRichText = false
            };
            label.AddToClassList("ice-counter-label");
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.color = s_faceColor;
            label.style.unityTextOutlineColor = s_outlineColor;
            label.style.whiteSpace = WhiteSpace.NoWrap;
            label.style.display = DisplayStyle.None;
            label.style.visibility = Visibility.Visible;
            label.style.width = 0f;
            label.style.height = 0f;
            label.style.fontSize = 0f;
            label.style.unityTextOutlineWidth = 0f;
            label.style.left = 0f;
            label.style.top = 0f;
            return label;
        }

        // A cyan inner fill gives the numeral its heavy rounded face while the wider parent stroke leaves a
        // consistent navy outer edge; the fill's own outline reuses its face colour.
        private static Label CreateFillLabel(Color color)
        {
            Label fill = new Label { name = "ice-count-fill", pickingMode = PickingMode.Ignore, enableRichText = false };
            fill.AddToClassList("ice-counter-label__fill");
            fill.style.unityFontStyleAndWeight = FontStyle.Bold;
            fill.style.unityTextAlign = TextAnchor.MiddleCenter;
            fill.style.color = color;
            fill.style.whiteSpace = WhiteSpace.NoWrap;
            fill.style.unityTextOutlineColor = color;
            fill.style.fontSize = 0f;
            fill.style.unityTextOutlineWidth = 0f;
            return fill;
        }

        public VisualElement Layer => m_layer;
        public int ActiveCount { get; private set; }
        public int TextWriteCount { get; private set; }
        public Label GetLabel(int block) => m_labels[block];

        public void Attach(int block, Transform target, int remaining)
        {
            m_targets[block] = target;
            SetCount(block, remaining);
            Reproject();
        }

        public void SetCount(int block, int remaining)
        {
            if (remaining < 0 || remaining > s_maxCount) throw new ArgumentOutOfRangeException(nameof(remaining));
            int previous = m_counts[block];
            if (previous == remaining) return;
            m_counts[block] = remaining;
            if (previous == 0 && remaining > 0) ActiveCount++;
            else if (previous > 0 && remaining == 0) ActiveCount--;
            m_labels[block].text = s_digits[remaining];
            m_fillLabels[block].text = s_digits[remaining];
            TextWriteCount++;
            m_labels[block].style.display = remaining > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (remaining > 0 && (previous == 0 || (previous >= 10) != (remaining >= 10))) Reproject();
        }

        public void Detach(int block)
        {
            SetCount(block, 0);
            m_targets[block] = null;
        }

        public void ReturnAll()
        {
            for (int block = 0; block < m_targets.Length; block++) Detach(block);
        }

        /// <summary>
        /// Re-anchors a block's label on its new cells (relative to its root) after a Rocket recompose or before a
        /// part is attached, and reprojects it if shown (B-10). Called only on footprint changes; allocates nothing.
        /// </summary>
        public void SetFootprint(int block, IReadOnlyList<Vector2Int> cells)
        {
            m_offsets[block] = GetLabelOffset(cells);
            if (!m_disposed && m_layer.panel != null && m_camera != null) Reproject(block);
        }

        public void Reproject()
        {
            if (m_disposed || m_layer.panel == null || m_camera == null) return;
            for (int block = 0; block < m_targets.Length; block++) Reproject(block);
        }

        private void Reproject(int block)
        {
            if (m_counts[block] == 0 || m_targets[block] == null) return;
            Transform target = m_targets[block];
            Vector3 offset = m_offsets[block];
            Vector3 screen = m_camera.WorldToScreenPoint(target.TransformPoint(offset));
            Label label = m_labels[block];
            label.style.visibility = screen.z > 0f ? Visibility.Visible : Visibility.Hidden;
            Vector2 position = RuntimePanelUtils.ScreenToPanel(m_layer.panel,
                new Vector2(screen.x, Screen.height - screen.y));
            position = m_layer.WorldToLocal(position);
            Vector3 cellScreen = m_camera.WorldToScreenPoint(target.TransformPoint(offset + Vector3.right));
            Vector2 cellPosition = RuntimePanelUtils.ScreenToPanel(m_layer.panel,
                new Vector2(cellScreen.x, Screen.height - cellScreen.y));
            cellPosition = m_layer.WorldToLocal(cellPosition);
            float cellWidth = Vector2.Distance(position, cellPosition);
            label.style.width = cellWidth;
            label.style.height = cellWidth;
            float fontSize = cellWidth * (m_counts[block] >= 10 ? k_twoDigitFontCellRatio : k_fontCellRatio);
            label.style.fontSize = fontSize;
            m_fillLabels[block].style.fontSize = fontSize;
            label.style.unityTextOutlineWidth = cellWidth * (k_outlineCellRatio + k_fillStrokeCellRatio);
            m_fillLabels[block].style.unityTextOutlineWidth = cellWidth * k_fillStrokeCellRatio;
            label.style.left = position.x - cellWidth * .5f;
            label.style.top = position.y - cellWidth * .5f;
        }

        public static Vector3 GetLabelOffset(IReadOnlyList<Vector2Int> cells)
        {
            if (cells == null || cells.Count == 0) throw new ArgumentException("Pass a nonempty footprint.", nameof(cells));
            Vector2Int min = cells[0];
            Vector2Int max = cells[0];
            for (int cell = 1; cell < cells.Count; cell++)
            {
                min = Vector2Int.Min(min, cells[cell]);
                max = Vector2Int.Max(max, cells[cell]);
            }
            Vector2 centre = ((Vector2)min + max) * 0.5f;
            // Probe the occupied-cell union around shared edges. A solid 2x2 centre
            // stays centred, while a concave corner or a hole uses an occupied cell.
            const float probe = 0.001f;
            if (Contains(cells, centre + new Vector2(-probe, -probe)) &&
                Contains(cells, centre + new Vector2(-probe, probe)) &&
                Contains(cells, centre + new Vector2(probe, -probe)) &&
                Contains(cells, centre + new Vector2(probe, probe)))
                return new Vector3(centre.x, k_labelHeight, centre.y);
            Vector2Int chosen = cells[0];
            float best = ((Vector2)chosen - centre).sqrMagnitude;
            for (int cell = 1; cell < cells.Count; cell++)
            {
                float distance = ((Vector2)cells[cell] - centre).sqrMagnitude;
                if (distance > best) continue;
                if (distance == best && (cells[cell].y > chosen.y ||
                    (cells[cell].y == chosen.y && cells[cell].x >= chosen.x))) continue;
                best = distance;
                chosen = cells[cell];
            }
            return new Vector3(chosen.x, k_labelHeight, chosen.y);
        }

        private static bool Contains(IReadOnlyList<Vector2Int> cells, Vector2 point)
        {
            for (int cell = 0; cell < cells.Count; cell++)
                if (Mathf.Abs(point.x - cells[cell].x) <= 0.5f &&
                    Mathf.Abs(point.y - cells[cell].y) <= 0.5f) return true;
            return false;
        }

        private void OnGeometryChanged(GeometryChangedEvent e) { Reproject(); }
        private void OnAttached(AttachToPanelEvent e) { Reproject(); }

        public void Dispose()
        {
            if (m_disposed) return;
            m_disposed = true;
            ReturnAll();
            m_layer.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            m_layer.UnregisterCallback<AttachToPanelEvent>(OnAttached);
            m_layer.RemoveFromHierarchy();
        }
    }
}
