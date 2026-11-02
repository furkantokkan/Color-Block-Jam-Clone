using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using Onity.DI;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>
    /// Runs the win result (reference 05, AC-4.7 to AC-4.11, AC-6.14, DEC-7, DEC-173): on Won it records the +10 coin
    /// reward and the next saved level as one save, shows the Level Complete popup once Finish and Teardown are over,
    /// and leaves through its one button, Continue to the next level, only after that save committed.
    /// </summary>
    /// <remarks>
    /// Rules: the save starts once per attempt when the session enters Won, independently of Finish (DEC-93). While
    /// it runs the button is off; a failed save turns it into Retry, which repeats the same call, so the reward is
    /// never added twice. When the popup shows, the next level starts building hidden behind it, so Continue opens it at
    /// once (DEC-199). After a commit, Continue loads the next level once. Level numbers keep counting past the last
    /// database level and its levels play again (DEC-173), so every level has a next one; only a Level Editor Test
    /// Level outside the active database, which saves nothing, offers Home instead. Back acts as Home once the save
    /// committed and does nothing before (AC-4.11); a scrim tap does nothing.
    /// Owner: the Game scene container (AsSingle, HUD branch), registered after the session so it is disposed first.
    /// Stops: <see cref="Dispose"/> disposes the two state subscriptions, removes the popup and Back handlers, disables
    /// and (when it created it) disposes its Back action, and cancels the lifetime token that the save wait and the
    /// next-level load await. The Single load of the next scene disposes this flow mid-await; the cancellation is
    /// caught and nothing is touched afterwards. Nothing runs per frame.
    /// </remarks>
    public sealed class LevelCompleteFlow : IDisposable
    {
        /// <summary>The coins one win adds (DEC-7: +10, adjustable case tuning).</summary>
        public const int k_winCoinReward = 10;

        public const string SaveFailedMessage = "Could not save. Try again.";

        /// <summary>The reward row on the button: <see cref="k_winCoinReward"/> beside a coin, without a sign.</summary>
        public const string RewardText = "10";

        private readonly IGameSessionControl m_session;
        private readonly ILevelCompletePopup m_popup;
        private readonly ILevelWinRecorder m_recorder;
        private readonly IGameExitNavigator m_exit;
        private readonly INextLevelTransition m_nextLevel;
        private readonly AttemptLevelNumber m_levelNumber;
        private readonly CurrencyDefinition m_coin;
        private readonly IOverlayBackInput m_back;
        private readonly OverlayBackInput m_ownedBack;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private readonly Action m_onPrimaryRequested;
        private readonly Action m_onHomeRequested;

        private WinSave m_save = WinSave.None;
        private int m_wonLevelIndex = -1;
        private bool m_isShowing;
        private bool m_isLeaving;
        private bool m_disposed;

        /// <summary>The Game scene's flow: it owns its own Back action, separate from the pause menu's.</summary>
        [Inject]
        public LevelCompleteFlow(IGameSessionControl session, ILevelCompletePopup popup, ILevelWinRecorder recorder,
            IGameExitNavigator exit, INextLevelTransition nextLevel, AttemptLevelNumber levelNumber,
            CurrencyCatalog currencies)
            : this(session, popup, recorder, exit, nextLevel, levelNumber, currencies, new OverlayBackInput(), true)
        {
        }

        /// <summary>A flow over a given Back source, which the caller keeps owning (tests).</summary>
        public LevelCompleteFlow(IGameSessionControl session, ILevelCompletePopup popup, ILevelWinRecorder recorder,
            IGameExitNavigator exit, INextLevelTransition nextLevel, AttemptLevelNumber levelNumber,
            CurrencyCatalog currencies, IOverlayBackInput back)
            : this(session, popup, recorder, exit, nextLevel, levelNumber, currencies, back, false)
        {
        }

        private LevelCompleteFlow(IGameSessionControl session, ILevelCompletePopup popup, ILevelWinRecorder recorder,
            IGameExitNavigator exit, INextLevelTransition nextLevel, AttemptLevelNumber levelNumber,
            CurrencyCatalog currencies, IOverlayBackInput back, bool ownsBack)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_popup = popup ?? throw new ArgumentNullException(nameof(popup));
            m_recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            m_exit = exit ?? throw new ArgumentNullException(nameof(exit));
            m_nextLevel = nextLevel ?? throw new ArgumentNullException(nameof(nextLevel));
            m_levelNumber = levelNumber ?? throw new ArgumentNullException(nameof(levelNumber));
            m_back = back ?? throw new ArgumentNullException(nameof(back));
            m_ownedBack = ownsBack ? back as OverlayBackInput : null;
            if (currencies == null)
            {
                throw new ArgumentNullException(nameof(currencies));
            }

            if (!currencies.TryGet(TopBar.GameTopBar.CoinCurrencyId, out m_coin))
            {
                Debug.LogWarning("Level Complete: the currency catalog has no '" + TopBar.GameTopBar.CoinCurrencyId
                                 + "' currency; a win saves no reward or progression.");
            }

            m_onPrimaryRequested = RequestPrimary;
            m_onHomeRequested = RequestHome;
            m_popup.PrimaryRequested += m_onPrimaryRequested;
            m_back.Pressed += m_onHomeRequested;

            // Stop point: Dispose. Both emit their current value at once.
            m_subscriptions.Add(m_session.State.Subscribe(OnStateChanged));
            m_subscriptions.Add(m_session.IsFinishing.Subscribe(OnFinishingChanged));
        }

        private enum WinSave
        {
            None,
            Saving,
            Saved,
            Failed
        }

        public bool IsShowing => m_isShowing;

        /// <summary>The win save in flight, or a completed task; it never faults. Tests await it.</summary>
        public Task PendingSave { get; private set; } = Task.CompletedTask;

        /// <summary>The next-level load in flight, or a completed task; it never faults. Tests await it.</summary>
        public Task PendingNextLevel { get; private set; } = Task.CompletedTask;

        /// <summary>The wait for the next level's hidden preparation, or a completed task; it never faults.</summary>
        public Task PendingPreparation { get; private set; } = Task.CompletedTask;

        // A level of the player's progression; a Test Level outside the active database has no number.
        private bool IsKnownLevel => m_wonLevelIndex >= 0;

        /// <summary>The one button: Retry after a failed save, else Continue, or Home for a level without a number.</summary>
        public void RequestPrimary()
        {
            if (m_disposed || !m_isShowing || m_isLeaving)
            {
                return;
            }

            switch (m_save)
            {
                case WinSave.Failed:
                    PendingSave = SaveAsync();
                    break;
                case WinSave.Saved when IsKnownLevel:
                    m_isLeaving = true;
                    Apply();
                    PendingNextLevel = LoadNextLevelAsync(m_wonLevelIndex + 1);
                    break;
                case WinSave.Saved:
                    RequestHome();
                    break;
            }
        }

        /// <summary>Home (Back, or the button of a level without a number): allowed once the win save committed.</summary>
        public void RequestHome()
        {
            if (m_disposed || !m_isShowing || m_isLeaving || m_save != WinSave.Saved)
            {
                return;
            }

            // Set before the call: the session raises Home inside it, and the popup must stay up during the exit.
            m_isLeaving = true;
            if (!m_exit.TryGoHome())
            {
                m_isLeaving = m_exit.IsExiting;
            }

            Apply();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
            m_subscriptions.Dispose();
            m_popup.PrimaryRequested -= m_onPrimaryRequested;
            m_back.Pressed -= m_onHomeRequested;
            m_back.Disable();
            m_ownedBack?.Dispose();
        }

        private void OnStateChanged(SessionState state)
        {
            switch (state)
            {
                case SessionState.Won:
                    StartWin();
                    TryShow();
                    break;
                case SessionState.Home when m_isLeaving:
                    // Our own Home exit: the popup stays until the Hub load replaces the scene.
                    break;
                default:
                    Hide();
                    break;
            }
        }

        private void OnFinishingChanged(bool finishing)
        {
            if (!finishing)
            {
                TryShow();
            }
        }

        // Once per attempt: Won is terminal, and the next level reloads the scene with a new flow.
        private void StartWin()
        {
            if (m_save != WinSave.None)
            {
                return;
            }

            // AttemptLevelNumber is 1-based and 0 for a Test Level outside the active database.
            m_wonLevelIndex = m_levelNumber.Value - 1;
            if (!IsKnownLevel || m_coin is null)
            {
                m_save = WinSave.Saved;
                return;
            }

            PendingSave = SaveAsync();
        }

        // Shows only after Finish and Teardown (PROJECT.md Won Finish): nothing live remains behind the popup.
        private void TryShow()
        {
            if (m_disposed || m_isShowing || m_session.State.Value != SessionState.Won || m_session.IsFinishing.Value)
            {
                return;
            }

            // A win that saves nothing (a level without a number) shows no reward row.
            if (!m_popup.Show(IsKnownLevel && !(m_coin is null) ? RewardText : null))
            {
                return;
            }

            m_isShowing = true;
            m_back.Enable();
            Apply();

            // Hot level (DEC-199): the next level builds hidden behind the popup, so Continue opens it at once.
            if (IsKnownLevel)
            {
                PendingPreparation = PrepareNextLevelAsync(m_wonLevelIndex + 1);
            }
        }

        private void Hide()
        {
            if (!m_isShowing)
            {
                return;
            }

            m_isShowing = false;
            m_back.Disable();
            m_popup.Hide();
        }

        // Owner: this flow, which stores the task. Stops: the lifetime token (scene unload) ends the wait for the write
        // lock; a started write completes under the save service's own lifetime (DEC-92).
        private async Task SaveAsync()
        {
            m_save = WinSave.Saving;
            Apply();

            PlayerSaveResult result;
            try
            {
                result = await m_recorder.RecordWinAsync(m_wonLevelIndex, m_coin, k_winCoinReward, m_lifetime.Token);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = PlayerSaveResult.SaveFailed;
            }

            if (m_disposed)
            {
                return;
            }

            m_save = result == PlayerSaveResult.Saved ? WinSave.Saved : WinSave.Failed;
            Apply();
        }

        // Owner: this flow, which stores the task; the preparation itself belongs to the level preloader, because the
        // prepared scene outlives this one. Stops: the preparation's end, or the lifetime token, which ends this wait.
        private async Task PrepareNextLevelAsync(int levelIndex)
        {
            try
            {
                await m_nextLevel.PrepareAsync(levelIndex, m_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The scene unloaded: nothing is touched.
            }
            catch (Exception exception)
            {
                // Continue then loads the level behind the fade.
                Debug.LogException(exception);
            }
        }

        // Owner: this flow, which stores the task. Stops: the lifetime token; the Single load disposes this flow.
        private async Task LoadNextLevelAsync(int levelIndex)
        {
            try
            {
                await m_nextLevel.TransitionAsync(levelIndex, m_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The Game scene unloaded under the load: this flow is gone, so nothing is touched.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!m_disposed)
                {
                    m_isLeaving = false;
                    Apply();
                }
            }
        }

        // Writes the popup's button and note from the save state; each write skips an unchanged value in the view.
        private void Apply()
        {
            if (!m_isShowing)
            {
                return;
            }

            bool saved = m_save == WinSave.Saved;
            bool failed = m_save == WinSave.Failed;
            LevelCompleteAction action = failed
                ? LevelCompleteAction.Retry
                : IsKnownLevel ? LevelCompleteAction.Continue : LevelCompleteAction.Home;
            m_popup.SetPrimary(action, !m_isLeaving && (saved || failed));
            m_popup.SetMessage(failed ? SaveFailedMessage : null);
        }
    }
}
