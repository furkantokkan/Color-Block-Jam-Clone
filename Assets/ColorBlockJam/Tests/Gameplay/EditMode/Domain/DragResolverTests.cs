using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class DragResolverTests
    {
        private static readonly string[] s_lane =
        {
            "......",
            "A..B..",
            "......"
        };

        [Test]
        public void StraightDragStopsAtTheFirstBlockedCell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);

            for (int x = 1; x <= 5; x++)
            {
                DragTo(sim, x, 1);
            }

            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
            Assert.That(sim.Preview.GetOccupant(new Cell(3, 1)), Is.EqualTo(1), "B never overlapped");
        }

        [Test]
        public void APointerJumpAcrossABlockStopsBeforeIt()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);

            DragResult result = DragTo(sim, 5, 1);

            Assert.That(result, Is.EqualTo(DragResult.Moved));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
        }

        [Test]
        public void AVoidCellStopsTheBlock()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "A.#..." });
            Begin(sim, 0);

            DragTo(sim, 5, 0);

            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 0)));
        }

        [Test]
        public void TheLogicalCellChangesExactlyAtHalfACell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);

            Assert.That(DragTo(sim, 0.49f, 1), Is.EqualTo(DragResult.Unchanged));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(0, 1)));
            Assert.That(DragTo(sim, 0.5f, 1), Is.EqualTo(DragResult.Moved));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
            Assert.That(DragTo(sim, 0.5f, 1), Is.EqualTo(DragResult.Unchanged));
        }

        private static readonly string[] s_corner =
        {
            "....",
            "....",
            ".B..",
            "A..."
        };

        [Test]
        public void ADiagonalDragMovesTheDominantXAxisFirst()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_corner);
            Begin(sim, 0);

            DragTo(sim, 2, 1);

            // Y first would reach (0,1) and then stop against B at (1,1).
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
        }

        [Test]
        public void ADiagonalDragMovesTheDominantYAxisFirst()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_corner);
            Begin(sim, 0);

            DragTo(sim, 1, 2);

            // X first would reach (1,0) and then stop under B at (1,1).
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 2)));
        }

        [Test]
        public void AnEqualDiagonalStepsXFirst()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new DragSettings(1, 0.5f), new[]
            {
                "...",
                "...",
                "A.."
            });
            Begin(sim, 0);

            DragTo(sim, 1, 1);
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 0)));

            DragTo(sim, 1, 1);
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void ABlockedAxisSlidesAlongTheFreeAxis()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[]
            {
                "....",
                "....",
                "A...",
                "BBBB"
            });
            Begin(sim, 0);

            DragResult result = DragTo(sim, 1, -2);

            Assert.That(result, Is.EqualTo(DragResult.Moved));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void StepsPerUpdateAreBounded()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new DragSettings(1, 0.5f), new[] { "A....." });
            Begin(sim, 0);

            DragTo(sim, 5, 0);
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 0)));

            DragTo(sim, 5, 0);
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 0)));
        }

        [Test]
        public void TheGrabOffsetIsKeptForTheWholeDrag()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0.3f, 1.2f)), Is.True);

            DragTo(sim, 1.3f, 1.2f);

            Assert.That(sim.Drag.GrabOffset.X, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(sim.Drag.GrabOffset.Y, Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(1, 1)));
        }

        [Test]
        public void ReleaseKeepsTheReachedCell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);
            DragTo(sim, 2.2f, 1);

            Release(sim);

            Assert.That(sim.Drag.IsActive, Is.False);
            Assert.That(sim.Drag.Block, Is.EqualTo(-1));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(2, 1)), "the release command moved the block");
        }

        [Test]
        public void TheDragPreviewsWithoutChangingTheBoard()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);

            DragTo(sim, 2, 1);

            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "only a command changes the board");
            Assert.That(sim.Board.GetOccupant(new Cell(2, 1)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Commands.Log.Count, Is.Zero);
        }

        [Test]
        public void AReleaseProducesOneMoveCommandFromStartToTheReachedCell()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);
            DragTo(sim, 2, 1);

            Assert.That(sim.Drag.TryEnd(out GameCommand move), Is.True);

            Assert.That(move, Is.EqualTo(GameCommand.Move(0, new Cell(0, 1), new Cell(2, 1))));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "the preview drops the drag at release");
        }

        [Test]
        public void MoveCommittedIsPublishedOnlyWhenTheBoardChanged()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_lane);

            Begin(sim, 0);
            DragTo(sim, 1, 1);
            DragTo(sim, 0, 1);
            Release(sim);
            Assert.That(listener.Total, Is.EqualTo(0), "a drag back to the start changed nothing");
            Assert.That(sim.Events.Count, Is.EqualTo(0));

            Begin(sim, 0);
            DragTo(sim, 2, 1);
            Release(sim);

            Assert.That(listener.Moves.Count, Is.EqualTo(1));
            Assert.That(listener.Moves[0].Block, Is.EqualTo(0));
            Assert.That(listener.Moves[0].From, Is.EqualTo(new Cell(0, 1)));
            Assert.That(listener.Moves[0].To, Is.EqualTo(new Cell(2, 1)));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
            Assert.That(sim.Events.GetAt(0).Kind, Is.EqualTo(BoardEventKind.MoveCommitted));
            Assert.That(sim.Events.GetAt(0).To, Is.EqualTo(new Cell(2, 1)));
        }

        [Test]
        public void AnExitRaisesNoMoveCommitted()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(),
                new[]
                {
                    "...",
                    ".A.",
                    "..."
                },
                BoardFixture.Door(Side.Top, 1, 1, 'A'));
            Begin(sim, 0);

            // The step into contact and the exit are one MoveBlock command (DEC-122, DEC-164).
            Assert.That(DragTo(sim, 1, 2), Is.EqualTo(DragResult.ExitReady));
            Release(sim);

            Assert.That(listener.Exited.Count, Is.EqualTo(1));
            Assert.That(listener.Moves.Count, Is.EqualTo(0));
        }

        [Test]
        public void InterruptDefersTheMoveUntilTheNextCommandPhase()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_lane);
            Begin(sim, 0);
            DragTo(sim, 2, 1);

            sim.Drag.Interrupt();

            Assert.That(sim.Drag.IsActive, Is.False);
            Assert.That(sim.Drag.HasPending, Is.True);
            Assert.That(listener.Total, Is.EqualTo(0), "listeners hear nothing outside the command phase");
            Assert.That(sim.Events.Count, Is.EqualTo(0));
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(0, 1)), "the interrupted move has not run");
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)), "the player still sees the block where it was left");
            Assert.Throws<InvalidOperationException>(() => sim.Drag.TryBegin(1, new BoardPoint(3, 1)));

            TakePending(sim);
            TakePending(sim);

            Assert.That(sim.Drag.HasPending, Is.False);
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
            Assert.That(listener.Moves.Count, Is.EqualTo(1));
            Assert.That(listener.Moves[0].To, Is.EqualTo(new Cell(2, 1)));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
        }

        [Test]
        public void InterruptWithoutAChangeLeavesNothingPending()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);
            DragTo(sim, 0.2f, 1);

            sim.Drag.Interrupt();

            Assert.That(sim.Drag.HasPending, Is.False);
            Assert.That(sim.Drag.IsActive, Is.False);
        }

        [Test]
        public void ARefusedBeginLeavesTheResolverInactive()
        {
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new DenyDragGate(1)).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, s_lane);

            Assert.That(sim.Drag.TryBegin(1, new BoardPoint(3, 1)), Is.False, "the drag gate refuses B");
            Assert.That(sim.Drag.IsActive, Is.False);
            Assert.That(sim.Drag.Block, Is.EqualTo(-1));

            sim.Board.Release(0, BlockStatus.Exited);
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0, 1)), Is.False, "A has exited");
            Assert.That(sim.Drag.IsActive, Is.False);
            Assert.That(DragTo(sim, 2, 1), Is.EqualTo(DragResult.Unchanged));
        }

        [Test]
        public void ASecondBeginWhileActiveIsRefused()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_lane);
            Begin(sim, 0);

            Assert.That(sim.Drag.TryBegin(1, new BoardPoint(3, 1)), Is.False);
            Assert.That(sim.Drag.Block, Is.EqualTo(0));
        }

        [Test]
        public void TheAxisConstraintIsFixedForTheWholeDrag()
        {
            AxisGate gate = new AxisGate(AxisMask.Horizontal);
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(gate).Build(), s_lane);
            Begin(sim, 0);

            gate.Axes = AxisMask.Both;
            DragTo(sim, 2, 2);

            Assert.That(sim.Drag.Axes, Is.EqualTo(AxisMask.Horizontal));
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(2, 1)));
        }

        private static void Begin(BoardSimulation sim, int block)
        {
            BoardPoint pointer = BoardPoint.FromCell(sim.Preview.GetOrigin(block));
            Assert.That(sim.Drag.TryBegin(block, pointer), Is.True);
        }

        // Runs a contact exit's command at once, as the pointer router does.
        private static DragResult DragTo(BoardSimulation sim, float x, float y)
        {
            DragResult result = sim.Drag.Update(new BoardPoint(x, y), out GameCommand exit);
            if (result == DragResult.ExitReady)
            {
                Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            }

            return result;
        }

        private static void Release(BoardSimulation sim)
        {
            if (sim.Drag.TryEnd(out GameCommand move))
            {
                Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));
            }
        }

        private static void TakePending(BoardSimulation sim)
        {
            if (sim.Drag.TryTakePending(out GameCommand move))
            {
                Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));
            }
        }
    }
}
