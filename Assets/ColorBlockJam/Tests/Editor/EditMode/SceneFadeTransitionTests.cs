using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.SceneFlow;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Proves the order and ownership of <see cref="SceneFadeTransition"/> (DEC-198) with a fake fade, a fake scene
    /// change and a fake readiness wait: the change runs only once the screen is black, the fade lifts only after the
    /// new scene is ready, one change runs at a time, a failed change reveals the old scene, and a cancelled caller or
    /// disposal ends exactly what it owns.
    /// </summary>
    public sealed class SceneFadeTransitionTests
    {
        private const float k_timeoutSeconds = 5f;

        private List<string> m_log;
        private FakeFade m_fade;
        private FakeReadiness m_readiness;
        private SceneFadeTransition m_transition;

        [SetUp]
        public void SetUp()
        {
            m_log = new List<string>();
            m_fade = new FakeFade(m_log);
            m_readiness = new FakeReadiness(m_log);
            m_transition = new SceneFadeTransition(m_fade, m_readiness);
        }

        [TearDown]
        public void TearDown()
        {
            m_transition.Dispose();
        }

        [UnityTest]
        public IEnumerator TheChangeRunsBehindTheBlackAndTheFadeLiftsOnlyOnceTheSceneIsReady()
        {
            var change = new FakeChange(m_log);
            Task run = m_transition.RunAsync(change.RunAsync, CancellationToken.None);

            Assert.That(m_log, Is.EqualTo(new[] { "fade out" }), "nothing changes before the screen is black");
            Assert.That(m_transition.IsRunning, Is.True);

            m_fade.CompleteFadeOut();
            yield return WaitUntil(() => m_log.Count == 2);
            Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change" }));

            change.Complete();
            yield return WaitUntil(() => m_log.Count == 3);
            Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change", "wait for scene" }),
                "the fade holds until the new scene reports ready");

            m_readiness.Complete();
            yield return WaitUntil(() => run.IsCompleted);
            Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change", "wait for scene", "fade in" }));
            Assert.That(run.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(m_transition.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator ARequestWhileAChangeRunsIsIgnoredAndCompletesAtOnce()
        {
            var first = new FakeChange(m_log);
            var second = new FakeChange(m_log);
            Task running = m_transition.RunAsync(first.RunAsync, CancellationToken.None);

            Task ignored = m_transition.RunAsync(second.RunAsync, CancellationToken.None);

            Assert.That(ignored.Status, Is.EqualTo(TaskStatus.RanToCompletion));
            m_fade.CompleteFadeOut();
            first.Complete();
            m_readiness.Complete();
            yield return WaitUntil(() => running.IsCompleted);
            Assert.That(first.Calls, Is.EqualTo(1));
            Assert.That(second.Calls, Is.EqualTo(0), "the ignored change never runs");
            Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change", "wait for scene", "fade in" }));
        }

        [UnityTest]
        public IEnumerator AFailedChangeFadesTheOldSceneBackInAndReportsTheFailure()
        {
            var change = new FakeChange(m_log);
            Task run = m_transition.RunAsync(change.RunAsync, CancellationToken.None);
            m_fade.CompleteFadeOut();
            yield return WaitUntil(() => change.Calls == 1);

            change.Fail(new InvalidOperationException("scene missing"));
            yield return WaitUntil(() => run.IsCompleted);

            Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change", "fade in" }), "no readiness wait for the old scene");
            Assert.That(run.IsFaulted, Is.True);
            Assert.That(run.Exception.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(m_transition.IsRunning, Is.False, "the caller can offer the action again");

            var retry = new FakeChange(m_log);
            m_transition.RunAsync(retry.RunAsync, CancellationToken.None);
            Assert.That(m_transition.IsRunning, Is.True, "a later request runs");
        }

        [UnityTest]
        public IEnumerator CancellingTheCallerEndsItsWaitButNotTheStartedChange()
        {
            var change = new FakeChange(m_log);
            using (var caller = new CancellationTokenSource())
            {
                Task run = m_transition.RunAsync(change.RunAsync, caller.Token);
                m_fade.CompleteFadeOut();
                yield return WaitUntil(() => change.Calls == 1);

                // The load disposes the scene that asked, which cancels its token.
                caller.Cancel();
                yield return WaitUntil(() => run.IsCompleted);
                Assert.That(run.IsCanceled, Is.True, "the caller's wait ends");
                Assert.That(change.Token.IsCancellationRequested, Is.False, "the change keeps its own token");
                Assert.That(m_transition.IsRunning, Is.True);

                change.Complete();
                m_readiness.Complete();
                yield return WaitUntil(() => !m_transition.IsRunning);
                Assert.That(m_log, Is.EqualTo(new[] { "fade out", "change", "wait for scene", "fade in" }),
                    "the new scene still fades in");
            }
        }

        [Test]
        public void AnAlreadyCancelledCallerStartsNothing()
        {
            var change = new FakeChange(m_log);
            Task run = m_transition.RunAsync(change.RunAsync, new CancellationToken(true));

            Assert.That(run.IsCanceled, Is.True);
            Assert.That(m_log, Is.Empty);
            Assert.That(m_transition.IsRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator DisposeStopsARunningChangeAndIgnoresLaterRequests()
        {
            var change = new FakeChange(m_log);
            Task run = m_transition.RunAsync(change.RunAsync, CancellationToken.None);

            m_transition.Dispose();
            yield return WaitUntil(() => run.IsCompleted);

            Assert.That(run.IsCanceled, Is.True, "the fade stopped on the lifetime token");
            Assert.That(change.Calls, Is.EqualTo(0));
            Assert.That(m_transition.IsRunning, Is.False);
            Assert.That(m_transition.RunAsync(change.RunAsync, CancellationToken.None).Status,
                Is.EqualTo(TaskStatus.RanToCompletion));
            Assert.That(change.Calls, Is.EqualTo(0), "a disposed transition runs nothing");
        }

        // Continuations resume through the Editor's synchronization context, so each step yields until it has run.
        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(condition(), Is.True, "the step never ran");
        }

        private sealed class FakeFade : IScreenFade
        {
            private readonly List<string> m_log;
            private TaskCompletionSource<bool> m_fadeOut;

            public FakeFade(List<string> log)
            {
                m_log = log;
            }

            public bool IsVisible { get; private set; }

            public Task FadeOutAsync(CancellationToken cancellationToken)
            {
                m_log.Add("fade out");
                IsVisible = true;
                m_fadeOut = new TaskCompletionSource<bool>();
                cancellationToken.Register(() => m_fadeOut.TrySetCanceled());
                return m_fadeOut.Task;
            }

            public Task FadeInAsync(CancellationToken cancellationToken)
            {
                m_log.Add("fade in");
                IsVisible = false;
                return Task.CompletedTask;
            }

            public void CompleteFadeOut()
            {
                m_fadeOut.TrySetResult(true);
            }
        }

        private sealed class FakeReadiness : IActiveSceneReadiness
        {
            private readonly List<string> m_log;
            private readonly TaskCompletionSource<bool> m_ready = new TaskCompletionSource<bool>();

            public FakeReadiness(List<string> log)
            {
                m_log = log;
            }

            public Task WaitUntilReadyAsync(CancellationToken cancellationToken)
            {
                m_log.Add("wait for scene");
                return m_ready.Task;
            }

            public void Complete()
            {
                m_ready.TrySetResult(true);
            }
        }

        private sealed class FakeChange
        {
            private readonly List<string> m_log;
            private readonly TaskCompletionSource<bool> m_done = new TaskCompletionSource<bool>();

            public FakeChange(List<string> log)
            {
                m_log = log;
            }

            public int Calls { get; private set; }

            public CancellationToken Token { get; private set; }

            public Task RunAsync(CancellationToken cancellationToken)
            {
                Calls++;
                Token = cancellationToken;
                m_log.Add("change");
                return m_done.Task;
            }

            public void Complete()
            {
                m_done.TrySetResult(true);
            }

            public void Fail(Exception exception)
            {
                m_done.TrySetException(exception);
            }
        }
    }
}
