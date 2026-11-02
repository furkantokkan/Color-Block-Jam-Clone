using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Hud.Result;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The win result (reference 05, AC-4.7 to AC-4.11, AC-6.14, DEC-173): the save starts once on Won, the popup opens
    /// only after Finish and Teardown, its one button waits for the commit, a failed save offers Retry of the same call,
    /// Continue loads the next level number once, also past the last database level, a level without a number goes
    /// Home, and Back acts as Home only after the commit.
    /// </summary>
    public sealed class LevelCompleteFlowTests
    {
        private const float k_timeoutSeconds = 5f;

        private CurrencyDefinition m_coin;
        private CurrencyCatalog m_currencies;
        private FakeSessionControl m_session;
        private FakePopup m_popup;
        private FakeRecorder m_recorder;
        private FakeHubTransition m_hub;
        private GameExitNavigator m_exit;
        private FakeNextLevel m_nextLevel;
        private AttemptLevelNumber m_levelNumber;
        private FakeOverlayBackInput m_back;
        private LevelCompleteFlow m_flow;

        [SetUp]
        public void SetUp()
        {
            m_coin = ScriptableObject.CreateInstance<CurrencyDefinition>();
            UnityEditor.SerializedObject coin = new UnityEditor.SerializedObject(m_coin);
            coin.FindProperty("m_id").stringValue = "coin";
            coin.FindProperty("m_displayName").stringValue = "Coins";
            coin.FindProperty("m_maxAmount").intValue = 1000;
            coin.ApplyModifiedPropertiesWithoutUndo();
            m_currencies = ScriptableObject.CreateInstance<CurrencyCatalog>();
            UnityEditor.SerializedObject catalog = new UnityEditor.SerializedObject(m_currencies);
            UnityEditor.SerializedProperty definitions = catalog.FindProperty("m_definitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = m_coin;
            catalog.ApplyModifiedPropertiesWithoutUndo();

            m_session = new FakeSessionControl();
            m_popup = new FakePopup();
            m_recorder = new FakeRecorder();
            m_hub = new FakeHubTransition();
            m_exit = new GameExitNavigator(m_session, m_hub);
            m_nextLevel = new FakeNextLevel();
            m_levelNumber = new AttemptLevelNumber();
            m_levelNumber.Set(1);
            m_back = new FakeOverlayBackInput();
            m_flow = new LevelCompleteFlow(m_session, m_popup, m_recorder, m_exit, m_nextLevel, m_levelNumber,
                m_currencies, m_back);
        }

        [TearDown]
        public void TearDown()
        {
            m_flow.Dispose();
            m_exit.Dispose();
            Object.DestroyImmediate(m_currencies);
            Object.DestroyImmediate(m_coin);
        }

        [Test]
        public void WonStartsTheSaveOnceAndThePopupWaitsForTheFinish()
        {
            m_session.SetFinishing(true);
            m_session.SetState(SessionState.Won);

            Assert.That(m_recorder.Calls, Is.EqualTo(1), "the save starts on Won, independently of Finish");
            Assert.That(m_recorder.LevelIndex, Is.EqualTo(0));
            Assert.That(m_recorder.Reward, Is.EqualTo(LevelCompleteFlow.k_winCoinReward));
            Assert.That(m_recorder.Currency, Is.SameAs(m_coin));
            Assert.That(m_popup.IsShowing, Is.False, "nothing shows while Finish runs");

            m_session.SetFinishing(false);

            Assert.That(m_popup.IsShowing, Is.True);
            Assert.That(m_popup.RewardText, Is.EqualTo(LevelCompleteFlow.k_winCoinReward.ToString()),
                "the amount without a sign, as in reference 05");
            Assert.That(m_popup.Action, Is.EqualTo(LevelCompleteAction.Continue));
            Assert.That(m_popup.PrimaryEnabled, Is.False, "Continue waits for the commit");
            Assert.That(m_back.IsEnabled, Is.True);
            Assert.That(m_recorder.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ThePopupShowingStartsPreparingTheNextLevelOnce()
        {
            m_session.SetFinishing(true);
            m_session.SetState(SessionState.Won);
            Assert.That(m_nextLevel.PrepareCalls, Is.EqualTo(0), "nothing prepares while Finish runs");

            m_session.SetFinishing(false);

            Assert.That(m_nextLevel.PrepareCalls, Is.EqualTo(1), "the next level builds hidden behind the popup (DEC-199)");
            Assert.That(m_nextLevel.PreparedIndex, Is.EqualTo(1));
            Assert.That(m_nextLevel.Calls, Is.EqualTo(0), "preparing opens nothing: Continue still waits for the commit");
        }

        [Test]
        public void ALevelWithoutANumberPreparesNothing()
        {
            m_levelNumber.Set(0);
            ShowWon();

            Assert.That(m_nextLevel.PrepareCalls, Is.EqualTo(0), "a Test Level outside the database has no next level");
        }

        [UnityTest]
        public IEnumerator ACommitEnablesContinueWhichLoadsTheNextLevelOnce()
        {
            ShowWon();
            m_recorder.Complete(PlayerSaveResult.Saved);
            yield return Await(m_flow.PendingSave);

            Assert.That(m_popup.PrimaryEnabled, Is.True);
            Assert.That(m_popup.Message, Is.Null.Or.Empty);

            m_popup.PressPrimary();
            m_popup.PressPrimary();

            Assert.That(m_nextLevel.Calls, Is.EqualTo(1), "a second tap during the load has no effect (AC-4.8)");
            Assert.That(m_nextLevel.LevelIndex, Is.EqualTo(1));
            Assert.That(m_popup.PrimaryEnabled, Is.False);
            Assert.That(m_hub.Calls, Is.EqualTo(0), "Continue never goes Home");
            Assert.That(m_recorder.Calls, Is.EqualTo(1), "no second reward");
        }

        [UnityTest]
        public IEnumerator AFailedSaveOffersRetryOfTheSameWin()
        {
            ShowWon();
            m_recorder.Complete(PlayerSaveResult.SaveFailed);
            yield return Await(m_flow.PendingSave);

            Assert.That(m_popup.Action, Is.EqualTo(LevelCompleteAction.Retry));
            Assert.That(m_popup.PrimaryEnabled, Is.True);
            Assert.That(m_popup.Message, Is.EqualTo(LevelCompleteFlow.SaveFailedMessage));

            m_back.Press();
            Assert.That(m_hub.Calls, Is.EqualTo(0), "Back stays on the Retry screen (AC-4.11)");

            m_popup.PressPrimary();
            Assert.That(m_recorder.Calls, Is.EqualTo(2));
            Assert.That(m_recorder.LevelIndex, Is.EqualTo(0), "the same win is saved again");
            Assert.That(m_nextLevel.Calls, Is.EqualTo(0));

            m_recorder.Complete(PlayerSaveResult.Saved);
            yield return Await(m_flow.PendingSave);

            Assert.That(m_popup.Action, Is.EqualTo(LevelCompleteAction.Continue));
            Assert.That(m_popup.PrimaryEnabled, Is.True);
            Assert.That(m_popup.Message, Is.Null.Or.Empty);
        }

        [UnityTest]
        public IEnumerator PastTheLastDatabaseLevelContinueLoadsTheNextNumber()
        {
            // Level 5 of the five-level case, then Level 12 of a later round: the numbers keep counting (DEC-173).
            m_levelNumber.Set(5);
            ShowWon();
            m_recorder.Complete(PlayerSaveResult.Saved);
            yield return Await(m_flow.PendingSave);

            Assert.That(m_recorder.LevelIndex, Is.EqualTo(4));
            Assert.That(m_popup.Action, Is.EqualTo(LevelCompleteAction.Continue));
            Assert.That(m_popup.RewardText, Is.EqualTo(LevelCompleteFlow.RewardText));
            Assert.That(m_popup.Message, Is.Null.Or.Empty, "no last-level note");

            m_popup.PressPrimary();

            Assert.That(m_nextLevel.LevelIndex, Is.EqualTo(5), "Level 6");
            Assert.That(m_hub.Calls, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator BackAfterTheCommitGoesHomeOnceAndThePopupStaysDuringTheExit()
        {
            ShowWon();
            m_back.Press();
            Assert.That(m_hub.Calls, Is.EqualTo(0), "Back waits for the commit");

            m_recorder.Complete(PlayerSaveResult.Saved);
            yield return Await(m_flow.PendingSave);

            m_back.Press();
            m_back.Press();

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_hub.Calls, Is.EqualTo(1));
            Assert.That(m_popup.IsShowing, Is.True, "the popup stays up while Hub loads");
            Assert.That(m_popup.PrimaryEnabled, Is.False);
        }

        [Test]
        public void ALevelWithoutANumberSavesNothingAndItsButtonGoesHome()
        {
            m_levelNumber.Set(AttemptLevelNumber.k_unknown);
            ShowWon();

            Assert.That(m_recorder.Calls, Is.EqualTo(0));
            Assert.That(m_popup.RewardText, Is.Null, "no reward row for a win that records nothing");
            Assert.That(m_popup.Action, Is.EqualTo(LevelCompleteAction.Home));
            Assert.That(m_popup.PrimaryEnabled, Is.True);

            m_popup.PressPrimary();
            m_popup.PressPrimary();
            Assert.That(m_hub.Calls, Is.EqualTo(1));
            Assert.That(m_nextLevel.Calls, Is.EqualTo(0));
        }

        [Test]
        public void DisposeCancelsTheSaveWaitAndDisablesBack()
        {
            ShowWon();

            m_flow.Dispose();

            Assert.That(m_recorder.Token.IsCancellationRequested, Is.True);
            Assert.That(m_back.IsEnabled, Is.False);
        }

        private void ShowWon()
        {
            m_session.SetState(SessionState.Won);
            Assert.That(m_popup.IsShowing, Is.True);
        }

        private static IEnumerator Await(Task task)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task finished in time");
            task.GetAwaiter().GetResult();
        }

        /// <summary>The popup without VisualElements: records every write.</summary>
        private sealed class FakePopup : ILevelCompletePopup
        {
            public event Action PrimaryRequested;

            public bool IsShowing { get; private set; }

            public string RewardText { get; private set; }

            public LevelCompleteAction Action { get; private set; } = LevelCompleteAction.Continue;

            public bool PrimaryEnabled { get; private set; } = true;

            public string Message { get; private set; }

            public bool Show(string rewardText)
            {
                RewardText = rewardText;
                IsShowing = true;
                return true;
            }

            public void SetPrimary(LevelCompleteAction action, bool enabled)
            {
                Action = action;
                PrimaryEnabled = enabled;
            }

            public void SetMessage(string text)
            {
                Message = text;
            }

            public void Hide()
            {
                IsShowing = false;
            }

            public void PressPrimary()
            {
                PrimaryRequested?.Invoke();
            }
        }

        /// <summary>The win save seam: each call waits until the test completes it.</summary>
        private sealed class FakeRecorder : ILevelWinRecorder
        {
            private TaskCompletionSource<PlayerSaveResult> m_pending;

            public int Calls { get; private set; }

            public int LevelIndex { get; private set; } = -1;

            public int Reward { get; private set; }

            public CurrencyDefinition Currency { get; private set; }

            public CancellationToken Token { get; private set; }

            public Task<PlayerSaveResult> RecordWinAsync(int wonLevelIndex, CurrencyDefinition currency, int reward,
                CancellationToken cancellationToken)
            {
                Calls++;
                LevelIndex = wonLevelIndex;
                Currency = currency;
                Reward = reward;
                Token = cancellationToken;
                m_pending = new TaskCompletionSource<PlayerSaveResult>();
                return m_pending.Task;
            }

            public void Complete(PlayerSaveResult result)
            {
                m_pending.SetResult(result);
            }
        }

        /// <summary>The next-level seam: records each call and stays in flight.</summary>
        private sealed class FakeNextLevel : INextLevelTransition
        {
            public int Calls { get; private set; }

            public int LevelIndex { get; private set; } = -1;

            public int PrepareCalls { get; private set; }

            public int PreparedIndex { get; private set; } = -1;

            public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
            {
                PrepareCalls++;
                PreparedIndex = levelIndex;
                return Task.CompletedTask;
            }

            public Task TransitionAsync(int levelIndex, CancellationToken cancellationToken)
            {
                Calls++;
                LevelIndex = levelIndex;
                return new TaskCompletionSource<bool>().Task;
            }
        }
    }
}
