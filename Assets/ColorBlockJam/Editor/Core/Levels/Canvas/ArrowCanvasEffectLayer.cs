using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Draws each Arrow block's canvas badge (catalog BLK-01, arrow-markers revision 1): a white double-headed arrow
    /// with a dark outline, centred on the block's bounding box along its axis through the shared ArrowMarkerRun rule,
    /// and sized in cells so it reads at every zoom. The block label draws above it with its own dark outline.
    /// </summary>
    internal sealed class ArrowCanvasEffectLayer : ILevelCanvasEffectLayer
    {
        private static readonly Color s_fillColor = Color.white;
        private static readonly Color s_outlineColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        private const float k_outlineWidth = 1.5f;
        private const float k_inset = 0.14f;
        private const float k_headLength = 0.26f;
        private const float k_headHalfWidth = 0.22f;
        private const float k_shaftHalfWidth = 0.07f;

        // Each block's marker in absolute board cells, rebuilt per model; none for a block without a valid Arrow.
        private readonly List<ArrowMarker> m_markers = new List<ArrowMarker>();
        // Reused for one block's cells while the markers are rebuilt.
        private readonly List<Vector2Int> m_cells = new List<Vector2Int>();

        /// <summary>Arrow badges the last repaint drew, one per block with a valid Arrow effect.</summary>
        public int DrawnBadgeCount { get; private set; }

        public void SetModel(LevelCanvasModel model, VisualElement parent)
        {
            m_markers.Clear();
            for (int block = 0; block < model.BlockCount; block++)
            {
                m_markers.Add(CreateMarker(model.GetBlock(block), m_cells));
            }
        }

        /// <summary>The marker of the block at this index in the current model, or none.</summary>
        public ArrowMarker GetMarker(int block)
        {
            return block >= 0 && block < m_markers.Count ? m_markers[block] : default;
        }

        // The badge is drawn on the canvas, so nothing needs sizing.
        public void Position(LevelCanvasGeometry geometry)
        {
        }

        // The badge sits on the block itself, not in the badge row below its name.
        public VisualElement GetBadge(int block)
        {
            return null;
        }

        // Each block's badge, in block order. The canvas draws this layer above its grid lines.
        public void Draw(Painter2D painter, LevelCanvasGeometry geometry)
        {
            int drawn = 0;
            for (int block = 0; block < m_markers.Count; block++)
            {
                if (m_markers[block].HasMarker)
                {
                    DrawArrowBadge(painter, GetArrowBadge(geometry, m_markers[block]));
                    drawn++;
                }
            }

            DrawnBadgeCount = drawn;
        }

        public void Dispose()
        {
            m_markers.Clear();
            DrawnBadgeCount = 0;
        }

        /// <summary>The marker of a canvas block's Arrow effect over its absolute board cells, or none.</summary>
        internal static ArrowMarker CreateMarker(LevelCanvasBlock block)
        {
            return CreateMarker(block, new List<Vector2Int>(block.CellCount));
        }

        private static ArrowMarker CreateMarker(LevelCanvasBlock block, List<Vector2Int> cells)
        {
            if (!block.TryGetEffect(out ArrowEffect arrow))
            {
                return default;
            }

            cells.Clear();
            for (int index = 0; index < block.CellCount; index++)
            {
                cells.Add(block.GetCell(index));
            }

            return ArrowMarkerRun.Create(cells, arrow.Axis);
        }

        /// <summary>
        /// Screen geometry of one Arrow badge: tip A at the low end of the axis (left, or the bottom of the board) and
        /// tip B at the high end, with head and shaft sizes in pixels.
        /// </summary>
        internal readonly struct ArrowBadge
        {
            public ArrowBadge(Vector2 tipA, Vector2 tipB, float headLength, float headHalfWidth, float shaftHalfWidth)
            {
                TipA = tipA;
                TipB = tipB;
                HeadLength = headLength;
                HeadHalfWidth = headHalfWidth;
                ShaftHalfWidth = shaftHalfWidth;
            }

            public Vector2 TipA { get; }
            public Vector2 TipB { get; }
            public float HeadLength { get; }
            public float HeadHalfWidth { get; }
            public float ShaftHalfWidth { get; }
            public float Length => Vector2.Distance(TipA, TipB);
        }

        /// <summary>
        /// The badge of a marker: centred on the block's bounding-box centre and as long as the marker's clamped length
        /// (the Arrow_N the preview and the game show), with both tips inset from its ends.
        /// </summary>
        internal static ArrowBadge GetArrowBadge(LevelCanvasGeometry geometry, ArrowMarker marker)
        {
            float cell = geometry.CellSize;
            // Board cell (x, y) is drawn at the centre of cell 0 plus (x, -y) cells: screen y grows downward.
            Vector2 centre = geometry.GetCellRect(Vector2Int.zero).center
                             + new Vector2(marker.Center.x * cell, -marker.Center.y * cell);
            // Vector2.down points up the screen, toward higher board rows.
            Vector2 direction = marker.Axis == LevelEffectAxis.Horizontal ? Vector2.right : Vector2.down;
            float half = (0.5f * marker.Variant - k_inset) * cell;
            float head = Mathf.Min(k_headLength * cell, 0.9f * half);
            return new ArrowBadge(centre - direction * half, centre + direction * half, head,
                k_headHalfWidth * cell, k_shaftHalfWidth * cell);
        }

        /// <summary>
        /// Fills the badge white and outlines it dark as one double-headed arrow. Only painter calls, so a repaint
        /// allocates nothing.
        /// </summary>
        internal static void DrawArrowBadge(Painter2D painter, ArrowBadge badge)
        {
            float length = badge.Length;
            if (length <= 0f)
            {
                return;
            }

            Vector2 direction = (badge.TipB - badge.TipA) / length;
            painter.fillColor = s_fillColor;
            painter.strokeColor = s_outlineColor;
            painter.lineWidth = k_outlineWidth;
            AddArrowPath(painter, badge.TipA, badge.TipB, direction, badge);
            painter.Fill();
            painter.Stroke();
        }

        // A double-headed arrow from tip a to tip b as one closed outline.
        private static void AddArrowPath(Painter2D painter, Vector2 a, Vector2 b, Vector2 direction, ArrowBadge badge)
        {
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 head = direction * badge.HeadLength;
            Vector2 wide = normal * badge.HeadHalfWidth;
            Vector2 narrow = normal * badge.ShaftHalfWidth;
            painter.BeginPath();
            painter.MoveTo(a);
            painter.LineTo(a + head + wide);
            painter.LineTo(a + head + narrow);
            painter.LineTo(b - head + narrow);
            painter.LineTo(b - head + wide);
            painter.LineTo(b);
            painter.LineTo(b - head - wide);
            painter.LineTo(b - head - narrow);
            painter.LineTo(a + head - narrow);
            painter.LineTo(a + head - wide);
            painter.ClosePath();
        }
    }
}
