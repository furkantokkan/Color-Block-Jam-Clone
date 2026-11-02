using System;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Wraps the Retry confirmation's subtree (RestartConfirmPopup.uxml, Game HUD design 6): it shows or hides the
    /// popup, writes the title into both heavy-text layers and the enabled states of Retry and Close (Retry is on only
    /// while the actions are enabled and its own press lock is off), and forwards the
    /// Retry, close and scrim clicks; it decides nothing.
    /// </summary>
    /// <remarks>
    /// Owner: <see cref="RestartConfirmHost"/>, which creates it on its first show and disposes it. Stops:
    /// <see cref="Dispose"/> removes both click handlers and the scrim's Clickable. Every write skips an unchanged value.
    /// </remarks>
    public sealed class RestartConfirmView : IDisposable
    {
        public const string ScreenName = "restart-confirm-screen";
        public const string ScrimName = "restart-confirm-scrim";
        public const string SafeAreaName = "restart-confirm-safe-area";
        public const string PopupName = "restart-confirm-popup";
        public const string TitleName = "restart-confirm-title";
        public const string TitleBackName = "restart-confirm-title-back";
        public const string HeartName = "restart-confirm-heart";
        public const string PenaltyName = "restart-confirm-penalty";
        public const string MessageName = "restart-confirm-message";
        public const string RetryButtonName = "restart-confirm-retry";
        public const string RetryLabelName = "restart-confirm-retry-label";
        public const string RetryLabelBackName = "restart-confirm-retry-label-back";
        public const string CloseButtonName = "restart-confirm-close";

        private readonly VisualElement m_screen;
        private readonly VisualElement m_scrim;
        private readonly Label m_title;
        private readonly Label m_titleBack;
        private readonly Button m_retry;
        private readonly Label m_retryLabel;
        private readonly Label m_retryLabelBack;
        private readonly Button m_close;
        private readonly Action m_onRetryClicked;
        private readonly Action m_onCloseClicked;
        private readonly Clickable m_scrimClickable;
        private bool m_isVisible;
        private bool m_actionsEnabled = true;
        private bool m_confirmEnabled = true;
        private bool m_disposed;

        /// <param name="screen">The <c>restart-confirm-screen</c> element of RestartConfirmPopup.uxml.</param>
        public RestartConfirmView(VisualElement screen)
        {
            m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
            m_scrim = Require<VisualElement>(screen, ScrimName);
            SafeArea = Require<VisualElement>(screen, SafeAreaName);
            m_title = Require<Label>(screen, TitleName);
            m_titleBack = Require<Label>(screen, TitleBackName);
            m_retry = Require<Button>(screen, RetryButtonName);
            m_retryLabel = Require<Label>(screen, RetryLabelName);
            m_retryLabelBack = Require<Label>(screen, RetryLabelBackName);
            m_close = Require<Button>(screen, CloseButtonName);

            m_onRetryClicked = OnRetryClicked;
            m_onCloseClicked = OnCloseClicked;
            m_retry.clicked += m_onRetryClicked;
            m_close.clicked += m_onCloseClicked;

            // A tap on the scrim outside the popup closes it, as on the Settings popup; the scrim blocks the board.
            m_scrimClickable = new Clickable(m_onCloseClicked);
            m_scrim.AddManipulator(m_scrimClickable);

            // Hidden until the host shows it (the USS default); written inline once so later writes only toggle it.
            m_screen.style.display = DisplayStyle.None;
            m_isVisible = false;
        }

        public event Action ConfirmRequested;

        public event Action CloseRequested;

        /// <summary>The element the host pads from the safe area.</summary>
        public VisualElement SafeArea { get; }

        public void SetVisible(bool visible)
        {
            if (visible == m_isVisible)
            {
                return;
            }

            m_isVisible = visible;
            m_screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public void SetTitle(string text)
        {
            string value = text ?? string.Empty;
            if (string.Equals(m_title.text, value, StringComparison.Ordinal))
            {
                return;
            }

            // Both heavy-text layers always carry the same text (Common.uss).
            m_titleBack.text = value;
            m_title.text = value;
        }

        /// <summary>The green button's text, Retry or Leave (DEC-189), written into both heavy-text layers.</summary>
        public void SetConfirmLabel(string text)
        {
            string value = text ?? string.Empty;
            if (string.Equals(m_retryLabel.text, value, StringComparison.Ordinal))
            {
                return;
            }

            m_retryLabelBack.text = value;
            m_retryLabel.text = value;
        }

        public void SetActionsEnabled(bool enabled)
        {
            if (enabled == m_actionsEnabled)
            {
                return;
            }

            m_actionsEnabled = enabled;
            m_close.SetEnabled(enabled);
            ApplyRetryEnabled();
        }

        /// <summary>Retry alone: off after its press until the flow or the next show turns it on (design 4).</summary>
        public void SetConfirmEnabled(bool enabled)
        {
            if (enabled == m_confirmEnabled)
            {
                return;
            }

            m_confirmEnabled = enabled;
            ApplyRetryEnabled();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_retry.clicked -= m_onRetryClicked;
            m_close.clicked -= m_onCloseClicked;
            m_scrim.RemoveManipulator(m_scrimClickable);
            ConfirmRequested = null;
            CloseRequested = null;
        }

        private void ApplyRetryEnabled()
        {
            bool enabled = m_actionsEnabled && m_confirmEnabled;
            if (m_retry.enabledSelf != enabled)
            {
                m_retry.SetEnabled(enabled);
            }
        }

        private void OnRetryClicked()
        {
            ConfirmRequested?.Invoke();
        }

        private void OnCloseClicked()
        {
            CloseRequested?.Invoke();
        }

        // Runs once per bind, outside any gameplay tick; the message concatenation is a setup error path only.
        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Retry confirmation view: RestartConfirmPopup.uxml has no "
                                                    + typeof(T).Name + " named '" + name + "'.");
            }

            return element;
        }
    }
}
