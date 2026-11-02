using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// The targeting branch of DragInputRouter (B-12): while a mode is set no press drags, a release hands only a
    /// directly hit block cell to the sink, and an empty cell, a wall, the border and any off-board press cancel for
    /// free. UI presses stay inert, a rejected target keeps the mode and asks the presenter for the blocked-drag
    /// feedback, and Back, Suspend and Teardown end
    /// the mode (AC-2, AC-10).
    /// </summary>
    public sealed class BoosterTargetingTests
    {
        // A at (0, 1), B at (3, 0) on a 4 x 3 board.
        private static readonly string[] s_twoBlocks =
        {
            "....",
            "A...",
            "...B"
        };

        private SessionFixture m_fixture;
        private RecordingTargetSink m_sink;

        [SetUp]
        public void SetUp()
        {
            m_fixture = new SessionFixture(s_twoBlocks, new DoorSpec[0], withTargeting: true);
            m_sink = new RecordingTargetSink();
            m_fixture.Targeting.Attach(m_sink);
            m_fixture.PrepareAndPlay();
        }

        [TearDown]
        public void TearDown()
        {
            m_fixture.Dispose();
            m_fixture = null;
        }

        [Test]
        public void WhileTargetingAPressOnABlockStartsNoDrag()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);

            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(m_sink.Count, Is.Zero, "the press resolves on release");
        }

        [Test]
        public void AReleaseHandsTheDirectlyHitCellOfThePressToTheSink()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);

            m_fixture.Tick(PointerPhase.Down, 0.3f, 1.2f);
            m_fixture.Tick(PointerPhase.Up, 2f, 2f);

            Assert.That(m_sink.Count, Is.EqualTo(1));
            Assert.That(m_sink.LastId, Is.EqualTo(BoosterId.Rocket));
            Assert.That(m_sink.LastCell, Is.EqualTo(new Cell(0, 1)));
            Assert.That(m_sink.LastAttempt, Is.SameAs(m_fixture.Session.Attempt));
        }

        [Test]
        public void APressOnAWallNextToABlockCancelsInsteadOfPickingTheNearestBlock()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);

            m_fixture.Tap(-1f, 1f);

            Assert.That(m_sink.Count, Is.Zero);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_fixture.Presenter.SetHeldCount, Is.Zero);
        }

        [Test]
        public void APressOnAnEmptyCellCancels()
        {
            m_fixture.Targeting.Select(BoosterId.Vacuum);

            m_fixture.Tap(1f, 1f);

            Assert.That(m_sink.Count, Is.Zero);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
        }

        [Test]
        public void APressOutsideTheBoardCancels()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Tap(-2f, 1f);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "outside the interaction area");

            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Projector.MissesPlane = true;
            m_fixture.Tap(0f, 1f);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "off the board plane");
            Assert.That(m_sink.Count, Is.Zero);
        }

        [Test]
        public void APressOverUiStaysInert()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Ui.OverUi = true;

            m_fixture.Tap(0f, 1f);

            Assert.That(m_sink.Count, Is.Zero);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.Hammer));
        }

        [Test]
        public void ARejectedTargetKeepsTheModeAndRequestsTheBlockedFeedback()
        {
            m_sink.Result = BoosterTargetResult.Rejected;
            m_fixture.Targeting.Select(BoosterId.Rocket);

            m_fixture.Tap(3f, 0f);

            Assert.That(m_sink.Count, Is.EqualTo(1));
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.Rocket));
            Assert.That(m_fixture.Presenter.BlockedDragCount, Is.EqualTo(1));
            Assert.That(m_fixture.Presenter.LastBlockedBlock, Is.EqualTo(1));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        [Test]
        public void AnEmptyResultCancels()
        {
            m_sink.Result = BoosterTargetResult.Empty;
            m_fixture.Targeting.Select(BoosterId.Hammer);

            m_fixture.Tap(0f, 1f);

            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
        }

        [Test]
        public void ACancelledTouchResolvesNothing()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Cancel, 0f, 1f);

            Assert.That(m_sink.Count, Is.Zero);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.Hammer));
        }

        [Test]
        public void BackCancelsTargetingAndOtherwiseDoesNothing()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            m_fixture.Pointer.PressBack();
            m_fixture.TickIdle();
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));

            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Pointer.PressBack();
            m_fixture.Tick(PointerPhase.Held, 1f, 1f);

            Assert.That(m_fixture.Presenter.HeldBlock, Is.EqualTo(0), "Back does not end a drag");
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void SwitchingTheModeIsFreeAndTheLastSelectionWins()
        {
            m_fixture.Targeting.Select(BoosterId.Rocket);
            m_fixture.Targeting.Select(BoosterId.Vacuum);

            m_fixture.Tap(0f, 1f);

            Assert.That(m_sink.LastId, Is.EqualTo(BoosterId.Vacuum));
        }

        [Test]
        public void ToggleEndsTheSameModeAndSwitchesToAnother()
        {
            Assert.That(m_fixture.Targeting.Toggle(BoosterId.Hammer), Is.True);
            Assert.That(m_fixture.Targeting.Toggle(BoosterId.Rocket), Is.True);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.Rocket));
            Assert.That(m_fixture.Targeting.Toggle(BoosterId.Rocket), Is.False);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => m_fixture.Targeting.Select(BoosterId.TimeFreeze));
        }

        [Test]
        public void SuspendAndTeardownEndTheMode()
        {
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Session.Pause(PauseReason.User);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "Suspend");

            m_fixture.Session.Resume();
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Session.NotifyBackground(true);
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "background Suspend");

            m_fixture.Session.NotifyBackground(false);
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Session.Pause(PauseReason.User);
            m_fixture.Targeting.Select(BoosterId.Hammer);
            m_fixture.Session.GoHome();
            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None), "Teardown");
        }

        [Test]
        public void WithoutASinkATargetCancels()
        {
            m_fixture.Targeting.Detach(m_sink);
            m_fixture.Targeting.Select(BoosterId.Hammer);

            m_fixture.Tap(0f, 1f);

            Assert.That(m_fixture.Targeting.Mode.Value, Is.EqualTo(BoosterId.None));
            Assert.That(m_sink.Count, Is.Zero);
        }

        [Test]
        public void WithoutAModeAPressDragsAsBefore()
        {
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 1f, 1f);
            m_fixture.Tick(PointerPhase.Up, 1f, 1f);

            Assert.That(m_sink.Count, Is.Zero);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }
    }
}
