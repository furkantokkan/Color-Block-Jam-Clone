using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor canvas's pointer badge: a small dark frame showing the active tool's icon that follows the
    /// pointer through style.translate only, so moving it never repaints the canvas.
    /// </summary>
    internal sealed class LevelCanvasToolBadge
    {
        internal const string ElementName = "level-canvas-tool-badge";
        // The badge sits right of and below the pointer, clear of the system arrow's tip.
        internal const float PointerOffset = 14f;
        private const float k_size = 24f;
        private const float k_iconSize = 18f;
        private static readonly Color s_frameColor = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        private static readonly Color s_borderColor = new Color(1f, 1f, 1f, 0.9f);

        // Tool icons, loaded once per badge and indexed by LevelEditorTool; a missing icon stays null.
        private readonly Texture2D[] m_icons = new Texture2D[Enum.GetValues(typeof(LevelEditorTool)).Length];

        /// <summary>Builds the hidden badge; the canvas adds <see cref="Element"/> as its child.</summary>
        public LevelCanvasToolBadge()
        {
            Element = new VisualElement
            {
                name = ElementName,
                pickingMode = PickingMode.Ignore,
                usageHints = UsageHints.DynamicTransform
            };
            Element.style.position = Position.Absolute;
            Element.style.left = 0;
            Element.style.top = 0;
            Element.style.width = k_size;
            Element.style.height = k_size;
            Element.style.alignItems = Align.Center;
            Element.style.justifyContent = Justify.Center;
            Element.style.backgroundColor = s_frameColor;
            Element.style.borderLeftWidth = 1;
            Element.style.borderRightWidth = 1;
            Element.style.borderTopWidth = 1;
            Element.style.borderBottomWidth = 1;
            Element.style.borderLeftColor = s_borderColor;
            Element.style.borderRightColor = s_borderColor;
            Element.style.borderTopColor = s_borderColor;
            Element.style.borderBottomColor = s_borderColor;
            Element.style.borderTopLeftRadius = 3;
            Element.style.borderTopRightRadius = 3;
            Element.style.borderBottomLeftRadius = 3;
            Element.style.borderBottomRightRadius = 3;
            Element.style.display = DisplayStyle.None;
            Icon = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleToFit };
            Icon.style.width = k_iconSize;
            Icon.style.height = k_iconSize;
            Element.Add(Icon);
        }

        /// <summary>The square frame; moved only through style.translate.</summary>
        internal VisualElement Element { get; }
        internal Image Icon { get; }

        /// <summary>Shows the badge at the pointer plus its offset.</summary>
        public void Move(Vector2 localPoint)
        {
            Element.style.translate = new Translate(localPoint.x + PointerOffset, localPoint.y + PointerOffset, 0f);
            Element.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            Element.style.display = DisplayStyle.None;
        }

        /// <summary>
        /// Shows the tool's icon. The frame is dark on both skins, so the icon uses the light tint unless the Pen
        /// colours it with penColor.
        /// </summary>
        public void SetLook(LevelEditorTool tool, Color penColor)
        {
            int index = (int)tool;
            if (m_icons[index] == null)
            {
                m_icons[index] = LevelEditorIcons.Load(LevelEditorIcons.GetToolIconName(tool));
            }

            Icon.image = m_icons[index];
            Icon.tintColor = tool == LevelEditorTool.Pen ? penColor : Color.white;
        }
    }
}
