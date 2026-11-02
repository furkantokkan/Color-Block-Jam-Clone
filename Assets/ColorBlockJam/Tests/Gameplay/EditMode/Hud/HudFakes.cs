using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using Onity.Reactive;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// A level session as the HUD port sees it, following LevelSession's rules: Pause only from Playing, Resume and
    /// Restart refused while a write is pending, Restart from Paused or Failed straight to Playing, Home from any other
    /// state. A pause reason change inside Paused raises no notification, as on the real session.
    /// </summary>
    internal sealed class FakeSessionControl : IGameSessionControl
    {
        private readonly ReactiveProperty<SessionState> m_state;
        private readonly ReactiveProperty<bool> m_isOpening = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isWritePending = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<bool> m_isFinishing = new ReactiveProperty<bool>(false);

        // A level in progress by default: the player already pressed a block, so HOME asks first (DEC-189).
        private readonly ReactiveProperty<bool> m_isTimerStarted = new ReactiveProperty<bool>(true);

        public FakeSessionControl(SessionState state = SessionState.Playing)
        {
            m_state = new ReactiveProperty<SessionState>(state);
        }

        public IReadOnlyReactiveProperty<SessionState> State => m_state;

        public PauseReason PauseReason { get; private set; }

        public IReadOnlyReactiveProperty<bool> IsOpening => m_isOpening;

        public IReadOnlyReactiveProperty<bool> IsWritePending => m_isWritePending;

        public IReadOnlyReactiveProperty<bool> IsFinishing => m_isFinishing;

        public IReadOnlyReactiveProperty<bool> IsTimerStarted => m_isTimerStarted;

        public bool AcceptsPause { get; set; } = true;

        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public int RestartCalls { get; private set; }

        public int AcceptedRestarts { get; private set; }

        public int GoHomeCalls { get; private set; }

        public bool Pause()
        {
            PauseCalls++;
            if (!AcceptsPause || m_state.Value != SessionState.Playing)
            {
                return false;
            }

            PauseReason = PauseReason.User;
            m_state.Value = SessionState.Paused;
            return true;
        }

        public bool Resume()
        {
            ResumeCalls++;
            if (m_state.Value != SessionState.Paused || m_isWritePending.Value)
            {
                return false;
            }

            PauseReason = PauseReason.None;
            m_state.Value = SessionState.Playing;
            return true;
        }

        public int ResumeFromBackgroundCalls { get; private set; }

        /// <summary>True when a foreground resume waits for the pending write, as LevelSession defers it (B-13).</summary>
        public bool IsForegroundResumeDeferred { get; private set; }

        public bool ResumeFromBackground()
        {
            ResumeFromBackgroundCalls++;
            if (m_state.Value != SessionState.Paused || PauseReason != PauseReason.Background)
            {
                return false;
            }

            if (m_isWritePending.Value)
            {
                IsForegroundResumeDeferred = true;
                return false;
            }

            return Resume();
        }

        /// <summary>Runs at the start of each Restart call, so a test can read the UI state the flow left before it.</summary>
        public Action BeforeRestart { get; set; }

        public bool Restart()
        {
            RestartCalls++;
            BeforeRestart?.Invoke();
            SessionState state = m_state.Value;
            if (m_isWritePending.Value || (state != SessionState.Paused && state != SessionState.Failed))
            {
                return false;
            }

            AcceptedRestarts++;
            PauseReason = PauseReason.None;
            m_state.Value = SessionState.Playing;
            return true;
        }

        public bool GoHome()
        {
            GoHomeCalls++;
            if (m_isWritePending.Value || m_state.Value == SessionState.Home)
            {
                return false;
            }

            PauseReason = PauseReason.None;
            m_state.Value = SessionState.Home;
            return true;
        }

        /// <summary>Sets the state as the session would after a transition the HUD did not request.</summary>
        public void SetState(SessionState state, PauseReason reason = PauseReason.None)
        {
            PauseReason = state == SessionState.Paused ? reason : PauseReason.None;
            m_state.Value = state;
        }

        /// <summary>A Paused(Saving) or Paused(Purchase) session latching to Background: the reason alone changes.</summary>
        public void LatchBackground()
        {
            PauseReason = PauseReason.Background;
        }

        public void SetOpening(bool value)
        {
            m_isOpening.Value = value;
        }

        public void SetWritePending(bool value)
        {
            m_isWritePending.Value = value;
        }

        public void SetFinishing(bool value)
        {
            m_isFinishing.Value = value;
        }

        public void SetTimerStarted(bool value)
        {
            m_isTimerStarted.Value = value;
        }
    }

    /// <summary>The pause menu as the flow sees it: counts shows and hides and records the state at each show.</summary>
    internal sealed class FakePauseMenu : IPauseMenu
    {
        private readonly IGameSessionControl m_session;

        public FakePauseMenu(IGameSessionControl session)
        {
            m_session = session;
        }

        public event Action CloseRequested;

        public event Action HomeRequested;

        public bool IsShowing { get; private set; }

        public bool CanShow { get; set; } = true;

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public SessionState StateAtShow { get; private set; }

        public PauseReason ReasonAtShow { get; private set; }

        public bool ActionsEnabled { get; private set; } = true;

        public bool Show()
        {
            if (!CanShow)
            {
                return false;
            }

            ShowCount++;
            StateAtShow = m_session.State.Value;
            ReasonAtShow = m_session.PauseReason;
            IsShowing = true;
            return true;
        }

        public void Hide()
        {
            HideCount++;
            IsShowing = false;
        }

        public void SetActionsEnabled(bool enabled)
        {
            ActionsEnabled = enabled;
        }

        public void RaiseClose()
        {
            CloseRequested?.Invoke();
        }

        public void RaiseHome()
        {
            HomeRequested?.Invoke();
        }
    }

    /// <summary>The Retry confirmation as the flow sees it.</summary>
    internal sealed class FakeRestartConfirm : IRestartConfirm
    {
        private readonly IGameSessionControl m_session;

        public FakeRestartConfirm(IGameSessionControl session)
        {
            m_session = session;
        }

        public event Action CloseRequested;

        public event Action ConfirmRequested;

        public bool IsShowing { get; private set; }

        public bool CanShow { get; set; } = true;

        public int ShowCount { get; private set; }

        public int HideCount { get; private set; }

        public string LastTitle { get; private set; }

        /// <summary>What the green button read at the last show: Retry or Leave.</summary>
        public LifeConfirmAction LastAction { get; private set; }

        public SessionState StateAtShow { get; private set; }

        public bool ActionsEnabled { get; private set; } = true;

        /// <summary>The Retry button's own lock, as the real host keeps it: on again at every show.</summary>
        public bool ConfirmEnabled { get; private set; } = true;

        /// <summary>How many times the flow locked the Retry button.</summary>
        public int ConfirmDisableCount { get; private set; }

        public bool Show(string title, LifeConfirmAction action)
        {
            if (!CanShow)
            {
                return false;
            }

            ShowCount++;
            LastTitle = title;
            LastAction = action;
            StateAtShow = m_session.State.Value;
            ConfirmEnabled = true;
            IsShowing = true;
            return true;
        }

        public void Hide()
        {
            HideCount++;
            IsShowing = false;
        }

        public void SetActionsEnabled(bool enabled)
        {
            ActionsEnabled = enabled;
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (!enabled)
            {
                ConfirmDisableCount++;
            }

            ConfirmEnabled = enabled;
        }

        public void RaiseClose()
        {
            CloseRequested?.Invoke();
        }

        public void RaiseConfirm()
        {
            ConfirmRequested?.Invoke();
        }
    }

    /// <summary>The overlay Back action: a press reaches the flow only while enabled, as a disabled InputAction does.</summary>
    internal sealed class FakeOverlayBackInput : IOverlayBackInput
    {
        public event Action Pressed;

        public bool IsEnabled { get; private set; }

        public int EnableCount { get; private set; }

        public void Enable()
        {
            EnableCount++;
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public void Press()
        {
            if (IsEnabled)
            {
                Pressed?.Invoke();
            }
        }
    }

    /// <summary>
    /// The Hub transition seam: records each call and its token and returns the task the test chose (an unfinished task
    /// by default, so the exit stays in flight).
    /// </summary>
    internal sealed class FakeHubTransition : IHubSceneTransition
    {
        public int Calls { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public Func<CancellationToken, Task> Next { get; set; } = token => new TaskCompletionSource<bool>().Task;

        public Task TransitionAsync(CancellationToken cancellationToken)
        {
            Calls++;
            LastToken = cancellationToken;
            return Next(cancellationToken);
        }
    }

    /// <summary>The top bar view without VisualElements: records every write.</summary>
    internal sealed class FakeTopBarView : ITopBarView
    {
        public event Action RetryPressed;

        public event Action PausePressed;

        public string LevelNumber { get; private set; }

        public string CoinAmount { get; private set; }

        public int CoinWrites { get; private set; }

        public int HideCoinCount { get; private set; }

        public bool RetryEnabled { get; private set; } = true;

        public bool PauseEnabled { get; private set; } = true;

        public int EnabledWrites { get; private set; }

        public void SetLevelNumber(string text)
        {
            LevelNumber = text;
        }

        public void SetCoinAmount(string text)
        {
            CoinWrites++;
            CoinAmount = text;
        }

        public void HideCoin()
        {
            HideCoinCount++;
        }

        public void SetRetryEnabled(bool enabled)
        {
            EnabledWrites++;
            RetryEnabled = enabled;
        }

        public void SetPauseEnabled(bool enabled)
        {
            PauseEnabled = enabled;
        }

        public void ClickRetry()
        {
            RetryPressed?.Invoke();
        }

        public void ClickPause()
        {
            PausePressed?.Invoke();
        }
    }

    /// <summary>The top bar's two menu requests, counted.</summary>
    internal sealed class FakeGameMenu : IGameMenu
    {
        public int PauseRequests { get; private set; }

        public int RestartRequests { get; private set; }

        public void RequestPause()
        {
            PauseRequests++;
        }

        public void RequestRestart()
        {
            RestartRequests++;
        }
    }

    /// <summary>One committed balance for every currency, changed by the test.</summary>
    internal sealed class FakeWallet : IWallet
    {
        private readonly ReactiveProperty<int> m_balance;

        public FakeWallet(int balance)
        {
            m_balance = new ReactiveProperty<int>(balance);
        }

        public int GetBalanceCalls { get; private set; }

        public IReadOnlyReactiveProperty<int> GetBalance(CurrencyDefinition currency)
        {
            GetBalanceCalls++;
            return m_balance;
        }

        public bool CanAfford(CurrencyDefinition currency, int amount)
        {
            return m_balance.Value >= amount;
        }

        public void SetBalance(int balance)
        {
            m_balance.Value = balance;
        }
    }
}
