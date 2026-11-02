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
    public sealed class IceRulesTests
    {
        [Test]
        public void FrozenBlockRefusesDragMoveAndExitUntilAnotherBlockLeaves()
        {
            var ice = new IceRules(new[] { 1, 0 });
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, new[] { "....", "A...", "...B" },
                BoardFixture.Door(Side.Top, 0, 1, 'A'), BoardFixture.Door(Side.Bottom, 3, 1, 'B'));
            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0, 1)), Is.False);
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 1), new Cell(1, 1))), Is.EqualTo(CommandOutcome.DragRefused));
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, new Cell(0, 1), new Cell(0, 2), Side.Top)), Is.EqualTo(CommandOutcome.DragRefused));
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(1, new Cell(3, 0), new Cell(3, 0), Side.Bottom)), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(ice.GetRemaining(0), Is.Zero);
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, new Cell(0, 1), new Cell(0, 2), Side.Top)), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void OnlyDistinctOtherClearsThawAndCountsNeverBecomeNegative()
        {
            var ice = new IceRules(new[] { 3, 0, 0, 0 });
            ice.OnMoveCommitted(new MoveCommittedEvent(1, default, new Cell(1, 0)));
            ice.OnFootprintChanged(new FootprintChangedEvent(1));
            ice.OnBlockExited(new BlockExitedEvent(1, 0, Side.Top, default, ExitOutcome.Stay));
            Assert.That(ice.GetRemaining(0), Is.EqualTo(3));
            ice.OnBlockExited(new BlockExitedEvent(1, 0, Side.Top, default, ExitOutcome.Leave));
            ice.OnBlockExited(new BlockExitedEvent(1, 0, Side.Top, default, ExitOutcome.Leave));
            ice.OnBlockRemoved(new BlockRemovedEvent(1));
            Assert.That(ice.GetRemaining(0), Is.EqualTo(2), "a block counts once, however it leaves");
            ice.OnBlockRemoved(new BlockRemovedEvent(2, CommandKind.RemoveBlock, 2));
            Assert.That(ice.GetRemaining(0), Is.EqualTo(1), "a booster removal thaws like an exit");
            ice.OnBlockExited(new BlockExitedEvent(3, 0, Side.Top, default, ExitOutcome.Leave));
            Assert.That(ice.GetRemaining(0), Is.Zero);
            Assert.That(ice.Revision, Is.EqualTo(3));
            var own = new IceRules(new[] { 1 });
            own.OnBlockExited(new BlockExitedEvent(0, 0, Side.Top, default, ExitOutcome.Leave));
            Assert.That(own.GetRemaining(0), Is.EqualTo(1));
        }

        [Test]
        public void ThawKeepsOtherAxisAndExitHooksAndFrozenOccupancy()
        {
            var ice = new IceRules(new[] { 1, 0 });
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Add(new AxisGate(AxisMask.Vertical)).Add(new VetoExitGate()).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, new[] { "....", "A...", "...B" },
                BoardFixture.Door(Side.Top, 0, 1, 'A'));
            Assert.That(sim.Board.GetOccupant(new Cell(0, 1)), Is.EqualTo(0));
            ice.OnBlockExited(new BlockExitedEvent(1, 0, Side.Bottom, default, ExitOutcome.Leave));
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 1), new Cell(1, 1))), Is.EqualTo(CommandOutcome.AxisRefused));
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, new Cell(0, 1), new Cell(0, 2), Side.Top)), Is.EqualTo(CommandOutcome.ExitRefused));
        }

        [Test]
        public void AttemptCopiesCountsAndHookReadsAllocateNothing()
        {
            int[] initial = { 20 };
            var ice = new IceRules(initial);
            initial[0] = 0;
            Assert.That(ice.GetRemaining(0), Is.EqualTo(20));
            ice.AllowsDrag(null, 0);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) ice.AllowsDrag(null, 0);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        [Test]
        public void ARocketPartInheritsItsParentsIceAndASplitThawsNobody()
        {
            // Rocket on the frozen L's corner (0,0) leaves (0,1) as block 0 and (1,0) as part 2.
            LevelModel model = BoardFixture.Create(new[] { "....", "A...", "AA.B" }, BoardFixture.Door(Side.Bottom, 3, 1, 'B'));
            var ice = new IceRules(new[] { 2, 0 }, model.TotalCellCount);
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Build();
            BoardSimulation sim = new BoardSimulation(model, hooks, DragSettings.Default);

            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0))), Is.EqualTo(CommandOutcome.Applied),
                "Rocket may target a frozen block");
            Assert.That(sim.Board.BlockCount, Is.EqualTo(3));
            Assert.That(ice.GetRemaining(0), Is.EqualTo(2), "the split thaws nobody");
            Assert.That(ice.GetRemaining(2), Is.EqualTo(2));
            Assert.That(ice.GetInitial(2), Is.EqualTo(2));
            Assert.That(ice.Revision, Is.EqualTo(1), "the new frozen part changes what views show");
            Assert.That(sim.Drag.TryBegin(2, new BoardPoint(1f, 0f)), Is.False, "the part is frozen too");
            Assert.That(sim.Commands.Execute(GameCommand.Move(2, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.DragRefused));

            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(2, new Cell(0, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(ice.GetRemaining(0), Is.EqualTo(1), "removing the frozen part thaws the other frozen block");

            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(1, new Cell(3, 0), new Cell(3, 0), Side.Bottom)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(ice.GetRemaining(0), Is.Zero, "a real exit still thaws");
        }

        [Test]
        public void ABlockThawedByAHammerRemovalExitsOnItsFirstDragToTheDoor()
        {
            var ice = new IceRules(new[] { 1, 0 });
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, new[] { "....", "A...", "...B" },
                BoardFixture.Door(Side.Top, 0, 1, 'A'));
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(1, new Cell(3, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(ice.GetRemaining(0), Is.Zero);

            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0f, 1f)), Is.True);
            Assert.That(sim.Drag.Update(new BoardPoint(0f, 2f), out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Exited));
        }

        [Test]
        public void ABlockThawedByAnExitExitsOnItsFirstDragToTheDoor()
        {
            var ice = new IceRules(new[] { 1, 0 });
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, new[] { "....", "A...", "...B" },
                BoardFixture.Door(Side.Top, 0, 1, 'A'), BoardFixture.Door(Side.Bottom, 3, 1, 'B'));
            Assert.That(sim.Drag.TryBegin(1, new BoardPoint(3f, 0f)), Is.True);
            Assert.That(sim.Drag.Update(new BoardPoint(3f, -1f), out GameCommand leave), Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Commands.Execute(leave), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Drag.TryEnd(out GameCommand _), Is.False);
            Assert.That(ice.GetRemaining(0), Is.Zero);

            Assert.That(sim.Drag.TryBegin(0, new BoardPoint(0f, 1f)), Is.True);
            Assert.That(sim.Drag.Update(new BoardPoint(0f, 2f), out GameCommand exit), Is.EqualTo(DragResult.ExitReady));
            Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
        }

        [Test]
        public void TheCapacityConstructorLeavesLaterIdsUnfrozenAndIgnoresTheParentsOwnEvent()
        {
            var ice = new IceRules(new[] { 3 }, 4);

            Assert.That(ice.BlockCount, Is.EqualTo(4));
            Assert.That(ice.GetRemaining(3), Is.Zero);
            Assert.That(ice.AllowsDrag(null, 3), Is.True);
            ice.OnFootprintChanged(new FootprintChangedEvent(0, 0, new Cell(1, 1)));
            Assert.That(ice.Revision, Is.Zero);
            Assert.That(ice.GetRemaining(0), Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new IceRules(new[] { 1, 1 }, 1));
        }

        [Test]
        public void RulesSizedToTheAuthoredBlocksNeverThrowForARocketPart()
        {
            // Without the capacity overload the part (ID 2) has no slot: its reads stay in bounds and it is unfrozen.
            var ice = new IceRules(new[] { 2, 0 });
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, new[] { "....", "A...", "AA.B" },
                BoardFixture.Door(Side.Bottom, 3, 1, 'B'));
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 0), new Cell(0, 0))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Board.BlockCount, Is.EqualTo(3));

            ExitOutcome outcome = ExitOutcome.Leave;
            Assert.That(ice.AllowsExit(sim.Board, new ExitQuery(2, 0, Side.Bottom, new Cell(0, 0)), ref outcome), Is.True);
            Assert.That(sim.Drag.TryBegin(2, new BoardPoint(1f, 0f)), Is.True);
            Assert.That(sim.Drag.TryEnd(out GameCommand _), Is.False, "an unmoved drag is no move");
            Assert.That(sim.Commands.Execute(GameCommand.Move(2, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 0), new Cell(1, 0))), Is.EqualTo(CommandOutcome.DragRefused),
                "the authored block keeps its Ice");
        }
    }
}
