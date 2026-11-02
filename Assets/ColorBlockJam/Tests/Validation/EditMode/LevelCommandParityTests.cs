using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// AC-9: along seeded random walks through legal solver steps, the solver's legal one-cell slides and exits
    /// equal what the Domain's CommandExecutor accepts, for every block, direction and side. A refused command
    /// changes nothing; after an accepted probe the attempt is rebuilt by replaying the walk.
    /// </summary>
    public sealed class LevelCommandParityTests
    {
        private const int k_seed = 20260928;
        private const int k_walksPerLevel = 3;
        private const int k_stepsPerWalk = 20;

        [Test]
        public void SolverLegalMovesMatchTheCommandExecutorAlongRandomWalks()
        {
            List<LevelData> levels = new List<LevelData>(ValidationLevelFixture.LoadShippedLevels());
            levels.AddRange(ValidationFixtures.CreateSearchable());
            Random random = new Random(k_seed);
            int compared = 0;
            for (int i = 0; i < levels.Count; i++)
            {
                LevelData level = levels[i];
                Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.True, error);
                LevelSolverTables tables = LevelSolverTables.Create(model, LevelSolvabilityCheck.ReadIceCounts(level),
                    LevelSolvabilityCheck.ReadArrowAxes(level));
                for (int walk = 0; walk < k_walksPerLevel; walk++)
                {
                    List<GameCommand> history = new List<GameCommand>();
                    BoardSimulation simulation = ValidationLevelFixture.CreateSimulation(level);
                    for (int step = 0; step < k_stepsPerWalk && !simulation.IsCleared; step++)
                    {
                        List<GameCommand> legal = CompareLegalMoves(level, tables, ref simulation, history, ref compared);
                        if (legal.Count == 0)
                        {
                            break;
                        }

                        GameCommand next = legal[random.Next(legal.Count)];
                        Assert.That(ValidationLevelFixture.Execute(simulation, next), Is.EqualTo(CommandOutcome.Applied),
                            Describe(level, i, history, next));
                        history.Add(next);
                    }
                }
            }

            Assert.That(compared, Is.GreaterThan(1000), "the walks compared enough moves to mean something");
        }

        // Compares every block, direction and side at the simulation's current position and returns the legal
        // solver steps as commands. The simulation is rebuilt after every accepted probe.
        private static List<GameCommand> CompareLegalMoves(LevelData level, LevelSolverTables tables,
            ref BoardSimulation simulation, List<GameCommand> history, ref int compared)
        {
            BoardState board = simulation.Board;
            int blockCount = board.BlockCount;
            int[] slots = new int[blockCount];
            CellMask[] footprints = new CellMask[blockCount];
            CellMask occupancy = CellMask.Empty;
            int exitedCount = 0;
            for (int block = 0; block < blockCount; block++)
            {
                if (board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    exitedCount++;
                    slots[block] = -1;
                    continue;
                }

                slots[block] = tables.GetSlot(block, board.GetOrigin(block));
                footprints[block] = tables.GetFootprint(block, slots[block]);
                occupancy = occupancy.Or(footprints[block]);
            }

            List<GameCommand> legal = new List<GameCommand>();
            for (int block = 0; block < blockCount; block++)
            {
                Cell origin = board.GetOrigin(block);
                bool movable = slots[block] >= 0 && !LevelSolverMoves.IsFrozen(tables, block, exitedCount);
                CellMask others = occupancy.Without(footprints[block]);
                for (int direction = 0; direction < LevelSolverTables.DirectionCount; direction++)
                {
                    bool expected = movable && LevelSolverMoves.GetSlide(tables, block, slots[block], direction, others) >= 0;
                    GameCommand command = GameCommand.Move(block, origin, origin + LevelSolverTables.GetDirection(direction));
                    Probe(level, ref simulation, history, command, expected, legal);
                    compared++;
                }

                for (int side = 0; side < LevelSolverTables.SideCount; side++)
                {
                    bool expected = movable && LevelSolverMoves.CanExit(tables, block, slots[block], (Side)side, others);
                    GameCommand command = GameCommand.MoveAndExit(block, origin, origin, (Side)side);
                    Probe(level, ref simulation, history, command, expected, legal);
                    compared++;
                }
            }

            return legal;
        }

        private static void Probe(LevelData level, ref BoardSimulation simulation, List<GameCommand> history,
            GameCommand command, bool expected, List<GameCommand> legal)
        {
            CommandOutcome outcome = ValidationLevelFixture.Execute(simulation, command);
            bool accepted = outcome == CommandOutcome.Applied;
            Assert.That(accepted, Is.EqualTo(expected), Describe(level, -1, history, command) + " executor said " + outcome);
            if (expected)
            {
                legal.Add(command);
            }

            if (accepted)
            {
                simulation = Replay(level, history);
            }
        }

        private static BoardSimulation Replay(LevelData level, List<GameCommand> history)
        {
            BoardSimulation simulation = ValidationLevelFixture.CreateSimulation(level);
            for (int i = 0; i < history.Count; i++)
            {
                Assert.That(ValidationLevelFixture.Execute(simulation, history[i]), Is.EqualTo(CommandOutcome.Applied));
            }

            return simulation;
        }

        private static string Describe(LevelData level, int index, List<GameCommand> history, GameCommand command)
        {
            return level.Id + (index >= 0 ? " #" + index : "") + " after " + history.Count + " commands: " + command;
        }
    }
}
