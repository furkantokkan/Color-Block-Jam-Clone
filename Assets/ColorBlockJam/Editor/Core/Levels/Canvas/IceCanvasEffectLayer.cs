using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Draws each Ice block's frost overlay on the canvas and its snowflake-and-count badge below the block name
    /// (catalog BLK-03).
    /// </summary>
    internal sealed class IceCanvasEffectLayer : ILevelCanvasEffectLayer
    {
        // s_counts is indexed by ClearsToThaw, which LevelEffectRegistry's "clearsToThaw" key clamps to 1-20; the
        // bounds check in SetModel keeps a wider range the registry might later allow from indexing out of range.
        private static readonly string[] s_counts = { "", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14", "15", "16", "17", "18", "19", "20" };
        private static readonly Color s_badgeColor = new Color(.08f, .39f, .62f, .95f);
        private static readonly Color s_frostFillColor = new Color(.66f, .9f, 1f, .38f);
        private static readonly Color s_frostLineColor = new Color(.9f, 1f, 1f, .85f);
        private static readonly Color s_snowflakeColor = new Color(.86f, .98f, 1f);
        private const float k_badgeCornerRadius = 5f;
        // Badge height and width follow the cell size within these bounds.
        private const float k_badgeHeightCellRatio = .28f;
        private const float k_badgeMinHeight = 14f;
        private const float k_badgeMaxHeight = 24f;
        private const float k_badgeWidthHeightRatio = 2.3f;
        private const float k_badgeWidthCellRatio = .52f;
        private const float k_countFontCellRatio = .22f;
        private const float k_countFontMinSize = 9f;
        private const float k_countFontMaxSize = 16f;
        private const float k_frostLineWidth = 1.5f;
        // The crack lines' inset from the frost diamond's corners, so they read as a crack rather than an X.
        private const float k_crackInsetXRatio = .12f;
        private const float k_crackInsetYRatio = .14f;
        // The badge's snowflake: six arms from the centre, each forking near its tip.
        private const float k_snowflakeRadiusRatio = .34f;
        private const float k_snowflakeLineWidthRatio = .06f;
        private const float k_snowflakeMinLineWidth = 1f;
        private const float k_snowflakeForkRatio = .6f;
        private const float k_snowflakeForkSpreadRatio = .24f;
        private readonly List<FrostBadge> m_badges = new List<FrostBadge>();
        private LevelCanvasModel m_model = LevelCanvasModel.Empty;

        public void SetModel(LevelCanvasModel model, VisualElement parent)
        {
            m_model = model;
            while (m_badges.Count > model.BlockCount)
            {
                int last = m_badges.Count - 1;
                m_badges[last].RemoveFromHierarchy();
                m_badges.RemoveAt(last);
            }
            while (m_badges.Count < model.BlockCount)
            {
                var badge = new FrostBadge();
                parent.Add(badge);
                m_badges.Add(badge);
            }
            for (int block = 0; block < m_badges.Count; block++)
            {
                bool ice = model.GetBlock(block).TryGetEffect(out IceEffect effect);
                m_badges[block].Count.text = ice
                    ? (effect.ClearsToThaw >= 0 && effect.ClearsToThaw < s_counts.Length
                        ? s_counts[effect.ClearsToThaw]
                        : effect.ClearsToThaw.ToString())
                    : string.Empty;
                m_badges[block].style.display = ice ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        public void Position(LevelCanvasGeometry geometry)
        {
            for (int block = 0; block < m_badges.Count; block++)
            {
                FrostBadge badge = m_badges[block];
                float height = Mathf.Clamp(geometry.CellSize * k_badgeHeightCellRatio, k_badgeMinHeight, k_badgeMaxHeight);
                float width = Mathf.Max(height * k_badgeWidthHeightRatio, geometry.CellSize * k_badgeWidthCellRatio);
                badge.style.width = width;
                badge.style.height = height;
                badge.Count.style.left = height;
                badge.Count.style.fontSize = Mathf.Clamp(geometry.CellSize * k_countFontCellRatio, k_countFontMinSize, k_countFontMaxSize);
                badge.style.visibility = geometry.IsValid ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public VisualElement GetBadge(int block)
        {
            return block >= 0 && block < m_badges.Count && m_model.GetBlock(block).TryGetEffect(out IceEffect _)
                ? m_badges[block] : null;
        }

        public void Dispose()
        {
            for (int index = 0; index < m_badges.Count; index++) m_badges[index].RemoveFromHierarchy();
            m_badges.Clear();
        }

        // The shared canvas layout places this child badge beneath the block name.
        private sealed class FrostBadge : VisualElement
        {
            public FrostBadge()
            {
                name = "ice-authoring-badge";
                pickingMode = PickingMode.Ignore;
                style.position = UnityEngine.UIElements.Position.Absolute;
                style.backgroundColor = s_badgeColor;
                style.borderTopLeftRadius = style.borderTopRightRadius = k_badgeCornerRadius;
                style.borderBottomLeftRadius = style.borderBottomRightRadius = k_badgeCornerRadius;
                Count = new Label { name = "ice-authoring-count", pickingMode = PickingMode.Ignore };
                Count.style.position = UnityEngine.UIElements.Position.Absolute;
                Count.style.top = Count.style.bottom = Count.style.right = 0f;
                Count.style.marginLeft = Count.style.marginTop = Count.style.marginRight = Count.style.marginBottom = 0f;
                Count.style.paddingLeft = Count.style.paddingTop = Count.style.paddingRight = Count.style.paddingBottom = 0f;
                Count.style.unityFontStyleAndWeight = FontStyle.Bold;
                Count.style.unityTextAlign = TextAnchor.MiddleCenter;
                Count.style.color = Color.white;
                Add(Count);
                generateVisualContent += DrawSnowflake;
            }

            public Label Count { get; }

            private void DrawSnowflake(MeshGenerationContext context)
            {
                float size = contentRect.height;
                Vector2 centre = new Vector2(size * .5f, size * .5f);
                float radius = size * k_snowflakeRadiusRatio;
                Painter2D painter = context.painter2D;
                painter.strokeColor = s_snowflakeColor;
                painter.lineWidth = Mathf.Max(k_snowflakeMinLineWidth, size * k_snowflakeLineWidthRatio);
                painter.BeginPath();
                for (int arm = 0; arm < 6; arm++)
                {
                    float angle = arm * Mathf.PI / 3f;
                    Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 normal = new Vector2(-direction.y, direction.x);
                    Vector2 fork = centre + direction * radius * k_snowflakeForkRatio;
                    painter.MoveTo(centre);
                    painter.LineTo(centre + direction * radius);
                    painter.MoveTo(fork + (direction + normal) * radius * k_snowflakeForkSpreadRatio);
                    painter.LineTo(fork);
                    painter.LineTo(fork + (direction - normal) * radius * k_snowflakeForkSpreadRatio);
                }
                painter.Stroke();
            }
        }

        public void Draw(Painter2D painter, LevelCanvasGeometry geometry)
        {
            for (int block = 0; block < m_model.BlockCount; block++)
            {
                LevelCanvasBlock value = m_model.GetBlock(block);
                if (!value.TryGetEffect(out IceEffect _)) continue;
                for (int cell = 0; cell < value.CellCount; cell++)
                {
                    Rect rect = geometry.GetCellRect(value.GetCell(cell));
                    painter.fillColor = s_frostFillColor;
                    painter.BeginPath();
                    painter.MoveTo(rect.min);
                    painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                    painter.LineTo(rect.max);
                    painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                    painter.ClosePath();
                    painter.Fill();
                    painter.strokeColor = s_frostLineColor;
                    painter.lineWidth = k_frostLineWidth;
                    painter.BeginPath();
                    painter.MoveTo(new Vector2(rect.xMin + rect.width * k_crackInsetXRatio, rect.yMax - rect.height * k_crackInsetYRatio));
                    painter.LineTo(rect.center);
                    painter.LineTo(new Vector2(rect.xMax - rect.width * k_crackInsetXRatio, rect.yMin + rect.height * k_crackInsetYRatio));
                    painter.Stroke();
                }
            }
        }
    }
}
