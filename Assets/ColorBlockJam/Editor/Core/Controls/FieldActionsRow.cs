using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Controls
{
    /// <summary>
    /// Row that stretches one field over the free width and keeps small action buttons, the green Add and red
    /// Delete icons, after it. Editor rows use it so every field-and-button row has the same button size and layout.
    /// </summary>
    internal class FieldActionsRow : VisualElement
    {
        private const float k_buttonWidth = 24f;
        private const float k_actionFontSize = 14f;
        // Just under the 20 px field height, so the round icon fills the row without growing it.
        private const float k_actionIconSize = 18f;
        private const float k_actionMargin = 2f;

        public FieldActionsRow(VisualElement field)
        {
            style.flexDirection = FlexDirection.Row;

            // A popup field in a row measures its basis from the full row width and cannot shrink by
            // default, which pushes the buttons outside the window. A zero basis keeps the row inside it.
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            field.style.flexBasis = 0;
            Add(field);
        }

        /// <summary>
        /// Adds a green Add button after the existing buttons and returns it, so the owner can enable it.
        /// </summary>
        public Button AddCreateButton(string name, string tooltip, Action action)
        {
            Button button = AddActionButton(name, tooltip, action);
            ActionIcons.ShowAdd(button, k_actionIconSize);
            return button;
        }

        /// <summary>
        /// Adds a red Delete button after the existing buttons and returns it, so the owner can enable it.
        /// </summary>
        public Button AddDeleteButton(string name, string tooltip, Action action)
        {
            Button button = AddActionButton(name, tooltip, action);
            ActionIcons.ShowDelete(button, k_actionIconSize);
            return button;
        }

        private Button AddActionButton(string name, string tooltip, Action action)
        {
            Button button = CreateButton(string.Empty, tooltip, action);
            button.name = name;
            // An even 4 px gap between the round icons, which have no button box to space them.
            button.style.marginLeft = k_actionMargin;
            button.style.marginRight = k_actionMargin;
            // A missing icon falls back to the thin + or × glyph, which reads lighter than the filled arrows.
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.fontSize = k_actionFontSize;
            Add(button);
            return button;
        }

        protected static Button CreateButton(string text, string tooltip, Action action)
        {
            Button button = new Button(action) { text = text, tooltip = tooltip };
            button.style.width = k_buttonWidth;
            button.style.flexShrink = 0;
            return button;
        }
    }
}
