using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class AttemptHooksTests
    {
        private static readonly string[] s_map =
        {
            "...",
            "AB.",
            "..."
        };

        [Test]
        public void EmptyHooksAllowEverythingAndPublishToNobody()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            AttemptHooks hooks = AttemptHooks.CreateEmpty();
            ExitQuery query = new ExitQuery(0, 0, Side.Top, new Cell(0, 1));

            Assert.That(hooks.AllowsDrag(board, 0), Is.True);
            Assert.That(hooks.GetAxes(board, 0), Is.EqualTo(AxisMask.Both));
            Assert.That(hooks.AllowsCell(board, 0, new Cell(2, 2)), Is.True);
            Assert.That(hooks.AllowsExit(board, query, out ExitOutcome outcome), Is.True);
            Assert.That(outcome, Is.EqualTo(ExitOutcome.Leave));
            Assert.DoesNotThrow(() =>
            {
                hooks.Publish(new BlockExitedEvent(0, 0, Side.Top, new Cell(0, 1), ExitOutcome.Leave));
                hooks.Publish(new BlockRemovedEvent(0));
                hooks.Publish(new MoveCommittedEvent(0, new Cell(0, 1), new Cell(0, 2)));
                hooks.Publish(new FootprintChangedEvent(0));
            });
        }

        [Test]
        public void GatesCombineWithAnd()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            AttemptHooks hooks = new AttemptHooksBuilder()
                .Add(new DenyDragGate(1))
                .Add(new DenyDragGate(-1))
                .Add(new DenyCellGate(new Cell(2, 2)))
                .Add(new DenyCellGate(new Cell(0, 0)))
                .Build();

            Assert.That(hooks.AllowsDrag(board, 0), Is.True);
            Assert.That(hooks.AllowsDrag(board, 1), Is.False);
            Assert.That(hooks.AllowsCell(board, 0, new Cell(1, 2)), Is.True);
            Assert.That(hooks.AllowsCell(board, 0, new Cell(2, 2)), Is.False);
            Assert.That(hooks.AllowsCell(board, 0, new Cell(0, 0)), Is.False);
        }

        [TestCase(AxisMask.Horizontal, AxisMask.Both, AxisMask.Horizontal)]
        [TestCase(AxisMask.Horizontal, AxisMask.Vertical, AxisMask.None)]
        [TestCase(AxisMask.Vertical, AxisMask.Vertical, AxisMask.Vertical)]
        public void AxisConstraintsIntersect(AxisMask first, AxisMask second, AxisMask expected)
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            AttemptHooks hooks = new AttemptHooksBuilder().Add(new AxisGate(first)).Add(new AxisGate(second)).Build();

            Assert.That(hooks.GetAxes(board, 0), Is.EqualTo(expected));
        }

        [Test]
        public void AnyExitGateMaySetStayButEveryGateMustAllow()
        {
            BoardState board = new BoardState(BoardFixture.Create(s_map));
            ExitQuery query = new ExitQuery(0, 0, Side.Left, new Cell(0, 1));
            AttemptHooks stay = new AttemptHooksBuilder().Add(new StayExitGate()).Build();
            AttemptHooks stayThenVeto = new AttemptHooksBuilder().Add(new StayExitGate()).Add(new VetoExitGate()).Build();

            Assert.That(stay.AllowsExit(board, query, out ExitOutcome stayOutcome), Is.True);
            Assert.That(stayOutcome, Is.EqualTo(ExitOutcome.Stay));
            Assert.That(stayThenVeto.AllowsExit(board, query, out _), Is.False);
        }

        [Test]
        public void ListenersHearEveryEventInRegistrationOrder()
        {
            List<string> order = new List<string>();
            AttemptHooks hooks = new AttemptHooksBuilder()
                .Add(new RecordingListener("first", order))
                .Add(new RecordingListener("second", order))
                .Build();

            hooks.Publish(new BlockExitedEvent(0, 0, Side.Top, new Cell(0, 1), ExitOutcome.Leave));
            hooks.Publish(new MoveCommittedEvent(1, new Cell(1, 1), new Cell(1, 2)));
            hooks.Publish(new BlockRemovedEvent(1));
            hooks.Publish(new FootprintChangedEvent(1));

            Assert.That(order, Is.EqualTo(new[]
            {
                "first:Exited", "second:Exited",
                "first:MoveCommitted", "second:MoveCommitted",
                "first:Removed", "second:Removed",
                "first:FootprintChanged", "second:FootprintChanged"
            }));
        }

        [Test]
        public void TheBuilderRejectsNullHooks()
        {
            AttemptHooksBuilder builder = new AttemptHooksBuilder();

            Assert.Throws<ArgumentNullException>(() => builder.Add((IDragGate)null));
            Assert.Throws<ArgumentNullException>(() => builder.Add((IBoardEventListener)null));
        }
    }
}
