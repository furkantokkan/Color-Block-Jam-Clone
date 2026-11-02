using ColorBlockJam.Runtime.Art;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// The Game Palette Inspector: every cell field shows a square of the atlas colour it names, so a designer can
    /// check the enum-to-cell mapping at a glance (DEC-202), and a line reports what Validate refuses.
    /// </summary>
    [CustomEditor(typeof(GamePalette))]
    public sealed class GamePaletteEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var palette = (GamePalette)target;
            var status = new HelpBox(string.Empty, HelpBoxMessageType.Warning);

            void Refresh()
            {
                bool valid = palette.Validate(out string error);
                status.text = error;
                status.style.display = valid ? DisplayStyle.None : DisplayStyle.Flex;
            }

            var script = new PropertyField(serializedObject.FindProperty("m_script"));
            script.SetEnabled(false);
            root.Add(script);
            root.Add(new PropertyField(serializedObject.FindProperty("m_atlas"), "Colour Atlas"));
            root.Add(new PropertyField(serializedObject.FindProperty("m_colors"), "Colours"));
            SerializedProperty arrow = serializedObject.FindProperty("m_arrowCell");
            root.Add(PaletteCellPreview.CreateRow(arrow, new PropertyField(arrow, "Arrow Cell")));
            root.Add(status);
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            Refresh();
            return root;
        }
    }

    /// <summary>Draws a palette colour entry as its type, its cells and a preview square per cell.</summary>
    [CustomPropertyDrawer(typeof(GamePalette.Entry))]
    public sealed class GamePaletteEntryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var column = new VisualElement();
            column.Add(new PropertyField(property.FindPropertyRelative("m_type"), "Type"));
            SerializedProperty cell = property.FindPropertyRelative("m_cell");
            SerializedProperty light = property.FindPropertyRelative("m_lightCell");
            column.Add(PaletteCellPreview.CreateRow(cell, new PropertyField(cell, "Cell")));
            column.Add(PaletteCellPreview.CreateRow(light, new PropertyField(light, "Light Cell")));
            return column;
        }
    }

    /// <summary>A cell field followed by a square filled with the atlas pixel that cell names.</summary>
    internal static class PaletteCellPreview
    {
        private const float k_size = 18f;
        private static readonly Color s_missing = new Color(.3f, .3f, .3f);

        public static VisualElement CreateRow(SerializedProperty cell, PropertyField field)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            field.style.flexGrow = 1;
            var square = new VisualElement
            {
                style =
                {
                    width = k_size, height = k_size, marginLeft = 6, flexShrink = 0,
                    borderTopWidth = 1, borderBottomWidth = 1, borderLeftWidth = 1, borderRightWidth = 1,
                    borderTopColor = Color.black, borderBottomColor = Color.black,
                    borderLeftColor = Color.black, borderRightColor = Color.black,
                },
            };
            row.Add(field);
            row.Add(square);

            // The atlas field and the cell both change the colour, so the square follows the whole palette.
            var palette = (GamePalette)cell.serializedObject.targetObject;
            string path = cell.propertyPath;
            void Refresh(SerializedObject data)
            {
                SerializedProperty current = data.FindProperty(path);
                if (current == null) return;
                Vector2Int value = current.vector2IntValue;
                bool readable = palette.Atlas != null && palette.Atlas.isReadable && PaletteAtlas.Contains(value);
                square.style.backgroundColor = readable ? palette.GetCellColor(value) : s_missing;
                square.tooltip = readable
                    ? "Atlas cell " + value
                    : "No colour: the atlas is missing or not readable, or the cell is outside it.";
            }

            square.TrackSerializedObjectValue(cell.serializedObject, Refresh);
            Refresh(cell.serializedObject);
            return row;
        }
    }
}
