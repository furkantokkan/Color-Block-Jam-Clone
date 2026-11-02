using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Loads the Level Editor's white-on-alpha tool and edge icons (Editor/Core/Levels/Icons, 64 x 64, Editor GUI
    /// import) and gives the neutral tint for the current editor skin. The icons are white so a tint can colour
    /// them; a missing icon returns null and callers show their text label instead.
    /// </summary>
    internal static class LevelEditorIcons
    {
        internal const string k_folder = "Assets/ColorBlockJam/Editor/Core/Levels/Icons/";
        internal const string k_cursor = "cursor";
        internal const string k_pen = "pen";
        internal const string k_eraser = "eraser";
        internal const string k_rotateLeft = "rotate-left";
        internal const string k_rotateRight = "rotate-right";
        internal const string k_reflectHorizontal = "reflect-horizontal";
        internal const string k_reflectVertical = "reflect-vertical";
        // Canvas edge controls (user icons of 2026-09-25, docs/art/level-editor-icons).
        internal const string k_arrowUp = "arrow-up";
        internal const string k_arrowDown = "arrow-down";
        internal const string k_arrowLeft = "arrow-left";
        internal const string k_arrowRight = "arrow-right";
        internal const string k_cornerTopLeft = "corner-top-left";
        internal const string k_cornerTopRight = "corner-top-right";
        internal const string k_cornerBottomLeft = "corner-bottom-left";
        internal const string k_cornerBottomRight = "corner-bottom-right";
        // The stopwatch beside the Level Editor's Timer field (user request of 2026-09-25).
        internal const string k_timer = "timer";
        // The colour actions under the Colors swatches (user icons of 2026-09-26, docs/art/level-editor-icons).
        internal const string k_colorSwitch = "color-switch";
        internal const string k_colorShuffle = "color-shuffle";

        /// <summary>Light grey on the dark skin, dark grey on the light skin.</summary>
        public static Color NeutralTint => EditorGUIUtility.isProSkin
            ? new Color(0.86f, 0.86f, 0.86f)
            : new Color(0.16f, 0.16f, 0.16f);

        /// <summary>The icon texture, or null when the file is missing (no Console message).</summary>
        public static Texture2D Load(string iconName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>(k_folder + iconName + ".png");
        }

        public static string GetToolIconName(LevelEditorTool tool)
        {
            switch (tool)
            {
                case LevelEditorTool.Pen:
                    return k_pen;
                case LevelEditorTool.Eraser:
                    return k_eraser;
                case LevelEditorTool.RotateCounterClockwise:
                    return k_rotateLeft;
                case LevelEditorTool.RotateClockwise:
                    return k_rotateRight;
                case LevelEditorTool.MirrorHorizontal:
                    return k_reflectHorizontal;
                case LevelEditorTool.MirrorVertical:
                    return k_reflectVertical;
                default:
                    return k_cursor;
            }
        }
    }
}
