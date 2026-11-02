using System;
using System.Collections.Generic;
using Cysharp.Text;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.Home
{
    /// <summary>
    /// Wraps the Home screen's VisualElement subtree (Home.uxml): it writes text, visibility and icons and
    /// forwards the level and settings clicks; it reads no service and decides nothing (design 6.3, 7).
    /// </summary>
    /// <remarks>
    /// Owner: <c>HubScreen</c>, which creates it on bind and disposes it on unbind. The currency holders are
    /// authored in Home.uxml (DEC-143): this view only shows, hides and writes them, it never instantiates or
    /// removes one. <see cref="Dispose"/> removes the button handlers and stops the current-level node's
    /// PrimeTween pulse, the view's only running work; press feedback is USS only (.cbj-pressable).
    /// </remarks>
    public sealed class HomeView : IHomeView, IDisposable
    {
        public const string LevelButtonName = "level-button";
        public const string LevelButtonLabelName = "level-button-label";
        public const string SettingsButtonName = "settings-button";
        public const string CurrencyHoldersName = "currency-holders";
        public const string LevelRouteName = "level-route";
        public const string RouteNodeClass = "route-node";
        public const string RouteNodeLabelName = "route-node-label";
        public const string RouteNodeLockName = "route-node-lock";
        public const string RouteNodeLockedClass = "route-node--locked";
        public const string HolderTextName = "holder-text";
        public const string HolderIconName = "holder-icon";
        public const string IconCountName = "icon-count";
        public const string HolderCountOnIconClass = "currency-holder--count-on-icon";

        /// <summary>Home.uxml names each authored holder "currency-holder-" plus the currency's stable ID.</summary>
        private const string k_holderNamePrefix = "currency-holder-";
        private const string k_heavyTextBackClass = "cbj-heavy-text__back";

        /// <summary>Current-level node pulse: a small, slow breathing scale (reference 06 idle feedback).</summary>
        private const float k_currentNodePulseScale = 1.05f;
        private const float k_currentNodePulseSeconds = 0.8f;

        private readonly Button m_levelButton;
        private readonly Label m_levelButtonLabelFront;
        private readonly Label m_levelButtonLabelBack;
        private readonly Button m_settingsButton;
        private readonly List<VisualElement> m_routeNodes = new List<VisualElement>(3);
        private readonly Dictionary<string, CurrencyHolderView> m_holdersById = new Dictionary<string, CurrencyHolderView>(4);
        private readonly Action m_onLevelClicked;
        private readonly Action m_onSettingsClicked;
        private Tween m_currentNodePulse;
        private bool m_disposed;

        /// <param name="home">The <c>home-screen</c> element from Home.uxml.</param>
        public HomeView(VisualElement home)
        {
            if (home == null)
            {
                throw new ArgumentNullException(nameof(home));
            }

            m_levelButton = Require<Button>(home, LevelButtonName);
            m_levelButtonLabelFront = Require<Label>(m_levelButton, LevelButtonLabelName);
            m_levelButtonLabelBack = FindHeavyTextBack(m_levelButton);
            if (m_levelButtonLabelBack == null)
            {
                throw new InvalidOperationException("Home view: level-button has no '.cbj-heavy-text__back' label.");
            }

            m_settingsButton = Require<Button>(home, SettingsButtonName);

            // DEC-143: every "currency-holder-<id>" child of currency-holders is authored, never instantiated.
            VisualElement currencyHolders = Require<VisualElement>(home, CurrencyHoldersName);
            for (int i = 0; i < currencyHolders.childCount; i++)
            {
                VisualElement child = currencyHolders[i];
                if (child.name != null && child.name.StartsWith(k_holderNamePrefix, StringComparison.Ordinal))
                {
                    string id = child.name.Substring(k_holderNamePrefix.Length);
                    m_holdersById[id] = new CurrencyHolderView(child);
                }
            }

            // Route nodes are the level-route children with the route-node class, bottom (current) first.
            VisualElement route = Require<VisualElement>(home, LevelRouteName);
            for (int i = 0; i < route.childCount; i++)
            {
                VisualElement child = route[i];
                if (child.ClassListContains(RouteNodeClass))
                {
                    m_routeNodes.Add(child);
                }
            }

            m_onLevelClicked = OnLevelClicked;
            m_onSettingsClicked = OnSettingsClicked;
            m_levelButton.clicked += m_onLevelClicked;
            m_settingsButton.clicked += m_onSettingsClicked;

            // Owner: this view; stop point: Dispose (HubScreen.OnDisable). Endless yoyo around the node's centre.
            if (m_routeNodes.Count > 0)
            {
                m_currentNodePulse = Tween.Scale(m_routeNodes[0].transform, Vector3.one, Vector3.one * k_currentNodePulseScale,
                    k_currentNodePulseSeconds, Ease.InOutSine, cycles: -1, cycleMode: CycleMode.Yoyo);
            }
        }

        public event Action LevelPressed;

        public event Action SettingsPressed;

        public int RouteNodeCount => m_routeNodes.Count;

        public void SetLevel(string label, bool interactable)
        {
            m_levelButtonLabelFront.text = label;
            m_levelButtonLabelBack.text = label;
            m_levelButton.SetEnabled(interactable);
        }

        public void SetRouteNode(int slot, string label, bool visible, bool locked)
        {
            VisualElement node = m_routeNodes[slot];
            node.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
            node.EnableInClassList(RouteNodeLockedClass, locked);
            Label front = node.Q<Label>(RouteNodeLabelName);
            if (front != null)
            {
                front.text = label;
            }

            Label back = FindHeavyTextBack(node);
            if (back != null)
            {
                back.text = label;
            }
        }

        public void HideCurrencyHolders()
        {
            foreach (CurrencyHolderView holder in m_holdersById.Values)
            {
                holder.SetVisible(false);
            }
        }

        public bool TryGetCurrencyHolder(string currencyId, Sprite icon, bool countOnIcon, out ICurrencyHolderView holder)
        {
            if (!m_holdersById.TryGetValue(currencyId, out CurrencyHolderView found))
            {
                holder = null;
                return false;
            }

            found.Configure(icon, countOnIcon);
            found.SetVisible(true);
            holder = found;
            return true;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_currentNodePulse.Stop();
            if (m_routeNodes.Count > 0)
            {
                m_routeNodes[0].transform.scale = Vector3.one;
            }

            m_levelButton.clicked -= m_onLevelClicked;
            m_settingsButton.clicked -= m_onSettingsClicked;
            LevelPressed = null;
            SettingsPressed = null;
        }

        private void OnLevelClicked()
        {
            LevelPressed?.Invoke();
        }

        private void OnSettingsClicked()
        {
            SettingsPressed?.Invoke();
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(ZString.Concat("Home view: Home.uxml has no ", typeof(T).Name, " named '", name, "'."));
            }

            return element;
        }

        /// <summary>The dark "cbj-heavy-text__back" Label under a heavy-text pair (D-010), or null if absent.</summary>
        private static Label FindHeavyTextBack(VisualElement root)
        {
            return root.Q<Label>(className: k_heavyTextBackClass);
        }

        /// <summary>
        /// One authored holder from Home.uxml (a CurrencyHolder instance): pill text, icon, optional icon count
        /// and plus badge. It is found once by name and reused for the life of the HomeView; nothing here adds
        /// or removes a VisualElement (DEC-143).
        /// </summary>
        private sealed class CurrencyHolderView : ICurrencyHolderView
        {
            private readonly VisualElement m_root;
            private readonly Label m_text;
            private readonly Label m_iconCount;
            private readonly VisualElement m_icon;

            public CurrencyHolderView(VisualElement root)
            {
                m_root = root;
                m_text = Require<Label>(root, HolderTextName);
                m_iconCount = Require<Label>(root, IconCountName);
                m_icon = Require<VisualElement>(root, HolderIconName);
            }

            /// <summary>Sets the runtime icon (overriding Home.uxml's design-time preview) and icon-count mode.</summary>
            public void Configure(Sprite icon, bool countOnIcon)
            {
                if (icon != null)
                {
                    m_icon.style.backgroundImage = new StyleBackground(icon);
                }

                m_root.EnableInClassList(HolderCountOnIconClass, countOnIcon);
                m_iconCount.style.display = countOnIcon ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void SetVisible(bool visible)
            {
                m_root.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void SetText(string text)
            {
                m_text.text = text;
            }

            public void SetIconCount(string text)
            {
                m_iconCount.text = text;
            }
        }
    }
}
