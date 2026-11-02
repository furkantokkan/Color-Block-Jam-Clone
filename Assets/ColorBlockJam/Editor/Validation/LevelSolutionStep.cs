using System;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// One solver step as plain data: a block slides one cell from one origin to the next, or leaves
    /// through a board side from its origin (then From equals To). Origins use the level's own origin
    /// convention, so a step maps straight onto a MoveBlock command (DEC-122).
    /// </summary>
    public readonly struct LevelSolutionStep : IEquatable<LevelSolutionStep>
    {
        public LevelSolutionStep(int blockIndex, int fromX, int fromY, int toX, int toY, bool isExit, BoardSide exitSide)
        {
            BlockIndex = blockIndex;
            FromX = fromX;
            FromY = fromY;
            ToX = toX;
            ToY = toY;
            IsExit = isExit;
            ExitSide = isExit ? exitSide : BoardSide.Top;
        }

        /// <summary>The block's authored index in the level (LevelData.GetBlockAt).</summary>
        public int BlockIndex { get; }

        public int FromX { get; }

        public int FromY { get; }

        public int ToX { get; }

        public int ToY { get; }

        /// <summary>True when the block leaves the board through ExitSide in this step.</summary>
        public bool IsExit { get; }

        /// <summary>The side the block leaves through; meaningful only when IsExit is true.</summary>
        public BoardSide ExitSide { get; }

        public bool Equals(LevelSolutionStep other)
        {
            return BlockIndex == other.BlockIndex && FromX == other.FromX && FromY == other.FromY && ToX == other.ToX
                && ToY == other.ToY && IsExit == other.IsExit && ExitSide == other.ExitSide;
        }

        public override bool Equals(object obj) => obj is LevelSolutionStep other && Equals(other);

        public override int GetHashCode()
        {
            int hash = BlockIndex;
            hash = (hash * 397) ^ FromX;
            hash = (hash * 397) ^ FromY;
            hash = (hash * 397) ^ ToX;
            hash = (hash * 397) ^ ToY;
            return (hash * 397) ^ (IsExit ? 1 + (int)ExitSide : 0);
        }

        public override string ToString()
        {
            return IsExit
                ? "block " + BlockIndex + " exits " + ExitSide + " from (" + FromX + "," + FromY + ")"
                : "block " + BlockIndex + " (" + FromX + "," + FromY + ") -> (" + ToX + "," + ToY + ")";
        }
    }
}
