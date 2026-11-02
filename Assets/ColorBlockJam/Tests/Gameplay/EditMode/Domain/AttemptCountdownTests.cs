using System;
using ColorBlockJam.Domain.Session;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The attempt countdown and its Time Freeze clock (B-8, B-9, AC-3, AC-4): the freeze holds the
    /// main time, only the leftover delta of the expiry tick reaches it, pause holds both clocks and
    /// the main time runs out once.
    /// </summary>
    public sealed class AttemptCountdownTests
    {
        private const float k_tolerance = 1e-4f;

        [Test]
        public void AFreezeHoldsTheMainTimeAndItsLeftoverDeltaReachesIt()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(3);

            Assert.That(countdown.TryFreeze(10f), Is.True);
            Assert.That(countdown.Tick(4f), Is.False);
            Assert.That(countdown.Remaining, Is.EqualTo(3f), "the main time is held");
            Assert.That(countdown.FreezeRemaining, Is.EqualTo(6f).Within(k_tolerance));
            Assert.That(DisplaySeconds.Calculate(countdown.FreezeRemaining), Is.EqualTo(6));
            Assert.That(DisplaySeconds.Calculate(countdown.Remaining), Is.EqualTo(3));

            Assert.That(countdown.Tick(6.25f), Is.False);
            Assert.That(countdown.Remaining, Is.EqualTo(2.75f).Within(k_tolerance), "no +10 is added");
            Assert.That(countdown.FreezeRemaining, Is.Zero);
            Assert.That(countdown.IsFrozen, Is.False);
        }

        [Test]
        public void AFreezeEndingInsideATickPassesOnlyTheRemainder()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(3);
            countdown.TryFreeze(10f);
            countdown.Tick(9.99f);
            Assert.That(countdown.FreezeRemaining, Is.EqualTo(0.01f).Within(k_tolerance));

            countdown.Tick(0.05f);

            Assert.That(countdown.Remaining, Is.EqualTo(2.96f).Within(k_tolerance));
            Assert.That(countdown.IsFrozen, Is.False);
        }

        [Test]
        public void FrameTicksShowTheFreezeCounterAtSixAfterFourSeconds()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(3);
            countdown.TryFreeze(10f);

            // Owner: this test. Stops: after 240 frames of 1/60 s.
            for (int frame = 0; frame < 240; frame++)
            {
                countdown.Tick(1f / 60f);
            }

            Assert.That(DisplaySeconds.Calculate(countdown.FreezeRemaining), Is.EqualTo(6), "float drift never shows 7");
            Assert.That(DisplaySeconds.Calculate(countdown.Remaining), Is.EqualTo(3));
        }

        [Test]
        public void PausedTicksChangeNeitherClock()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(5);
            countdown.TryFreeze(10f);
            countdown.SetPaused(true);

            Assert.That(countdown.Tick(3f), Is.False);
            Assert.That(countdown.Tick(3f), Is.False);
            Assert.That(countdown.Remaining, Is.EqualTo(5f));
            Assert.That(countdown.FreezeRemaining, Is.EqualTo(10f));

            countdown.SetPaused(false);
            countdown.Tick(1f);
            Assert.That(countdown.FreezeRemaining, Is.EqualTo(9f).Within(k_tolerance), "resume keeps the freeze's progress");
            Assert.That(countdown.Remaining, Is.EqualTo(5f));
        }

        [Test]
        public void ASecondFreezeIsRefusedWhileOneRuns()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            Assert.That(countdown.TryFreeze(10f), Is.False, "a countdown that has not started cannot freeze");
            countdown.Start(30);

            Assert.That(countdown.TryFreeze(10f), Is.True);
            countdown.Tick(2f);
            Assert.That(countdown.TryFreeze(10f), Is.False);
            Assert.That(countdown.FreezeRemaining, Is.EqualTo(8f).Within(k_tolerance), "the refusal changed nothing");

            countdown.Tick(8f);
            Assert.That(countdown.IsFrozen, Is.False);
            Assert.That(countdown.TryFreeze(10f), Is.True, "a freeze may start again after the last one ended");
            Assert.Throws<ArgumentOutOfRangeException>(() => countdown.TryFreeze(0f));
        }

        [Test]
        public void ExtraTimeAddsWholeSecondsToAStartedCountdown()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            Assert.Throws<InvalidOperationException>(() => countdown.AddSeconds(10));

            countdown.Start(60);
            countdown.AddSeconds(10);

            Assert.That(countdown.Remaining, Is.EqualTo(70f));
            Assert.Throws<ArgumentOutOfRangeException>(() => countdown.AddSeconds(-1));
        }

        [Test]
        public void TheMainCountdownRunsOutOnce()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(1);

            Assert.That(countdown.Tick(0.5f), Is.False);
            Assert.That(countdown.Tick(0.6f), Is.True);
            Assert.That(countdown.Remaining, Is.Zero);
            Assert.That(countdown.Tick(1f), Is.False, "expiry is signalled once");
            Assert.That(countdown.TryFreeze(10f), Is.False);
            Assert.Throws<InvalidOperationException>(() => countdown.AddSeconds(10));
        }

        [Test]
        public void StopEndsTheFreezeAndHoldsTheMainTime()
        {
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(10);
            countdown.TryFreeze(5f);
            countdown.Tick(1f);

            countdown.Stop();

            Assert.That(countdown.IsFrozen, Is.False, "teardown ends the freeze");
            Assert.That(countdown.Tick(3f), Is.False);
            Assert.That(countdown.Remaining, Is.EqualTo(10f), "the result keeps the main time");

            countdown.Start(20);
            countdown.Tick(1f);
            Assert.That(countdown.Remaining, Is.EqualTo(19f).Within(k_tolerance), "a stopped countdown runs again after Start");
        }

        [Test]
        public void StartReArmsACountdownThatRanOut()
        {
            // Restart after a timeout Fail: the scene's one countdown is started again without a Stop in between.
            AttemptCountdown countdown = new AttemptCountdown();
            countdown.Start(1);
            Assert.That(countdown.Tick(2f), Is.True);

            countdown.Start(5);

            Assert.That(countdown.Tick(1f), Is.False);
            Assert.That(countdown.Remaining, Is.EqualTo(4f).Within(k_tolerance), "the new attempt's clock runs");
            countdown.AddSeconds(2);
            Assert.That(countdown.Remaining, Is.EqualTo(6f).Within(k_tolerance), "Extra Time is accepted again");
            Assert.That(countdown.TryFreeze(3f), Is.True, "Time Freeze is accepted again");
            Assert.That(countdown.Tick(10f), Is.True, "the new attempt's expiry is signalled");
        }

        [Test]
        public void StartClearsARunningFreezeAndAPause()
        {
            AttemptCountdown frozen = new AttemptCountdown();
            frozen.Start(10);
            frozen.TryFreeze(5f);
            frozen.Tick(1f);

            frozen.Start(20);

            Assert.That(frozen.IsFrozen, Is.False, "the old attempt's freeze does not carry over");
            frozen.Tick(1f);
            Assert.That(frozen.Remaining, Is.EqualTo(19f).Within(k_tolerance));

            AttemptCountdown paused = new AttemptCountdown();
            paused.Start(10);
            paused.SetPaused(true);

            paused.Start(3);

            paused.Tick(1f);
            Assert.That(paused.Remaining, Is.EqualTo(2f).Within(k_tolerance), "the old attempt's pause does not carry over");
        }

        [TestCase(6f, 6)]
        [TestCase(5.2f, 6)]
        [TestCase(0.01f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        [TestCase(6.00001f, 6)]
        public void DisplaySecondsRoundUp(float remaining, int expected)
        {
            Assert.That(DisplaySeconds.Calculate(remaining), Is.EqualTo(expected));
        }
    }
}
