using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class BoardEventLogTests
    {
        [Test]
        public void AddedEventsReadBackInOrderUntilCleared()
        {
            BoardEventLog log = new BoardEventLog(2);

            log.Add(BoardEvent.MoveCommitted(new MoveCommittedEvent(1, new Cell(0, 0), new Cell(2, 0))));
            log.Add(BoardEvent.Exited(new BlockExitedEvent(0, 3, Side.Right, new Cell(4, 1), ExitOutcome.Leave)));

            Assert.That(log.Count, Is.EqualTo(2));
            Assert.That(log.GetAt(0).Kind, Is.EqualTo(BoardEventKind.MoveCommitted));
            Assert.That(log.GetAt(0).Door, Is.EqualTo(-1));
            Assert.That(log.GetAt(1).Kind, Is.EqualTo(BoardEventKind.Exited));
            Assert.That(log.GetAt(1).Door, Is.EqualTo(3));
            Assert.That(log.GetAt(1).Side, Is.EqualTo(Side.Right));

            log.Clear();

            Assert.That(log.Count, Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => log.GetAt(0));
        }

        [Test]
        public void AFullLogThrowsInsteadOfGrowing()
        {
            BoardEventLog log = new BoardEventLog(1);
            log.Add(BoardEvent.Removed(new BlockRemovedEvent(0)));

            Assert.Throws<InvalidOperationException>(() => log.Add(BoardEvent.FootprintChanged(new FootprintChangedEvent(0))));
            Assert.That(log.Count, Is.EqualTo(1));
        }

        [Test]
        public void TheSimulationSizesItsLogForEveryBlockPlusHeadroom()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "A.B", "..." });

            Assert.That(sim.Events.Capacity, Is.EqualTo(2 + 4));
            Assert.That(sim.IsCleared, Is.False);
        }

        [Test]
        public void TheSimulationSizesItsLogForEveryCellPlusHeadroom()
        {
            // A Vacuum can remove every live block, and Rocket parts can outnumber the authored blocks (B-7).
            BoardSimulation sim = BoardFixture.CreateSimulation(new[] { "AAA", "B.." });

            Assert.That(sim.Events.Capacity, Is.EqualTo(4 + 4));
        }

        [Test]
        public void BoosterEntriesCarryTheirParentCauseAndRemovedCell()
        {
            BoardEvent split = BoardEvent.FootprintChanged(new FootprintChangedEvent(4, 1, new Cell(2, 3)));
            BoardEvent removed = BoardEvent.Removed(new BlockRemovedEvent(3, CommandKind.RemoveColor, 1));
            BoardEvent plain = BoardEvent.Removed(new BlockRemovedEvent(2));
            BoardEvent move = BoardEvent.MoveCommitted(new MoveCommittedEvent(0, new Cell(0, 0), new Cell(1, 0)));

            Assert.That(split.Block, Is.EqualTo(4));
            Assert.That(split.Parent, Is.EqualTo(1));
            Assert.That(split.From, Is.EqualTo(new Cell(2, 3)));
            Assert.That(split.Cause, Is.EqualTo(CommandKind.None));
            Assert.That(removed.Parent, Is.EqualTo(1));
            Assert.That(removed.Cause, Is.EqualTo(CommandKind.RemoveColor));
            Assert.That(plain.Parent, Is.EqualTo(2), "a removal without a cause names the block itself");
            Assert.That(plain.Cause, Is.EqualTo(CommandKind.None));
            Assert.That(move.Parent, Is.EqualTo(-1));
            Assert.That(new FootprintChangedEvent(5).Parent, Is.EqualTo(5), "a block's own footprint event");
        }
    }
}
