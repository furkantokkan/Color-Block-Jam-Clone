using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    public sealed class CommandExecutorTests
    {
        private static readonly GameCommand s_move = GameCommand.Move(0, new Cell(0, 0), new Cell(1, 0));

        [Test]
        public void AnAppliedCommandIsLoggedAndARefusedOneIsNot()
        {
            FixedHandler handler = new FixedHandler(CommandOutcome.Applied);
            CommandExecutor executor = new CommandExecutor(new ICommandHandler[] { handler }, new CommandLog(8));

            Assert.That(executor.Execute(s_move), Is.EqualTo(CommandOutcome.Applied));
            handler.Outcome = CommandOutcome.Unreachable;
            GameCommand refused = GameCommand.Move(0, new Cell(1, 0), new Cell(3, 0));
            Assert.That(executor.Execute(refused), Is.EqualTo(CommandOutcome.Unreachable));

            Assert.That(handler.Calls, Is.EqualTo(2));
            Assert.That(executor.Log.Count, Is.EqualTo(1));
            Assert.That(executor.Log.GetAt(0), Is.EqualTo(s_move));
        }

        [Test]
        public void CommandsAreLoggedInTheOrderTheyApplied()
        {
            CommandExecutor executor = new CommandExecutor(
                new ICommandHandler[] { new FixedHandler(CommandOutcome.Applied) }, new CommandLog(8));
            GameCommand second = GameCommand.MoveAndExit(1, new Cell(2, 2), new Cell(2, 3), Side.Top);

            executor.Execute(s_move);
            executor.Execute(second);

            Assert.That(executor.Log.ToArray(), Is.EqualTo(new[] { s_move, second }));
        }

        [Test]
        public void EachLoggedCommandCarriesItsTurnTickAndPlayingSeconds()
        {
            FixedHandler handler = new FixedHandler(CommandOutcome.Applied);
            CommandExecutor executor = new CommandExecutor(new ICommandHandler[] { handler }, new CommandLog(8));
            GameCommand second = GameCommand.Move(0, new Cell(1, 0), new Cell(2, 0));

            executor.AdvanceTick(0.5f);
            executor.Execute(s_move);
            handler.Outcome = CommandOutcome.Unreachable;
            executor.Execute(second);
            handler.Outcome = CommandOutcome.Applied;
            executor.AdvanceTick(0.25f);
            executor.AdvanceTick(-1f);
            executor.Execute(second);

            CommandRecord first = executor.Log.GetRecordAt(0);
            Assert.That(first.Command, Is.EqualTo(s_move));
            Assert.That(first.Turn, Is.EqualTo(1));
            Assert.That(first.Tick, Is.EqualTo(1));
            Assert.That(first.Seconds, Is.EqualTo(0.5f));

            // The refused command took no turn; a negative delta adds no time but still counts a tick.
            CommandRecord last = executor.Log.GetRecordAt(1);
            Assert.That(last.Command, Is.EqualTo(second));
            Assert.That(last.Turn, Is.EqualTo(2));
            Assert.That(last.Tick, Is.EqualTo(3));
            Assert.That(last.Seconds, Is.EqualTo(0.75f));
            Assert.That(executor.Log.ToRecords(), Has.Length.EqualTo(2));
        }

        [Test]
        public void AKindWithoutAHandlerIsRefusedAndNotLogged()
        {
            FixedHandler handler = new FixedHandler(CommandOutcome.Applied);
            CommandExecutor executor = new CommandExecutor(new ICommandHandler[] { handler }, new CommandLog(8));

            Assert.That(executor.Execute(new GameCommand(CommandKind.None, 0, default, default, 0)),
                Is.EqualTo(CommandOutcome.UnknownKind));
            Assert.That(executor.Execute(new GameCommand((CommandKind)42, 0, default, default, 0)),
                Is.EqualTo(CommandOutcome.UnknownKind));
            Assert.That(executor.Execute(new GameCommand((CommandKind)(-3), 0, default, default, 0)),
                Is.EqualTo(CommandOutcome.UnknownKind));

            Assert.That(handler.Calls, Is.Zero);
            Assert.That(executor.Log.Count, Is.Zero);
        }

        [Test]
        public void AFullLogStopsRecordingButCommandsStillApply()
        {
            CommandExecutor executor = new CommandExecutor(
                new ICommandHandler[] { new FixedHandler(CommandOutcome.Applied) }, new CommandLog(2));

            executor.Execute(s_move);
            executor.Execute(s_move);
            Assert.That(executor.Log.IsTruncated, Is.False);

            Assert.That(executor.Execute(s_move), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(executor.Log.Count, Is.EqualTo(2));
            Assert.That(executor.Log.IsTruncated, Is.True);
        }

        [Test]
        public void TwoHandlersForOneKindOrAHandlerWithoutAKindAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new CommandExecutor(
                new ICommandHandler[] { new FixedHandler(CommandOutcome.Applied), new FixedHandler(CommandOutcome.Applied) },
                new CommandLog(1)));
            Assert.Throws<ArgumentException>(() => new CommandExecutor(
                new ICommandHandler[] { new FixedHandler(CommandOutcome.Applied, CommandKind.None) }, new CommandLog(1)));
            Assert.Throws<ArgumentException>(() => new CommandExecutor(new ICommandHandler[] { null }, new CommandLog(1)));
        }

        [TestCase(Side.Top)]
        [TestCase(Side.Bottom)]
        [TestCase(Side.Left)]
        [TestCase(Side.Right)]
        public void AnExitSideSurvivesTheCommandData(Side side)
        {
            GameCommand command = GameCommand.MoveAndExit(3, new Cell(1, 1), new Cell(1, 2), side);

            Assert.That(command.Kind, Is.EqualTo(CommandKind.MoveBlock));
            Assert.That(command.TryGetExit(out Side read), Is.True);
            Assert.That(read, Is.EqualTo(side));
        }

        [Test]
        public void AMoveHasNoExitAndAnUnknownOptionIsNoSide()
        {
            Assert.That(s_move.TryGetExit(out Side _), Is.False);
            Assert.That(new GameCommand(CommandKind.MoveBlock, 0, default, default, 9).TryGetExit(out Side _), Is.False);
            Assert.That(new GameCommand(CommandKind.MoveBlock, 0, default, default, -1).TryGetExit(out Side _), Is.False);
        }

        private sealed class FixedHandler : ICommandHandler
        {
            public FixedHandler(CommandOutcome outcome, CommandKind kind = CommandKind.MoveBlock)
            {
                Outcome = outcome;
                Kind = kind;
            }

            public CommandKind Kind { get; }

            public CommandOutcome Outcome { get; set; }

            public int Calls { get; private set; }

            public CommandOutcome Execute(in GameCommand command)
            {
                Calls++;
                return Outcome;
            }
        }
    }
}
