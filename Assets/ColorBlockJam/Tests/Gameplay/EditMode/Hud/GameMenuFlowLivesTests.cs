using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using Onity.Reactive;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// Verifies the life cost of the in-game menus (DEC-174, TASK-20260928-124512603-1f645a0d AC-1 to AC-3): a failed
    /// attempt costs one life and its Retry takes no second one, the in-level Retry and HOME from a level in progress cost
    /// one, HOME after Won is free, the overlay's actions stay off while the life saves, and with no life left Retry
    /// returns to Hub instead of restarting.
    /// </summary>
    public sealed class GameMenuFlowLivesTests
    {
        private const float k_timeoutSeconds = 5f;

        private FakeSessionControl m_session;
        private FakePauseMenu m_menu;
        private FakeRestartConfirm m_confirm;
        private FakeHubTransition m_transition;
        private GameExitNavigator m_exit;
        private GameMenuFlow m_flow;
        private FakeLives m_lives;

        [TearDown]
        public void TearDown()
        {
            m_flow?.Dispose();
            m_exit?.Dispose();
        }

        [UnityTest]
        public IEnumerator AFailedAttemptCostsOneLifeAndItsRetryTakesNoOther()
        {
            Create(lives: 5);

            m_session.SetState(SessionState.Failed);
            yield return Await(m_flow.PendingLifeCharge);

            Assert.That(m_lives.Count.Value, Is.EqualTo(4));
            Assert.That(m_confirm.IsShowing, Is.True, "the fail popup shows");

            m_confirm.RaiseConfirm();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
            Assert.That(m_lives.Losses, Is.EqualTo(1), "the fail popup's Retry takes no second life");
            Assert.That(m_lives.Count.Value, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator TheInLevelRetryCostsOneLifeAndRestarts()
        {
            Create(lives: 3);

            m_flow.RequestRestart();
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            m_confirm.RaiseConfirm();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_lives.Count.Value, Is.EqualTo(2));
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Playing));

            // The restarted attempt can cost its own life again.
            m_session.SetState(SessionState.Failed);
            yield return Await(m_flow.PendingLifeCharge);
            Assert.That(m_lives.Count.Value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HomeFromALevelInProgressCostsOneLifeButHomeAfterWonIsFree()
        {
            Create(lives: 3);

            m_flow.RequestPause();
            m_menu.RaiseHome();
            Assert.That(m_lives.Losses, Is.EqualTo(0), "HOME only asks; the life goes on Leave (DEC-189)");
            m_confirm.RaiseConfirm();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_lives.Count.Value, Is.EqualTo(2));
            Assert.That(m_transition.Calls, Is.EqualTo(1), "Leave leaves for Hub after the life is saved");

            TearDown();
            Create(lives: 3);
            m_session.SetState(SessionState.Won);
            m_flow.RequestPause();
            m_menu.RaiseHome();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_lives.Losses, Is.EqualTo(0), "a won level costs no life");
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HomeBeforeAnyBlockPressIsFreeAndAsksNothing()
        {
            Create(lives: 3);
            m_session.SetTimerStarted(false);

            m_flow.RequestPause();
            m_menu.RaiseHome();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_confirm.ShowCount, Is.Zero, "no Leave confirmation (DEC-189)");
            Assert.That(m_lives.Losses, Is.EqualTo(0), "an untouched level costs no life");
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClosingTheLeaveConfirmationCostsNoLife()
        {
            Create(lives: 3);

            m_flow.RequestPause();
            m_menu.RaiseHome();
            m_confirm.RaiseClose();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_lives.Losses, Is.EqualTo(0));
            Assert.That(m_menu.IsShowing, Is.True, "back on the pause menu");
            Assert.That(m_transition.Calls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TheOverlayWaitsWhileTheLifeIsSaving()
        {
            Create(lives: 3);
            m_lives.Gate = new TaskCompletionSource<PlayerSaveResult>();

            m_flow.RequestRestart();
            m_confirm.RaiseConfirm();

            Assert.That(m_confirm.ActionsEnabled, Is.False, "Retry and Close are off while the life saves");
            m_confirm.RaiseClose();
            Assert.That(m_session.ResumeCalls, Is.EqualTo(0), "Close cannot resume a paid attempt");
            Assert.That(m_session.RestartCalls, Is.EqualTo(0));

            m_lives.Gate.SetResult(PlayerSaveResult.Saved);
            yield return Await(m_flow.PendingAction);

            Assert.That(m_confirm.ActionsEnabled, Is.True);
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WithNoLifeLeftRetryReturnsToHubInsteadOfRestarting()
        {
            Create(lives: 1);

            m_session.SetState(SessionState.Failed);
            yield return Await(m_flow.PendingLifeCharge);
            Assert.That(m_lives.Count.Value, Is.EqualTo(0));

            m_confirm.RaiseConfirm();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(0), "no attempt starts without a life");
            Assert.That(m_transition.Calls, Is.EqualTo(1), "Retry leaves for Hub, which offers the refill");

            TearDown();
            Create(lives: 1);
            m_flow.RequestRestart();
            m_confirm.RaiseConfirm();
            yield return Await(m_flow.PendingAction);

            Assert.That(m_lives.Count.Value, Is.EqualTo(0), "the in-level Retry spent the last life");
            Assert.That(m_session.AcceptedRestarts, Is.EqualTo(0));
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        private void Create(int lives)
        {
            m_session = new FakeSessionControl();
            m_menu = new FakePauseMenu(m_session);
            m_confirm = new FakeRestartConfirm(m_session);
            m_transition = new FakeHubTransition();
            m_exit = new GameExitNavigator(m_session, m_transition);
            m_lives = new FakeLives(lives);
            m_flow = new GameMenuFlow(m_session, m_menu, m_confirm, new FakeOverlayBackInput(), m_exit,
                new AttemptLevelNumber(), m_lives);
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

        /// <summary>Lives as a counter; a gate holds each loss open until the test releases it.</summary>
        private sealed class FakeLives : ILives
        {
            private readonly ReactiveProperty<int> m_count;

            public FakeLives(int count)
            {
                m_count = new ReactiveProperty<int>(count);
            }

            public CurrencyDefinition Currency => null;

            public IReadOnlyReactiveProperty<int> Count => m_count;

            public int Losses { get; private set; }

            public TaskCompletionSource<PlayerSaveResult> Gate { get; set; }

            public int GetAvailable()
            {
                return m_count.Value;
            }

            public long GetSecondsUntilNext()
            {
                return 0;
            }

            public Task<PlayerSaveResult> RefreshAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(PlayerSaveResult.Saved);
            }

            public async Task<PlayerSaveResult> LoseOneAsync(CancellationToken cancellationToken)
            {
                Losses++;
                if (Gate != null)
                {
                    await Gate.Task;
                }

                m_count.Value = Math.Max(0, m_count.Value - 1);
                return PlayerSaveResult.Saved;
            }

            public Task<CurrencyRefillResult> RefillAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(CurrencyRefillResult.Refilled);
            }
        }
    }
}
