using System;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>
    /// Wraps the Level Complete subtree (LevelCompletePopup.uxml, reference 05): it shows or hides the popup, writes the
    /// one button's label and reward row and the note into both heavy-text layers, forwards the button click and hands
    /// out the confetti surface and the coin pile it bursts from; it decides nothing.
    /// </summary>
    /// <remarks>
    /// Owner: <see cref="LevelCompleteHost"/>, which creates it on its first show and disposes it. Stops:
    /// <see cref="Dispose"/> removes the click handler. Every write skips an unchanged value; nothing runs per frame.
    /// </remarks>
    public sealed class LevelCompleteView : IDisposable
    {
        public const string ScreenName = "level-complete-screen";
        public const string SafeAreaName = "level-complete-safe-area";
        public const string CoinsName = "level-complete-coins";
        public const string ConfettiName = "level-complete-confetti";
        public const string ContinueButtonName = "level-complete-continue";
        public const string ContinueLabelName = "level-complete-continue-label";
        public const string ContinueLabelBackName = "level-complete-continue-label-back";
        public const string RewardName = "level-complete-reward";
        public const string RewardLabelName = "level-complete-reward-label";
        public const string RewardLabelBackName = "level-complete-reward-label-back";
        public const string MessageBoxName = "level-complete-message-box";
        public const string MessageName = "level-complete-message";
        public const string MessageBackName = "level-complete-message-back";

        public const string ContinueText = "Continue";
        public const string RetryText = "Retry";
        public const string HomeText = "Home";

        private readonly VisualElement m_screen;
        private readonly Button m_continue;
        private readonly Label m_continueLabel;
        private readonly Label m_continueLabelBack;
        private readonly VisualElement m_reward;
        private readonly Label m_rewardLabel;
        private readonly Label m_rewardLabelBack;
        private readonly VisualElement m_messageBox;
        private readonly Label m_message;
        private readonly Label m_messageBack;
        private readonly Action m_onContinueClicked;
        private bool m_isVisible;
        private LevelCompleteAction m_action = LevelCompleteAction.Continue;
        private bool m_disposed;

        /// <param name="screen">The <c>level-complete-screen</c> element of LevelCompletePopup.uxml.</param>
        public LevelCompleteView(VisualElement screen)
        {
            m_screen = screen ?? throw new ArgumentNullException(nameof(screen));
            SafeArea = Require<VisualElement>(screen, SafeAreaName);
            Coins = Require<VisualElement>(screen, CoinsName);
            ConfettiSurface = Require<VisualElement>(screen, ConfettiName);
            m_continue = Require<Button>(screen, ContinueButtonName);
            m_continueLabel = Require<Label>(screen, ContinueLabelName);
            m_continueLabelBack = Require<Label>(screen, ContinueLabelBackName);
            m_reward = Require<VisualElement>(screen, RewardName);
            m_rewardLabel = Require<Label>(screen, RewardLabelName);
            m_rewardLabelBack = Require<Label>(screen, RewardLabelBackName);
            m_messageBox = Require<VisualElement>(screen, MessageBoxName);
            m_message = Require<Label>(screen, MessageName);
            m_messageBack = Require<Label>(screen, MessageBackName);

            m_onContinueClicked = OnContinueClicked;
            m_continue.clicked += m_onContinueClicked;

            // Hidden until the host shows it (the USS default); written inline once so later writes only toggle it.
            m_screen.style.display = DisplayStyle.None;
            m_messageBox.style.display = DisplayStyle.None;
        }

        public event Action PrimaryRequested;

        /// <summary>The element the host pads from the safe area.</summary>
        public VisualElement SafeArea { get; }

        /// <summary>The coin pile: the confetti bursts from its centre, behind it.</summary>
        public VisualElement Coins { get; }

        /// <summary>The full-screen element that shows the confetti camera's image, behind the popup's content.</summary>
        public VisualElement ConfettiSurface { get; }

        public LevelCompleteAction Action => m_action;

        public string PrimaryText => m_continueLabel.text;

        public bool IsPrimaryEnabled => m_continue.enabledSelf;

        public string RewardText => m_rewardLabel.text;

        public string Message => m_message.text;

        public void SetVisible(bool visible)
        {
            if (visible == m_isVisible)
            {
                return;
            }

            m_isVisible = visible;
            m_screen.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>The amount beside the coin on the button; null or empty hides the reward row.</summary>
        public void SetReward(string text)
        {
            SetPair(m_rewardLabel, m_rewardLabelBack, text);
            ApplyRewardRow();
        }

        public void SetPrimary(LevelCompleteAction action, bool enabled)
        {
            if (action != m_action)
            {
                m_action = action;
                SetPair(m_continueLabel, m_continueLabelBack, GetLabel(action));
                ApplyRewardRow();
            }

            if (m_continue.enabledSelf != enabled)
            {
                m_continue.SetEnabled(enabled);
            }
        }

        public void SetMessage(string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            m_messageBox.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            SetPair(m_message, m_messageBack, text);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_continue.clicked -= m_onContinueClicked;
            PrimaryRequested = null;
        }

        private static string GetLabel(LevelCompleteAction action)
        {
            switch (action)
            {
                case LevelCompleteAction.Retry:
                    return RetryText;
                case LevelCompleteAction.Home:
                    return HomeText;
                default:
                    return ContinueText;
            }
        }

        // The reward is what Continue or Home collects; Retry repeats a save and shows none (AC-6.14).
        private void ApplyRewardRow()
        {
            bool visible = m_action != LevelCompleteAction.Retry && !string.IsNullOrEmpty(m_rewardLabel.text);
            m_reward.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        // Both heavy-text layers always carry the same text (Common.uss).
        private static void SetPair(Label front, Label back, string text)
        {
            string value = text ?? string.Empty;
            if (string.Equals(front.text, value, StringComparison.Ordinal))
            {
                return;
            }

            back.text = value;
            front.text = value;
        }

        private void OnContinueClicked()
        {
            PrimaryRequested?.Invoke();
        }

        // Runs once per bind, outside any gameplay tick; the message concatenation is a setup error path only.
        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Level Complete view: LevelCompletePopup.uxml has no "
                                                    + typeof(T).Name + " named '" + name + "'.");
            }

            return element;
        }
    }
}
