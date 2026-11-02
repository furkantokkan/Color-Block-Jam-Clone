using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Search
{
    /// <summary>
    /// Opens a searchable popup instead of the plain menu when the user clicks a DropdownField.
    /// </summary>
    internal static class SearchableDropdown
    {
        /// <summary>
        /// Enables search on <paramref name="field"/>. Picking a choice sets the field value,
        /// so the field's change callbacks run exactly as they do for the plain menu.
        /// </summary>
        public static void Enable(DropdownField field)
        {
            // Trickle-down callbacks run before the field's own pointer handler, which opens the plain menu.
            field.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
        }

        private static void OnPointerDown(PointerDownEvent evt)
        {
            DropdownField field = (DropdownField)evt.currentTarget;
            VisualElement input = field.Q(className: DropdownField.inputUssClassName);
            if (evt.button != (int)MouseButton.LeftMouse || !input.worldBound.Contains(evt.position))
            {
                return;
            }

            // Stop the event before opening: PopupWindow.Show ends the current GUI pass with ExitGUI.
            evt.StopPropagation();
            ChoiceSearchPopup.Show(input.worldBound, field.choices, field.index, choice => field.value = choice);
        }
    }
}
