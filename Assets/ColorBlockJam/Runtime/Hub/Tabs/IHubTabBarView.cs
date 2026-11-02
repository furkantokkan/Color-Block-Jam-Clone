using System;
using UnityEngine;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// What <see cref="HubTabPresenter"/> may do to the tab bar, without any VisualElement, so EditMode tests can use a
    /// fake view.
    /// </summary>
    public interface IHubTabBarView
    {
        /// <summary>Raised with the tab ID when a slot is released as a click, whether the tab is open or locked.</summary>
        event Action<string> TabPressed;

        /// <summary>Hides every authored slot. The presenter calls this once per bind, before showing its tabs.</summary>
        void HideTabs();

        /// <summary>
        /// Shows the slot authored in Home.uxml as <c>tab-&lt;tabId&gt;</c> with its icon, label and look. Nothing is
        /// instantiated or removed: a missing slot returns false so the presenter can log it.
        /// </summary>
        /// <param name="icon">The catalog icon; null keeps Home.uxml's design-time icon. Lock Only ignores it.</param>
        /// <param name="label">Empty shows no text but keeps the label's line height.</param>
        bool TryShowTab(string tabId, Sprite icon, string label, HubTabLook look);
    }
}
