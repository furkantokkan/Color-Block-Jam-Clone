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
    /// The Barrier rule (catalog OBJ-05, DEC-MECH-3, DEC-MECH-18) on the domain alone, contract
    /// TASK-20260925-103527585-3d1b0a34 acceptance 1–4 and the domain half of 6: a closed barrier stops a drag and a move
    /// command until one block clears; closing under a block keeps it and lets only it leave, a Rocket part included;
    /// a Vacuum of N blocks flips it N times while a Stay exit, a move and a split flip nothing; a closed barrier in a
    /// concave swept exit path refuses the exit; and the gate and event dispatch allocate nothing.
    /// </summary>
    public sealed class BarrierRuleTests
    {
        // Block A (0) at the bottom-left, block B (1) at the top-left under a B-coloured Top door; the barrier is the
        // whole column x = 2.
        private static readonly string[] s_lane = { "B....", ".....", "A...." };
        private static readonly DoorSpec[] s_laneDoors = { BoardFixture.Door(Side.Top, 0, 1, 'B') };
        private static readonly Cell[] s_laneBarrier = { new Cell(2, 0), new Cell(2, 1), new Cell(2, 2) };

        // A (0) stands on the one-cell barrier at (2, 0); B (1) can clear through its Top door; C (2) waits on the right.
        private static readonly string[] s_standing = { "B....", ".....", "..A.C" };
        private static readonly Cell[] s_standingBarrier = { new Cell(2, 0) };

        // 'A' (0) and 'a' (2, ordinal letter order) share a colour; B (1) has its own. The barrier cell (1, 0) is empty.
        private static readonly string[] s_colours = { "A.a.B", "....." };

        // A U opening upward under a full-width Top door: the notch (1, 1) lies in the bottom cell's swept path.
        private static readonly string[] s_notch = { "A.A", "AAA" };
        private static readonly DoorSpec[] s_notchDoor = { BoardFixture.Door(Side.Top, 0, 3, 'A') };

        // An I3 whose right cell stands on the open barrier at (2, 0); B (1) waits for a Hammer.
        private static readonly string[] s_split = { ".....", "AAA.B" };

        [OneTimeSetUp]
        public void WarmUpTheAllocationRecorder()
        {
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void AClosedBarrierStopsADragAndAMoveUntilOneBlockClears()
        {
            BarrierRule rule = CreateRule(5, 3, false, s_laneBarrier);
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(rule), s_lane, s_laneDoors);

            Assert.That(DragAndRelease(sim, 0, new BoardPoint(4f, 0f)), Is.EqualTo(new Cell(1, 0)),
                "The drag stops on the cell before the closed barrier.");
            Assert.That(Run(sim, GameCommand.Move(0, new Cell(1, 0), new Cell(4, 0))), Is.EqualTo(CommandOutcome.Unreachable));
            Assert.That(MoveRules.CanOccupy(sim.Board, sim.Hooks, 0, new Cell(2, 0)), Is.False);

            Assert.That(Run(sim, GameCommand.MoveAndExit(1, new Cell(0, 2), new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied), "B clears through its door.");
            Assert.That(rule.IsOpen(0), Is.True, "One clear opens the barrier.");
            Assert.That(rule.GetVersion(0), Is.EqualTo(1));

            Assert.That(DragAndRelease(sim, 0, new BoardPoint(3f, 0f)), Is.EqualTo(new Cell(4, 0)),
                "The open barrier's cells are floor.");
        }

        [Test]
        public void ABarrierClosingUnderABlockKeepsItAndLetsOnlyItLeave()
        {
            BarrierRule rule = CreateRule(5, 3, true, s_standingBarrier);
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(rule), s_standing, s_laneDoors);

            Assert.That(Run(sim, GameCommand.MoveAndExit(1, new Cell(0, 2), new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(rule.IsOpen(0), Is.False, "B's clear closes the barrier.");
            Assert.That(sim.Board.GetOrigin(0), Is.EqualTo(new Cell(2, 0)), "The closing barrier does not move A.");
            Assert.That(rule.AllowsCell(sim.Board, 0, new Cell(2, 0)), Is.True, "A may stay on its own cell.");
            Assert.That(rule.AllowsCell(sim.Board, 2, new Cell(2, 0)), Is.False);

            Assert.That(DragAndRelease(sim, 0, new BoardPoint(0f, 1f)), Is.EqualTo(new Cell(2, 1)), "A may leave.");
            Assert.That(DragAndRelease(sim, 2, new BoardPoint(-4f, 0f)), Is.EqualTo(new Cell(3, 0)),
                "C stops before the closed, now empty cell.");
            Assert.That(Run(sim, GameCommand.Move(2, new Cell(3, 0), new Cell(2, 0))), Is.EqualTo(CommandOutcome.Unreachable));
            Assert.That(DragAndRelease(sim, 0, new BoardPoint(0f, -1f)), Is.EqualTo(new Cell(2, 1)),
                "Once off the cell, A cannot return until the barrier opens.");
        }

        [Test]
        public void ARocketPartOnAClosedBarrierMayLeaveAndNoOtherBlockEnters()
        {
            BarrierRule rule = CreateRule(5, 2, true, new Cell(2, 0));
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(rule), s_split);

            Assert.That(Run(sim, GameCommand.RemoveBlock(1, new Cell(4, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(rule.IsOpen(0), Is.False, "The Hammer removal closes the barrier under the I3.");
            Assert.That(Run(sim, GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(rule.GetVersion(0), Is.EqualTo(1), "A Rocket split is not a clear.");

            const int part = 2;
            Assert.That(sim.Board.GetOccupant(new Cell(2, 0)), Is.EqualTo(part), "the right part got a new ID (DEC-146)");
            Assert.That(rule.AllowsCell(sim.Board, part, new Cell(2, 0)), Is.True);
            Assert.That(rule.AllowsCell(sim.Board, 0, new Cell(2, 0)), Is.False);

            Cell partOrigin = sim.Board.GetOrigin(part);
            Assert.That(Run(sim, GameCommand.Move(part, partOrigin, partOrigin + new Cell(0, 1))), Is.EqualTo(CommandOutcome.Applied),
                "The part standing on the closed cell may leave.");
            Assert.That(Run(sim, GameCommand.Move(0, new Cell(0, 0), new Cell(2, 0))), Is.EqualTo(CommandOutcome.Unreachable),
                "The other part may not enter.");
            Assert.That(Run(sim, GameCommand.Move(0, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void AVacuumOfNBlocksFlipsNTimesAndAHammerOnce()
        {
            BarrierRule rule = CreateRule(5, 2, false, new Cell(1, 0));
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(rule), s_colours);

            Assert.That(Run(sim, GameCommand.RemoveColor(0, new Cell(0, 1), BoardFixture.ColorOf('A'))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Events.Count, Is.EqualTo(2), "one removal event per vacuumed block");
            Assert.That(rule.GetVersion(0), Is.EqualTo(2), "two removals, two flips (DEC-MECH-3)");
            Assert.That(rule.IsOpen(0), Is.False, "An even number of flips returns the barrier to its state.");

            Assert.That(Run(sim, GameCommand.RemoveBlock(1, new Cell(4, 1))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(rule.GetVersion(0), Is.EqualTo(3));
            Assert.That(rule.IsOpen(0), Is.True);
        }

        [Test]
        public void AStayExitAMoveAndASplitFlipNothing()
        {
            BarrierRule rule = CreateRule(5, 3, false, s_laneBarrier);
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new StayExitGate()).Add((ICellGate)rule)
                .Add((IBoardEventListener)rule).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, s_lane, s_laneDoors);

            Assert.That(Run(sim, GameCommand.MoveAndExit(1, new Cell(0, 2), new Cell(0, 2), Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard), "The Stay outcome kept B.");
            Assert.That(Run(sim, GameCommand.Move(0, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            rule.OnFootprintChanged(new FootprintChangedEvent(0));
            rule.OnBlockExited(new BlockExitedEvent(0, 0, Side.Top, Cell.Zero, ExitOutcome.Stay));

            Assert.That(rule.GetVersion(0), Is.Zero);
            Assert.That(rule.IsOpen(0), Is.False);
        }

        [Test]
        public void AClosedBarrierInAConcaveSweptExitPathRefusesTheExit()
        {
            BarrierRule closed = CreateRule(3, 2, false, new Cell(1, 1));
            BoardSimulation sim = BoardFixture.CreateSimulation(CreateHooks(closed), s_notch, s_notchDoor);
            Cell origin = sim.Board.GetOrigin(0);

            Assert.That(ExitRules.Evaluate(sim.Board, sim.Hooks, 0, origin, Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.PathBlocked), "The notch cell is closed (XP-3 in ExitRules).");
            Assert.That(Run(sim, GameCommand.MoveAndExit(0, origin, origin, Side.Top)), Is.EqualTo(CommandOutcome.ExitRefused));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard));

            AttemptHooks open = CreateHooks(CreateRule(3, 2, true, new Cell(1, 1)));
            Assert.That(ExitRules.Evaluate(sim.Board, open, 0, origin, Side.Top, out ExitCandidate _),
                Is.EqualTo(ExitVerdict.Fits), "An open barrier is floor.");
        }

        [Test]
        public void TheRuleCopiesItsArraysAndRefusesInvalidBarriers()
        {
            Cell[][] cells = { new[] { new Cell(0, 0), new Cell(1, 0) }, new[] { new Cell(3, 1) } };
            bool[] startsOpen = { false, true };
            BarrierRule rule = new BarrierRule(4, 2, cells, startsOpen);
            cells[0][0] = new Cell(3, 0);
            startsOpen[0] = true;

            Assert.That(rule.BarrierCount, Is.EqualTo(2));
            Assert.That(rule.GetBarrierAt(new Cell(0, 0)), Is.Zero, "The cells were copied at construction.");
            Assert.That(rule.GetBarrierAt(new Cell(3, 0)), Is.EqualTo(BarrierRule.k_noBarrier));
            Assert.That(rule.GetBarrierAt(new Cell(3, 1)), Is.EqualTo(1));
            Assert.That(rule.GetBarrierAt(new Cell(-1, 0)), Is.EqualTo(BarrierRule.k_noBarrier));
            Assert.That(rule.IsOpen(0), Is.False, "The start states were copied at construction.");
            Assert.That(rule.IsOpen(1), Is.True);

            Assert.Throws<ArgumentNullException>(() => new BarrierRule(4, 2, null, startsOpen));
            Assert.Throws<ArgumentNullException>(() => new BarrierRule(4, 2, cells, null));
            Assert.Throws<ArgumentException>(() => new BarrierRule(4, 2, cells, new[] { true }));
            Assert.Throws<ArgumentException>(() => new BarrierRule(4, 2, new[] { new Cell[0] }, new[] { true }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarrierRule(4, 2, new[] { new[] { new Cell(4, 0) } }, new[] { true }));
            Assert.Throws<ArgumentException>(() => new BarrierRule(4, 2,
                new[] { new[] { new Cell(1, 1) }, new[] { new Cell(1, 1) } }, new[] { true, false }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BarrierRule(0, 2, new Cell[0][], new bool[0]));
        }

        [Test]
        public void TheGateAndTheFlipDispatchAllocateNothing()
        {
            BarrierRule rule = CreateRule(5, 3, false, s_laneBarrier);
            AttemptHooks hooks = CreateHooks(rule);
            BoardState board = new BoardState(BoardFixture.Create(s_lane, s_laneDoors));
            AskAndFlip(board, hooks);

            Assert.That(() => AskAndFlip(board, hooks), Is.Not.AllocatingGCMemory());
            Assert.That(rule.GetVersion(0), Is.EqualTo(6), "each call flipped three times");
        }

        private static void AskAndFlip(BoardState board, AttemptHooks hooks)
        {
            hooks.AllowsCell(board, 0, new Cell(2, 0));
            hooks.AllowsCell(board, 0, new Cell(1, 0));
            MoveRules.CanOccupy(board, hooks, 0, new Cell(2, 0));
            ExitRules.Evaluate(board, hooks, 1, board.GetOrigin(1), Side.Top, out ExitCandidate _);
            hooks.Publish(new BlockRemovedEvent(0));
            hooks.Publish(new BlockExitedEvent(1, 0, Side.Top, Cell.Zero, ExitOutcome.Leave));
            hooks.Publish(new BlockExitedEvent(1, 0, Side.Top, Cell.Zero, ExitOutcome.Stay));
            hooks.Publish(new MoveCommittedEvent(0, Cell.Zero, Cell.Zero));
            hooks.Publish(new BlockRemovedEvent(0));
        }

        private static BarrierRule CreateRule(int width, int height, bool startsOpen, params Cell[] cells)
        {
            return new BarrierRule(width, height, new[] { cells }, new[] { startsOpen });
        }

        // The hooks the Barrier module registers: the rule as cell gate and as clear listener.
        private static AttemptHooks CreateHooks(BarrierRule rule)
        {
            return new AttemptHooksBuilder().Add((ICellGate)rule).Add((IBoardEventListener)rule).Build();
        }

        // A tick clears the event log before its command phase; a headless caller does the same.
        private static CommandOutcome Run(BoardSimulation sim, GameCommand command)
        {
            sim.Events.Clear();
            return sim.Commands.Execute(command);
        }

        // Presses the block where it stands, drags the pointer by the offset in four steps and releases: the reached
        // move runs as one command.
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
                Assert.That(Run(sim, move), Is.EqualTo(CommandOutcome.Applied));
            }

            return sim.Board.GetOrigin(block);
        }
    }
}
