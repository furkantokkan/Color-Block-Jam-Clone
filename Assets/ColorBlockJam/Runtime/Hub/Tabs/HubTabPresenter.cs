using System;
using System.Collections.Generic;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using Cysharp.Text;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// Drives the Home tab bar from the navigation catalog and the player's saved level: each entry shows, locks, opens
    /// or hides its authored slot, and pressing an open tab opens the menu it names (DEC-169).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). The saved-level subscription goes into one CompositeDisposable cleared
    /// by <see cref="Unbind"/>, which <c>HubScreen.OnDisable</c> calls; <see cref="Dispose"/> at Hub unload is the last
    /// stop point. A slot is written only when its tab's status changes, so a level change that opens no tab writes
    /// nothing. It knows no VisualElement and no concrete menu, so a new tab or menu needs no change here.
    /// </remarks>
    public sealed class HubTabPresenter : IDisposable
    {
        private readonly HubNavigationCatalog m_catalog;
        private readonly IPlayerProgress m_progress;
        private readonly IHubMenuNavigator m_menus;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly List<TabBinding> m_tabs = new List<TabBinding>(8);
        private readonly Action<string> m_onTabPressed;
        private readonly Observer<int> m_onSavedLevelChanged;

        private IHubTabBarView m_view;
        private bool m_disposed;

        public HubTabPresenter(HubNavigationCatalog catalog, IPlayerProgress progress, IHubMenuNavigator menus)
        {
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_progress = progress ?? throw new ArgumentNullException(nameof(progress));
            m_menus = menus ?? throw new ArgumentNullException(nameof(menus));
            m_onTabPressed = OnTabPressed;
            m_onSavedLevelChanged = OnSavedLevelChanged;
        }

        public bool IsBound => m_view != null;

        /// <summary>
        /// Binds the view once per show: hides every slot, then shows each catalog tab for the current saved level.
        /// </summary>
        public void Bind(IHubTabBarView view)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(HubTabPresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Hub tab presenter: already bound; unbind first.");
            }

            m_view = view;
            m_view.TabPressed += m_onTabPressed;
            if (!m_catalog.Validate(out string error))
            {
                // The catalog Inspector shows the same message; the valid entries still show.
                Debug.LogError(ZString.Concat("Hub tabs: ", error));
            }

            // Hides every authored slot first, so a Hidden tab, or a slot that no entry claims, stays hidden.
            m_view.HideTabs();
            for (int i = 0; i < m_catalog.Count; i++)
            {
                HubTabDefinition definition = m_catalog.GetAt(i);
                if (definition != null)
                {
                    m_tabs.Add(new TabBinding(definition));
                }
            }

            // Emits the saved level at once, then only on a committed change, so a Level Gated tab opens without a
            // restart.
            m_subscriptions.Add(m_progress.SavedLevelIndex.Subscribe(m_onSavedLevelChanged));
        }

        /// <summary>Disposes the subscription and detaches the view; the slots keep their last look.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_tabs.Clear();
            m_view.TabPressed -= m_onTabPressed;
            m_view = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_disposed = true;
        }

        private void OnSavedLevelChanged(int savedLevelIndex)
        {
            for (int i = 0; i < m_tabs.Count; i++)
            {
                TabBinding tab = m_tabs[i];
                HubTabStatus status = HubTabRules.GetStatus(tab.Definition, savedLevelIndex);
                if (tab.HasStatus && tab.Status == status)
                {
                    continue;
                }

                tab.SetStatus(status);
                Show(tab);
            }
        }

        private void Show(TabBinding tab)
        {
            // HideTabs hid every slot on bind, and a tab's availability is authored, so a Hidden tab needs no write.
            if (tab.Status == HubTabStatus.Hidden)
            {
                return;
            }

            HubTabDefinition definition = tab.Definition;
            HubTabLook look = GetLook(definition, tab.Status);
            string label = HubTabRules.GetLabel(definition, tab.Status);
            if (m_view.TryShowTab(definition.Id, definition.Icon, label, look) || tab.ReportedMissingSlot)
            {
                return;
            }

            tab.ReportedMissingSlot = true;
            Debug.LogError(ZString.Concat("Hub tabs: tab '", definition.Id, "' has no slot in Home.uxml (expected a Button named '",
                HubTabDefinition.SlotNamePrefix, definition.Id, "' in tab-row)."));
        }

        private void OnTabPressed(string tabId)
        {
            TabBinding tab = Find(tabId);

            // A locked tab, or an open one that names no menu, answers with its press feedback only.
            if (tab == null || tab.Status != HubTabStatus.Unlocked || !tab.Definition.OpensMenu)
            {
                return;
            }

            string menuId = tab.Definition.MenuId;
            if (m_menus.OpenMenu(menuId) || m_menus.HasMenu(menuId) || tab.ReportedMissingMenu)
            {
                return;
            }

            tab.ReportedMissingMenu = true;
            Debug.LogError(ZString.Concat("Hub tabs: tab '", tabId, "' opens menu '", menuId,
                "', but no menu is registered under that ID (HubNavigator.RegisterMenu)."));
        }

        private TabBinding Find(string tabId)
        {
            for (int i = 0; i < m_tabs.Count; i++)
            {
                if (string.Equals(m_tabs[i].Definition.Id, tabId, StringComparison.Ordinal))
                {
                    return m_tabs[i];
                }
            }

            return null;
        }

        private static HubTabLook GetLook(HubTabDefinition definition, HubTabStatus status)
        {
            if (status == HubTabStatus.Unlocked)
            {
                return HubTabLook.Open;
            }

            return definition.LockedLook == HubTabLockedLook.LockOnly ? HubTabLook.LockedLockOnly : HubTabLook.LockedDimmed;
        }

        /// <summary>One catalog tab and what this bind last showed for it; cleared with the bind.</summary>
        private sealed class TabBinding
        {
            public TabBinding(HubTabDefinition definition)
            {
                Definition = definition;
            }

            public HubTabDefinition Definition { get; }

            public bool HasStatus { get; private set; }

            public HubTabStatus Status { get; private set; }

            /// <summary>One designer-language error per bind for an entry with no authored slot.</summary>
            public bool ReportedMissingSlot { get; set; }

            /// <summary>One designer-language error per bind for a menu ID with no registered menu.</summary>
            public bool ReportedMissingMenu { get; set; }

            public void SetStatus(HubTabStatus status)
            {
                Status = status;
                HasStatus = true;
            }
        }
    }
}
