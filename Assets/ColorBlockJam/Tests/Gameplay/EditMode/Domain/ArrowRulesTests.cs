using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The Arrow block rules (catalog BLK-01, RULE-34, AC-BLK-01.1, AC-BLK-01.2, AC-BLK-01.6) on the domain alone:
    /// the axis constraint and exit gate tables, ExitRules.Evaluate vetoing a perpendicular door, the drag and the
    /// move command keeping a 1x1, an I3 and an L3 block on its axis in both directions, a Rocket part reading its
    /// parent's axis through its footprint source, and calls that allocate nothing.
    /// </summary>
    public sealed class ArrowRulesTests
    {
        // A block in the top-left corner of a 4 x 4 board, flush against the Top and the Left side.
        private static readonly string[] s_cornerSingle = { "A...", "....", "....", "...." };
        private static readonly string[] s_cornerLine = { "AAA.", "....", "....", "...." };
        private static readonly string[] s_cornerColumn = { "A...", "A...", "A...", "...." };
        private static readonly string[] s_cornerEll = { "A...", "AA..", "....", "...." };

        // The same footprints in the middle of a 7 x 7 board, free to travel both ways on both axes.
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

        // A Horizontal I3 on the top row of a 5 x 3 board; a Rocket on its middle cell leaves two singles. The right
        // part (ID 1) stands under a matching Top door and in line with a matching Right door.
        private static readonly string[] s_splitLine = { "AAA..", ".....", "....." };
        private static readonly DoorSpec[] s_splitDoors =
        {
            BoardFixture.Door(Side.Top, 2, 1, 'A'), BoardFixture.Door(Side.Right, 2, 1, 'A')
        };

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [TestCase(AxisMask.Horizontal)]
        [TestCase(AxisMask.Vertical)]
        [TestCase(AxisMask.Both)]
        public void TheConstraintReturnsEachBlocksOwnAxes(AxisMask first)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "A.B" }));
            ArrowAxisConstraint constraint = new ArrowAxisConstraint(new[] { first, AxisMask.Both });

            Assert.That(constraint.GetAxes(board, 0), Is.EqualTo(first));
            Assert.That(constraint.GetAxes(board, 1), Is.EqualTo(AxisMask.Both), "A block without Arrow keeps both axes.");
            AttemptHooks hooks = new AttemptHooksBuilder().Add(constraint).Build();
            Assert.That(hooks.GetAxes(board, 0), Is.EqualTo(first));
        }

        [TestCase(AxisMask.Horizontal, Side.Top, false)]
        [TestCase(AxisMask.Horizontal, Side.Bottom, false)]
        [TestCase(AxisMask.Horizontal, Side.Left, true)]
        [TestCase(AxisMask.Horizontal, Side.Right, true)]
        [TestCase(AxisMask.Vertical, Side.Top, true)]
        [TestCase(AxisMask.Vertical, Side.Bottom, true)]
        [TestCase(AxisMask.Vertical, Side.Left, false)]
        [TestCase(AxisMask.Vertical, Side.Right, false)]
        [TestCase(AxisMask.Both, Side.Top, true)]
        [TestCase(AxisMask.Both, Side.Left, true)]
        public void TheGateAllowsOnlyTheSidesTheAxisPointsTo(AxisMask axes, Side side, bool allowed)
        {
            BoardState board = new BoardState(BoardFixture.Create(new[] { "A.." }));
            ArrowExitGate gate = new ArrowExitGate(new[] { axes });
            ExitOutcome outcome = ExitOutcome.Leave;

            Assert.That(gate.AllowsExit(board, new ExitQuery(0, 0, side, Cell.Zero), ref outcome), Is.EqualTo(allowed));
            Assert.That(outcome, Is.EqualTo(ExitOutcome.Leave), "The gate never turns an exit into Stay.");
        }

        [Test]
        public void TheHooksCopyTheirAxesAndRefuseAMissingAxis()
        {
            AxisMask[] axes = { AxisMask.Horizontal };
            ArrowAxisConstraint constraint = new ArrowAxisConstraint(axes);
            ArrowExitGate gate = new ArrowExitGate(axes);
            axes[0] = AxisMask.Vertical;
            BoardState board = new BoardState(BoardFixture.Create(new[] { "A.." }));
            ExitOutcome outcome = ExitOutcome.Leave;

            Assert.That(constraint.GetAxes(board, 0), Is.EqualTo(AxisMask.Horizontal), "The axis is fixed for the attempt.");
            Assert.That(gate.AllowsExit(board, new ExitQuery(0, 0, Side.Left, Cell.Zero), ref outcome), Is.True);
            Assert.Throws<ArgumentNullException>(() => new ArrowAxisConstraint(null));
            Assert.Throws<ArgumentNullException>(() => new ArrowExitGate(null));
            Assert.Throws<ArgumentException>(() => new ArrowAxisConstraint(new[] { AxisMask.None }));
            Assert.Throws<ArgumentException>(() => new ArrowExitGate(new[] { (AxisMask)4 }));
        }

        [TestCase("single", AxisMask.Horizontal)]
        [TestCase("single", AxisMask.Vertical)]
        [TestCase("single", AxisMask.Both)]
        [TestCase("line", AxisMask.Horizontal)]
        [TestCase("line", AxisMask.Vertical)]
        [TestCase("column", AxisMask.Horizontal)]
        [TestCase("column", AxisMask.Vertical)]
        [TestCase("ell", AxisMask.Horizontal)]
        [TestCase("ell", AxisMask.Vertical)]
        public void ExitRulesVetoAMatchingDoorOnAPerpendicularSide(string shape, AxisMask axes)
        {
            BoardState board = CreateCorner(shape);
            AttemptHooks hooks = CreateHooks(axes);
            Cell origin = board.GetOrigin(0);

            ExitVerdict left = ExitRules.Evaluate(board, hooks, 0, origin, Side.Left, out ExitCandidate _);
            ExitVerdict top = ExitRules.Evaluate(board, hooks, 0, origin, Side.Top, out ExitCandidate _);

            Assert.That(left, Is.EqualTo((axes & AxisMask.Horizontal) != 0 ? ExitVerdict.Fits : ExitVerdict.Vetoed), "Left");
            Assert.That(top, Is.EqualTo((axes & AxisMask.Vertical) != 0 ? ExitVerdict.Fits : ExitVerdict.Vetoed), "Top");
            Assert.That(ExitRules.Evaluate(board, AttemptHooks.CreateEmpty(), 0, origin, Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Fits), "Without the Arrow hooks both doors fit.");
        }

        [TestCase("single", AxisMask.Horizontal)]
        [TestCase("single", AxisMask.Vertical)]
        [TestCase("line", AxisMask.Horizontal)]
        [TestCase("line", AxisMask.Vertical)]
        [TestCase("ell", AxisMask.Horizontal)]
        [TestCase("ell", AxisMask.Vertical)]
        public void AnExitCommandLeavesOnlyThroughAnAxisSide(string shape, AxisMask axes)
        {
            Side along = axes == AxisMask.Horizontal ? Side.Left : Side.Top;
            Side across = axes == AxisMask.Horizontal ? Side.Top : Side.Left;
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(axes), GetCornerRows(shape), GetCornerDoors(shape));
            Cell origin = sim.Board.GetOrigin(0);

            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, origin, origin, across)),
                Is.EqualTo(CommandOutcome.AxisRefused), "A perpendicular door never lets it exit.");
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));
            Assert.That(sim.Events.Count, Is.Zero);

            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, origin, origin, along)), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
            Assert.That(sim.Commands.Log.Count, Is.EqualTo(1), "Only the applied exit is logged.");
        }

        [TestCase("single", AxisMask.Horizontal)]
        [TestCase("single", AxisMask.Vertical)]
        [TestCase("line", AxisMask.Horizontal)]
        [TestCase("line", AxisMask.Vertical)]
        [TestCase("ell", AxisMask.Horizontal)]
        [TestCase("ell", AxisMask.Vertical)]
        public void ALiveContactWithBothDoorsLeavesOnlyThroughTheAxisDoor(string shape, AxisMask axes)
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(axes), GetCornerRows(shape), GetCornerDoors(shape));
            Cell origin = sim.Board.GetOrigin(0);
            BoardPoint start = BoardPoint.FromCell(origin);
            BoardPoint across = axes == AxisMask.Horizontal ? new BoardPoint(0f, 0.9f) : new BoardPoint(-0.9f, 0f);

            Assert.That(sim.Drag.TryBegin(0, start), Is.True);
            Assert.That(sim.Drag.Axes, Is.EqualTo(axes));

            // Even a pointer pulling toward the perpendicular door exits by contact through the axis door (DEC-164).
            Assert.That(sim.Drag.Update(start + across, out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(exit.TryGetExit(out Side side), Is.True);
            Assert.That(side, Is.EqualTo(axes == AxisMask.Horizontal ? Side.Left : Side.Top));
            Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
        }

        [TestCase("single", AxisMask.Horizontal)]
        [TestCase("single", AxisMask.Vertical)]
        [TestCase("line", AxisMask.Horizontal)]
        [TestCase("line", AxisMask.Vertical)]
        [TestCase("ell", AxisMask.Horizontal)]
        [TestCase("ell", AxisMask.Vertical)]
        public void ADiagonalDragMovesOnlyAlongTheAxisBothWays(string shape, AxisMask axes)
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(axes), GetOpenRows(shape));
            Cell start = sim.Board.GetOrigin(0);

            Cell forward = DragAndRelease(sim, 0, new BoardPoint(3f, 3f));
            AssertOnAxis(start, forward, axes);
            Assert.That(forward, Is.Not.EqualTo(start), "It travels forward along its axis.");

            Cell back = DragAndRelease(sim, 0, new BoardPoint(-6f, -6f));
            AssertOnAxis(start, back, axes);
            Assert.That(axes == AxisMask.Horizontal ? back.X < start.X : back.Y < start.Y, Is.True,
                "It travels back past its start.");
            Assert.That(sim.Drag.Axes, Is.EqualTo(axes), "Every drag of the attempt reads the same axis.");
        }

        [Test]
        public void AnOffAxisMoveCommandIsRefusedAndABlockWithoutArrowStillMovesDiagonally()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(AxisMask.Horizontal, AxisMask.Both),
                new[] { "A....", ".....", "....B" });

            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 2), new Cell(0, 1))),
                Is.EqualTo(CommandOutcome.AxisRefused));
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 2), new Cell(2, 1))),
                Is.EqualTo(CommandOutcome.AxisRefused));
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 2), new Cell(3, 2))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Commands.Execute(GameCommand.Move(1, new Cell(4, 0), new Cell(2, 1))),
                Is.EqualTo(CommandOutcome.Applied), "The plain block keeps both axes.");
        }

        [Test]
        public void ARocketPartKeepsItsParentsAxisThroughItsFootprintSource()
        {
            AttemptHooks hooks = CreateHooks(AxisMask.Horizontal);
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, s_splitLine, s_splitDoors);

            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2))),
                Is.EqualTo(CommandOutcome.Applied));
            const int part = 1;
            Assert.That(sim.Board.BlockCount, Is.EqualTo(2), "the middle cell splits the I3 into two singles");
            Assert.That(sim.Board.GetFootprint(part).Source, Is.Zero, "the part's authored source is the I3");
            // A part stands at its parent's origin with its own cells, so no world cell moved (B-4).
            Cell origin = sim.Board.GetOrigin(part);
            Assert.That(origin, Is.EqualTo(new Cell(0, 2)));

            Assert.That(hooks.GetAxes(sim.Board, part), Is.EqualTo(AxisMask.Horizontal), "the part keeps the parent's axis");
            Assert.That(hooks.GetAxes(sim.Board, 0), Is.EqualTo(AxisMask.Horizontal));
            Assert.That(ExitRules.Evaluate(sim.Board, hooks, part, origin, Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Vetoed), "its matching Top door is perpendicular to the parent's axis");
            Assert.That(ExitRules.Evaluate(sim.Board, AttemptHooks.CreateEmpty(), part, origin, Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Fits), "without the Arrow hooks the same door fits");
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(part, origin, origin, Side.Top)),
                Is.EqualTo(CommandOutcome.AxisRefused));
            Assert.That(sim.Commands.Execute(GameCommand.Move(part, origin, new Cell(0, 1))),
                Is.EqualTo(CommandOutcome.AxisRefused), "the part never changes its row");

            // A diagonal drag moves the part along its row only; touching the Right door exits it (DEC-164),
            // while the Top door it touched from the start stays perpendicular.
            BoardPoint start = BoardPoint.FromCell(origin);
            Assert.That(sim.Drag.TryBegin(part, start), Is.True);
            Assert.That(sim.Drag.Update(new BoardPoint(start.X + 2f, start.Y - 2f), out GameCommand exit),
                Is.EqualTo(DragResult.ExitReady));
            Assert.That(exit.TryGetExit(out Side side), Is.True);
            Assert.That(side, Is.EqualTo(Side.Right), "it leaves through the matching door on its axis side");
            Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(part), Is.EqualTo(BlockStatus.Exited));
            ref readonly BoardEvent exited = ref sim.Events.GetAt(sim.Events.Count - 1);
            Assert.That(exited.Kind, Is.EqualTo(BoardEventKind.Exited));
            Assert.That(exited.From, Is.EqualTo(new Cell(2, 2)), "it exits from the row's end, not from where it started");
        }

        [Test]
        public void TheAxisConstraintAndTheExitGateAllocateNothingPerCall()
        {
            AxisMask[] axes = { AxisMask.Vertical };
            ArrowAxisConstraint constraint = new ArrowAxisConstraint(axes);
            ArrowExitGate gate = new ArrowExitGate(axes);
            AttemptHooks hooks = new AttemptHooksBuilder().Add(constraint).Add(gate).Build();
            BoardState board = CreateCorner("line");
            ReadRules(board, constraint, gate, hooks);

            Assert.That(() => ReadRules(board, constraint, gate, hooks), Is.Not.AllocatingGCMemory());
        }

        private static void ReadRules(BoardState board, ArrowAxisConstraint constraint, ArrowExitGate gate, AttemptHooks hooks)
        {
            ExitOutcome outcome = ExitOutcome.Leave;
            constraint.GetAxes(board, 0);
            gate.AllowsExit(board, new ExitQuery(0, 0, Side.Left, Cell.Zero), ref outcome);
            gate.AllowsExit(board, new ExitQuery(0, 0, Side.Top, Cell.Zero), ref outcome);
            ExitRules.Evaluate(board, hooks, 0, board.GetOrigin(0), Side.Left, out ExitCandidate _);
            ExitRules.Evaluate(board, hooks, 0, board.GetOrigin(0), Side.Top, out ExitCandidate _);
        }

        private static AttemptHooks CreateHooks(params AxisMask[] axes)
        {
            return new AttemptHooksBuilder().Add(new ArrowAxisConstraint(axes)).Add(new ArrowExitGate(axes)).Build();
        }

        private static BoardState CreateCorner(string shape)
        {
            return new BoardState(BoardFixture.Create(GetCornerRows(shape), GetCornerDoors(shape)));
        }

        private static string[] GetCornerRows(string shape)
        {
            switch (shape)
            {
                case "single": return s_cornerSingle;
                case "line": return s_cornerLine;
                case "column": return s_cornerColumn;
                default: return s_cornerEll;
            }
        }

        // A matching Top door over the block's columns and a matching Left door over its rows.
        private static DoorSpec[] GetCornerDoors(string shape)
        {
            switch (shape)
            {
                case "single":
                    return new[] { BoardFixture.Door(Side.Top, 0, 1, 'A'), BoardFixture.Door(Side.Left, 3, 1, 'A') };
                case "line":
                    return new[] { BoardFixture.Door(Side.Top, 0, 3, 'A'), BoardFixture.Door(Side.Left, 3, 1, 'A') };
                case "column":
                    return new[] { BoardFixture.Door(Side.Top, 0, 1, 'A'), BoardFixture.Door(Side.Left, 1, 3, 'A') };
                default:
                    return new[] { BoardFixture.Door(Side.Top, 0, 2, 'A'), BoardFixture.Door(Side.Left, 2, 2, 'A') };
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

        // Presses the block where it stands, drags the pointer by the offset and releases: the reached move runs.
        private static Cell DragAndRelease(BoardSimulation sim, int block, BoardPoint offset)
        {
            BoardPoint start = BoardPoint.FromCell(sim.Board.GetOrigin(block));
            Assert.That(sim.Drag.TryBegin(block, start), Is.True);
            for (int step = 1; step <= 4; step++)
            {
                float t = step / 4f;
                Assert.That(sim.Drag.Update(new BoardPoint(start.X + offset.X * t, start.Y + offset.Y * t), out GameCommand _),
                    Is.Not.EqualTo(DragResult.ExitReady));
            }

            if (sim.Drag.TryEnd(out GameCommand move))
            {
                Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));
            }

            return sim.Board.GetOrigin(block);
        }

        private static void AssertOnAxis(Cell start, Cell reached, AxisMask axes)
        {
            if (axes == AxisMask.Horizontal)
            {
                Assert.That(reached.Y, Is.EqualTo(start.Y), "A Horizontal block keeps its row and changes only its column.");
            }
            else
            {
                Assert.That(reached.X, Is.EqualTo(start.X), "A Vertical block keeps its column and changes only its row.");
            }
        }
    }
}
