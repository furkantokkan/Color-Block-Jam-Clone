using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Gameplay
{
    /// <summary>
    /// Plays Arrow blocks (catalog BLK-01, AC-BLK-01.1, AC-BLK-01.2, AC-BLK-01.6) through the level session with the
    /// Game scene's EffectAttemptHookSource over an Arrow module list and the pointer: a diagonal drag moves a 1x1, I3
    /// or L3 Arrow block only along its axis, both ways; it exits through a matching door on an axis side and never
    /// through one on a perpendicular side; the axis survives Restart; and a block without Arrow keeps both axes.
    /// </summary>
    public sealed class ArrowSessionTests
    {
        private static readonly DoorSpec[] s_noDoors = new DoorSpec[0];

        private static readonly string[] s_openSingle =
        {
            ".......", ".......", ".......", "...A...", ".......", ".......", "......."
        };

        private static readonly string[] s_openLine =
        {
            ".......", ".......", ".......", "..AAA..", ".......", ".......", "......."
        };

        private static readonly string[] s_openEll =
        {
            ".......", ".......", "..A....", "..AA...", ".......", ".......", "......."
        };

        // The block in the top-left corner, flush against a matching Top door and a matching Left door.
        private static readonly string[] s_cornerSingle = { "A...", "....", "....", "...." };
        private static readonly string[] s_cornerLine = { "AAA.", "....", "....", "...." };
        private static readonly string[] s_cornerEll = { "A...", "AA..", "....", "...." };

        // Block 0 (A) is a Horizontal Arrow block; block 1 (B) has no effect and a Top door of its colour.
        private static readonly string[] s_mixed =
        {
            ".....",
            ".....",
            "..B..",
            ".....",
            "A...."
        };

        private SessionFixture m_fixture;

        [TearDown]
        public void TearDown()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [TestCase("single", LevelEffectAxis.Horizontal)]
        [TestCase("single", LevelEffectAxis.Vertical)]
        [TestCase("line", LevelEffectAxis.Horizontal)]
        [TestCase("line", LevelEffectAxis.Vertical)]
        [TestCase("ell", LevelEffectAxis.Horizontal)]
        [TestCase("ell", LevelEffectAxis.Vertical)]
        public void ADiagonalDragMovesTheArrowBlockOnlyAlongItsAxisBothWays(string shape, LevelEffectAxis axis)
        {
            m_fixture = Create(GetOpenRows(shape), s_noDoors, axis);
            m_fixture.PrepareAndPlay();
            Cell start = GetCommittedOrigin(0);

            Cell forward = DragAndRelease(0, 3f, 3f);
            AssertOnAxis(start, forward, axis);
            Assert.That(forward, Is.Not.EqualTo(start), "It travels forward along its axis.");

            Cell back = DragAndRelease(0, -6f, -6f);
            AssertOnAxis(start, back, axis);
            Assert.That(axis == LevelEffectAxis.Horizontal ? back.X < start.X : back.Y < start.Y, Is.True,
                "It travels back past its start.");

            CommandLog log = m_fixture.Session.Attempt.Simulation.Commands.Log;
            Assert.That(log.Count, Is.EqualTo(2), "Each release ran one on-axis MoveBlock command.");
        }

        [TestCase("single", LevelEffectAxis.Horizontal)]
        [TestCase("single", LevelEffectAxis.Vertical)]
        [TestCase("line", LevelEffectAxis.Horizontal)]
        [TestCase("line", LevelEffectAxis.Vertical)]
        [TestCase("ell", LevelEffectAxis.Horizontal)]
        [TestCase("ell", LevelEffectAxis.Vertical)]
        public void TheArrowBlockExitsThroughAnAxisDoorAndNeverThroughAPerpendicularOne(string shape, LevelEffectAxis axis)
        {
            m_fixture = Create(GetCornerRows(shape), GetCornerDoors(shape), axis);
            m_fixture.PrepareAndPlay();
            BoardSimulation simulation = m_fixture.Session.Attempt.Simulation;
            Cell origin = simulation.Board.GetOrigin(0);
            Side along = axis == LevelEffectAxis.Horizontal ? Side.Left : Side.Top;
            Side across = axis == LevelEffectAxis.Horizontal ? Side.Top : Side.Left;
            Cell acrossStep = SideMath.GetOutward(across);

            Assert.That(ExitRules.Evaluate(simulation.Board, simulation.Hooks, 0, origin, across, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Vetoed), "The matching perpendicular door is vetoed for every caller.");
            Assert.That(simulation.Commands.Execute(GameCommand.MoveAndExit(0, origin, origin, across)),
                Is.EqualTo(CommandOutcome.AxisRefused), "A perpendicular exit command is refused too.");
            Assert.That(simulation.Commands.Log.Count, Is.Zero);

            // It touches both matching doors. Held and pulled toward the perpendicular one, contact exits it
            // through the axis door only (DEC-164), and the last block's exit wins.
            m_fixture.Tick(PointerPhase.Down, origin.X, origin.Y);
            m_fixture.Tick(PointerPhase.Held, origin.X + acrossStep.X * 0.9f, origin.Y + acrossStep.Y * 0.9f);
            m_fixture.Tick(PointerPhase.Up, origin.X + acrossStep.X * 0.9f, origin.Y + acrossStep.Y * 0.9f);

            Assert.That(simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(simulation.Commands.Log.GetAt(0).TryGetExit(out Side side) && side == along, Is.True);
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Won));
        }

        [Test]
        public void RestartKeepsTheAuthoredAxis()
        {
            m_fixture = Create(s_openSingle, s_noDoors, LevelEffectAxis.Vertical);
            m_fixture.PrepareAndPlay();
            DragAndRelease(0, 2f, 2f);
            Assert.That(m_fixture.Session.Pause(PauseReason.User), Is.True);
            Assert.That(m_fixture.Session.Restart(), Is.True);

            Cell start = GetCommittedOrigin(0);
            Assert.That(start, Is.EqualTo(new Cell(3, 3)), "Restart rebuilt the authored board.");
            Cell reached = DragAndRelease(0, -2f, 2f);

            Assert.That(reached, Is.EqualTo(new Cell(3, 5)), "The new attempt reads the same Vertical axis.");
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.Axes, Is.EqualTo(AxisMask.Vertical));
        }

        [Test]
        public void ABlockWithoutArrowKeepsBothAxesAndExitsAsBefore()
        {
            m_fixture = Create(s_mixed, new[] { BoardFixture.Door(Side.Top, 3, 1, 'B') }, LevelEffectAxis.Horizontal, null);
            m_fixture.PrepareAndPlay();
            BoardSimulation simulation = m_fixture.Session.Attempt.Simulation;
            Assert.That(simulation.Hooks.GetAxes(simulation.Board, 0), Is.EqualTo(AxisMask.Horizontal));
            Assert.That(simulation.Hooks.GetAxes(simulation.Board, 1), Is.EqualTo(AxisMask.Both));

            Cell reached = DragAndRelease(1, 1f, 2f);
            Assert.That(reached, Is.EqualTo(new Cell(3, 4)), "The plain block moved on both axes.");

            m_fixture.Tick(PointerPhase.Down, 3f, 4f);
            m_fixture.Tick(PointerPhase.Held, 3f, 4.4f);
            m_fixture.Tick(PointerPhase.Up, 3f, 4.4f);

            Assert.That(simulation.Board.GetStatus(1), Is.EqualTo(BlockStatus.Exited), "It left through its Top door.");
            Assert.That(simulation.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(m_fixture.Session.State.Value, Is.EqualTo(SessionState.Playing));
        }

        /// <summary>
        /// A session whose hook source is the Game scene's EffectAttemptHookSource over the Arrow module, prepared on Home
        /// from an authored level matching the fixture's converted level (ID "fixture", blocks "b0", "b1", ...).
        /// </summary>
        internal static SessionFixture Create(string[] rows, DoorSpec[] doors, params LevelEffectAxis?[] axes)
        {
            BlockEffectContextSource source = new BlockEffectContextSource(new IBlockEffectModule[] { new ArrowEffectModule() });
            SessionFixture fixture = new SessionFixture(rows, doors, hookSource: new EffectAttemptHookSource(source));
            source.Prepare(fixture.Level, CreateAuthored(fixture.Level.BlockCount, axes));
            return fixture;
        }

        private static LevelData CreateAuthored(int blockCount, LevelEffectAxis?[] axes)
        {
            LevelBlockData[] blocks = new LevelBlockData[blockCount];
            for (int block = 0; block < blockCount; block++)
            {
                LevelEffectAxis? axis = block < axes.Length ? axes[block] : null;
                LevelEffectData[] effects = axis.HasValue
                    ? new[] { new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis.Value.ToString()) }) }
                    : Array.Empty<LevelEffectData>();
                blocks[block] = new LevelBlockData("b" + block, "Red", Vector2Int.zero, new[] { Vector2Int.zero },
                    effects);
            }

            return new LevelData("fixture", "Fixture", 7, 7, 60, Array.Empty<LevelDoorData>(), blocks);
        }

        private Cell GetCommittedOrigin(int block) => m_fixture.Session.Attempt.Simulation.Board.GetOrigin(block);

        // Presses the block where it stands, drags the pointer by the offset in four ticks and releases.
        private Cell DragAndRelease(int block, float dx, float dy)
        {
            Cell origin = GetCommittedOrigin(block);
            m_fixture.Tick(PointerPhase.Down, origin.X, origin.Y);
            for (int step = 1; step <= 4; step++)
            {
                float t = step / 4f;
                m_fixture.Tick(PointerPhase.Held, origin.X + dx * t, origin.Y + dy * t);
            }

            m_fixture.Tick(PointerPhase.Up, origin.X + dx, origin.Y + dy);
            Assert.That(m_fixture.Session.Attempt.Simulation.Drag.IsActive, Is.False);
            return GetCommittedOrigin(block);
        }

        private static void AssertOnAxis(Cell start, Cell reached, LevelEffectAxis axis)
        {
            if (axis == LevelEffectAxis.Horizontal)
            {
                Assert.That(reached.Y, Is.EqualTo(start.Y), "A Horizontal block keeps its row and changes only its column.");
            }
            else
            {
                Assert.That(reached.X, Is.EqualTo(start.X), "A Vertical block keeps its column and changes only its row.");
            }
        }

        private static string[] GetOpenRows(string shape)
        {
            switch (shape)
            {
                case "single": return s_openSingle;
                case "line": return s_openLine;
                default: return s_openEll;
            }
        }

        private static string[] GetCornerRows(string shape)
        {
            switch (shape)
            {
                case "single": return s_cornerSingle;
                case "line": return s_cornerLine;
                default: return s_cornerEll;
            }
        }

        private static DoorSpec[] GetCornerDoors(string shape)
        {
            switch (shape)
            {
                case "single":
                    return new[] { BoardFixture.Door(Side.Top, 0, 1, 'A'), BoardFixture.Door(Side.Left, 3, 1, 'A') };
                case "line":
                    return new[] { BoardFixture.Door(Side.Top, 0, 3, 'A'), BoardFixture.Door(Side.Left, 3, 1, 'A') };
                default:
                    return new[] { BoardFixture.Door(Side.Top, 0, 2, 'A'), BoardFixture.Door(Side.Left, 2, 2, 'A') };
            }
        }
    }
}
