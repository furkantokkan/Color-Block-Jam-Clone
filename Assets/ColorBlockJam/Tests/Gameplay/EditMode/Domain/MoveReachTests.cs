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
    public sealed class MoveReachTests
    {
        // A must go around the B wall to reach the right half.
        private static readonly string[] s_detour =
        {
            ".....",
            "A.B..",
            "..B..",
            "..B.."
        };

        [Test]
        public void ItFindsAPathAroundAnObstacle()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_detour));
            MoveReach reach = new MoveReach(board);

            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(4, 0), AxisMask.Both), Is.True);
        }

        [Test]
        public void ACellWithNoFreePathIsUnreachable()
        {
            BoardState board = new BoardState(BoardFixture.Create(new[]
            {
                "..B..",
                "A.B..",
                "..B.."
            }));
            MoveReach reach = new MoveReach(board);

            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 1), new Cell(4, 1), AxisMask.Both), Is.False);
        }

        [Test]
        public void TheAllowedAxesLimitTheSearch()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_detour));
            MoveReach reach = new MoveReach(board);

            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(1, 2), AxisMask.Horizontal), Is.True);
            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(0, 3), AxisMask.Horizontal), Is.False);
            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(4, 0), AxisMask.Vertical), Is.False);
        }

        [Test]
        public void ACellGateClosesThePathLikeTheDrag()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_detour));
            MoveReach reach = new MoveReach(board);
            AttemptHooks closedTop = new AttemptHooksBuilder().Add(new DenyCellGate(new Cell(2, 3))).Build();

            Assert.That(reach.CanReach(closedTop, 0, new Cell(0, 2), new Cell(4, 0), AxisMask.Both), Is.False,
                "B fills column 2 below row 3, so the closed cell (2, 3) was the only way across");
        }

        [Test]
        public void EachStepAsksTheGatesWithTheBlockShownWhereItStands()
        {
            // The leash allows only cells beside the shown block: every single step passes, but a
            // search that asked about the committed board would stop one cell from the start.
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(new LeashCellGate()).Build(),
                new[] { "A...." });
            MoveReach reach = new MoveReach(sim.Board);

            Assert.That(reach.CanReach(sim.Hooks, 0, new Cell(0, 0), new Cell(4, 0), AxisMask.Horizontal), Is.True);
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 0), new Cell(4, 0))), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(4f, 0f)), Is.True);
            sim.Drag.Update(new BoardPoint(0f, 0f), out GameCommand _);
            Assert.That(sim.Preview.GetOrigin(0), Is.EqualTo(new Cell(0, 0)), "the live drag walks back the same way");
        }

        [Test]
        public void TheStartIsReachableAndAnOffBoardTargetIsNot()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_detour));
            MoveReach reach = new MoveReach(board);

            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(0, 2), AxisMask.None), Is.True);
            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(-1, 2), AxisMask.Both), Is.False);
            Assert.That(reach.CanReach(AttemptHooks.CreateEmpty(), 0, new Cell(0, 2), new Cell(5, 2), AxisMask.Both), Is.False);
        }

        [Test]
        public void EveryMoveTheLiveDragMakesIsAccepted()
        {
            // A seeded wandering pointer drives many drags; each release must be a move the executor accepts.
            string[] rows =
            {
                "......",
                ".AA.#.",
                ".A..B.",
                "C...B.",
                "C.#..."
            };
            BoardSimulation sim = BoardFixture.CreateSimulation(rows);
            Random random = new Random(20260925);
            int applied = 0;
            for (int drag = 0; drag < 200; drag++)
            {
                int block = random.Next(sim.Board.BlockCount);
                Cell start = sim.Board.GetOrigin(block);
                if (!sim.Drag.TryBegin(block, BoardPoint.FromCell(start)))
                {
                    continue;
                }

                for (int step = 0; step < 6; step++)
                {
                    BoardPoint pointer = new BoardPoint((float)(random.NextDouble() * 7.0 - 1.0), (float)(random.NextDouble() * 6.0 - 1.0));
                    sim.Drag.Update(pointer, out GameCommand _);
                }

                if (sim.Drag.TryEnd(out GameCommand move))
                {
                    sim.Events.Clear();
                    Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied), "drag " + drag + ": " + move);
                    applied++;
                }
            }

            Assert.That(applied, Is.GreaterThan(50), "the walk must actually move blocks");
        }
    }
}
