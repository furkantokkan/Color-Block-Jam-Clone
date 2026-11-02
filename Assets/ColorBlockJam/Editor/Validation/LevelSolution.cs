using System;
using System.Collections.Generic;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// One listed solution as plain data: its one-cell steps and exits in order, its player move count (DEC-122)
    /// and the authored block indices in the order the blocks leave the board.
    /// </summary>
    public sealed class LevelSolution
    {
        private readonly LevelSolutionStep[] m_steps;
        private readonly int[] m_exitOrder;

        internal LevelSolution(LevelSolutionStep[] steps)
        {
            m_steps = steps ?? throw new ArgumentNullException(nameof(steps));
            m_exitOrder = GetExitOrder(steps);
            MoveCount = LevelCheckResult.CountMoves(steps);
        }

        /// <summary>The one-cell steps and exits in order; they clear the board.</summary>
        public IReadOnlyList<LevelSolutionStep> Steps => m_steps;

        /// <summary>Player moves: consecutive steps of one block merge into one move (DEC-122).</summary>
        public int MoveCount { get; }

        /// <summary>Authored block indices (LevelData.GetBlockAt) in the order the blocks leave the board.</summary>
        public IReadOnlyList<int> ExitOrder => m_exitOrder;

        /// <summary>The block index of each exit step, in step order.</summary>
        internal static int[] GetExitOrder(IReadOnlyList<LevelSolutionStep> steps)
        {
            int exitCount = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].IsExit)
                {
                    exitCount++;
                }
            }

            int[] order = new int[exitCount];
            int next = 0;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].IsExit)
                {
                    order[next++] = steps[i].BlockIndex;
                }
            }

            return order;
        }
    }
}
