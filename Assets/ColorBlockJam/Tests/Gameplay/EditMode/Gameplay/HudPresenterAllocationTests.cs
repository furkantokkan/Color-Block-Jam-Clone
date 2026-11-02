using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// A Playing tick with the whole HUD bound allocates nothing (B-9, Performance Rule 5): the timer and freeze texts
    /// come from prebuilt tables, observers are cached, and the reticles re-place from a Home-sized buffer. Each tick
    /// runs once before the measurement, so JIT and first-use work are excluded; Editor Mono only.
    /// </summary>
    public sealed class HudPresenterAllocationTests
    {
        // A two-cell block A and a one-cell block B with room to move.
        private static readonly string[] s_board =
        {
            "AA..",
            "....",
            "...B"
        };

        private SessionFixture m_fixture;
        private BoosterFixture m_boosters;
        private BoosterBarPresenter m_bar;
        private TimerPresenter m_timer;
        private OpeningPresenter m_opening;
        private RocketReticlePresenter m_reticles;
        private FakeGameplayHudView m_view;

        [SetUp]
        public void SetUp()
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown(), withTargeting: true);
            m_boosters = new BoosterFixture();
            BoosterUseCoordinator coordinator = m_boosters.CreateCoordinator(m_fixture);
            m_view = new FakeGameplayHudView();
            HudText text = new HudText(99);
            text.ReserveClock(BoardFixture.k_timerSeconds + 10);
            m_bar = new BoosterBarPresenter(m_fixture.Session, m_fixture.Targeting, coordinator, m_boosters.Catalog,
                m_boosters.Store, m_boosters.Store);
            m_timer = new TimerPresenter(m_fixture.Session, m_boosters.Catalog);
            m_opening = new OpeningPresenter(m_fixture.Session, m_boosters.Catalog);
            m_reticles = new RocketReticlePresenter(m_fixture.Session, m_fixture.Targeting, m_fixture.Projector);
            Assert.That(m_fixture.Session.TryPrepare(m_fixture.Level, out string error), NUnit.Framework.Is.True, error);
            m_bar.Bind(m_view, text);
            m_timer.Bind(m_view, text);
            m_opening.Bind(m_view);
            m_reticles.Bind(new FakeReticleView());
            m_reticles.Prepare(m_fixture.Level.Model.TotalCellCount);
            m_timer.ShowSeconds(BoardFixture.k_timerSeconds);
            Assert.That(m_fixture.Session.Play(), NUnit.Framework.Is.True);
            m_fixture.StartTimer(3f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            m_bar.Dispose();
            m_timer.Dispose();
            m_opening.Dispose();
            m_reticles.Dispose();
            m_boosters.Dispose();
            m_fixture.Dispose();
        }

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            // The first measurement in a domain also measures the recorder's own setup; this runs before every test.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TicksThatChangeTheShownSecondAllocateNothing()
        {
            m_fixture.TickIdle(0.6f);
            int writes = m_view.TimerWrites;

            // Two ticks cross two whole seconds, so the timer text changes inside the measurement.
            Assert.That(() =>
            {
                m_fixture.TickIdle(1f);
                m_fixture.TickIdle(1f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(m_view.TimerWrites, NUnit.Framework.Is.EqualTo(writes + 2));
        }

        [Test]
        public void TicksWhileTimeFreezeRunsAllocateNothing()
        {
            BoosterIntent intent = BoosterIntent.ForTimeFreeze(m_fixture.Session.Attempt.Generation, 10f);
            Assert.That(m_fixture.Session.TrySubmitBooster(intent), NUnit.Framework.Is.True);
            m_fixture.Session.CompleteBoosterCommit(intent.Generation, true);
            m_fixture.TickIdle(0.4f);
            int writes = m_view.FreezeWrites;

            Assert.That(() =>
            {
                m_fixture.TickIdle(0.4f);
                m_fixture.TickIdle(0.4f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(m_view.FreezeWrites, NUnit.Framework.Is.EqualTo(writes + 1));
            Assert.That(m_view.FreezeText, NUnit.Framework.Is.EqualTo("9"));
        }

        [Test]
        public void ABoardChangeDuringRocketTargetingReplacesTheReticlesWithoutAllocating()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            ReplayCommandSource source = new ReplayCommandSource(new[]
            {
                GameCommand.Move(0, new Cell(0, 2), new Cell(1, 2)),
                GameCommand.Move(0, new Cell(1, 2), new Cell(0, 2)),
                GameCommand.Move(0, new Cell(0, 2), new Cell(1, 2))
            });
            Assert.That(m_fixture.Session.TrySetCommandSource(source), NUnit.Framework.Is.True);
            m_fixture.TickIdle();

            Assert.That(() =>
            {
                m_fixture.TickIdle();
                m_fixture.TickIdle();
            }, Is.Not.AllocatingGCMemory());
            Assert.That(m_fixture.Session.BoardRevision.Value, NUnit.Framework.Is.EqualTo(3));
            Assert.That(m_reticles.ShownCount, NUnit.Framework.Is.EqualTo(2));
        }
    }
}
