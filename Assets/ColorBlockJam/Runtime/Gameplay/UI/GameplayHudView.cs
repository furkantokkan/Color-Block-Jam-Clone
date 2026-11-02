using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Wraps the GameplayHud.uxml tree (B-22 to B-24): it finds the named elements, forwards the booster and banner
    /// clicks as C# events and writes only text, icons, classes, bar fill and enabled states. Selected, empty, frozen,
    /// targeting and opening looks are USS classes; a blocked press adds a flash class that its own transition removes.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD, which creates it once over the HUD document's tree. Stops: <see cref="Dispose"/>
    /// removes every click and transition callback it registered and stops the Time Freeze bar's drain. Every write
    /// skips an unchanged value, so a steady tick changes nothing in the tree. The drain is this view's only tween: it
    /// targets the view, the timer presenter pauses it outside Playing, and a counter write, SetFrozen(false) and
    /// Dispose stop it.
    /// </remarks>
    public sealed class GameplayHudView : IGameplayHudView, IDisposable
    {
        public const string RootName = "hud-root";
        public const string TargetLayerName = "target-layer";
        public const string TopAreaName = "top-area";
        public const string TimerBandName = "timer-band";
        public const string TimerLabelName = "timer-label";
        public const string FreezeCounterName = "freeze-counter";
        public const string FreezeFillName = "freeze-fill";
        public const string FreezeLabelName = "freeze-label";
        public const string BannerName = "info-banner";
        public const string BannerIconName = "info-icon";
        public const string BannerTextName = "info-text";
        public const string BannerTitleName = "info-title";
        public const string BannerTitleBackName = "info-title-back";
        public const string BannerCloseName = "info-close";
        public const string OpeningLayerName = "opening-layer";
        public const string OpeningRowName = "opening-row";
        public const string OpeningIconClass = "opening-icon";
        public const string BoosterBarName = "booster-bar";
        public const string BoosterButtonPrefix = "booster-button-";
        public const string BoosterIconName = "booster-icon";
        public const string BoosterCountName = "booster-count";
        public const string BoosterCountLabelName = "booster-count-label";
        public const string BoosterPlusName = "booster-plus";

        public const string FrozenClass = "hud--frozen";
        public const string TargetingClass = "hud--targeting";
        public const string OpeningClass = "hud--opening";
        public const string SelectedClass = "booster-button--selected";
        public const string EmptyClass = "booster-button--empty";
        public const string BlockedClass = "booster-button--blocked";

        /// <summary>The booster buttons GameplayHud.uxml declares.</summary>
        public const int k_slotCount = 4;

        // The tint transition that ends a blocked flash.
        private static readonly StylePropertyName s_tintProperty = new StylePropertyName("-unity-background-image-tint-color");

        // Cached once: the drain's value callback, which captures nothing.
        private static readonly Action<GameplayHudView, float> s_applyFreezeFill = (view, fill) => view.ApplyFreezeFill(fill);

        // PrimeTween rejects shorter durations; a shorter drain is written at once.
        private const float k_minDrainSeconds = 0.001f;

        private readonly VisualElement m_root;
        private readonly Label m_timer;
        private readonly VisualElement m_freezeFill;
        private readonly Label m_freezeLabel;
        private readonly VisualElement m_bannerIcon;
        private readonly Label m_bannerText;
        private readonly Label m_bannerTitle;
        private readonly Label m_bannerTitleBack;
        private readonly Button m_bannerClose;
        private readonly VisualElement m_openingRow;
        private readonly Button[] m_buttons = new Button[k_slotCount];
        private readonly VisualElement[] m_icons = new VisualElement[k_slotCount];
        private readonly Label[] m_counts = new Label[k_slotCount];
        private readonly Action[] m_onButtonClicked = new Action[k_slotCount];
        private readonly EventCallback<TransitionEndEvent> m_onTransitionEnded;
        private readonly Action m_onBannerCloseClicked;
        private float m_freezeFillShown = -1f;
        private Tween m_freezeDrain;
        private bool m_freezeBarPaused;

        // The icon the banner shows now (null: none), so an unchanged icon is not written again: UI Toolkit allocates
        // on every inline background write (B-31).
        private Sprite m_bannerIconShown;
        private bool m_disposed;

        /// <param name="root">The hud-root element of GameplayHud.uxml.</param>
        public GameplayHudView(VisualElement root)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            TargetLayer = Require<VisualElement>(root, TargetLayerName);
            TopArea = Require<VisualElement>(root, TopAreaName);
            BoosterBar = Require<VisualElement>(root, BoosterBarName);
            Banner = Require<VisualElement>(root, BannerName);
            m_timer = Require<Label>(root, TimerLabelName);
            m_freezeFill = Require<VisualElement>(root, FreezeFillName);
            m_freezeLabel = Require<Label>(root, FreezeLabelName);
            m_bannerIcon = Require<VisualElement>(root, BannerIconName);
            m_bannerText = Require<Label>(root, BannerTextName);
            m_bannerTitle = Require<Label>(root, BannerTitleName);
            m_bannerTitleBack = Require<Label>(root, BannerTitleBackName);
            m_bannerClose = Require<Button>(root, BannerCloseName);
            m_openingRow = Require<VisualElement>(root, OpeningRowName);

            // The banner shows no icon until ShowBanner writes one; writing that inline value here, on Home, means the
            // first targeting during Playing replaces it instead of creating the icon's inline style (B-31).
            m_bannerIcon.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            m_bannerIconShown = null;

            m_onTransitionEnded = OnTransitionEnded;
            for (int slot = 0; slot < k_slotCount; slot++)
            {
                Button button = Require<Button>(BoosterBar, BoosterButtonPrefix + slot);
                m_buttons[slot] = button;
                m_icons[slot] = Require<VisualElement>(button, BoosterIconName);
                m_counts[slot] = Require<Label>(button, BoosterCountLabelName);

                // One cached handler per slot, so a click allocates nothing.
                int index = slot;
                m_onButtonClicked[slot] = () => SlotPressed?.Invoke(index);
                button.clicked += m_onButtonClicked[slot];
                button.RegisterCallback(m_onTransitionEnded);
            }

            m_onBannerCloseClicked = OnBannerCloseClicked;
            m_bannerClose.clicked += m_onBannerCloseClicked;
        }

        public event Action<int> SlotPressed;

        public event Action BannerClosed;

        public int SlotCount => k_slotCount;

        public VisualElement Root => m_root;

        /// <summary>The full-screen layer the Rocket reticles go into.</summary>
        public VisualElement TargetLayer { get; }

        /// <summary>The top band (timer, freeze counter, banner); it pads left, top and right from the safe area.</summary>
        public VisualElement TopArea { get; }

        /// <summary>The bottom bar; it pads left, right and bottom from the safe area.</summary>
        public VisualElement BoosterBar { get; }

        public VisualElement Banner { get; }

        public Button GetButton(int slot) => m_buttons[slot];

        public void ShowSlot(int slot, Sprite icon)
        {
            m_buttons[slot].style.display = DisplayStyle.Flex;
            m_icons[slot].style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
        }

        public void HideSlot(int slot)
        {
            // A hidden button runs no transition, so a flash it still shows ends here.
            m_buttons[slot].RemoveFromClassList(BlockedClass);
            m_buttons[slot].style.display = DisplayStyle.None;
        }

        public void SetSlotStock(int slot, string count, bool empty)
        {
            SetText(m_counts[slot], count);
            m_buttons[slot].EnableInClassList(EmptyClass, empty);
        }

        public void SetSlotSelected(int slot, bool selected)
        {
            m_buttons[slot].EnableInClassList(SelectedClass, selected);
        }

        public void SetSlotEnabled(int slot, bool enabled)
        {
            if (m_buttons[slot].enabledSelf != enabled)
            {
                m_buttons[slot].SetEnabled(enabled);
            }

            if (!enabled)
            {
                m_buttons[slot].RemoveFromClassList(BlockedClass);
            }
        }

        public void PlaySlotBlocked(int slot)
        {
            // The flash class ends with its own tint transition (OnTransitionEnded); a repeat while it shows adds nothing.
            m_buttons[slot].AddToClassList(BlockedClass);
        }

        public void ShowBanner(string title, string instruction, Sprite icon)
        {
            SetText(m_bannerTitle, title);
            SetText(m_bannerTitleBack, title);
            SetText(m_bannerText, instruction);
            if (!ReferenceEquals(m_bannerIconShown, icon))
            {
                m_bannerIcon.style.backgroundImage = icon != null ? new StyleBackground(icon) : new StyleBackground(StyleKeyword.None);
                m_bannerIconShown = icon;
            }

            m_root.EnableInClassList(TargetingClass, true);
        }

        public void HideBanner()
        {
            m_root.EnableInClassList(TargetingClass, false);
        }

        public void SetTimer(string text)
        {
            SetText(m_timer, text);
        }

        public void SetFrozen(bool frozen)
        {
            m_root.EnableInClassList(FrozenClass, frozen);
            if (!frozen)
            {
                StopFreezeDrain();
            }
        }

        public void SetFreezeCounter(string text, float fill)
        {
            SetText(m_freezeLabel, text);
            StopFreezeDrain();
            ApplyFreezeFill(fill);
        }

        public void DrainFreezeBar(float fill, float seconds)
        {
            StopFreezeDrain();
            if (m_disposed || seconds < k_minDrainSeconds)
            {
                ApplyFreezeFill(fill);
                return;
            }

            // Owner: this view (target). Stops: it ends by itself, or the next counter write or drain, SetFrozen(false)
            // and Dispose end it; SetFreezeBarPaused (Suspend) holds it.
            float from = m_freezeFillShown < 0f ? 1f : m_freezeFillShown;
            m_freezeDrain = Tween.Custom(this, from, Clamp01(fill), seconds, s_applyFreezeFill, Ease.Linear);
            m_freezeDrain.isPaused = m_freezeBarPaused;
        }

        public void SetFreezeBarPaused(bool paused)
        {
            m_freezeBarPaused = paused;
            if (m_freezeDrain.isAlive)
            {
                m_freezeDrain.isPaused = paused;
            }
        }

        // Home only: grows the row with one element per missing icon, then reuses them; unused ones are hidden.
        public void SetOpeningIcons(IReadOnlyList<Sprite> icons)
        {
            int count = icons?.Count ?? 0;
            while (m_openingRow.childCount < count)
            {
                VisualElement icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList(OpeningIconClass);
                m_openingRow.Add(icon);
            }

            for (int index = 0; index < m_openingRow.childCount; index++)
            {
                VisualElement element = m_openingRow[index];
                bool shown = index < count;
                element.style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
                element.style.backgroundImage = shown
                    ? new StyleBackground(icons[index])
                    : new StyleBackground(StyleKeyword.None);
            }
        }

        /// <summary>The opening icons shown now, for tests.</summary>
        public int OpeningIconCount
        {
            get
            {
                int shown = 0;
                for (int index = 0; index < m_openingRow.childCount; index++)
                {
                    if (m_openingRow[index].style.display.value == DisplayStyle.Flex)
                    {
                        shown++;
                    }
                }

                return shown;
            }
        }

        public void SetOpening(bool opening)
        {
            m_root.EnableInClassList(OpeningClass, opening);
        }

        /// <summary>True when the root carries the class, for tests.</summary>
        public bool HasRootClass(string className) => m_root.ClassListContains(className);

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            StopFreezeDrain();
            for (int slot = 0; slot < k_slotCount; slot++)
            {
                m_buttons[slot].clicked -= m_onButtonClicked[slot];
                m_buttons[slot].UnregisterCallback(m_onTransitionEnded);
                m_buttons[slot].RemoveFromClassList(BlockedClass);
            }

            m_bannerClose.clicked -= m_onBannerCloseClicked;
            SlotPressed = null;
            BannerClosed = null;
        }

        private static float Clamp01(float value)
        {
            return value < 0f ? 0f : value > 1f ? 1f : value;
        }

        // The fill is a horizontal scale from the bar's left edge (freeze-fill's transform-origin), so the per-frame
        // drain changes only the transform and never runs a layout pass.
        private void ApplyFreezeFill(float fill)
        {
            float clamped = Clamp01(fill);
            if (clamped != m_freezeFillShown)
            {
                m_freezeFillShown = clamped;
                m_freezeFill.style.scale = new StyleScale(new Scale(new Vector3(clamped, 1f, 1f)));
            }
        }

        private void StopFreezeDrain()
        {
            if (m_freezeDrain.isAlive)
            {
                m_freezeDrain.Stop();
            }

            m_freezeDrain = default;
        }

        private static void SetText(Label label, string text)
        {
            if (!string.Equals(label.text, text, StringComparison.Ordinal))
            {
                label.text = text;
            }
        }

        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException($"Gameplay HUD: GameplayHud.uxml has no {typeof(T).Name} named '{name}'.");
            }

            return element;
        }

        private void OnBannerCloseClicked()
        {
            BannerClosed?.Invoke();
        }

        // The blocked flash lasts one tint transition: once it ends, the class goes and the tint eases back. A disabled or
        // hidden button drops the class itself, so an interrupted flash never sticks.
        private void OnTransitionEnded(TransitionEndEvent transitionEnded)
        {
            if (transitionEnded.target is VisualElement element && element.ClassListContains(BlockedClass)
                && transitionEnded.stylePropertyNames.Contains(s_tintProperty))
            {
                element.RemoveFromClassList(BlockedClass);
            }
        }
    }
}
