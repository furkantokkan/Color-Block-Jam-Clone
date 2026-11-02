using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Another system drives the level session with the same commands the pointer produces (DEC-122):
    /// one command per command phase, only while no drag is held, dropped with its attempt.
    /// </summary>
    public sealed class CommandSourceTests
    {
        private static readonly string[] s_exitMap =
        {
            "...",
            ".A.",
            "..."
        };

        private static readonly DoorSpec[] s_exitDoor = { BoardFixture.Door(Side.Top, 1, 1, 'A') };

        private static readonly string[] s_twoBlockMap =
        {
            "....",
            "A...",
            "...B"
        };

        private SessionFixture m_fixture;

        [TearDown]
        public void TearDown()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [Test]
        public void AReplayPlaysTheLevelToWon()
        {
            m_fixture = new SessionFixture(s_exitMap, s_exitDoor);
            m_fixture.PrepareAndPlay();
            ReplayCommandSource source = new ReplayCommandSource(new[]
            {
                GameCommand.MoveAndExit(0, new Cell(1, 1), new Cell(1, 2), Side.Top)
            });

            Assert.That(m_fixture.Session.TrySetCommandSource(source), Is.True);
            m_fixture.TickIdle();

            Assert.That(source.Position, Is.EqualTo(1));
            Assert.That(source.HasFailed, Is.False);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
            Assert.That(m_fixture.Presenter.LastExitCount, Is.EqualTo(1));
        }

        [Test]
        public void ARecordedAttemptReplaysAfterRestart()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            Drag(0f, 1f, 2f, 1f);
            Drag(3f, 0f, 3f, 2f);
            GameCommand[] recorded = m_fixture.Session.Attempt.Simulation.Commands.Log.ToArray();
            Cell first = m_fixture.GetOrigin(0);
            Cell second = m_fixture.GetOrigin(1);
            m_fixture.Session.Pause(PauseReason.User);
            Assert.That(m_fixture.Session.Restart(), Is.True);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "Restart rebuilt the authored board");

            ReplayCommandSource source = new ReplayCommandSource(recorded);
            m_fixture.Session.TrySetCommandSource(source);
            m_fixture.TickIdle();
            m_fixture.TickIdle();
            m_fixture.TickIdle();

            Assert.That(recorded.Length, Is.EqualTo(2));
            Assert.That(source.Position, Is.EqualTo(2));
            Assert.That(source.IsFinished, Is.True);
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(first));
            Assert.That(m_fixture.GetOrigin(1), Is.EqualTo(second));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.ToArray(), Is.EqualTo(recorded));
        }

        [Test]
        public void TheSourceWaitsForAHeldDragToEnd()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(1, new Cell(3, 0), new Cell(3, 2)) });
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Session.TrySetCommandSource(source);

            m_fixture.Tick(PointerPhase.Held, 1f, 1f);
            Assert.That(source.Position, Is.Zero, "a held drag keeps other drivers off the board");

            m_fixture.Tick(PointerPhase.Up, 1f, 1f);
            Assert.That(source.Position, Is.EqualTo(1), "the source runs after the release in the same phase");
            Assert.That(m_fixture.Presenter.LastEventCount, Is.EqualTo(2));
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
            Assert.That(m_fixture.GetOrigin(1), Is.EqualTo(new Cell(3, 2)));
        }

        [Test]
        public void AnInterruptedMoveRunsBeforeTheSource()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            m_fixture.Tick(PointerPhase.Down, 0f, 1f);
            m_fixture.Tick(PointerPhase.Held, 2f, 1f);
            m_fixture.Session.Pause(PauseReason.User);
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(1, new Cell(3, 0), new Cell(3, 2)) });
            m_fixture.Session.TrySetCommandSource(source);
            m_fixture.Session.Resume();

            m_fixture.TickIdle();

            GameCommand[] log = m_fixture.Session.Attempt.Simulation.Commands.Log.ToArray();
            Assert.That(log, Is.EqualTo(new[]
            {
                GameCommand.Move(0, new Cell(0, 1), new Cell(2, 1)),
                GameCommand.Move(1, new Cell(3, 0), new Cell(3, 2))
            }));
        }

        [Test]
        public void ARefusedSourceCommandStopsTheReplayAndChangesNothing()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(0, new Cell(1, 1), new Cell(2, 1)) });
            m_fixture.Session.TrySetCommandSource(source);

            m_fixture.TickIdle();
            m_fixture.TickIdle();

            Assert.That(source.HasFailed, Is.True);
            Assert.That(source.FailedIndex, Is.Zero);
            Assert.That(source.FailedOutcome, Is.EqualTo(CommandOutcome.StaleOrigin));
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(m_fixture.Presenter.LastEventCount, Is.Zero);
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.Zero);
        }

        [Test]
        public void RestartDropsTheSource()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(1, new Cell(3, 0), new Cell(3, 2)) });
            m_fixture.Session.TrySetCommandSource(source);
            m_fixture.Session.Pause(PauseReason.User);

            m_fixture.Session.Restart();
            m_fixture.TickIdle();

            Assert.That(source.Position, Is.Zero, "a source never drives a later attempt");
            Assert.That(m_fixture.GetOrigin(1), Is.EqualTo(new Cell(3, 0)));
        }

        [Test]
        public void ThePresenterReadsTheBoardAsThePlayerSeesIt()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();

            Assert.That(m_fixture.Presenter.PlacedBoard, Is.SameAs(m_fixture.Session.Attempt.Simulation.Preview),
                "held and interrupted moves show on the preview board before their command runs");
        }

        [Test]
        public void NoSourceCanBeAttachedWithoutAnAttempt()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);

            Assert.That(m_fixture.Session.TrySetCommandSource(new ReplayCommandSource(new GameCommand[0])), Is.False);
        }

        [Test]
        public void TheSourceIsNotAskedWhilePaused()
        {
            m_fixture = new SessionFixture(s_twoBlockMap, new DoorSpec[0]);
            m_fixture.PrepareAndPlay();
            ReplayCommandSource source = new ReplayCommandSource(new[] { GameCommand.Move(1, new Cell(3, 0), new Cell(3, 2)) });
            m_fixture.Session.TrySetCommandSource(source);
            m_fixture.Session.Pause(PauseReason.User);

            m_fixture.TickIdle();

            Assert.That(source.Position, Is.Zero);
        }

        private void Drag(float fromX, float fromY, float toX, float toY)
        {
            m_fixture.Tick(PointerPhase.Down, fromX, fromY);
            m_fixture.Tick(PointerPhase.Held, toX, toY);
            m_fixture.Tick(PointerPhase.Up, toX, toY);
        }
    }
}
