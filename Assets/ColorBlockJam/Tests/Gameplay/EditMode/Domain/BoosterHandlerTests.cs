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
    /// <summary>
    /// Drives the Rocket, Hammer and Vacuum commands through the attempt's executor (B-6): valid uses
    /// change the board once and raise their events, refusals change nothing and raise nothing.
    /// </summary>
    public sealed class BoosterHandlerTests
    {
        // A (0) and D (3) have two cells, B (1) and C (2) one; A fits the Top door over lanes 0 and 1.
        private static readonly string[] s_board =
        {
            "AA.C",
            "...B",
            "DD.."
        };

        private static readonly DoorSpec s_topDoor = BoardFixture.Door(Side.Top, 0, 2, 'A');

        private static readonly Cell s_aOrigin = new Cell(0, 2);
        private static readonly Cell s_dOrigin = new Cell(0, 0);

        // 'A' and 'a' share a colour: A (0) is an I3 above, B (1) and a (2) sit on the bottom row.
        private static readonly string[] s_colours =
        {
            "AAA.",
            "....",
            "B..a"
        };

        [Test]
        public void RocketRefusesEveryInvalidTargetAndChangesNothing()
        {
            BoardSimulation sim = Create(out RecordingListener listener);

            AssertRefused(sim, listener, GameCommand.RemoveCell(1, new Cell(3, 1), new Cell(3, 1)), CommandOutcome.FinalCell);
            AssertRefused(sim, listener, GameCommand.RemoveCell(0, s_aOrigin, new Cell(3, 1)), CommandOutcome.NotBlockCell);
            AssertRefused(sim, listener, GameCommand.RemoveCell(0, s_aOrigin, new Cell(2, 2)), CommandOutcome.NotBlockCell);
            AssertRefused(sim, listener, GameCommand.RemoveCell(0, s_aOrigin, new Cell(-1, 2)), CommandOutcome.NotBlockCell);
            AssertRefused(sim, listener, GameCommand.RemoveCell(0, new Cell(1, 2), new Cell(0, 2)), CommandOutcome.StaleOrigin);
            AssertRefused(sim, listener, GameCommand.RemoveCell(4, s_aOrigin, new Cell(0, 2)), CommandOutcome.UnknownBlock);
            AssertRefused(sim, listener, GameCommand.RemoveCell(-1, s_aOrigin, new Cell(0, 2)), CommandOutcome.UnknownBlock);
            AssertRefused(sim, listener, new GameCommand(CommandKind.RemoveCell, 0, s_aOrigin, new Cell(0, 2), 1),
                CommandOutcome.Malformed);
        }

        [Test]
        public void HammerAndVacuumRefuseInvalidTargetsAndChangeNothing()
        {
            BoardSimulation sim = Create(out RecordingListener listener);

            AssertRefused(sim, listener, GameCommand.RemoveBlock(0, new Cell(1, 2)), CommandOutcome.StaleOrigin);
            AssertRefused(sim, listener, GameCommand.RemoveBlock(4, s_aOrigin), CommandOutcome.UnknownBlock);
            AssertRefused(sim, listener, new GameCommand(CommandKind.RemoveBlock, 0, s_aOrigin, default, 1),
                CommandOutcome.Malformed);
            AssertRefused(sim, listener, GameCommand.RemoveColor(0, s_aOrigin, BoardFixture.ColorOf('B')),
                CommandOutcome.ColorMismatch);
            AssertRefused(sim, listener, GameCommand.RemoveColor(0, new Cell(1, 2), BoardFixture.ColorOf('A')),
                CommandOutcome.StaleOrigin);
            AssertRefused(sim, listener, GameCommand.RemoveColor(-1, s_aOrigin, BoardFixture.ColorOf('A')),
                CommandOutcome.UnknownBlock);
        }

        [Test]
        public void ExitedAndRemovedBlocksAreNoTarget()
        {
            BoardSimulation sim = Create(out RecordingListener listener);
            Assert.That(sim.Commands.Execute(GameCommand.MoveAndExit(0, s_aOrigin, s_aOrigin, Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(3, s_dOrigin)), Is.EqualTo(CommandOutcome.Applied));
            sim.Events.Clear();

            AssertRefused(sim, listener, GameCommand.RemoveCell(0, s_aOrigin, new Cell(0, 2)), CommandOutcome.NotOnBoard);
            AssertRefused(sim, listener, GameCommand.RemoveCell(3, s_dOrigin, new Cell(0, 0)), CommandOutcome.NotOnBoard);
            AssertRefused(sim, listener, GameCommand.RemoveBlock(0, s_aOrigin), CommandOutcome.NotOnBoard);
            AssertRefused(sim, listener, GameCommand.RemoveBlock(3, s_dOrigin), CommandOutcome.NotOnBoard);
            AssertRefused(sim, listener, GameCommand.RemoveColor(3, s_dOrigin, BoardFixture.ColorOf('D')),
                CommandOutcome.NotOnBoard);
        }

        [Test]
        public void HammerRemovesAWholeMultiCellBlockWithOneBlockRemoved()
        {
            BoardSimulation sim = Create(out RecordingListener listener);
            GameCommand hammer = GameCommand.RemoveBlock(3, s_dOrigin);

            Assert.That(sim.Commands.Execute(hammer), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetStatus(3), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetOccupant(new Cell(0, 0)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.GetOccupant(new Cell(1, 0)), Is.EqualTo(Occupant.k_empty));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(3));
            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.OnBoard), "other blocks are unchanged");
            Assert.That(listener.Removed, Is.EqualTo(new[] { new BlockRemovedEvent(3, CommandKind.RemoveBlock, 3) }));
            Assert.That(listener.Exited, Is.Empty, "a removal is not an exit");
            Assert.That(listener.Total, Is.EqualTo(1));
            Assert.That(sim.Events.Count, Is.EqualTo(1));
            Assert.That(sim.Events.GetAt(0).Kind, Is.EqualTo(BoardEventKind.Removed));
            Assert.That(sim.Events.GetAt(0).Block, Is.EqualTo(3));
            Assert.That(sim.Events.GetAt(0).Parent, Is.EqualTo(3));
            Assert.That(sim.Events.GetAt(0).Cause, Is.EqualTo(CommandKind.RemoveBlock));
            Assert.That(sim.Commands.Log.ToArray(), Is.EqualTo(new[] { hammer }));
        }

        [Test]
        public void HammerRemovesAFrozenBlockWholeAndThawsTheOthersOnce()
        {
            IceRules ice = new IceRules(new[] { 2, 0, 1, 0 });
            RecordingListener listener = new RecordingListener();
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Add(listener).Build();
            BoardSimulation sim = BoardFixture.CreateSimulation(hooks, s_board, s_topDoor);
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, s_aOrigin, new Cell(1, 2))), Is.EqualTo(CommandOutcome.DragRefused),
                "the block is frozen");

            Assert.That(sim.Commands.Execute(GameCommand.RemoveBlock(0, s_aOrigin)), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Removed));
            Assert.That(listener.Removed.Count, Is.EqualTo(1));
            Assert.That(listener.Exited, Is.Empty);
            Assert.That(ice.GetRemaining(2), Is.Zero, "a removal thaws the other frozen blocks like an exit");
            Assert.That(ice.Revision, Is.EqualTo(1));
        }

        [Test]
        public void VacuumRemovesFrozenAndAxisGatedBlocksOfTheColourAndThawsOncePerBlock()
        {
            // A (0) is frozen, B (1) of another colour is frozen, and the vertical-only axis gate binds a (2).
            LevelModel model = BoardFixture.Create(s_colours);
            IceRules ice = new IceRules(new[] { 2, 1, 0 }, model.TotalCellCount);
            RecordingListener listener = new RecordingListener();
            AttemptHooks hooks = new AttemptHooksBuilder().Add((IDragGate)ice).Add((IExitGate)ice)
                .Add((IBoardEventListener)ice).Add(new AxisGate(AxisMask.Vertical)).Add(listener).Build();
            BoardSimulation sim = new BoardSimulation(model, hooks, DragSettings.Default);
            Assert.That(sim.Commands.Execute(GameCommand.Move(0, new Cell(0, 2), new Cell(0, 1))),
                Is.EqualTo(CommandOutcome.DragRefused), "A is frozen");
            Assert.That(sim.Commands.Execute(GameCommand.Move(2, new Cell(3, 0), new Cell(2, 0))),
                Is.EqualTo(CommandOutcome.AxisRefused), "a moves only vertically");

            Assert.That(sim.Commands.Execute(GameCommand.RemoveColor(0, new Cell(0, 2), BoardFixture.ColorOf('A'))),
                Is.EqualTo(CommandOutcome.Applied), "a frozen block is a valid Vacuum target");

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(2), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard), "another colour stays");
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(listener.Removed, Is.EqualTo(new[]
            {
                new BlockRemovedEvent(0, CommandKind.RemoveColor, 0),
                new BlockRemovedEvent(2, CommandKind.RemoveColor, 0)
            }));
            Assert.That(listener.Exited, Is.Empty);
            Assert.That(sim.Events.Count, Is.EqualTo(2));
            Assert.That(ice.GetRemaining(1), Is.Zero, "the two removed blocks thaw B, never below zero");
            Assert.That(ice.Revision, Is.EqualTo(2), "each removal changes a count");
        }

        [Test]
        public void VacuumRemovesEveryBlockOfTheColourInAscendingId()
        {
            RecordingListener listener = new RecordingListener();
            BoardSimulation sim = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_colours);

            // Splitting the I3 first gives the colour a third block: A keeps (0,2) and (2,2) becomes block 3.
            Assert.That(sim.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2))),
                Is.EqualTo(CommandOutcome.Applied));
            sim.Events.Clear();
            GameCommand vacuum = GameCommand.RemoveColor(2, new Cell(3, 0), BoardFixture.ColorOf('a'));

            Assert.That(sim.Commands.Execute(vacuum), Is.EqualTo(CommandOutcome.Applied));

            Assert.That(sim.Board.GetStatus(0), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(2), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(3), Is.EqualTo(BlockStatus.Removed));
            Assert.That(sim.Board.GetStatus(1), Is.EqualTo(BlockStatus.OnBoard), "another colour stays");
            Assert.That(sim.Board.GetOccupant(new Cell(0, 0)), Is.EqualTo(1));
            Assert.That(sim.Board.RequiredRemaining, Is.EqualTo(1));
            Assert.That(listener.Removed, Is.EqualTo(new[]
            {
                new BlockRemovedEvent(0, CommandKind.RemoveColor, 2),
                new BlockRemovedEvent(2, CommandKind.RemoveColor, 2),
                new BlockRemovedEvent(3, CommandKind.RemoveColor, 2)
            }));
            Assert.That(listener.Exited, Is.Empty);
            Assert.That(sim.Events.Count, Is.EqualTo(3));
            Assert.That(sim.Events.GetAt(0).Block, Is.EqualTo(0));
            Assert.That(sim.Events.GetAt(1).Block, Is.EqualTo(2));
            Assert.That(sim.Events.GetAt(2).Block, Is.EqualTo(3));
            Assert.That(sim.Events.GetAt(2).Parent, Is.EqualTo(2), "each removal names the tapped block");
            Assert.That(sim.Events.GetAt(2).Cause, Is.EqualTo(CommandKind.RemoveColor));

            sim.Events.Clear();
            Assert.That(sim.Commands.Execute(GameCommand.RemoveColor(1, new Cell(0, 0), BoardFixture.ColorOf('B'))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(sim.IsCleared, Is.True, "clearing the last colour clears the board");
        }

        [Test]
        public void AFullTickLogFailsBeforeABoosterChangesTheBoard()
        {
            // The I3 split needs two events, the Vacuum three and the Hammer one; each finds one slot too few.
            BoardSimulation rocket = BoardFixture.CreateSimulation(s_colours);
            AssertThrowsBeforeChanging(rocket, rocket.Events.Capacity - 1,
                GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2)));

            BoardSimulation vacuum = BoardFixture.CreateSimulation(s_colours);
            vacuum.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2)));
            vacuum.Events.Clear();
            AssertThrowsBeforeChanging(vacuum, vacuum.Events.Capacity - 2,
                GameCommand.RemoveColor(2, new Cell(3, 0), BoardFixture.ColorOf('a')));

            BoardSimulation hammer = BoardFixture.CreateSimulation(s_colours);
            AssertThrowsBeforeChanging(hammer, hammer.Events.Capacity, GameCommand.RemoveBlock(1, new Cell(0, 0)));
        }

        [Test]
        public void ABarrierStyleListenerFlipsOncePerClearedBlockAndNeverForASplitOrAMove()
        {
            // The barrier rule (OBJ-05) has no gameplay listener yet; this one flips as it would, once per whole-block
            // clear (an exit or a removal), and ignores moves and footprint changes (AC-17, B-5).
            ClearCountingListener barrier = new ClearCountingListener();
            BoardSimulation boosters = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(barrier).Build(), s_colours);

            Assert.That(boosters.Commands.Execute(GameCommand.RemoveCell(0, new Cell(0, 2), new Cell(1, 2))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(barrier.Flips, Is.Zero, "a Rocket split clears no block");
            Assert.That(barrier.FootprintChanges, Is.EqualTo(2), "the parent and its new part changed footprint");

            boosters.Events.Clear();
            Assert.That(boosters.Commands.Execute(GameCommand.RemoveColor(2, new Cell(3, 0), BoardFixture.ColorOf('a'))),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(barrier.Flips, Is.EqualTo(3), "a Vacuum flips once per removed block: A, its part and a");

            boosters.Events.Clear();
            Assert.That(boosters.Commands.Execute(GameCommand.RemoveBlock(1, new Cell(0, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(barrier.Flips, Is.EqualTo(4), "a Hammer flips once");

            ClearCountingListener exits = new ClearCountingListener();
            BoardSimulation moves = BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(exits).Build(), s_board, s_topDoor);
            Assert.That(moves.Commands.Execute(GameCommand.Move(3, s_dOrigin, new Cell(1, 0))), Is.EqualTo(CommandOutcome.Applied));
            Assert.That(exits.Flips, Is.Zero, "a committed move clears no block");
            moves.Events.Clear();
            Assert.That(moves.Commands.Execute(GameCommand.MoveAndExit(0, s_aOrigin, s_aOrigin, Side.Top)),
                Is.EqualTo(CommandOutcome.Applied));
            Assert.That(exits.Flips, Is.EqualTo(1), "an exit flips once");
        }

        private static BoardSimulation Create(out RecordingListener listener)
        {
            listener = new RecordingListener();
            return BoardFixture.CreateSimulation(new AttemptHooksBuilder().Add(listener).Build(), s_board, s_topDoor);
        }

        // Fills the tick log to fill entries, then checks the command throws with the board, the log and the IDs unchanged.
        private static void AssertThrowsBeforeChanging(BoardSimulation sim, int fill, GameCommand command)
        {
            for (int i = sim.Events.Count; i < fill; i++)
            {
                sim.Events.Add(BoardEvent.MoveCommitted(new MoveCommittedEvent(0, default, default)));
            }

            BoardSnapshot before = BoardSnapshot.Take(sim);

            Assert.Throws<InvalidOperationException>(() => sim.Commands.Execute(command), command.ToString());

            before.AssertUnchanged(sim);
            Assert.That(sim.Events.Count, Is.EqualTo(fill));
        }

        // A refusal changes neither the board, the IDs, the tick's events, the listeners nor the log.
        private static void AssertRefused(BoardSimulation sim, RecordingListener listener, GameCommand command,
            CommandOutcome expected)
        {
            BoardSnapshot before = BoardSnapshot.Take(sim);
            int events = sim.Events.Count;
            int heard = listener.Total;

            Assert.That(sim.Commands.Execute(command), Is.EqualTo(expected), command.ToString());

            before.AssertUnchanged(sim);
            Assert.That(sim.Events.Count, Is.EqualTo(events));
            Assert.That(listener.Total, Is.EqualTo(heard));
        }

        /// <summary>Counts whole-block clears as a barrier would flip on them, and footprint changes apart.</summary>
        private sealed class ClearCountingListener : IBoardEventListener
        {
            public int Flips { get; private set; }

            public int FootprintChanges { get; private set; }

            public void OnBlockExited(in BlockExitedEvent e) => Flips++;

            public void OnBlockRemoved(in BlockRemovedEvent e) => Flips++;

            public void OnMoveCommitted(in MoveCommittedEvent e)
            {
            }

            public void OnFootprintChanged(in FootprintChangedEvent e) => FootprintChanges++;
        }

        /// <summary>Every block's origin, status and cells, the occupancy, the ID count and the log length.</summary>
        private sealed class BoardSnapshot
        {
            private Cell[] m_origins;
            private BlockStatus[] m_statuses;
            private Cell[][] m_cells;
            private int[] m_occupants;
            private int m_blockCount;
            private int m_required;
            private int m_logged;

            public static BoardSnapshot Take(BoardSimulation sim)
            {
                IBoardReader board = sim.Board;
                BoardSnapshot snapshot = new BoardSnapshot
                {
                    m_blockCount = board.BlockCount,
                    m_required = board.RequiredRemaining,
                    m_logged = sim.Commands.Log.Count,
                    m_origins = new Cell[board.BlockCount],
                    m_statuses = new BlockStatus[board.BlockCount],
                    m_cells = new Cell[board.BlockCount][],
                    m_occupants = new int[board.Width * board.Height]
                };

                for (int block = 0; block < board.BlockCount; block++)
                {
                    snapshot.m_origins[block] = board.GetOrigin(block);
                    snapshot.m_statuses[block] = board.GetStatus(block);
                    snapshot.m_cells[block] = FootprintReader.GetWorldCells(board, block);
                }

                for (int i = 0; i < snapshot.m_occupants.Length; i++)
                {
                    snapshot.m_occupants[i] = board.GetOccupant(new Cell(i % board.Width, i / board.Width));
                }

                return snapshot;
            }

            public void AssertUnchanged(BoardSimulation sim)
            {
                IBoardReader board = sim.Board;
                Assert.That(board.BlockCount, Is.EqualTo(m_blockCount), "block count");
                Assert.That(board.RequiredRemaining, Is.EqualTo(m_required), "required blocks");
                Assert.That(sim.Commands.Log.Count, Is.EqualTo(m_logged), "logged commands");
                for (int block = 0; block < m_blockCount; block++)
                {
                    Assert.That(board.GetOrigin(block), Is.EqualTo(m_origins[block]), "origin of " + block);
                    Assert.That(board.GetStatus(block), Is.EqualTo(m_statuses[block]), "status of " + block);
                    Assert.That(FootprintReader.GetWorldCells(board, block), Is.EqualTo(m_cells[block]), "cells of " + block);
                }

                for (int i = 0; i < m_occupants.Length; i++)
                {
                    Assert.That(board.GetOccupant(new Cell(i % board.Width, i / board.Width)), Is.EqualTo(m_occupants[i]),
                        "occupant " + i);
                }
            }
        }
    }
}
