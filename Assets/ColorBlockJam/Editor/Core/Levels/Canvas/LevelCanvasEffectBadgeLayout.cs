using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>Reused effect rows and columns, anchored beneath each block's actual name position.</summary>
    internal sealed class LevelCanvasEffectBadgeLayout : IDisposable
    {
        private const float k_gap = 3f;
        private readonly List<BlockBadges> m_blocks = new List<BlockBadges>();

        public void SetModel(LevelCanvasModel model, VisualElement parent)
        {
            while (m_blocks.Count > model.BlockCount)
            {
                int last = m_blocks.Count - 1;
                m_blocks[last].Row.RemoveFromHierarchy();
                m_blocks.RemoveAt(last);
            }
            while (m_blocks.Count < model.BlockCount)
            {
                var block = new BlockBadges();
                parent.Add(block.Row);
                m_blocks.Add(block);
            }
        }

        public void Position(int index, LevelCanvasBlock block, LevelCanvasGeometry geometry,
            Vector2 nameCentre, float nameHeight, ILevelCanvasEffectLayer[] layers)
        {
            BlockBadges owner = m_blocks[index];
            owner.Badges.Clear();
            float width = 0f;
            float height = 0f;
            for (int layer = 0; layer < layers.Length; layer++)
            {
                VisualElement badge = layers[layer].GetBadge(index);
                if (badge == null) continue;
                owner.Badges.Add(badge);
                width = Mathf.Max(width, badge.style.width.value.value);
                height = Mathf.Max(height, badge.style.height.value.value);
            }
            owner.Row.style.display = geometry.IsValid && owner.Badges.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            for (int column = 0; column < owner.Columns.Count; column++)
            {
                owner.Columns[column].Clear();
                owner.Columns[column].style.display = DisplayStyle.None;
            }
            if (!geometry.IsValid || owner.Badges.Count == 0 || block.CellCount == 0) return;

            Rect bounds = geometry.GetCellRect(block.GetCell(0));
            for (int cell = 1; cell < block.CellCount; cell++)
            {
                Rect rect = geometry.GetCellRect(block.GetCell(cell));
                bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                    Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax));
            }
            float top = nameCentre.y + nameHeight * .5f + k_gap;
            float bottom = bounds.yMax - 2f;
            bool besideName = top + height > bottom;
            if (besideName) top = bounds.yMin + 2f;
            owner.Row.style.left = besideName ? nameCentre.x + nameHeight * .5f + k_gap : nameCentre.x - width * .5f;
            owner.Row.style.top = top;
            int rows = Mathf.Max(1, Mathf.FloorToInt((bottom - top + k_gap) / (height + k_gap)));
            int count = (owner.Badges.Count + rows - 1) / rows;
            while (owner.Columns.Count < count)
            {
                var column = new VisualElement { name = "effect-badge-column", pickingMode = PickingMode.Ignore };
                column.style.flexDirection = FlexDirection.Column;
                column.style.flexShrink = 0;
                column.style.alignItems = Align.Center;
                column.style.marginRight = k_gap;
                owner.Row.Add(column);
                owner.Columns.Add(column);
            }
            for (int badgeIndex = 0; badgeIndex < owner.Badges.Count; badgeIndex++)
            {
                VisualElement badge = owner.Badges[badgeIndex];
                VisualElement column = owner.Columns[badgeIndex / rows];
                column.style.display = DisplayStyle.Flex;
                column.style.width = width;
                badge.style.position = UnityEngine.UIElements.Position.Relative;
                badge.style.left = StyleKeyword.Auto;
                badge.style.top = StyleKeyword.Auto;
                badge.style.flexShrink = 0;
                badge.style.marginBottom = badgeIndex % rows == rows - 1 || badgeIndex == owner.Badges.Count - 1 ? 0f : k_gap;
                badge.pickingMode = PickingMode.Ignore;
                column.Add(badge);
            }
        }

        public void Hide(int index)
        {
            m_blocks[index].Row.style.display = DisplayStyle.None;
        }

        public void Dispose()
        {
            for (int index = 0; index < m_blocks.Count; index++) m_blocks[index].Row.RemoveFromHierarchy();
            m_blocks.Clear();
        }

        private sealed class BlockBadges
        {
            public readonly VisualElement Row = new VisualElement { name = "effect-badge-row", pickingMode = PickingMode.Ignore };
            public readonly List<VisualElement> Columns = new List<VisualElement>();
            public readonly List<VisualElement> Badges = new List<VisualElement>();

            public BlockBadges()
            {
                Row.style.position = UnityEngine.UIElements.Position.Absolute;
                Row.style.flexDirection = FlexDirection.Row;
                Row.style.alignItems = Align.FlexStart;
            }
        }
    }
}
