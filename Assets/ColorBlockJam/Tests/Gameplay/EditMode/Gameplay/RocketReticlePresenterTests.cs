using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The Rocket crosshairs (B-24, AC-2, AC-7): while Rocket targeting runs, one reticle on every cell Rocket accepts on
    /// the committed board, none on a block's final cell, none outside Rocket mode; placed through the gameplay projector
    /// at 0.6 of the cell's projected width, again on a board change and on a layer geometry change, without allocating.
    /// </summary>
    public sealed class RocketReticlePresenterTests
    {
        // A two-cell block A (cells (0, 2) and (1, 2)) and two one-cell blocks B at (0, 0) and C at (2, 0).
        private static readonly string[] s_board =
        {
            "AA.",
            "...",
            "B.C"
        };

        private SessionFixture m_fixture;
        private RocketReticlePresenter m_presenter;
        private FakeReticleView m_view;

        [SetUp]
        public void SetUp()
        {
            m_fixture = new SessionFixture(s_board, new DoorSpec[0], clock: new AttemptCountdown(), withTargeting: true);
            m_presenter = new RocketReticlePresenter(m_fixture.Session, m_fixture.Targeting, m_fixture.Projector);
            m_view = new FakeReticleView();
            m_presenter.Bind(m_view);
            m_fixture.PrepareAndPlay();
            m_presenter.Prepare(m_fixture.Level.Model.TotalCellCount);
        }

        [TearDown]
        public void TearDown()
        {
            m_presenter.Dispose();
            m_fixture.Dispose();
        }

        [Test]
        public void HomePreparationSizesThePoolForEveryCell()
        {
            Assert.That(m_view.Capacity, Is.EqualTo(4));
            Assert.That(m_view.Shown, Is.Zero);
        }

        [Test]
        public void NoReticleShowsOutsideRocketTargeting()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);
            Assert.That(m_view.Shown, Is.Zero);

            m_fixture.Targeting.Select(BoosterId.Vacuum);
            Assert.That(m_view.Shown, Is.Zero, "Vacuum shows no crosshair");
        }

        [Test]
        public void RocketTargetingMarksEveryCellRocketAcceptsAndNoFinalCell()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);

            Assert.That(m_view.Shown, Is.EqualTo(2), "only the two-cell block's cells; B and C keep their final cell");
            Assert.That(Centers(), Is.EquivalentTo(new[] { "0,200", "100,200" }));
            Assert.That(m_view.GetSize(0), Is.EqualTo(RocketReticlePresenter.k_cellFraction * FakeBoardProjector.k_pixelsPerCell)
                .Within(1e-3f));
        }

        [Test]
        public void LeavingRocketTargetingHidesThem()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);

            m_fixture.Targeting.Select(BoosterId.Hammer);
            Assert.That(m_view.Shown, Is.Zero, "switching tools hides them");

            m_fixture.Targeting.Select(BoosterId.Rocket);
            m_fixture.Targeting.Cancel();
            Assert.That(m_view.Shown, Is.Zero);
        }

        [Test]
        public void SuspendEndsTargetingAndHidesThem()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);

            Assert.That(m_view.Shown, Is.Zero);
        }

        [Test]
        public void ABoardChangeDuringTargetingMovesTheReticles()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(0, new Cell(0, 2), new Cell(0, 1)) });
            Assert.That(m_fixture.Session.TrySetCommandSource(source), Is.True);

            m_fixture.TickIdle();

            Assert.That(m_fixture.Session.BoardRevision.Value, Is.EqualTo(1));
            Assert.That(Centers(), Is.EquivalentTo(new[] { "0,100", "100,100" }));
        }

        [Test]
        public void ABlockLeftWithOneCellLosesItsReticles()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2)) });
            Assert.That(m_fixture.Session.TrySetCommandSource(source), Is.True);

            m_fixture.TickIdle();

            Assert.That(m_view.Shown, Is.Zero, "A's remaining cell is its final cell");
        }

        [Test]
        public void AGeometryChangePlacesThemAgain()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            int calls = m_view.ShowCalls;

            m_view.Relayout();

            Assert.That(m_view.ShowCalls, Is.EqualTo(calls + 2));

            m_fixture.Targeting.Cancel();
            m_view.Relayout();
            Assert.That(m_view.ShowCalls, Is.EqualTo(calls + 2), "outside Rocket mode a relayout places nothing");
        }

        [Test]
        public void PlacingTheReticlesAllocatesNothing()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            m_presenter.Refresh();

            Assert.That(() => m_presenter.Refresh(), Is.Not.AllocatingGCMemory());
            Assert.That(m_view.Shown, NUnit.Framework.Is.EqualTo(2));
        }

        [Test]
        public void UnbindHidesTheReticlesAndStopsFollowing()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);

            m_presenter.Unbind();
            Assert.That(m_view.Shown, Is.Zero);

            m_fixture.Targeting.Cancel();
            m_fixture.Targeting.Select(BoosterId.Rocket);
            Assert.That(m_view.Shown, Is.Zero);
        }

        private string[] Centers()
        {
            List<string> centers = new List<string>();
            for (int index = 0; index < m_view.Shown; index++)
            {
                ScreenPoint center = m_view.GetCenter(index);
                centers.Add(center.X + "," + center.Y);
            }

            return centers.ToArray();
        }
    }
}
