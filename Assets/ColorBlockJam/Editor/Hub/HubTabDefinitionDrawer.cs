using ColorBlockJam.Hub.Tabs;
using Cysharp.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Editor
{
    /// <summary>
    /// Draws one catalog tab as a foldout titled with its ID and availability, and shows only the fields that apply:
    /// Unlock Level for Level Gated tabs, the locked look and label choice for tabs that can be locked (DEC-169).
    /// </summary>
    [CustomPropertyDrawer(typeof(HubTabDefinition))]
    public sealed class HubTabDefinitionDrawer : PropertyDrawer
    {
        internal const string UnlockLevelName = "tab-unlock-level";
        internal const string LockedLookName = "tab-locked-look";
        internal const string LabelWhileLockedName = "tab-label-while-locked";

        private const string k_idProperty = "m_id";
        private const string k_labelProperty = "m_label";
        private const string k_iconProperty = "m_icon";
        private const string k_availabilityProperty = "m_availability";
        private const string k_unlockLevelProperty = "m_unlockLevel";
        private const string k_lockedLookProperty = "m_lockedLook";
        private const string k_labelWhileLockedProperty = "m_showLabelWhileLocked";
        private const string k_menuIdProperty = "m_menuId";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            SerializedProperty id = property.FindPropertyRelative(k_idProperty);
            SerializedProperty availability = property.FindPropertyRelative(k_availabilityProperty);
            var foldout = new Foldout { value = false };

            foldout.Add(new PropertyField(id, "ID"));
            foldout.Add(new PropertyField(property.FindPropertyRelative(k_labelProperty), "Label"));
            foldout.Add(new PropertyField(property.FindPropertyRelative(k_iconProperty), "Icon"));
            var availabilityField = new PropertyField(availability, "Availability");
            foldout.Add(availabilityField);
            var unlockLevel = new PropertyField(property.FindPropertyRelative(k_unlockLevelProperty), "Unlock Level")
            {
                name = UnlockLevelName
            };
            foldout.Add(unlockLevel);
            var lockedLook = new PropertyField(property.FindPropertyRelative(k_lockedLookProperty), "Locked Look")
            {
                name = LockedLookName
            };
            foldout.Add(lockedLook);
            var labelWhileLocked = new PropertyField(property.FindPropertyRelative(k_labelWhileLockedProperty),
                "Show Label While Locked")
            {
                name = LabelWhileLockedName
            };
            foldout.Add(labelWhileLocked);
            foldout.Add(new PropertyField(property.FindPropertyRelative(k_menuIdProperty), "Menu ID"));

            Refresh(foldout, id, availability, unlockLevel, lockedLook, labelWhileLocked);

            // Owner: the foldout. Stop point: the callbacks end with the foldout when the inspector releases it.
            availabilityField.RegisterValueChangeCallback(_ => Refresh(foldout, id, availability, unlockLevel, lockedLook, labelWhileLocked));
            foldout.TrackPropertyValue(id, _ => Refresh(foldout, id, availability, unlockLevel, lockedLook, labelWhileLocked));
            return foldout;
        }

        private static void Refresh(Foldout foldout, SerializedProperty id, SerializedProperty availability,
            VisualElement unlockLevel, VisualElement lockedLook, VisualElement labelWhileLocked)
        {
            var value = (HubTabAvailability)availability.intValue;
            bool canLock = value == HubTabAvailability.Locked || value == HubTabAvailability.LevelGated;
            unlockLevel.style.display = value == HubTabAvailability.LevelGated ? DisplayStyle.Flex : DisplayStyle.None;
            lockedLook.style.display = canLock ? DisplayStyle.Flex : DisplayStyle.None;
            labelWhileLocked.style.display = canLock ? DisplayStyle.Flex : DisplayStyle.None;
            string title = string.IsNullOrEmpty(id.stringValue) ? "(no ID)" : id.stringValue;
            foldout.text = ZString.Concat(title, " — ", ObjectNames.NicifyVariableName(value.ToString()));
        }
    }
}
