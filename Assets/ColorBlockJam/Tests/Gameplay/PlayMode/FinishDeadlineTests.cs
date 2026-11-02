#if UNITY_EDITOR
using System.Collections;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.PlayMode
{
    /// <summary>
    /// Proves the bounded Won Finish with the real presenter: the last exit latches Won and stops input
    /// in that tick, and Teardown completes within 0.8 foreground seconds, at the 0.8 s cap when the
    /// final motion reports more, or at once when the app is backgrounded (AC-4.12, DEC-93, V-BST-9
    /// door-exit part).
    /// </summary>
    public sealed class FinishDeadlineTests
    {
        private const float k_finishBoundSeconds = 0.8f;
        private const float k_frameSeconds = 1f / 60f;

        private PresenterScene m_scene;
        private ScriptedPointerSource m_pointer;
        private LevelSession m_session;
        private PrimeTweenFinishDeadline m_finishDeadline;
        private Transform m_exitRoot;

        [SetUp]
        public void SetUp()
        {
            m_scene = new PresenterScene();
            m_pointer = new ScriptedPointerSource();
        }

        [TearDown]
        public void TearDown()
        {
            m_session?.Dispose();
            m_session = null;
            m_finishDeadline?.Dispose();
            m_finishDeadline = null;
            m_scene.Dispose();
        }

        [UnityTest]
        public IEnumerator TheLastExitLatchesWonAndTearsDownWithinTheFinishBound()
        {
            StartSession(m_scene.Presenter);
            PushTheOnlyBlockOut();

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_pointer.IsEnabled, Is.False, "Input stops in the tick that latches Won.");
            Assert.That(m_session.Attempt, Is.Not.Null, "The final exit motion runs before Teardown.");
            Assert.That(Tween.GetTweensCount(m_exitRoot), Is.EqualTo(1));

            float start = Time.realtimeSinceStartup;
            float longestFrame = 0f;
            while (m_session.Attempt != null && Time.realtimeSinceStartup - start < 2f * k_finishBoundSeconds)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            }

            float elapsed = Time.realtimeSinceStartup - start;
            Assert.That(m_session.Attempt, Is.Null, "Teardown ran.");
            Assert.That(elapsed, Is.LessThanOrEqualTo(k_finishBoundSeconds + longestFrame), "Finish ends within 0.8 s plus one frame.");
            AssertTornDown();
        }

        [UnityTest]
        public IEnumerator AFinishLongerThanTheBoundEndsAtTheCap()
        {
            // The real exit ends after about 0.24 s, but the presenter reports 5 s: only the cap ends it.
            StartSession(new SlowFinishPresenter(m_scene.Presenter, 5f));
            PushTheOnlyBlockOut();
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Won));

            float start = Time.realtimeSinceStartup;
            float longestFrame = 0f;
            while (m_session.Attempt != null && Time.realtimeSinceStartup - start < 2f * k_finishBoundSeconds)
            {
                yield return null;
                longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            }

            float elapsed = Time.realtimeSinceStartup - start;
            Assert.That(m_session.Attempt, Is.Null, "Teardown ran.");
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(k_finishBoundSeconds - longestFrame), "The cap, not the exit's end, ended Finish.");
            Assert.That(elapsed, Is.LessThanOrEqualTo(k_finishBoundSeconds + longestFrame), "Finish ends within 0.8 s plus one frame.");
            AssertTornDown();
        }

        [Test]
        public void BackgroundingDuringFinishTearsDownAtOnce()
        {
            StartSession(m_scene.Presenter);
            PushTheOnlyBlockOut();
            Assert.That(m_session.Attempt, Is.Not.Null);

            m_session.NotifyBackground(true);

            Assert.That(m_session.Attempt, Is.Null);
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Won));
            AssertTornDown();
        }

        [UnityTest]
        public IEnumerator ReplacingADeadlineRunsOnlyTheNewCompletion()
        {
            m_finishDeadline = new PrimeTweenFinishDeadline();
            int replacedCalls = 0;
            int completedCalls = 0;
            m_finishDeadline.Start(0.01f, () => replacedCalls++);
            m_finishDeadline.Start(0.02f, () => completedCalls++);

            yield return new WaitForSecondsRealtime(0.08f);

            Assert.That(replacedCalls, Is.Zero);
            Assert.That(completedCalls, Is.EqualTo(1));
            Assert.That(Tween.GetTweensCount(m_finishDeadline), Is.Zero);
        }

        [UnityTest]
        public IEnumerator CancellationAndDisposalSuppressPendingCallbacks()
        {
            m_finishDeadline = new PrimeTweenFinishDeadline();
            int completedCalls = 0;
            m_finishDeadline.Start(0.01f, () => completedCalls++);
            m_finishDeadline.Cancel();
            m_finishDeadline.Cancel();
            yield return new WaitForSecondsRealtime(0.04f);
            Assert.That(completedCalls, Is.Zero);

            m_finishDeadline.Start(0.01f, () => completedCalls++);
            m_finishDeadline.Dispose();
            yield return new WaitForSecondsRealtime(0.04f);
            Assert.That(completedCalls, Is.Zero);
            Assert.That(Tween.GetTweensCount(m_finishDeadline), Is.Zero);
        }

        // Prepares the one-block level on Home with the given presenter and plays it.
        private void StartSession(IBoardPresenter presenter)
        {
            m_finishDeadline = new PrimeTweenFinishDeadline();
            m_session = new LevelSession(new DragInputRouter(m_pointer, new NoUiPointerFilter(), new IdentityBoardProjector()),
                m_pointer, presenter, new InactiveCountdown(), new NoPauseOverlay(true),
                new EmptyAttemptHookSource(), m_finishDeadline);
            AttemptLevel level = PresenterScene.CreateLevel(withSecondBlock: false);
            Assert.That(m_session.TryPrepare(level, out string error), Is.True, error);
            Assert.That(m_session.Play(), Is.True);
            m_exitRoot = m_scene.Presenter.GetPiece(0).transform;
        }

        // Press the block at (1, 2), then push 0.4 cell up through the red top door: its only block leaves.
        private void PushTheOnlyBlockOut()
        {
            Vector2 press = new Vector2(PresenterScene.ExitBlockOrigin.X, PresenterScene.ExitBlockOrigin.Y);
            m_pointer.Set(PointerPhase.Down, press);
            m_session.RunTick(k_frameSeconds);
            m_pointer.Set(PointerPhase.Held, press + new Vector2(0f, 0.4f));
            m_session.RunTick(k_frameSeconds);
        }

        private void AssertTornDown()
        {
            Assert.That(m_pointer.IsEnabled, Is.False);
            Assert.That(m_scene.Presenter.ActivePieceCount, Is.Zero, "Every root is back in its pool.");
            Assert.That(Tween.GetTweensCount(m_exitRoot), Is.Zero, "No gameplay tween survives Teardown.");
            Assert.That(Tween.GetTweensCount(m_finishDeadline), Is.Zero, "The Finish deadline is stopped.");
        }
    }
}
#endif
