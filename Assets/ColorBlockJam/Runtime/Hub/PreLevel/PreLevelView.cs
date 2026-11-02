using System;
using System.Collections.Generic;
using Cysharp.Text;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Hub.PreLevel
{
    /// <summary>
    /// Wraps the pre-level popup's VisualElement subtree (PreLevel.uxml): it shows or hides the popup, writes the title,
    /// clones and restyles authored selectors, sets Play's state and forwards clicks; it reads no service and decides nothing
    /// (design 6.3, 9.1).
    /// </summary>
    /// <remarks>
    /// Owner: <c>HubScreen</c>, which creates it on bind and disposes it on unbind. <see cref="Dispose"/> removes every
    /// click handler and selector it added and hides the popup. Selector looks are USS modifier classes; press feedback
    /// is .cbj-pressable (DEC-14, DEC-45). The scrim blocks Home underneath (AC-2.6) but does not close the popup.
    /// The host pads <see cref="SafeAreaName"/> from its safe area.
    /// </remarks>
    public sealed class PreLevelView : IPreLevelView, IDisposable
    {
        private const float k_popupIntroStartScale = 0.96f;
        private const float k_popupIntroSeconds = 0.15f;

        public const string ScreenName = "pre-level-screen";
        public const string ScrimName = "pre-level-scrim";
        public const string SafeAreaName = "pre-level-safe-area";
        public const string PopupName = "pre-level-popup";
        public const string TitleName = "pre-level-title";
        public const string SubtitleName = "pre-level-subtitle";
        public const string CloseButtonName = "pre-level-close";
        public const string SelectorsName = "pre-level-selectors";
        public const string PlayButtonName = "pre-level-play";
        public const string SelectorClass = "booster-selector";
        public const string SelectorButtonClass = "booster-selector__button";
        public const string SelectorIconClass = "booster-selector__icon";
        public const string SelectorCountBadgeClass = "booster-selector__count-badge";
        public const string SelectorCountClass = "booster-selector__count";
        public const string SelectorCheckBadgeClass = "booster-selector__check-badge";
        public const string SelectorCheckMarkClass = "booster-selector__check-mark";
        public const string SelectorPlusBadgeClass = "booster-selector__plus-badge";
        public const string SelectorPlusIconClass = "booster-selector__plus-icon";
        public const string SelectorLabelClass = "booster-selector__label";
        public const string SelectedClass = "booster-selector--selected";
        public const string OutOfStockClass = "booster-selector--empty";

        private const string k_heavyBackClass = "cbj-heavy-text__back";

        private readonly VisualElement m_screen;
        private readonly VisualElement m_popup;
        private readonly Label m_title;
        private readonly Label m_titleBack;
        private readonly Button m_closeButton;
        private readonly VisualElement m_selectorRow;
        private readonly VisualTreeAsset m_selectorTemplate;
        private readonly Button m_playButton;
        private readonly List<Selector> m_selectors = new List<Selector>(2);
        private readonly Action m_onCloseClicked;
        private readonly Action m_onPlayClicked;
        private Tween m_popupIntro;
        private bool m_disposed;

        /// <param name="screen">The <c>pre-level-screen</c> element of PreLevel.uxml.</param>
        public PreLevelView(VisualElement screen)
        {
            m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
            m_popup = Require<VisualElement>(screen, PopupName);
            m_title = Require<Label>(screen, TitleName);
            m_titleBack = m_title.parent?.Q<Label>(className: k_heavyBackClass)
                          ?? throw new InvalidOperationException("Pre-level view: the title has no '.cbj-heavy-text__back' label.");
            m_closeButton = Require<Button>(screen, CloseButtonName);
            m_selectorRow = Require<VisualElement>(screen, SelectorsName);
            // Keep the authored template reference across unbind/rebind; preview instances never become inventory.
            m_selectorTemplate = screen.Q<TemplateContainer>("selector-preview-time")?.templateSource
                                 ?? m_selectorRow.userData as VisualTreeAsset
                                 ?? throw new InvalidOperationException("Pre-level view: the authored BoosterSelector template is missing.");
            m_selectorRow.userData = m_selectorTemplate;
            m_selectorRow.Clear();
            m_playButton = Require<Button>(screen, PlayButtonName);

            m_onCloseClicked = OnCloseClicked;
            m_onPlayClicked = OnPlayClicked;
            m_closeButton.clicked += m_onCloseClicked;
            m_playButton.clicked += m_onPlayClicked;
            SetVisible(false);
        }

        public event Action ClosePressed;

        public event Action PlayPressed;

        public event Action<int> SelectorPressed;

        public void SetVisible(bool visible)
        {
            if (m_popupIntro.isAlive)
            {
                m_popupIntro.Stop();
            }

            m_screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                m_popupIntro = Tween.Scale(m_popup.transform, Vector3.one * k_popupIntroStartScale, Vector3.one,
                    k_popupIntroSeconds, Ease.OutQuad, useUnscaledTime: true);
            }
        }

        public void SetTitle(string title)
        {
            // Both layers of the heavy-text pair carry the same text (Common.uss).
            m_title.text = title;
            m_titleBack.text = title;
        }

        public int AddSelector(Sprite icon, string label)
        {
            int slot = m_selectors.Count;
            var selector = new Selector(this, slot, icon, label);
            m_selectors.Add(selector);
            m_selectorRow.Add(selector.Root);
            return slot;
        }

        public void ClearSelectors()
        {
            for (int i = 0; i < m_selectors.Count; i++)
            {
                m_selectors[i].Remove();
            }

            m_selectors.Clear();
        }

        public void SetSelector(int slot, PreLevelSelectorState state, string count)
        {
            m_selectors[slot].Set(state, count);
        }

        public void SetPlayInteractable(bool interactable)
        {
            m_playButton.SetEnabled(interactable);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_closeButton.clicked -= m_onCloseClicked;
            m_playButton.clicked -= m_onPlayClicked;
            ClearSelectors();
            SetVisible(false);
            ClosePressed = null;
            PlayPressed = null;
            SelectorPressed = null;
        }

        private void OnCloseClicked()
        {
            ClosePressed?.Invoke();
        }

        private void OnPlayClicked()
        {
            PlayPressed?.Invoke();
        }

        private void OnSelectorClicked(int slot)
        {
            SelectorPressed?.Invoke(slot);
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException(ZString.Concat("Pre-level view: PreLevel.uxml has no ", typeof(T).Name,
                    " named '", name, "'."));
            }

            return element;
        }

        /// <summary>
        /// One authored selector clone: a rounded-square button with the booster icon and a count, check or plus badge,
        /// and its name below. The state is two modifier classes on the root; PreLevel.uss draws each look.
        /// </summary>
        private sealed class Selector
        {
            private readonly Button m_button;
            private readonly Label m_countBack;
            private readonly Label m_count;
            private readonly Action m_onClicked;

            public Selector(PreLevelView owner, int slot, Sprite icon, string label)
            {
                Root = owner.m_selectorTemplate.CloneTree().Q(className: SelectorClass);
                Root.RemoveFromHierarchy();
                m_button = Root.Q<Button>(className: SelectorButtonClass);
                VisualElement iconElement = Root.Q(className: SelectorIconClass);
                iconElement.style.backgroundImage = icon != null
                    ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
                m_countBack = Root.Q<Label>("selector-count-back");
                m_count = Root.Q<Label>("selector-count");
                m_countBack.text = string.Empty;
                m_count.text = string.Empty;
                Root.Q<Label>("selector-label-back").text = label ?? string.Empty;
                Root.Q<Label>("selector-label").text = label ?? string.Empty;

                m_onClicked = () => owner.OnSelectorClicked(slot);
                m_button.clicked += m_onClicked;
            }

            public VisualElement Root { get; }

            public void Set(PreLevelSelectorState state, string count)
            {
                Root.EnableInClassList(SelectedClass, state == PreLevelSelectorState.Selected);
                Root.EnableInClassList(OutOfStockClass, state == PreLevelSelectorState.OutOfStock);
                m_count.text = count;
                m_countBack.text = count;
            }

            public void Remove()
            {
                m_button.clicked -= m_onClicked;
                Root.RemoveFromHierarchy();
            }
        }
    }
}
