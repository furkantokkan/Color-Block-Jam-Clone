using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Editor.Levels.Validation;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Validation
{
    /// <summary>
    /// Builds authored LevelData from ASCII maps for the solvability tests, loads the shipped levels and replays
    /// solutions through the Domain. Rows run top to bottom ('.' floor, '#' void, a letter is a block cell).
    /// Each distinct letter is one block in ordinal letter order; its colour is the letter's case-insensitive
    /// alphabet position in the gate palette (A Red, B Orange, C Yellow, ...), so 'A' and 'a' share a colour.
    /// </summary>
    public static class ValidationLevelFixture
    {
        // The Editor runs tests with the project root as the working directory. The shipped database is a folder with a
        // manifest and its packs (DEC-196); this assembly reads it with the Runtime readers only.
        public const string ShippedManifestPath = "Assets/ColorBlockJam/Data/Json/Levels/Main/manifest.json";

        private static readonly string[] s_gateColors =
        {
            "Red", "Orange", "Yellow", "LightGreen", "DarkGreen", "LightTurquoise", "DarkTurquoise", "LightBlue",
            "Blue", "Purple", "Pink"
        };

        public static string ColorOf(char letter)
        {
            return s_gateColors[char.ToUpperInvariant(letter) - 'A'];
        }

        public static LevelDoorData Door(BoardSide side, int start, int length, char colorLetter)
        {
            return new LevelDoorData("unset", side, start, length, ColorOf(colorLetter));
        }

        public static LevelEffectData Ice(int clearsToThaw)
        {
            return new LevelEffectData("ice", new[] { new LevelEffectParamData("clearsToThaw",
                clearsToThaw.ToString(System.Globalization.CultureInfo.InvariantCulture)) });
        }

        public static LevelEffectData Arrow(string axis)
        {
            return new LevelEffectData("arrow", new[] { new LevelEffectParamData("axis", axis) });
        }

        /// <summary>Doors get ids d1, d2, ... and blocks b1, b2, ... in order; effects map a block letter to its effects.</summary>
        public static LevelData Create(string[] rows, LevelDoorData[] doors,
            IDictionary<char, LevelEffectData[]> effects = null, LevelCellObjectData[] cellObjects = null)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            List<Vector2Int> voidCells = new List<Vector2Int>();
            SortedDictionary<char, List<Vector2Int>> cellsByLetter = new SortedDictionary<char, List<Vector2Int>>();
            for (int row = 0; row < height; row++)
            {
                if (rows[row].Length != width)
                {
                    throw new ArgumentException($"Row {row} has a different width.", nameof(rows));
                }

                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    char symbol = rows[row][x];
                    if (symbol == '#')
                    {
                        voidCells.Add(new Vector2Int(x, y));
                    }
                    else if (char.IsLetter(symbol))
                    {
                        if (!cellsByLetter.TryGetValue(symbol, out List<Vector2Int> cells))
                        {
                            cells = new List<Vector2Int>();
                            cellsByLetter.Add(symbol, cells);
                        }

                        cells.Add(new Vector2Int(x, y));
                    }
                    else if (symbol != '.')
                    {
                        throw new ArgumentException($"Unknown map symbol '{symbol}'.", nameof(rows));
                    }
                }
            }

            LevelBlockData[] blocks = new LevelBlockData[cellsByLetter.Count];
            int index = 0;
            foreach (KeyValuePair<char, List<Vector2Int>> pair in cellsByLetter)
            {
                blocks[index] = CreateBlock("b" + (index + 1), pair.Key, pair.Value,
                    effects != null && effects.TryGetValue(pair.Key, out LevelEffectData[] blockEffects)
                        ? blockEffects
                        : Array.Empty<LevelEffectData>());
                index++;
            }

            LevelDoorData[] namedDoors = new LevelDoorData[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                namedDoors[i] = new LevelDoorData("d" + (i + 1), doors[i].Side, doors[i].Start, doors[i].Length,
                    doors[i].ColorId);
            }

            return new LevelData("level-fixture", "Fixture", width, height, 120, voidCells.ToArray(), namedDoors, blocks,
                cellObjects ?? Array.Empty<LevelCellObjectData>());
        }

        public static LevelData[] LoadShippedLevels()
        {
            Assert.That(LevelManifestJson.TryRead(File.ReadAllText(ShippedManifestPath), out LevelManifest manifest,
                out string error), Is.True, error);
            string folder = Path.GetDirectoryName(ShippedManifestPath);
            List<LevelData> levels = new List<LevelData>(manifest.Count);
            for (int packIndex = 0; packIndex < manifest.PackCount; packIndex++)
            {
                LevelPackInfo pack = manifest.GetPackAt(packIndex);
                string json = File.ReadAllText(Path.Combine(folder, pack.File));
                Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] packLevels, out error), Is.True,
                    $"{pack.File}: {error}");
                Assert.That(manifest.TryMatchPack(packIndex, packLevels, out error), Is.True, error);
                levels.AddRange(packLevels);
            }

            return levels.ToArray();
        }

        /// <summary>
        /// The attempt's Domain side with the level's Ice and Arrow hooks, registered the way Gameplay's Ice and
        /// Arrow modules do, in the installer's order (Ice first).
        /// </summary>
        public static BoardSimulation CreateSimulation(LevelData level)
        {
            Assert.That(LevelSolverModelBuilder.TryBuild(level, out LevelModel model, out string error), Is.True, error);
            int[] counts = LevelSolvabilityCheck.ReadIceCounts(level);
            AttemptHooksBuilder hooks = new AttemptHooksBuilder();
            bool active = false;
            for (int block = 0; block < counts.Length; block++)
            {
                active |= counts[block] > 0;
            }

            if (active)
            {
                IceRules rules = new IceRules(counts);
                hooks.Add((IDragGate)rules);
                hooks.Add((IExitGate)rules);
                hooks.Add((IBoardEventListener)rules);
            }

            AxisMask[] axes = LevelSolvabilityCheck.ReadArrowAxes(level);
            bool hasArrow = false;
            for (int block = 0; block < axes.Length; block++)
            {
                hasArrow |= axes[block] != AxisMask.Both;
            }

            if (hasArrow)
            {
                hooks.Add(new ArrowAxisConstraint(axes));
                hooks.Add(new ArrowExitGate(axes));
            }

            return new BoardSimulation(model, hooks.Build(), DragSettings.Default);
        }

        /// <summary>Turns steps into DEC-122 player moves: each run of one block's steps is one MoveBlock command.</summary>
        public static List<GameCommand> ToMoveCommands(IReadOnlyList<LevelSolutionStep> steps)
        {
            List<GameCommand> commands = new List<GameCommand>();
            int start = 0;
            while (start < steps.Count)
            {
                int end = start;
                while (end + 1 < steps.Count && steps[end + 1].BlockIndex == steps[start].BlockIndex)
                {
                    end++;
                }

                LevelSolutionStep first = steps[start];
                LevelSolutionStep last = steps[end];
                Cell from = new Cell(first.FromX, first.FromY);
                Cell to = new Cell(last.ToX, last.ToY);
                commands.Add(last.IsExit
                    ? GameCommand.MoveAndExit(first.BlockIndex, from, to, (Side)(int)last.ExitSide)
                    : GameCommand.Move(first.BlockIndex, from, to));
                start = end + 1;
            }

            return commands;
        }

        /// <summary>One command per step: a one-cell move, or an exit from where the block stands.</summary>
        public static GameCommand ToStepCommand(LevelSolutionStep step)
        {
            Cell from = new Cell(step.FromX, step.FromY);
            return step.IsExit
                ? GameCommand.MoveAndExit(step.BlockIndex, from, from, (Side)(int)step.ExitSide)
                : GameCommand.Move(step.BlockIndex, from, new Cell(step.ToX, step.ToY));
        }

        /// <summary>Runs one command the way a headless caller must: the tick's event log is cleared afterwards.</summary>
        public static CommandOutcome Execute(BoardSimulation simulation, GameCommand command)
        {
            CommandOutcome outcome = simulation.Commands.Execute(command);
            simulation.Events.Clear();
            return outcome;
        }

        /// <summary>
        /// The steps replay with the level's hooks as DEC-122 player moves (one command per move, moveCount of them):
        /// every command is Applied, the command log holds exactly moveCount entries, and the board clears.
        /// </summary>
        public static void AssertReplaysAsMoves(LevelData level, IReadOnlyList<LevelSolutionStep> steps, int moveCount,
            string label)
        {
            BoardSimulation moves = CreateSimulation(level);
            List<GameCommand> commands = ToMoveCommands(steps);
            Assert.That(commands.Count, Is.EqualTo(moveCount), label);
            for (int c = 0; c < commands.Count; c++)
            {
                Assert.That(Execute(moves, commands[c]), Is.EqualTo(CommandOutcome.Applied), label + " " + commands[c]);
            }

            Assert.That(moves.IsCleared, Is.True, label);
            Assert.That(moves.Commands.Log.Count, Is.EqualTo(moveCount), label);
        }

        /// <summary>The steps replay with the level's hooks as one-cell commands; every command is Applied and the board clears.</summary>
        public static void AssertReplaysAsSteps(LevelData level, IReadOnlyList<LevelSolutionStep> steps, string label)
        {
            BoardSimulation cells = CreateSimulation(level);
            for (int s = 0; s < steps.Count; s++)
            {
                GameCommand command = ToStepCommand(steps[s]);
                Assert.That(Execute(cells, command), Is.EqualTo(CommandOutcome.Applied), label + " " + command);
            }

            Assert.That(cells.IsCleared, Is.True, label);
        }

        /// <summary>
        /// A listed result keeps the verdict (status, sentence, steps, moves) and lists 1 to 20 entries: fewest player
        /// moves first, equal moves fewest one-cell steps first (DEC-181), the verdict's own solution among them, every
        /// exit order names each block once
        /// and differs from every other entry's, every entry replays and clears the board, and the texts read the count.
        /// </summary>
        private static bool SameSteps(IReadOnlyList<LevelSolutionStep> a, IReadOnlyList<LevelSolutionStep> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static void AssertListedSolutions(LevelData level, LevelCheckResult verdict, LevelCheckResult listed,
            string label)
        {
            Assert.That(verdict.Status, Is.EqualTo(LevelCheckStatus.Solvable), label + ": " + verdict.Text);
            Assert.That(listed.Status, Is.EqualTo(verdict.Status), label);
            Assert.That(listed.Text, Is.EqualTo(verdict.Text), label);
            Assert.That(listed.Steps, Is.EqualTo(verdict.Steps), label);
            Assert.That(listed.MoveCount, Is.EqualTo(verdict.MoveCount), label);
            Assert.That(listed.SolutionListState, Is.Not.EqualTo(LevelSolutionListState.NotListed), label);
            Assert.That(listed.Solutions.Count, Is.InRange(1, LevelCheckOptions.MaxListedSolutions), label);
            // DEC-181: entries run from the fewest player moves; the check's own solution is still listed.
            Assert.That(listed.Solutions[0].MoveCount, Is.LessThanOrEqualTo(verdict.MoveCount), label);
            bool listsCheckSolution = false;
            for (int i = 0; i < listed.Solutions.Count; i++)
            {
                listsCheckSolution |= SameSteps(listed.Solutions[i].Steps, verdict.Steps);
            }

            Assert.That(listsCheckSolution, Is.True, label + ": the check's solution is listed");

            HashSet<string> orders = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < listed.Solutions.Count; i++)
            {
                LevelSolution entry = listed.Solutions[i];
                string entryLabel = label + " #" + (i + 1);
                AssertOrderOfEveryBlock(entry, level.BlockCount, entryLabel);
                Assert.That(orders.Add(string.Join(",", entry.ExitOrder)), Is.True, entryLabel + ": exit order listed twice");
                if (i >= 1)
                {
                    LevelSolution previous = listed.Solutions[i - 1];
                    Assert.That(entry.MoveCount, Is.GreaterThanOrEqualTo(previous.MoveCount),
                        entryLabel + ": fewest player moves first");
                    if (entry.MoveCount == previous.MoveCount && i >= 2)
                    {
                        Assert.That(entry.Steps.Count, Is.GreaterThanOrEqualTo(previous.Steps.Count),
                            entryLabel + ": equal moves keep fewest one-cell steps first");
                    }
                }

                AssertReplaysAsMoves(level, entry.Steps, entry.MoveCount, entryLabel);
                AssertReplaysAsSteps(level, entry.Steps, entryLabel);
            }

            string count = LevelDesignerText.FormatSolutionCount(listed.Solutions.Count, listed.SolutionListState);
            Assert.That(listed.SolutionCountText, Is.EqualTo(count), label);
            Assert.That(listed.SummaryText,
                Is.EqualTo(verdict.Text.Substring(0, verdict.Text.Length - 1) + " · " + count), label);
        }

        /// <summary>Two listed results hold the same entries (steps and exit orders) in the same order and state.</summary>
        public static void AssertSameList(LevelCheckResult expected, LevelCheckResult actual, string label)
        {
            Assert.That(actual.SolutionListState, Is.EqualTo(expected.SolutionListState), label);
            Assert.That(actual.Solutions.Count, Is.EqualTo(expected.Solutions.Count), label);
            for (int i = 0; i < expected.Solutions.Count; i++)
            {
                Assert.That(actual.Solutions[i].ExitOrder, Is.EqualTo(expected.Solutions[i].ExitOrder), label + " #" + (i + 1));
                Assert.That(actual.Solutions[i].Steps, Is.EqualTo(expected.Solutions[i].Steps), label + " #" + (i + 1));
                Assert.That(actual.Solutions[i].MoveCount, Is.EqualTo(expected.Solutions[i].MoveCount), label + " #" + (i + 1));
            }
        }

        // The exit order names every block exactly once, in the order of the entry's exit steps.
        private static void AssertOrderOfEveryBlock(LevelSolution entry, int blockCount, string label)
        {
            Assert.That(entry.ExitOrder.Count, Is.EqualTo(blockCount), label);
            bool[] seen = new bool[blockCount];
            int exit = 0;
            for (int s = 0; s < entry.Steps.Count; s++)
            {
                if (!entry.Steps[s].IsExit)
                {
                    continue;
                }

                int block = entry.Steps[s].BlockIndex;
                Assert.That(entry.ExitOrder[exit++], Is.EqualTo(block), label);
                Assert.That(seen[block], Is.False, label + ": block " + block + " leaves twice");
                seen[block] = true;
            }

            Assert.That(exit, Is.EqualTo(blockCount), label);
        }

        private static LevelBlockData CreateBlock(string id, char letter, List<Vector2Int> boardCells,
            LevelEffectData[] effects)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < boardCells.Count; i++)
            {
                minX = Math.Min(minX, boardCells[i].x);
                minY = Math.Min(minY, boardCells[i].y);
            }

            Vector2Int origin = new Vector2Int(minX, minY);
            Vector2Int[] cells = new Vector2Int[boardCells.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = boardCells[i] - origin;
            }

            return new LevelBlockData(id, ColorOf(letter), origin, cells, effects);
        }

        /// <summary>Counts reports, records the last position count, and asks to cancel from a given report on (0 never cancels).</summary>
        public sealed class ScriptedProgress : ILevelCheckProgress
        {
            private readonly int m_cancelFromReport;

            public ScriptedProgress(int cancelFromReport)
            {
                m_cancelFromReport = cancelFromReport;
            }

            public int ReportCount { get; private set; }

            public int LastPositions { get; private set; }

            public bool IsCancelRequested => m_cancelFromReport > 0 && ReportCount >= m_cancelFromReport;

            public void Report(int positions, double elapsedSeconds)
            {
                ReportCount++;
                LastPositions = positions;
            }
        }
    }
}
