using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Tests.Gameplay.Fixtures;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay.Domain
{
    /// <summary>
    /// The same level and the same command list give the same board and the same events, whether the
    /// pointer produced the commands or a replay submits them (DEC-122).
    /// </summary>
    public sealed class ReplayTests
    {
        // A leaves through the Top, B through the Bottom span, and C only moves.
        private static readonly string[] s_level =
        {
            "....",
            "A...",
            ".BB.",
            "...C"
        };

        private static readonly DoorSpec[] s_doors =
        {
            BoardFixture.Door(Side.Top, 0, 1, 'A'),
            BoardFixture.Door(Side.Bottom, 1, 2, 'B')
        };

        // A (0) is an I5, B (1) and a (2) sit on the bottom row; 'A' and 'a' share a colour.
        private static readonly string[] s_boosterLevel =
        {
            "......",
            "AAAAA.",
            "......",
            "a....B"
        };

        [Test]
        public void APointerPlayedAttemptReplaysToTheSameBoardAndEvents()
        {
            AssertReplayMatches(builder => { });
        }

        [Test]
        public void AReplayMatchesWithAStayExitGate()
        {
            AssertReplayMatches(builder => builder.Add(new StayExitGate()));
        }

        [Test]
        public void AReplayMatchesWithACellGateThatReadsTheBoard()
        {
            // The leash answers from where the asked board shows the block, so a handler that asked
            // about the committed board instead of each step's origin would refuse C's two-cell move.
            AssertReplayMatches(builder => builder.Add(new LeashCellGate()));
        }

        [Test]
        public void AReplaySourceStopsAtTheFirstRefusedCommand()
        {
            BoardSimulation sim = BoardFixture.CreateSimulation(s_level, s_doors);
            GameCommand valid = GameCommand.Move(2, new Cell(3, 0), new Cell(3, 1));
            GameCommand stale = GameCommand.Move(2, new Cell(3, 0), new Cell(3, 2));
            ReplayCommandSource source = new ReplayCommandSource(new[] { valid, stale, valid });

            // Owner: this test. Stops: the source finishes or stops at a refusal.
            while (source.TryGetNext(sim.Board, out GameCommand command))
            {
                source.OnExecuted(command, sim.Commands.Execute(command));
            }

            Assert.That(source.HasFailed, Is.True);
            Assert.That(source.FailedIndex, Is.EqualTo(1));
            Assert.That(source.FailedOutcome, Is.EqualTo(CommandOutcome.StaleOrigin));
            Assert.That(source.Position, Is.EqualTo(1));
            Assert.That(source.IsFinished, Is.True);
            Assert.That(sim.Board.GetOrigin(2), Is.EqualTo(new Cell(3, 1)));
        }

        [Test]
        public void AReplaySourceCopiesItsList()
        {
            GameCommand[] commands = { GameCommand.Move(2, new Cell(3, 0), new Cell(3, 1)) };
            ReplayCommandSource source = new ReplayCommandSource(commands);
            commands[0] = default;

            Assert.That(source.TryGetNext(null, out GameCommand next), Is.True);
            Assert.That(next.Kind, Is.EqualTo(CommandKind.MoveBlock));
        }

        [Test]
        public void ABoosterLogReplaysToTheSameIdsFootprintsAndEvents()
        {
            List<string> playedOrder = new List<string>();
            RecordingListener played = new RecordingListener("event", playedOrder);
            BoardSimulation live = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(played).Build(), s_boosterLevel);

            // Rocket splits A into (0,2) and the I3 part 3, the pointer drags part 3 down a row, Rocket
            // splits it into (2,1) and part 4, Hammer removes B and a Vacuum on a clears A's colour.
            Execute(live, GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2)));
            Drag(live, 3, new BoardPoint(0f, 1f));
            Execute(live, GameCommand.RemoveCell(3, new Cell(0, 1), new Cell(3, 1)));
            Execute(live, GameCommand.RemoveBlock(1, new Cell(5, 0)));
            Execute(live, GameCommand.RemoveColor(2, new Cell(0, 0), BoardFixture.ColorOf('a')));
            GameCommand[] log = live.Commands.Log.ToArray();
            Assert.That(log.Length, Is.EqualTo(5), "every booster use and the move is one logged command");
            Assert.That(live.Board.BlockCount, Is.EqualTo(5));
            Assert.That(live.IsCleared, Is.True);

            List<string> replayedOrder = new List<string>();
            RecordingListener replayed = new RecordingListener("event", replayedOrder);
            BoardSimulation fresh = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(replayed).Build(), s_boosterLevel);
            for (int i = 0; i < log.Length; i++)
            {
                Execute(fresh, log[i]);
            }

            Assert.That(fresh.Board.BlockCount, Is.EqualTo(live.Board.BlockCount));
            for (int block = 0; block < live.Board.BlockCount; block++)
            {
                Assert.That(fresh.Board.GetStatus(block), Is.EqualTo(live.Board.GetStatus(block)), "status of " + block);
                Assert.That(fresh.Board.GetOrigin(block), Is.EqualTo(live.Board.GetOrigin(block)), "origin of " + block);
                Assert.That(FootprintReader.GetWorldCells(fresh.Board, block),
                    Is.EqualTo(FootprintReader.GetWorldCells(live.Board, block)), "cells of " + block);
            }

            Assert.That(replayedOrder, Is.EqualTo(playedOrder));
            Assert.That(replayed.Footprints, Is.EqualTo(played.Footprints));
            Assert.That(replayed.Removed, Is.EqualTo(played.Removed));
            Assert.That(replayed.Moves, Is.EqualTo(played.Moves));
            Assert.That(fresh.Commands.Log.ToArray(), Is.EqualTo(log));
        }

        private static void Execute(BoardSimulation sim, GameCommand command)
        {
            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(command), Is.EqualTo(CommandOutcome.Applied), command.ToString());
        }

        // Plays the level with the pointer, then executes its log on a fresh simulation with fresh
        // instances of the same rules, and compares the boards, the events in order and the logs.
        private static void AssertReplayMatches(Action<AttemptHooksBuilder> addRules)
        {
            List<string> playedOrder = new List<string>();
            RecordingListener played = new RecordingListener("event", playedOrder);
            BoardSimulation live = Create(addRules, played);

            // A pushes out through the Top, C moves up, B drops out through the Bottom, then C moves back.
            Drag(live, 0, new BoardPoint(0f, 3.45f));
            Drag(live, 2, new BoardPoint(3f, 2f));
            Drag(live, 1, new BoardPoint(1f, -0.45f));
            Drag(live, 2, new BoardPoint(3f, 1f));
            GameCommand[] log = live.Commands.Log.ToArray();
            Assert.That(log.Length, Is.EqualTo(4), "the pointer produced one command per move");

            List<string> replayedOrder = new List<string>();
            RecordingListener replayed = new RecordingListener("event", replayedOrder);
            BoardSimulation fresh = Create(addRules, replayed);
            for (int i = 0; i < log.Length; i++)
            {
                fresh.Events.Clear();
                Assert.That(fresh.Commands.Execute(log[i]), Is.EqualTo(CommandOutcome.Applied), log[i].ToString());
            }

            for (int block = 0; block < live.Board.BlockCount; block++)
            {
                Assert.That(fresh.Board.GetStatus(block), Is.EqualTo(live.Board.GetStatus(block)), "status of " + block);
                Assert.That(fresh.Board.GetOrigin(block), Is.EqualTo(live.Board.GetOrigin(block)), "origin of " + block);
            }

            Assert.That(replayedOrder, Is.EqualTo(playedOrder));
            Assert.That(replayed.Exited, Is.EqualTo(played.Exited));
            Assert.That(replayed.Moves, Is.EqualTo(played.Moves));
            Assert.That(fresh.Commands.Log.ToArray(), Is.EqualTo(log));
        }

        private static BoardSimulation Create(Action<AttemptHooksBuilder> addRules, RecordingListener listener)
        {
            AttemptHooksBuilder builder = new AttemptHooksBuilder();
            addRules(builder);
            return BoardFixture.CreateSimulation(builder.Add(listener).Build(), s_level, s_doors);
        }

        // One press, one pointer position and a release, running each command as the router does.
        private static void Drag(BoardSimulation sim, int block, BoardPoint to)
        {
            Assert.That(sim.Drag.TryBegin(block, BoardPoint.FromCell(sim.Board.GetOrigin(block))), Is.True);
            sim.Events.Clear();
            if (sim.Drag.Update(to, out GameCommand exit) == DragResult.ExitReady)
            {
                Assert.That(sim.Commands.Execute(exit), Is.EqualTo(CommandOutcome.Applied));
            }

            if (sim.Drag.TryEnd(out GameCommand move))
            {
                Assert.That(sim.Commands.Execute(move), Is.EqualTo(CommandOutcome.Applied));
            }
        }
    }
}
