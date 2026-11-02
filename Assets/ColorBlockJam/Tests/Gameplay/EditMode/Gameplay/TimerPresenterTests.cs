using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The main timer and the separate Time Freeze counter (B-9, AC-3): "m:ss" of the rounded-up remaining time and the
    /// counter's whole seconds, both from prebuilt strings and written only when the shown second changes.
    /// </summary>
    public sealed class TimerPresenterTests
    {
        private static readonly string[] s_board =
        {
            "A..",
            "..."
        };

        private SessionFixture m_fixture;
        private BoosterFixture m_boosters;
        private TimerPresenter m_presenter;
        private FakeGameplayHudView m_view;

        [TearDown]
        public void TearDown()
        {
            m_presenter?.Dispose();
            m_boosters?.Dispose();
            m_fixture?.Dispose();
            m_presenter = null;
            m_boosters = null;
            m_fixture = null;
        }

        [Test]
        public void TheTimerShowsTheLevelTimeOnHomeAndChangesOnlyWithTheWholeSecond()
        {
            Create(play: false);
            Assert.That(m_view.Timer, Is.EqualTo("01:00"), "Home shows the authored time");
            Assert.That(m_fixture.Session.Play(), Is.True);
            m_fixture.TickIdle(5f);
            Assert.That(m_view.Timer, Is.EqualTo("01:00"), "the countdown waits for a block press (DEC-189)");
            m_fixture.StartTimer(0f, 0f);
            int writes = m_view.TimerWrites;

            m_fixture.TickIdle(0.4f);
            m_fixture.TickIdle(0.4f);
            Assert.That(m_view.TimerWrites, Is.EqualTo(writes), "0:59.2 still shows 01:00");

            m_fixture.TickIdle(0.3f);
            Assert.That(m_view.Timer, Is.EqualTo("00:59"));
            Assert.That(m_view.TimerWrites, Is.EqualTo(writes + 1));

            m_fixture.TickIdle(49f);
            Assert.That(m_view.Timer, Is.EqualTo("00:10"));
        }

        [Test]
        public void TimeFreezeHoldsThreeSecondsAndCountsItsOwnTenToZero()
        {
            Create(play: true);
            m_fixture.TickIdle(BoardFixture.k_timerSeconds - 3f);
            Assert.That(m_view.Timer, Is.EqualTo("00:03"));
            Assert.That(m_view.Frozen, Is.False);

            StartFreeze();
            m_fixture.TickIdle(0f);
            Assert.That(m_view.Frozen, Is.True);
            Assert.That(m_view.FreezeText, Is.EqualTo("10"));
            Assert.That(m_view.FreezeFill, Is.EqualTo(1f));

            m_fixture.TickIdle(4f);
            Assert.That(m_view.FreezeText, Is.EqualTo("6"), "after four Playing seconds it shows six");
            Assert.That(m_view.FreezeFill, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(m_view.Timer, Is.EqualTo("00:03"), "the main time is held");

            m_fixture.TickIdle(6.25f);
            Assert.That(m_view.Frozen, Is.False, "the freeze ended inside the tick");
            Assert.That(m_view.FreezeText, Is.EqualTo("0"));
            Assert.That(m_view.Timer, Is.EqualTo("00:03"), "only the leftover 0.25 s reached the main clock (2.75 shows 3)");
        }

        [Test]
        public void PauseHoldsBothDisplays()
        {
            Create(play: true);
            StartFreeze();
            m_fixture.TickIdle(1f);
            int timerWrites = m_view.TimerWrites;
            int freezeWrites = m_view.FreezeWrites;

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            m_fixture.TickIdle(5f);

            Assert.That(m_view.TimerWrites, Is.EqualTo(timerWrites));
            Assert.That(m_view.FreezeWrites, Is.EqualTo(freezeWrites));
            Assert.That(m_view.FreezeText, Is.EqualTo("9"));
        }

        [Test]
        public void EachShownFreezeSecondDrainsTheBarToTheNextSecondAndPauseHoldsIt()
        {
            Create(play: true);
            StartFreeze();
            m_fixture.TickIdle(0f);
            Assert.That(m_view.FreezeFill, Is.EqualTo(1f));
            Assert.That(m_view.FreezeDrainFill, Is.EqualTo(0.9f).Within(1e-5f), "ten shown seconds drain toward nine");
            Assert.That(m_view.FreezeDrainSeconds, Is.EqualTo(1f));
            Assert.That(m_view.FreezeBarPaused, Is.False);

            m_fixture.TickIdle(4f);
            Assert.That(m_view.FreezeFill, Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(m_view.FreezeDrainFill, Is.EqualTo(0.5f).Within(1e-5f));

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_view.FreezeBarPaused, Is.True, "a pause holds the drain with the freeze clock");

            m_fixture.TickIdle(6.25f);
            Assert.That(m_view.FreezeBarPaused, Is.True);
        }

        [Test]
        public void RestartClearsTheFreezeCounter()
        {
            Create(play: true);
            StartFreeze();
            m_fixture.TickIdle(1f);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);

            Assert.That(m_fixture.Session.Restart(), Is.True);

            Assert.That(m_view.Frozen, Is.False);
            Assert.That(m_view.FreezeText, Is.EqualTo("0"));
            Assert.That(m_view.Timer, Is.EqualTo("01:00"));
        }

        [Test]
        public void TheClockTableFormatsMinutesAndSecondsAndClampsOutsideIt()
        {
            HudText text = new HudText(99);
            text.ReserveClock(125);

            Assert.That(text.GetClock(0), Is.EqualTo("00:00"));
            Assert.That(text.GetClock(5), Is.EqualTo("00:05"));
            Assert.That(text.GetClock(65), Is.EqualTo("01:05"));
            Assert.That(text.GetClock(125), Is.EqualTo("02:05"));
            Assert.That(text.GetClock(500), Is.EqualTo("02:05"), "beyond the table shows its last entry");
            Assert.That(text.GetClock(-3), Is.EqualTo("00:00"));
            Assert.That(HudText.FormatClock(600), Is.EqualTo("10:00"));
            Assert.That(text.GetNumber(42), Is.EqualTo("42"));
            Assert.That(text.GetNumber(120), Is.EqualTo("99"));
            Assert.That(text.GetNumber(-1), Is.EqualTo("0"));
        }

        [Test]
        public void AShorterLevelReusesTheClockTable()
        {
            HudText text = new HudText(9);
            text.ReserveClock(70);
            string shown = text.GetClock(61);

            text.ReserveClock(30);

            Assert.That(text.MaxClockSeconds, Is.EqualTo(70));
            Assert.That(text.GetClock(61), Is.SameAs(shown), "no string was rebuilt");
        }

        private void Create(bool play)
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown(), withTargeting: true);
            m_boosters = new BoosterFixture();
            m_presenter = new TimerPresenter(m_fixture.Session, m_boosters.Catalog);
            m_view = new FakeGameplayHudView();
            HudText text = new HudText(99);
            text.ReserveClock(BoardFixture.k_timerSeconds + 10);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), Is.True, error);
            m_presenter.Bind(m_view, text);
            m_presenter.ShowSeconds(BoardFixture.k_timerSeconds);
            if (play)
            {
                Assert.That(m_fixture.Session.Play(), Is.True);
                m_fixture.StartTimer(0f, 0f);
            }
        }

        // The committed Time Freeze use of the fixture's freeze duration, applied by the next tick (B-13).
        private void StartFreeze()
        {
            Assert.That(m_presenter.FreezeSeconds, Is.EqualTo(10));
            BoosterIntent intent = BoosterIntent.ForTimeFreeze(m_fixture.Session.Attempt.Generation, 10f);
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), Is.True);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
        }
    }
}
