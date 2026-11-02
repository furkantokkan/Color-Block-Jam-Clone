using ColorBlockJam.Hub.Tabs;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Editor
{
    /// <summary>
    /// UI Toolkit inspector for the Home tab catalog (DEC-169): a short guide, the catalog's validation result as one
    /// message that names the broken entry, and the tab list. Edits go through the bound SerializedObject, so they
    /// record Undo and mark the asset dirty.
    /// </summary>
    [CustomEditor(typeof(HubNavigationCatalog))]
    public sealed class HubNavigationCatalogEditor : UnityEditor.Editor
    {
        internal const string StatusName = "navigation-catalog-status";
        internal const string ValidText = "Every tab is valid.";

        private const string k_tabsProperty = "m_tabs";
        private const string k_guide =
            "One entry per Home tab, in tab-bar order. Each ID needs a Button named 'tab-<id>' in UI/Hub/Home.uxml. "
            + "Available tabs open, Locked tabs stay locked, Level Gated tabs open when the player reaches the unlock "
            + "level, and Hidden tabs are turned off. A Menu ID opens the menu registered under that ID with "
            + "HubNavigator.RegisterMenu.";

        private HelpBox m_status;

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.Add(new HelpBox(k_guide, HelpBoxMessageType.Info));
            m_status = new HelpBox(string.Empty, HelpBoxMessageType.Info) { name = StatusName };
            root.Add(m_status);
            root.Add(new PropertyField(serializedObject.FindProperty(k_tabsProperty), "Tabs"));
            RefreshStatus();

            // Owner: the root element. Stop point: the tracking binding ends with the root when the inspector's panel
            // releases it.
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshStatus());
            return root;
        }

        private void RefreshStatus()
        {
            bool valid = ((HubNavigationCatalog)target).Validate(out string error);
            m_status.text = valid ? ValidText : error;
            m_status.messageType = valid ? HelpBoxMessageType.Info : HelpBoxMessageType.Error;
        }
    }
}
