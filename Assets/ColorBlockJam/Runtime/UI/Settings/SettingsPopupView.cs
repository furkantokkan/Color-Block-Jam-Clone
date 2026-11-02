using System;
using ColorBlockJam.Player;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.UI.Settings
{
    /// <summary>
    /// Wraps the Settings popup's VisualElement subtree (SettingsPopup.uxml): it shows or hides the popup, writes the
    /// toggle state class, the message and the HOME button's display, and forwards toggle, close, scrim and HOME
    /// clicks; it reads no service and decides nothing (design 8, DEC-134, DEC-136).
    /// </summary>
    /// <remarks>
    /// Owner: the host screen, which creates it on bind and disposes it on unbind. <see cref="Dispose"/> removes
    /// every click handler and the scrim's Clickable. Knob movement and press feedback are USS transitions only
    /// (DEC-45); the knob transitions run only after a tap (<see cref="ToggleAnimatedClass"/>), so the saved state is
    /// already in place when the popup opens. Legal Terms, Restore Purchases, Support and Language have no handler, so they give the shared press
    /// feedback and nothing else (AC-2.4). The host pads <see cref="SafeAreaName"/> from its safe area.
    /// </remarks>
    public sealed class SettingsPopupView : ISettingsPopupView, IDisposable
    {
        private const float k_popupIntroStartScale = 0.96f;
        private const float k_popupIntroSeconds = 0.15f;

        public const string ScreenName = "settings-screen";
        public const string ScrimName = "settings-scrim";
        public const string SafeAreaName = "settings-safe-area";
        public const string PopupName = "settings-popup";
        public const string TitleName = "settings-title";
        public const string CloseButtonName = "settings-close-button";
        public const string VibrationToggleName = "vibration-toggle";
        public const string SoundToggleName = "sound-toggle";
        public const string MusicToggleName = "music-toggle";
        public const string LegalTermsButtonName = "legal-terms-button";
        public const string RestorePurchasesButtonName = "restore-purchases-button";
        public const string SupportButtonName = "support-button";
        public const string LanguageButtonName = "language-button";
        public const string HomeButtonName = "home-button";
        public const string MessageName = "settings-message";
        public const string KnobName = "toggle-knob";
        public const string ToggleOnClass = "settings-toggle--on";
        public const string ToggleAnimatedClass = "settings-toggle--animated";
        public const string WithHomeClass = "settings-screen--with-home";

        /// <summary>One slot per PlayerSettingId value (Music, Sound, Vibration); update together with the enum.</summary>
        internal const int SettingCount = 3;

        private readonly VisualElement m_screen;
        private readonly VisualElement m_popup;
        private readonly VisualElement m_scrim;
        private readonly Button m_closeButton;
        private readonly Button m_homeButton;
        private readonly Label m_message;
        private readonly Button[] m_toggles = new Button[SettingCount];
        private readonly Action[] m_onToggleClicked = new Action[SettingCount];
        private readonly Action m_onCloseClicked;
        private readonly Action m_onHomeClicked;
        private readonly Clickable m_scrimClickable;
        private Tween m_popupIntro;
        private bool m_disposed;

        /// <param name="screen">The <c>settings-screen</c> element of SettingsPopup.uxml.</param>
        public SettingsPopupView(VisualElement screen)
        {
            m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
            m_popup = Require<VisualElement>(screen, PopupName);
            m_scrim = Require<VisualElement>(screen, ScrimName);
            m_closeButton = Require<Button>(screen, CloseButtonName);
            m_homeButton = Require<Button>(screen, HomeButtonName);
            m_message = Require<Label>(screen, MessageName);
            m_toggles[(int)PlayerSettingId.Music] = Require<Button>(screen, MusicToggleName);
            m_toggles[(int)PlayerSettingId.Sound] = Require<Button>(screen, SoundToggleName);
            m_toggles[(int)PlayerSettingId.Vibration] = Require<Button>(screen, VibrationToggleName);

            for (int i = 0; i < SettingCount; i++)
            {
                PlayerSettingId id = (PlayerSettingId)i;
                Button toggle = m_toggles[i];
                m_onToggleClicked[i] = () =>
                {
                    // Set before the presenter flips the state, so only a tap slides the knob.
                    toggle.AddToClassList(ToggleAnimatedClass);
                    TogglePressed?.Invoke(id);
                };
                m_toggles[i].clicked += m_onToggleClicked[i];
            }

            m_onCloseClicked = OnCloseClicked;
            m_closeButton.clicked += m_onCloseClicked;
            m_onHomeClicked = OnHomeClicked;
            m_homeButton.clicked += m_onHomeClicked;

            // A tap on the scrim outside the popup closes it (AC-2.3a); the scrim also blocks the host (AC-2.6).
            m_scrimClickable = new Clickable(m_onCloseClicked);
            m_scrim.AddManipulator(m_scrimClickable);
        }

        public event Action<PlayerSettingId> TogglePressed;

        public event Action CloseRequested;

        public event Action HomePressed;

        public void SetVisible(bool visible)
        {
            if (m_popupIntro.isAlive)
            {
                m_popupIntro.Stop();
            }

            // A state written while the popup was hidden, or just before it shows, must appear at once, not animate.
            for (int i = 0; i < SettingCount; i++)
            {
                m_toggles[i].RemoveFromClassList(ToggleAnimatedClass);
            }

            m_screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (visible)
            {
                m_popupIntro = Tween.Scale(m_popup.transform, Vector3.one * k_popupIntroStartScale, Vector3.one,
                    k_popupIntroSeconds, Ease.OutQuad, useUnscaledTime: true);
            }
        }

        public void SetToggle(PlayerSettingId id, bool isOn)
        {
            m_toggles[(int)id].EnableInClassList(ToggleOnClass, isOn);
        }

        public void SetMessage(string text)
        {
            bool hasText = !string.IsNullOrEmpty(text);
            m_message.text = hasText ? text : string.Empty;
            m_message.style.visibility = hasText ? Visibility.Visible : Visibility.Hidden;
        }

        public void SetHomeVisible(bool visible)
        {
            m_homeButton.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

            // D-002: the fifth (HOME) button makes the popup taller than an unnotched 1080 x 1920 host can show with
            // the header's overhang clear; this modifier (an ancestor of every row and button) tightens spacing only
            // while HOME is shown. See SettingsPopup.uss "HOME variant fit".
            m_screen.EnableInClassList(WithHomeClass, visible);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            if (m_popupIntro.isAlive)
            {
                m_popupIntro.Stop();
            }

            for (int i = 0; i < SettingCount; i++)
            {
                m_toggles[i].clicked -= m_onToggleClicked[i];
            }

            m_closeButton.clicked -= m_onCloseClicked;
            m_homeButton.clicked -= m_onHomeClicked;
            m_scrim.RemoveManipulator(m_scrimClickable);
            TogglePressed = null;
            CloseRequested = null;
            HomePressed = null;
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        private void OnHomeClicked()
        {
            HomePressed?.Invoke();
        }

        // Runs once per bind, outside any gameplay tick; the message concatenation is a setup error path only.
        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Settings popup view: SettingsPopup.uxml has no " + typeof(T).Name
                                                    + " named '" + name + "'.");
            }

            return element;
        }
    }
}
