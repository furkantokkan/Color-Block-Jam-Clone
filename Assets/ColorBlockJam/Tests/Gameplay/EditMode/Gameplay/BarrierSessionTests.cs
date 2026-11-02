using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Plays Barrier levels (catalog OBJ-05) through the level session with fakes and the pointer, contract
    /// TASK-20260925-103527585-3d1b0a34 acceptance 1, 2, 5 and the tick half of 6: a closed barrier stops a drag until
    /// a door exit opens it, a barrier closing under a block keeps it and lets only it leave, Restart builds a fresh
    /// rule at startsOpen, and a Playing tick that drags across a barrier allocates nothing. The hook source registers
    /// the rule exactly as BarrierEffectModule.AddHooks does; the module and its view are covered in PlayMode, where the
    /// EnvironmentView assembly is referenced.
    /// </summary>
    public sealed class BarrierSessionTests
    {
        // A (0) at the bottom-left, B (1) at the top-left under a B-coloured Top door.
        private static readonly string[] s_lane = { "B....", ".....", "A...." };
        private static readonly DoorSpec[] s_topDoor = { BoardFixture.Door(Side.Top, 0, 1, 'B') };
        private static readonly Cell[] s_column = { new Cell(2, 0), new Cell(2, 1), new Cell(2, 2) };

        // A (0) stands on the open one-cell barrier at (2, 0); C (2) waits on the right.
        private static readonly string[] s_standing = { "B....", ".....", "..A.C" };

        private SessionFixture m_fixture;
        private BarrierHookSource m_hooks;

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [TearDown]
        public void TearDown()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [Test]
        public void AClosedBarrierStopsTheDragUntilADoorExitOpensIt()
        {
            Create(s_lane, s_topDoor, false, s_column);
            m_fixture.PrepareAndPlay();

            Assert.That(DragAndRelease(0, 4f, 0f), Is.EqualTo(new Cell(1, 0)), "A stops before the closed barrier.");
            ExitThroughTheTop(new Cell(0, 2));
            BoardSimulation simulation = m_fixture.Session.Attempt.Simulation;
            Assert.That(simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.Exited));
            Assert.That(m_hooks.Rule.IsOpen(0), Is.True, "B's exit flipped the barrier open.");
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));

            Assert.That(DragAndRelease(0, 3f, 0f), Is.EqualTo(new Cell(4, 0)), "A passes the open barrier.");
        }

        [Test]
        public void ABarrierClosingUnderABlockKeepsItAndOnlyThatBlockMayLeave()
        {
            Create(s_standing, s_topDoor, true, new Cell(2, 0));
            m_fixture.PrepareAndPlay();

            ExitThroughTheTop(new Cell(0, 2));
            Assert.That(m_hooks.Rule.IsOpen(0), Is.False, "B's exit closed the barrier under A.");
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(2, 0)), "A was not moved.");

            Assert.That(DragAndRelease(0, 0f, 1f), Is.EqualTo(new Cell(2, 1)), "A may leave the closed cell.");
            Assert.That(DragAndRelease(2, -4f, 0f), Is.EqualTo(new Cell(3, 0)), "C may not enter it.");
            Assert.That(DragAndRelease(0, 0f, -1f), Is.EqualTo(new Cell(2, 1)), "Nor may A return while it is closed.");
        }

        [Test]
        public void RestartBuildsAFreshRuleAtItsStartState()
        {
            Create(s_lane, s_topDoor, false, s_column);
            m_fixture.PrepareAndPlay();
            ExitThroughTheTop(new Cell(0, 2));
            BarrierRule played = m_hooks.Rule;
            Assert.That(played.IsOpen(0), Is.True);

            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_fixture.Session.Restart(), Is.True);

            Assert.That(m_hooks.CreateCount, Is.EqualTo(2), "Restart asked the hook source for a new attempt.");
            Assert.That(m_hooks.Rule, Is.Not.SameAs(played));
            Assert.That(m_hooks.Rule.IsOpen(0), Is.False, "The barrier is closed again, as authored.");
            Assert.That(m_hooks.Rule.GetVersion(0), Is.Zero);
            Assert.That(DragAndRelease(0, 4f, 0f), Is.EqualTo(new Cell(1, 0)), "The rebuilt board is blocked again.");
        }

        [Test]
        public void APlayingTickThatDragsAcrossABarrierAllocatesNothing()
        {
            Create(s_lane, s_topDoor, true, s_column);
            m_fixture.PrepareAndPlay();
            DragThereAndBack();
            m_fixture.Journal.Clear();

            Assert.That(() => DragThereAndBack(), Is.Not.AllocatingGCMemory());
            Assert.That(m_fixture.GetOrigin(0), Is.EqualTo(new Cell(0, 0)));
            Assert.That(m_fixture.Session.Attempt.Simulation.Commands.Log.Count, Is.EqualTo(4));
        }

        private void Create(string[] rows, DoorSpec[] doors, bool startsOpen, params Cell[] cells)
        {
            BarrierEffectLevel level = new BarrierEffectLevel(BoardFixture.Create(rows, doors).BlockCount, rows[0].Length,
                rows.Length, new[] { "barrier" }, new[] { cells }, new[] { startsOpen });
            m_hooks = new BarrierHookSource(level);
            m_fixture = new SessionFixture(rows, doors, hookSource: m_hooks);
        }

        // Presses the block where it stands, drags the pointer by the offset in four ticks and releases.
        private Cell DragAndRelease(int block, float dx, float dy)
        {
            Cell origin = m_fixture.Session.Attempt.Simulation.Board.GetOrigin(block);
            m_fixture.Tick(PointerPhase.Down, origin.X, origin.Y);
            for (int step = 1; step <= 4; step++)
            {
                float t = step / 4f;
                m_fixture.Tick(PointerPhase.Held, origin.X + dx * t, origin.Y + dy * t);
            }

            m_fixture.Tick(PointerPhase.Up, origin.X + dx, origin.Y + dy);
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
            return m_fixture.Session.Attempt.Simulation.Board.GetOrigin(block);
        }

        // A drag of 0.4 cell into the Top door exits a flush block on contact (DEC-164).
        private void ExitThroughTheTop(Cell origin)
        {
            m_fixture.Tick(PointerPhase.Down, origin.X, origin.Y);
            m_fixture.Tick(PointerPhase.Held, origin.X, origin.Y + 0.4f);
            m_fixture.Tick(PointerPhase.Up, origin.X, origin.Y + 0.4f);
        }

        // A drags across the open barrier column to (3, 0) and back: two releases, two commands each way.
        private void DragThereAndBack()
        {
            DragOnce(0f, 3f);
            DragOnce(3f, 0f);
        }

        private void DragOnce(float fromX, float toX)
        {
            m_fixture.Tick(PointerPhase.Down, fromX, 0f);
            m_fixture.Tick(PointerPhase.Held, (fromX + toX) * 0.5f, 0f);
            m_fixture.Tick(PointerPhase.Held, toX, 0f);
            m_fixture.Tick(PointerPhase.Up, toX, 0f);
        }

        /// <summary>
        /// A fresh BarrierRule per attempt, registered as cell gate and clear listener like BarrierEffectModule.AddHooks,
        /// and the latest rule for the assertions.
        /// </summary>
        private sealed class BarrierHookSource : IAttemptHookSource
        {
            private readonly BarrierEffectLevel m_level;

            public BarrierHookSource(BarrierEffectLevel level)
            {
                m_level = level;
            }

            public BarrierRule Rule { get; private set; }

            public int CreateCount { get; private set; }

            public AttemptHooks Create(AttemptLevel level)
            {
                CreateCount++;
                Rule = m_level.CreateRule();
                return new AttemptHooksBuilder().Add((ICellGate)Rule).Add((IBoardEventListener)Rule).Build();
            }
        }
    }
}
