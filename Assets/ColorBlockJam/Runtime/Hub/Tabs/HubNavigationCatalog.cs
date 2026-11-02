using System;
using UnityEngine;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// Ordered list of every Home tab, in tab-bar order: the one place a designer opens, locks, level-gates or hides a
    /// tab and names the menu it opens (DEC-169). It stores no player state; adding a tab needs an entry here and a
    /// <c>tab-&lt;id&gt;</c> slot in Home.uxml, with no code change.
    /// </summary>
    [CreateAssetMenu(
        fileName = "HubNavigationCatalog",
        menuName = "Color Block Jam/Hub/Navigation Catalog")]
    public sealed class HubNavigationCatalog : ScriptableObject
    {
        [SerializeField] private HubTabDefinition[] m_tabs;

        public int Count => m_tabs?.Length ?? 0;

        public HubTabDefinition GetAt(int index)
        {
            return m_tabs[index];
        }

        public bool TryGet(string id, out HubTabDefinition tab)
        {
            if (m_tabs != null && id != null)
            {
                for (int index = 0; index < m_tabs.Length; index++)
                {
                    HubTabDefinition candidate = m_tabs[index];
                    if (candidate != null && string.Equals(candidate.Id, id, StringComparison.Ordinal))
                    {
                        tab = candidate;
                        return true;
                    }
                }
            }

            tab = null;
            return false;
        }

        public bool Validate(out string error)
        {
            if (m_tabs == null || m_tabs.Length == 0)
            {
                error = "The navigation catalog lists no tabs. Add at least one tab.";
                return false;
            }

            for (int index = 0; index < m_tabs.Length; index++)
            {
                HubTabDefinition tab = m_tabs[index];
                if (tab == null)
                {
                    error = $"Navigation catalog entry {index + 1} is empty. Fill it in or remove it.";
                    return false;
                }

                if (!tab.Validate(out error))
                {
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(m_tabs[previous].Id, tab.Id, StringComparison.Ordinal))
                    {
                        error = $"Tab ID '{tab.Id}' is used by entries {previous + 1} and {index + 1}. Every tab needs its own ID.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }

        /// <summary>Replaces the tab list; for EditMode fixtures, which build catalogs without an asset.</summary>
        internal void SetTabs(HubTabDefinition[] tabs)
        {
            m_tabs = tabs;
        }
    }
}
