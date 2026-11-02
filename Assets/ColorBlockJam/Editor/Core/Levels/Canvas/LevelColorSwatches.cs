using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Art;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Colors group's 11 named swatches, one per gate colour. A click raises <see cref="ColorPicked"/>; the
    /// swatch of the current tool colour is outlined white and a hovered swatch light grey. The swatches sit in
    /// colour-order position, so the framed badge at each swatch's top-right reads 1 to 11 in the grid whatever the
    /// order; each tooltip names the Pen key for its swatch. It holds no draft state.
    /// </summary>
    internal sealed class LevelColorSwatches : VisualElement
    {
        internal const string k_name = "color-swatches";
        internal const string k_swatchClass = "level-color-swatch";
        internal const string k_badgeClass = "level-color-swatch-badge";
        private const float k_badgeHeight = 14f;
        private const float k_badgeMinWidth = 13f;
        private const float k_badgeFontSize = 10f;
        // Prebuilt badge texts, so a refresh formats nothing.
        private static readonly string[] s_positionTexts = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11" };
        // Each swatch's tooltip at each order position ("Red: key 3 with the Pen ([ / ] cycles)"), indexed
        // colour * count + position and built once, so an order change formats nothing.
        private static readonly string[] s_keyTooltips = CreateKeyTooltips();
        private static readonly Color s_badgeFill = new Color(0.1f, 0.1f, 0.1f, 0.85f);
        private static readonly Color s_badgeFrame = new Color(1f, 1f, 1f, 0.55f);
        // 40 px swatches in two rows on the user's request of 2026-09-26 ("40 px, iki satır"): six with 4 px gaps take
        // 264 px and a seventh would need 308, so a 330 px Level Editor tools pane always wraps them 6 + 5, with or
        // without its scrollbar (289 or 302 px row).
        private const float k_swatchSize = 40f;
        private const float k_swatchGap = 4f;
        private const float k_borderWidth = 2f;
        internal static readonly Color s_selectedBorder = Color.white;
        // The same hover grey as the Blocks tiles: between idle and selected, so it never reads as selected.
        internal static readonly Color s_hoverBorder = LevelShapePalette.s_hoverBorder;
        internal static readonly Color s_idleBorder = new Color(0f, 0f, 0f, 0.45f);

        private readonly List<VisualElement> m_swatches = new List<VisualElement>(LevelEditorToolState.k_colorCount);
        private readonly List<Label> m_badges = new List<Label>(LevelEditorToolState.k_colorCount);
        // -1 until the window first sets the tool colour, so no swatch is outlined before then.
        private int m_selectedIndex = -1;
        private int m_hoveredIndex = -1;

        public LevelColorSwatches()
        {
            name = k_name;
            style.flexDirection = FlexDirection.Row;
            style.flexWrap = Wrap.Wrap;
            for (int index = 0; index < LevelEditorToolState.k_colorCount; index++)
            {
                PaletteColor color = (PaletteColor)index;
                VisualElement swatch = new VisualElement
                {
                    name = "swatch-" + color,
                    tooltip = $"{color} ([ / ] cycles)"
                };
                swatch.AddToClassList(k_swatchClass);
                swatch.style.width = k_swatchSize;
                swatch.style.height = k_swatchSize;
                swatch.style.marginRight = k_swatchGap;
                swatch.style.marginBottom = k_swatchGap;
                SetBorder(swatch, s_idleBorder);
                // Owner: this swatch; stop point: it is dropped with the window GUI.
                swatch.AddManipulator(new Clickable(() => ColorPicked?.Invoke(color)));
                int swatchIndex = index;
                swatch.RegisterCallback<PointerEnterEvent>(_ => SetHovered(swatchIndex, true));
                swatch.RegisterCallback<PointerLeaveEvent>(_ => SetHovered(swatchIndex, false));
                Label badge = CreateBadge(color);
                swatch.Add(badge);
                m_badges.Add(badge);
                m_swatches.Add(swatch);
                Add(swatch);
            }
        }

        /// <summary>Raised when a swatch is clicked. Subscriber: the Level Editor window.</summary>
        public event Action<PaletteColor> ColorPicked;

        internal VisualElement GetSwatch(PaletteColor color)
        {
            return m_swatches[(int)color];
        }

        internal Label GetBadge(PaletteColor color)
        {
            return m_badges[(int)color];
        }

        /// <summary>
        /// Moves each swatch to its 1-based position in the colour order and shows that position in its badge, so
        /// after Color Shuffle or Color Switch the colours move and the numbers stay 1 to 11 in the grid (user
        /// request of 2026-09-26). Until the first call every badge stays hidden
        /// (TASK-20260926-145804549-da16f02f). Each tooltip names the Pen key for that position
        /// (TASK-20260926-180414067-b923c8a9).
        /// </summary>
        public void SetOrder(LevelColorOrder order)
        {
            for (int index = 0; index < m_badges.Count; index++)
            {
                int position = order.IndexOf((PaletteColor)index);
                Label badge = m_badges[index];
                badge.text = s_positionTexts[position];
                badge.style.display = DisplayStyle.Flex;
                m_swatches[index].tooltip = s_keyTooltips[index * LevelEditorToolState.k_colorCount + position];
            }

            // Each swatch in order goes to the end, so the children end up in order position.
            for (int position = 0; position < m_swatches.Count; position++)
            {
                m_swatches[(int)order.GetColorAt(position)].BringToFront();
            }
        }

        /// <summary>Paints each swatch with the colour the canvas draws for it.</summary>
        public void SetColors(Func<PaletteColor, Color> getColor)
        {
            for (int index = 0; index < m_swatches.Count; index++)
            {
                m_swatches[index].style.backgroundColor = getColor((PaletteColor)index);
            }
        }

        /// <summary>Outlines the swatch of the current tool colour.</summary>
        public void SetSelected(PaletteColor color)
        {
            m_selectedIndex = (int)color;
            RefreshBorders();
        }

        // Pointer enter and leave land here; leave clears only its own swatch, so an enter on the next swatch that
        // arrives first is kept.
        private void SetHovered(int index, bool hovered)
        {
            if (hovered)
            {
                m_hoveredIndex = index;
            }
            else if (m_hoveredIndex == index)
            {
                m_hoveredIndex = -1;
            }

            RefreshBorders();
        }

        // Every border is re-derived from the selected and hovered swatch, so neither state can leave a stale
        // colour behind when the other changes.
        private void RefreshBorders()
        {
            for (int index = 0; index < m_swatches.Count; index++)
            {
                Color border = index == m_selectedIndex ? s_selectedBorder
                    : index == m_hoveredIndex ? s_hoverBorder
                    : s_idleBorder;
                SetBorder(m_swatches[index], border);
            }
        }

        // A small framed number in the swatch's top-right corner, hidden until SetOrder. It ignores the pointer, so the
        // swatch keeps its click, hover and tooltip, and its absolute position leaves the swatch's size unchanged.
        private static Label CreateBadge(PaletteColor color)
        {
            Label badge = new Label { name = "badge-" + color, pickingMode = PickingMode.Ignore };
            badge.AddToClassList(k_badgeClass);
            badge.style.display = DisplayStyle.None;
            badge.style.position = Position.Absolute;
            badge.style.top = 0;
            badge.style.right = 0;
            badge.style.height = k_badgeHeight;
            badge.style.minWidth = k_badgeMinWidth;
            badge.style.marginLeft = 0;
            badge.style.marginRight = 0;
            badge.style.marginTop = 0;
            badge.style.marginBottom = 0;
            badge.style.paddingLeft = 1;
            badge.style.paddingRight = 1;
            badge.style.paddingTop = 0;
            badge.style.paddingBottom = 0;
            badge.style.fontSize = k_badgeFontSize;
            badge.style.unityTextAlign = TextAnchor.MiddleCenter;
            badge.style.color = Color.white;
            badge.style.backgroundColor = s_badgeFill;
            badge.style.borderLeftWidth = 1;
            badge.style.borderRightWidth = 1;
            badge.style.borderTopWidth = 1;
            badge.style.borderBottomWidth = 1;
            badge.style.borderLeftColor = s_badgeFrame;
            badge.style.borderRightColor = s_badgeFrame;
            badge.style.borderTopColor = s_badgeFrame;
            badge.style.borderBottomColor = s_badgeFrame;
            badge.style.borderTopLeftRadius = 2;
            badge.style.borderTopRightRadius = 2;
            badge.style.borderBottomLeftRadius = 2;
            badge.style.borderBottomRightRadius = 2;
            return badge;
        }

        private static string[] CreateKeyTooltips()
        {
            const int count = LevelEditorToolState.k_colorCount;
            string[] tooltips = new string[count * count];
            for (int color = 0; color < count; color++)
            {
                for (int position = 0; position < count; position++)
                {
                    tooltips[color * count + position] = (PaletteColor)color + ": key " +
                                                         LevelColorKeys.GetKeyLabel(position + 1) +
                                                         " with the Pen ([ / ] cycles)";
                }
            }

            return tooltips;
        }

        private static void SetBorder(VisualElement swatch, Color color)
        {
            swatch.style.borderLeftWidth = k_borderWidth;
            swatch.style.borderRightWidth = k_borderWidth;
            swatch.style.borderTopWidth = k_borderWidth;
            swatch.style.borderBottomWidth = k_borderWidth;
            swatch.style.borderLeftColor = color;
            swatch.style.borderRightColor = color;
            swatch.style.borderTopColor = color;
            swatch.style.borderBottomColor = color;
        }
    }
}
