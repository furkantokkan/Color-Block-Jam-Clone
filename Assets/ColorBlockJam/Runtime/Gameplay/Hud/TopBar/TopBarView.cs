using System;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.TopBar
{
    /// <summary>
    /// Wraps the top bar of GameTopBar.uxml (Game HUD design 3): it finds the Level badge labels, the coin holder and
    /// the Retry and Pause buttons, forwards the two button clicks as C# events and writes only text, display and
    /// enabled states. The coin plus has no handler, so it gives the shared press feedback and nothing else (RULE-24).
    /// Every bar text is a heavy-text pair (Common.uss); Booster's HUD view writes only the front timer and freeze
    /// labels, so this view copies each of their changes into the back layer.
    /// </summary>
    /// <remarks>
    /// Owner: <see cref="GameTopBar"/>, which creates it once over the HUD tree. Stops: <see cref="Dispose"/> removes
    /// both click handlers, both text-change callbacks and the attach callback. Every write skips an unchanged value,
    /// so repeated writes change nothing in the tree. The text-change callbacks run only when Booster's view changes
    /// the text (once per shown second at most) and copy the same string reference, so they allocate nothing of their
    /// own. A label raises ChangeEvent only while it is on a panel, so the bar also resynchronises both back layers
    /// when it is attached to a panel again (for example after its UIDocument is re-enabled).
    /// </remarks>
    public sealed class TopBarView : ITopBarView, IDisposable
    {
        public const string BarName = "game-top-bar";
        public const string LevelNumberName = "level-number";
        public const string LevelNumberBackName = "level-number-back";
        public const string CoinHolderName = "coin-holder";
        public const string CoinAmountName = "coin-amount";
        public const string CoinAmountBackName = "coin-amount-back";
        public const string CoinPlusName = "coin-plus-button";
        public const string RetryButtonName = "retry-button";
        public const string PauseButtonName = "pause-button";
        public const string TimerLabelName = "timer-label";
        public const string TimerLabelBackName = "timer-label-back";
        public const string FreezeLabelName = "freeze-label";
        public const string FreezeLabelBackName = "freeze-label-back";

        private readonly Label m_levelNumber;
        private readonly Label m_levelNumberBack;
        private readonly VisualElement m_coinHolder;
        private readonly Label m_coinAmount;
        private readonly Label m_coinAmountBack;
        private readonly Label m_timer;
        private readonly Label m_timerBack;
        private readonly Label m_freeze;
        private readonly Label m_freezeBack;
        private readonly Button m_retry;
        private readonly Button m_pause;
        private readonly Action m_onRetryClicked;
        private readonly Action m_onPauseClicked;
        private readonly EventCallback<ChangeEvent<string>> m_onTimerChanged;
        private readonly EventCallback<ChangeEvent<string>> m_onFreezeChanged;
        private readonly EventCallback<AttachToPanelEvent> m_onAttached;
        private bool m_coinVisible = true;
        private bool m_disposed;

        /// <param name="root">The hud-root element of GameplayHud.uxml, or any ancestor of the top bar.</param>
        public TopBarView(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            Bar = Require<VisualElement>(root, BarName);
            m_levelNumber = Require<Label>(Bar, LevelNumberName);
            m_levelNumberBack = Require<Label>(Bar, LevelNumberBackName);
            m_coinHolder = Require<VisualElement>(Bar, CoinHolderName);
            m_coinAmount = Require<Label>(Bar, CoinAmountName);
            m_coinAmountBack = Require<Label>(Bar, CoinAmountBackName);
            m_timer = Require<Label>(Bar, TimerLabelName);
            m_timerBack = Require<Label>(Bar, TimerLabelBackName);
            m_freeze = Require<Label>(Bar, FreezeLabelName);
            m_freezeBack = Require<Label>(Bar, FreezeLabelBackName);
            CoinPlus = Require<Button>(Bar, CoinPlusName);
            m_retry = Require<Button>(Bar, RetryButtonName);
            m_pause = Require<Button>(Bar, PauseButtonName);

            m_onRetryClicked = OnRetryClicked;
            m_onPauseClicked = OnPauseClicked;
            m_retry.clicked += m_onRetryClicked;
            m_pause.clicked += m_onPauseClicked;

            // Booster's view may already have written the fronts (the HUD shows before the top bar on Home).
            SyncBackLayers();
            m_onTimerChanged = OnTimerChanged;
            m_onFreezeChanged = OnFreezeChanged;
            m_onAttached = OnAttached;
            m_timer.RegisterCallback(m_onTimerChanged);
            m_freeze.RegisterCallback(m_onFreezeChanged);
            Bar.RegisterCallback(m_onAttached);
        }

        public event Action RetryPressed;

        public event Action PausePressed;

        public VisualElement Bar { get; }

        public Button RetryButton => m_retry;

        public Button PauseButton => m_pause;

        public Button CoinPlus { get; }

        public string LevelNumber => m_levelNumber.text;

        public string CoinAmount => m_coinAmount.text;

        public void SetLevelNumber(string text)
        {
            string value = text ?? string.Empty;
            if (string.Equals(m_levelNumber.text, value, StringComparison.Ordinal))
            {
                return;
            }

            // Both heavy-text layers always carry the same text (Common.uss).
            m_levelNumberBack.text = value;
            m_levelNumber.text = value;
        }

        public void SetCoinAmount(string text)
        {
            if (!m_coinVisible)
            {
                m_coinVisible = true;
                m_coinHolder.style.display = DisplayStyle.Flex;
            }

            string value = text ?? string.Empty;
            if (!string.Equals(m_coinAmount.text, value, StringComparison.Ordinal))
            {
                // Both heavy-text layers always carry the same text (Common.uss).
                m_coinAmountBack.text = value;
                m_coinAmount.text = value;
            }
        }

        public void HideCoin()
        {
            if (m_coinVisible)
            {
                m_coinVisible = false;
                m_coinHolder.style.display = DisplayStyle.None;
            }
        }

        public void SetRetryEnabled(bool enabled)
        {
            if (m_retry.enabledSelf != enabled)
            {
                m_retry.SetEnabled(enabled);
            }
        }

        public void SetPauseEnabled(bool enabled)
        {
            if (m_pause.enabledSelf != enabled)
            {
                m_pause.SetEnabled(enabled);
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_retry.clicked -= m_onRetryClicked;
            m_pause.clicked -= m_onPauseClicked;
            m_timer.UnregisterCallback(m_onTimerChanged);
            m_freeze.UnregisterCallback(m_onFreezeChanged);
            Bar.UnregisterCallback(m_onAttached);
            RetryPressed = null;
            PausePressed = null;
        }

        // A changed front text raises ChangeEvent<string> on its label; the back layer takes the same string without a
        // second event.
        private static void CopyText(Label back, string text)
        {
            if (!string.Equals(back.text, text, StringComparison.Ordinal))
            {
                ((INotifyValueChanged<string>)back).SetValueWithoutNotify(text);
            }
        }

        // Texts written while the tree was off a panel raised no ChangeEvent; attaching again copies them once.
        private void SyncBackLayers()
        {
            CopyText(m_timerBack, m_timer.text);
            CopyText(m_freezeBack, m_freeze.text);
        }

        private void OnAttached(AttachToPanelEvent attached)
        {
            SyncBackLayers();
        }

        private void OnTimerChanged(ChangeEvent<string> change)
        {
            CopyText(m_timerBack, change.newValue);
        }

        private void OnFreezeChanged(ChangeEvent<string> change)
        {
            CopyText(m_freezeBack, change.newValue);
        }

        private void OnRetryClicked()
        {
            RetryPressed?.Invoke();
        }

        private void OnPauseClicked()
        {
            PausePressed?.Invoke();
        }

        // Runs once per bind, outside any gameplay tick; the message concatenation is a setup error path only.
        private static T Require<T>(VisualElement root, string name) where T : VisualElement
        {
            T element = root.Q<T>(name);
            if (element == null)
            {
                throw new InvalidOperationException("Top bar view: GameTopBar.uxml has no " + typeof(T).Name
                                                    + " named '" + name + "'.");
            }

            return element;
        }
    }
}
