using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the one-transition guard of <see cref="LevelLauncher"/> through a fake scene transition, and the
    /// immutable <see cref="LevelLaunchRequest"/> it carries (contract scenarios 6 and 8, design 9.3).
    /// </summary>
    public sealed class LevelLauncherTests
    {
        private FakeTransition m_transition;
        private LevelLauncher m_launcher;

        [SetUp]
        public void SetUp()
        {
            m_transition = new FakeTransition();
            m_launcher = new LevelLauncher(m_transition);
        }

        [TearDown]
        public void TearDown()
        {
            m_launcher.Dispose();
        }

        [UnityTest]
        public IEnumerator Launch_TransitionsOnceWithTheRequest_AndIgnoresEveryLaterLaunch()
        {
            var request = new LevelLaunchRequest(3, new[] { BoosterId.ExtraTime });

            Task<LevelLaunchResult> first = m_launcher.LaunchAsync(request, CancellationToken.None);
            Task<LevelLaunchResult> second = m_launcher.LaunchAsync(new LevelLaunchRequest(0, null), CancellationToken.None);

            Assert.That(m_launcher.IsLaunching, Is.True);
            Assert.That(second.IsCompleted, Is.True);
            Assert.That(second.Result, Is.EqualTo(LevelLaunchResult.Ignored));
            Assert.That(m_transition.Requests, Is.EqualTo(new[] { request }));

            m_transition.Complete();
            yield return PreLevelTestFixtures.Await(first);

            Assert.That(first.Result, Is.EqualTo(LevelLaunchResult.Completed));
            Assert.That(m_launcher.IsLaunching, Is.True, "a completed launch stays latched");
            Assert.That(m_launcher.LaunchAsync(request, CancellationToken.None).Result, Is.EqualTo(LevelLaunchResult.Ignored));
            Assert.That(m_transition.Requests.Count, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator FailedTransition_LogsOnceAndAllowsAnotherLaunch()
        {
            var request = new LevelLaunchRequest(1, null);
            Task<LevelLaunchResult> first = m_launcher.LaunchAsync(request, CancellationToken.None);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: scene missing");
            m_transition.Fail(new InvalidOperationException("scene missing"));
            yield return PreLevelTestFixtures.Await(first);

            Assert.That(first.Result, Is.EqualTo(LevelLaunchResult.Failed));
            Assert.That(m_launcher.IsLaunching, Is.False);

            m_launcher.LaunchAsync(request, CancellationToken.None);
            Assert.That(m_transition.Requests.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator Dispose_CancelsThePendingWait()
        {
            Task<LevelLaunchResult> launch = m_launcher.LaunchAsync(new LevelLaunchRequest(0, null), CancellationToken.None);

            m_launcher.Dispose();
            yield return PreLevelTestFixtures.Await(launch);

            Assert.That(m_transition.LastToken.IsCancellationRequested, Is.True);
            Assert.That(launch.Result, Is.EqualTo(LevelLaunchResult.Cancelled));
            Assert.That(m_launcher.LaunchAsync(new LevelLaunchRequest(0, null), CancellationToken.None).Result,
                Is.EqualTo(LevelLaunchResult.Ignored), "a disposed launcher starts nothing");
        }

        [UnityTest]
        public IEnumerator CallerCancellation_ReleasesTheGuard()
        {
            using (var caller = new CancellationTokenSource())
            {
                Task<LevelLaunchResult> launch = m_launcher.LaunchAsync(new LevelLaunchRequest(0, null), caller.Token);

                caller.Cancel();
                yield return PreLevelTestFixtures.Await(launch);

                Assert.That(launch.Result, Is.EqualTo(LevelLaunchResult.Cancelled));
                Assert.That(m_launcher.IsLaunching, Is.False);
            }
        }

        [Test]
        public void Launch_WithAnAlreadyCancelledToken_StartsNothing()
        {
            Task<LevelLaunchResult> launch = m_launcher.LaunchAsync(new LevelLaunchRequest(0, null), new CancellationToken(true));

            Assert.That(launch.Result, Is.EqualTo(LevelLaunchResult.Ignored));
            Assert.That(m_transition.Requests, Is.Empty);
        }

        [Test]
        public void Launch_WithNoRequest_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => m_launcher.LaunchAsync(null, CancellationToken.None));
        }

        [Test]
        public void Request_CopiesTheStartingBoosters()
        {
            var selected = new List<BoosterId> { BoosterId.ExtraTime, BoosterId.StartingRocket };
            var request = new LevelLaunchRequest(2, selected);
            selected.Clear();

            Assert.That(request.LevelIndex, Is.EqualTo(2));
            Assert.That(request.StartingBoosters, Is.EqualTo(new[] { BoosterId.ExtraTime, BoosterId.StartingRocket }));
            Assert.That(new LevelLaunchRequest(0, null).StartingBoosters, Is.Empty);
        }

        [Test]
        public void Request_RejectsANegativeIndexNoneAndDuplicates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelLaunchRequest(-1, null));
            Assert.Throws<ArgumentException>(() => new LevelLaunchRequest(0, new[] { BoosterId.None }));
            Assert.Throws<ArgumentException>(() => new LevelLaunchRequest(0, new[] { BoosterId.StartingRocket, BoosterId.StartingRocket }));
        }

        /// <summary>A scene transition that records each request and finishes when the test says so.</summary>
        private sealed class FakeTransition : ILevelSceneTransition
        {
            private TaskCompletionSource<bool> m_pending;

            public List<LevelLaunchRequest> Requests { get; } = new List<LevelLaunchRequest>();

            public CancellationToken LastToken { get; private set; }

            public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }

            public Task TransitionAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
            {
                Requests.Add(request);
                LastToken = cancellationToken;
                m_pending = new TaskCompletionSource<bool>();
                cancellationToken.Register(() => m_pending.TrySetCanceled());
                return m_pending.Task;
            }

            public void Complete()
            {
                m_pending.TrySetResult(true);
            }

            public void Fail(Exception exception)
            {
                m_pending.TrySetException(exception);
            }
        }
    }
}
