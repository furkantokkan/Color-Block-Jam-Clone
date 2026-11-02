using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using Onity.Reactive;

namespace ColorBlockJam.Gameplay.Hud.TopBar
{
    /// <summary>
    /// Drives the top bar (Game HUD design 4): the Level number it is given on Home, the coin balance as
    /// <see cref="CurrencyText.FormatCompact"/> (formatted only when the committed balance changes, AC-4), and the
    /// enabled states of Retry and Pause from the session state, the opening hold and a pending booster write, matching
    /// the menu flow's rules so an enabled button always acts (AC-9); Retry and Pause clicks go to the menu flow.
    /// </summary>
    /// <remarks>
    /// Owner: <see cref="GameTopBar"/>. Its four subscriptions go into one CompositeDisposable that
    /// <see cref="Unbind"/> clears; <see cref="Dispose"/> is the last stop point. Every update comes from a transition,
    /// a balance change or a click, never from a Playing tick.
    /// </remarks>
    public sealed class TopBarPresenter : IDisposable
    {
        private const int k_nothingShown = int.MinValue;

        private readonly IGameSessionControl m_session;
        private readonly IGameMenu m_menu;
        private readonly IWallet m_wallet;
        private readonly CurrencyDefinition m_coin;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<SessionState> m_onStateChanged;
        private readonly Observer<bool> m_onFlagChanged;
        private readonly Observer<int> m_onBalanceChanged;
        private readonly Action m_onRetryPressed;
        private readonly Action m_onPausePressed;

        private ITopBarView m_view;
        private string m_levelText = string.Empty;
        private int m_shownBalance = k_nothingShown;
        private bool m_disposed;

        /// <param name="coin">The coin currency, or null: the holder is then hidden.</param>
        public TopBarPresenter(IGameSessionControl session, IGameMenu menu, IWallet wallet, CurrencyDefinition coin)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_menu = menu ?? throw new ArgumentNullException(nameof(menu));
            m_wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            m_coin = coin;
            m_onStateChanged = OnStateChanged;
            m_onFlagChanged = OnFlagChanged;
            m_onBalanceChanged = OnBalanceChanged;
            m_onRetryPressed = OnRetryPressed;
            m_onPausePressed = OnPausePressed;
        }

        public bool IsBound => m_view != null;

        /// <summary>
        /// True exactly where <see cref="GameMenuFlow.RequestRestart"/> acts: Playing outside the opening hold and
        /// without a pending write, or Failed.
        /// </summary>
        public static bool IsRetryEnabled(SessionState state, bool isOpening, bool isWritePending)
        {
            switch (state)
            {
                case SessionState.Playing:
                    return !isOpening && !isWritePending;
                case SessionState.Failed:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True exactly where <see cref="GameMenuFlow.RequestPause"/> acts: Playing (the opening hold included) or Won,
        /// and never while a booster write is pending. Failed shows the fail popup instead, whose close goes Home.
        /// </summary>
        public static bool IsPauseEnabled(SessionState state, bool isWritePending)
        {
            bool stateAllows = state == SessionState.Playing || state == SessionState.Won;
            return stateAllows && !isWritePending;
        }

        public void Bind(ITopBarView view)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(TopBarPresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Top bar presenter: already bound; unbind first.");
            }

            m_view = view;
            m_view.RetryPressed += m_onRetryPressed;
            m_view.PausePressed += m_onPausePressed;
            m_view.SetLevelNumber(m_levelText);
            m_shownBalance = k_nothingShown;

            // Stop point: Unbind. Each emits its current value at once, then only on a change.
            m_subscriptions.Add(m_session.State.Subscribe(m_onStateChanged));
            m_subscriptions.Add(m_session.IsOpening.Subscribe(m_onFlagChanged));
            m_subscriptions.Add(m_session.IsWritePending.Subscribe(m_onFlagChanged));
            if (m_coin != null)
            {
                m_subscriptions.Add(m_wallet.GetBalance(m_coin).Subscribe(m_onBalanceChanged));
            }
            else
            {
                m_view.HideCoin();
            }
        }

        /// <summary>Shows the level's number text; called on Home for each level.</summary>
        public void ShowLevel(string numberText)
        {
            m_levelText = numberText ?? string.Empty;
            m_view?.SetLevelNumber(m_levelText);
        }

        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_view.RetryPressed -= m_onRetryPressed;
            m_view.PausePressed -= m_onPausePressed;
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

        private void OnStateChanged(SessionState state)
        {
            UpdateButtons();
        }

        private void OnFlagChanged(bool value)
        {
            UpdateButtons();
        }

        private void UpdateButtons()
        {
            if (m_view == null)
            {
                return;
            }

            SessionState state = m_session.State.Value;
            bool isWritePending = m_session.IsWritePending.Value;
            m_view.SetRetryEnabled(IsRetryEnabled(state, m_session.IsOpening.Value, isWritePending));
            m_view.SetPauseEnabled(IsPauseEnabled(state, isWritePending));
        }

        // The committed balance changes only after a save, never in a Playing tick; one string per change.
        private void OnBalanceChanged(int balance)
        {
            if (m_view == null || balance == m_shownBalance)
            {
                return;
            }

            m_shownBalance = balance;
            m_view.SetCoinAmount(CurrencyText.FormatCompact(balance));
        }

        private void OnRetryPressed()
        {
            m_menu.RequestRestart();
        }

        private void OnPausePressed()
        {
            m_menu.RequestPause();
        }
    }
}
