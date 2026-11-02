using System;
using System.Collections.Generic;
using Cysharp.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// Wraps the tab bar's authored slots (Home.uxml <c>tab-row</c>): it shows or hides a slot, writes its icon, label
    /// and state class, keeps the dividers right for the shown slots and forwards clicks. It reads no service and
    /// decides nothing (DEC-169).
    /// </summary>
    /// <remarks>
    /// Owner: <c>HubScreen</c>, which creates it on bind and disposes it on unbind. Every slot is a Button in
    /// <c>tab-row</c> named "tab-" plus its tab ID, found once; nothing is instantiated or removed. <see cref="Dispose"/>
    /// removes the click handlers, the view's only callbacks. Press feedback is USS only (.cbj-pressable).
    /// </remarks>
    public sealed class HubTabBarView : IHubTabBarView, IDisposable
    {
        public const string TabRowName = "tab-row";
        public const string IconClass = "tab__icon";
        public const string LabelClass = "tab__label";
        public const string ActiveClass = "tab--active";
        public const string LockedClass = "tab--locked";
        public const string LockOnlyClass = "tab--lock-only";
        public const string NoDividerClass = "tab--no-divider";

        // In Home.uxml order, which is the tab-bar order the dividers follow.
        private readonly List<TabSlot> m_slots = new List<TabSlot>(8);
        private bool m_disposed;

        /// <param name="tabBar">The <c>tab-bar</c> element from Home.uxml.</param>
        public HubTabBarView(VisualElement tabBar)
        {
            if (tabBar == null)
            {
                throw new ArgumentNullException(nameof(tabBar));
            }

            VisualElement row = tabBar.Q<VisualElement>(TabRowName);
            if (row == null)
            {
                throw new InvalidOperationException("Tab bar view: Home.uxml has no element named 'tab-row'.");
            }

            for (int i = 0; i < row.childCount; i++)
            {
                if (row[i] is Button button && button.name != null
                    && button.name.StartsWith(HubTabDefinition.SlotNamePrefix, StringComparison.Ordinal))
                {
                    string tabId = button.name.Substring(HubTabDefinition.SlotNamePrefix.Length);
                    m_slots.Add(new TabSlot(this, button, tabId));
                }
            }
        }

        public event Action<string> TabPressed;

        public void HideTabs()
        {
            for (int i = 0; i < m_slots.Count; i++)
            {
                m_slots[i].SetVisible(false);
            }

            UpdateDividers();
        }

        public bool TryShowTab(string tabId, Sprite icon, string label, HubTabLook look)
        {
            TabSlot slot = Find(tabId);
            if (slot == null)
            {
                return false;
            }

            slot.Show(icon, label, look);
            UpdateDividers();
            return true;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            for (int i = 0; i < m_slots.Count; i++)
            {
                m_slots[i].Dispose();
            }

            TabPressed = null;
        }

        private TabSlot Find(string tabId)
        {
            for (int i = 0; i < m_slots.Count; i++)
            {
                if (string.Equals(m_slots[i].Id, tabId, StringComparison.Ordinal))
                {
                    return m_slots[i];
                }
            }

            return null;
        }

        /// <summary>
        /// A shown slot draws its right divider only when another shown slot follows and neither of the two is the
        /// raised active tab (reference 06), so hiding a tab never leaves a stray divider.
        /// </summary>
        private void UpdateDividers()
        {
            TabSlot previous = null;
            for (int i = 0; i < m_slots.Count; i++)
            {
                TabSlot slot = m_slots[i];
                if (!slot.IsVisible)
                {
                    continue;
                }

                previous?.SetDivider(!previous.IsActive && !slot.IsActive);
                previous = slot;
            }

            previous?.SetDivider(false);
        }

        private void RaiseTabPressed(string tabId)
        {
            TabPressed?.Invoke(tabId);
        }

        private static T Require<T>(VisualElement root, string className) where T : VisualElement
        {
            T element = root.Q<T>(className: className);
            if (element == null)
            {
                throw new InvalidOperationException(ZString.Concat("Tab bar view: slot '", root.name, "' in Home.uxml has no ",
                    typeof(T).Name, " with the class '", className, "'."));
            }

            return element;
        }

        /// <summary>One authored tab slot: its Button, icon and label, found once and reused for the view's life.</summary>
        private sealed class TabSlot : IDisposable
        {
            private readonly HubTabBarView m_owner;
            private readonly Button m_button;
            private readonly VisualElement m_icon;
            private readonly Label m_label;
            private readonly Action m_onClicked;

            public TabSlot(HubTabBarView owner, Button button, string tabId)
            {
                m_owner = owner;
                m_button = button;
                Id = tabId;
                m_icon = Require<VisualElement>(button, IconClass);
                m_label = Require<Label>(button, LabelClass);
                IsActive = button.ClassListContains(ActiveClass);
                m_onClicked = OnClicked;
                m_button.clicked += m_onClicked;
            }

            public string Id { get; }

            /// <summary>The raised Home tab, authored with <c>tab--active</c>; it never draws a divider.</summary>
            public bool IsActive { get; }

            public bool IsVisible { get; private set; } = true;

            public void SetVisible(bool visible)
            {
                IsVisible = visible;
                m_button.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void Show(Sprite icon, string label, HubTabLook look)
            {
                SetVisible(true);
                m_button.EnableInClassList(LockedClass, look == HubTabLook.LockedDimmed);
                m_button.EnableInClassList(LockOnlyClass, look == HubTabLook.LockedLockOnly);

                // Lock Only draws ic_lock.png from USS (.tab--lock-only > .tab__icon); an inline icon would cover it.
                bool drawsIcon = look != HubTabLook.LockedLockOnly && icon != null;
                m_icon.style.backgroundImage = drawsIcon ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.Null);
                m_label.text = label;
            }

            public void SetDivider(bool shown)
            {
                m_button.EnableInClassList(NoDividerClass, !shown);
            }

            public void Dispose()
            {
                m_button.clicked -= m_onClicked;
            }

            private void OnClicked()
            {
                m_owner.RaiseTabPressed(Id);
            }
        }
    }
}
