using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Controls
{
    /// <summary>
    /// Turns editor action buttons into the shared round green Add and red Delete icons (Editor/Controls/Icons,
    /// 64 x 64, Editor GUI import) in place of the + and × glyphs, so New and Delete read by colour as well as by
    /// shape. The round icon is the whole button: no box, border or hover square shows around it, and hover darkens
    /// the icon instead. A missing file leaves the ordinary button with the glyph as its text.
    /// </summary>
    internal static class ActionIcons
    {
        internal const string k_folder = "Assets/ColorBlockJam/Editor/Core/Controls/Icons/";
        internal const string k_imageName = "action-icon";
        internal const string k_add = "add";
        internal const string k_delete = "delete";
        internal const string k_addGlyph = "+";
        internal const string k_deleteGlyph = "×";
        // The icons are full colour; a tint only multiplies, so hover darkens them slightly on either editor skin.
        internal static readonly Color s_hoverTint = new Color(0.8f, 0.8f, 0.8f);

        public static void ShowAdd(Button button, float size)
        {
            Show(button, Load(k_add), k_addGlyph, size);
        }

        public static void ShowDelete(Button button, float size)
        {
            Show(button, Load(k_delete), k_deleteGlyph, size);
        }

        /// <summary>The icon texture, or null when the file is missing (no Console message).</summary>
        internal static Texture2D Load(string iconName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(k_folder + iconName + ".png");
        }

        // The caller owns the button's position and margins; with an icon, the button becomes a size x size square.
        private static void Show(Button button, Texture2D icon, string glyph, float size)
        {
            if (icon == null)
            {
                button.text = glyph;
                return;
            }

            button.text = string.Empty;
            button.style.width = size;
            button.style.height = size;
            button.style.alignSelf = Align.Center;
            button.style.paddingLeft = 0;
            button.style.paddingRight = 0;
            button.style.paddingTop = 0;
            button.style.paddingBottom = 0;
            // Inline styles win over the theme's :hover and :active states, so the button box never shows.
            button.style.backgroundColor = Color.clear;
            button.style.borderLeftWidth = 0;
            button.style.borderRightWidth = 0;
            button.style.borderTopWidth = 0;
            button.style.borderBottomWidth = 0;
            Image image = new Image
            {
                name = k_imageName,
                image = icon,
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            // An unsized Image measures its 64 px texture and would stretch the row, so it gets a fixed size.
            image.style.width = size;
            image.style.height = size;
            image.style.flexShrink = 0;
            button.Add(image);
            // Owner: this button; stop point: the callbacks are dropped with the button. A disabled button gets no
            // pointer events, and its theme opacity dims the icon.
            button.RegisterCallback<PointerEnterEvent>(_ => image.tintColor = s_hoverTint);
            button.RegisterCallback<PointerLeaveEvent>(_ => image.tintColor = Color.white);
        }
    }
}
