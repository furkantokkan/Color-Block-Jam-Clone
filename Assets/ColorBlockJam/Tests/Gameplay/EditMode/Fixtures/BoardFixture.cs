using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Domain.Movement;
using ColorBlockJam.Domain.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// Builds domain levels from ASCII maps so every rule test reads like a picture. Rows run top to
    /// bottom ('.' floor, '#' void, a letter is a block cell). Each distinct letter is one block;
    /// blocks are indexed in ordinal letter order ('A' before 'B' before 'a'), a block's origin is
    /// the bottom-left of its bounding box, and its colour is the letter's case-insensitive alphabet
    /// position, so 'A' and 'a' are two blocks of the same colour.
    /// </summary>
    public static class BoardFixture
    {
        public const int k_timerSeconds = 60;

        public static DoorSpec Door(Side side, int start, int length, char color)
        {
            return new DoorSpec(side, start, length, ColorOf(color));
        }

        public static BlockColor ColorOf(char letter)
        {
            return new BlockColor(char.ToUpperInvariant(letter) - 'A');
        }

        public static LevelModel Create(string[] rows, params DoorSpec[] doors)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            bool[] floor = new bool[width * height];
            SortedDictionary<char, List<Cell>> cellsByLetter = new SortedDictionary<char, List<Cell>>();
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
                    floor[y * width + x] = symbol != '#';
                    if (char.IsLetter(symbol))
                    {
                        if (!cellsByLetter.TryGetValue(symbol, out List<Cell> cells))
                        {
                            cells = new List<Cell>();
                            cellsByLetter.Add(symbol, cells);
                        }

                        cells.Add(new Cell(x, y));
                    }
                    else if (symbol != '.' && symbol != '#')
                    {
                        throw new ArgumentException($"Unknown map symbol '{symbol}'.", nameof(rows));
                    }
                }
            }

            BlockDefinition[] blocks = new BlockDefinition[cellsByLetter.Count];
            int index = 0;
            foreach (KeyValuePair<char, List<Cell>> pair in cellsByLetter)
            {
                blocks[index] = CreateBlock(index, ColorOf(pair.Key), pair.Value);
                index++;
            }

            DoorDefinition[] doorDefinitions = new DoorDefinition[doors.Length];
            for (int i = 0; i < doors.Length; i++)
            {
                DoorSpec door = doors[i];
                doorDefinitions[i] = new DoorDefinition(i, door.Side, door.Start, door.Length, door.Color);
            }

            if (!LevelModel.TryCreate(width, height, floor, doorDefinitions, blocks, k_timerSeconds,
                    out LevelModel model, out string error))
            {
                throw new InvalidOperationException(error);
            }

            return model;
        }

        public static BoardSimulation CreateSimulation(string[] rows, params DoorSpec[] doors)
        {
            return new BoardSimulation(Create(rows, doors), AttemptHooks.CreateEmpty(), DragSettings.Default);
        }

        public static BoardSimulation CreateSimulation(AttemptHooks hooks, string[] rows, params DoorSpec[] doors)
        {
            return new BoardSimulation(Create(rows, doors), hooks, DragSettings.Default);
        }

        public static BoardSimulation CreateSimulation(DragSettings settings, string[] rows, params DoorSpec[] doors)
        {
            return new BoardSimulation(Create(rows, doors), AttemptHooks.CreateEmpty(), settings);
        }

        private static BlockDefinition CreateBlock(int index, BlockColor color, List<Cell> worldCells)
        {
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            for (int i = 0; i < worldCells.Count; i++)
            {
                minX = Math.Min(minX, worldCells[i].X);
                minY = Math.Min(minY, worldCells[i].Y);
            }

            Cell origin = new Cell(minX, minY);
            Cell[] cells = new Cell[worldCells.Count];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = worldCells[i] - origin;
            }

            return new BlockDefinition(index, color, origin, cells);
        }
    }

    /// <summary>One door of a fixture map, indexed by its position in the door list.</summary>
    public readonly struct DoorSpec
    {
        public DoorSpec(Side side, int start, int length, BlockColor color)
        {
            Side = side;
            Start = start;
            Length = length;
            Color = color;
        }

        public Side Side { get; }

        public int Start { get; }

        public int Length { get; }

        public BlockColor Color { get; }
    }
}
