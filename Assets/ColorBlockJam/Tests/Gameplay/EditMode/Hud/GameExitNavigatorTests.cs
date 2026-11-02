using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Hud.Navigation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The pause menu's HOME exit guard (Game HUD design 4, contract AC-8) over a fake Hub transition: Home is entered
    /// first and the transition starts once; a second HOME while it runs or after it completed does nothing; a refused
    /// Home starts nothing; a failed transition logs once and releases the guard, and the retry repeats only the
    /// transition; Dispose cancels the token the transition awaits and refuses later exits.
    /// </summary>
    public sealed class GameExitNavigatorTests
    {
        private FakeSessionControl m_session;
        private FakeHubTransition m_transition;
        private GameExitNavigator m_exit;

        // NUnit reuses the fixture instance, so every collaborator is rebuilt here.
        [SetUp]
        public void SetUp()
        {
            m_session = new FakeSessionControl(SessionState.Paused);
            m_transition = new FakeHubTransition();
            m_exit = new GameExitNavigator(m_session, m_transition);
        }

        [TearDown]
        public void TearDown()
        {
            m_exit.Dispose();
        }

        [Test]
        public void TheFirstHomeEntersHomeAndStartsOneTransition()
        {
            Assert.That(m_exit.TryGoHome(), Is.True);

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Home), "the attempt is torn down before the load");
            Assert.That(m_transition.Calls, Is.EqualTo(1));
            Assert.That(m_exit.IsExiting, Is.True);
            Assert.That(m_transition.LastToken.CanBeCanceled, Is.True, "the transition awaits the lifetime token");
        }

        [Test]
        public void ASecondHomeWhileTheTransitionRunsDoesNothing()
        {
            Assert.That(m_exit.TryGoHome(), Is.True);

            Assert.That(m_exit.TryGoHome(), Is.False);

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1));
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ACompletedTransitionKeepsTheGuard()
        {
            m_transition.Next = token => Task.CompletedTask;

            Assert.That(m_exit.TryGoHome(), Is.True);
            Assert.That(m_exit.PendingExit.IsCompleted, Is.True);

            Assert.That(m_exit.TryGoHome(), Is.False);
            Assert.That(m_exit.IsExiting, Is.True);
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ARefusedHomeStartsNoTransition()
        {
            m_session.SetWritePending(true);

            Assert.That(m_exit.TryGoHome(), Is.False);

            Assert.That(m_session.State.Value, Is.EqualTo(SessionState.Paused));
            Assert.That(m_transition.Calls, Is.Zero);
            Assert.That(m_exit.IsExiting, Is.False);
        }

        [Test]
        public void AFailedTransitionLogsOnceReleasesTheGuardAndTheRetryOnlyRepeatsTheTransition()
        {
            m_transition.Next = token => Task.FromException(new InvalidOperationException("Hub failed to load"));
            LogAssert.Expect(LogType.Exception, new Regex("Hub failed to load"));

            Assert.That(m_exit.TryGoHome(), Is.True);

            Assert.That(m_exit.PendingExit.IsCompleted, Is.True);
            Assert.That(m_exit.PendingExit.IsFaulted, Is.False, "the exit task never faults");
            Assert.That(m_exit.IsExiting, Is.False, "HOME can be pressed again");

            m_transition.Next = token => Task.CompletedTask;
            Assert.That(m_exit.TryGoHome(), Is.True);

            Assert.That(m_session.GoHomeCalls, Is.EqualTo(1), "the session already is Home");
            Assert.That(m_transition.Calls, Is.EqualTo(2));
        }

        [Test]
        public void DisposeCancelsTheRunningTransitionAndRefusesLaterExits()
        {
            Assert.That(m_exit.TryGoHome(), Is.True);

            m_exit.Dispose();

            Assert.That(m_transition.LastToken.IsCancellationRequested, Is.True);
            Assert.That(m_exit.TryGoHome(), Is.False);
            Assert.That(m_transition.Calls, Is.EqualTo(1));
        }

        [Test]
        public void ADisposedNavigatorNeverEntersHome()
        {
            m_exit.Dispose();

            Assert.That(m_exit.TryGoHome(), Is.False);
            Assert.That(m_session.GoHomeCalls, Is.Zero);
            Assert.That(m_transition.Calls, Is.Zero);
        }
    }
}
