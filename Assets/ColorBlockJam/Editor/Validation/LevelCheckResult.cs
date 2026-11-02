using System;
using System.Collections.Generic;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The immutable outcome of one solvability check: its status, the designer sentence the Level Editor
    /// shows, the found solution for a Solvable level, the search counters and, when asked for, the listed
    /// distinct solutions.
    /// </summary>
    public sealed class LevelCheckResult
    {
        private const string k_solvablePrefix = "Solvable in ";
        private const string k_unsolvablePrefix = "Cannot be solved: ";
        private const string k_unknownPrefix = "Not confirmed: the check stopped after ";
        private const string k_notCheckedPrefix = "Not checked: ";

        private static readonly LevelSolutionStep[] s_noSteps = Array.Empty<LevelSolutionStep>();
        private static readonly LevelSolution[] s_noSolutions = Array.Empty<LevelSolution>();

        private readonly LevelSolutionStep[] m_steps;
        private readonly LevelSolution[] m_solutions;

        private LevelCheckResult(LevelCheckStatus status, string text, string reason, int moveCount,
            LevelSolutionStep[] steps, int visitedPositions, double elapsedSeconds, LevelCheckLimit limitHit)
            : this(status, text, reason, moveCount, steps, visitedPositions, elapsedSeconds, limitHit, null,
                LevelSolutionListState.NotListed)
        {
        }

        private LevelCheckResult(LevelCheckStatus status, string text, string reason, int moveCount,
            LevelSolutionStep[] steps, int visitedPositions, double elapsedSeconds, LevelCheckLimit limitHit,
            LevelSolution[] solutions, LevelSolutionListState solutionListState)
        {
            Status = status;
            Text = text;
            Reason = reason;
            MoveCount = moveCount;
            m_steps = steps ?? s_noSteps;
            VisitedPositions = visitedPositions;
            ElapsedSeconds = elapsedSeconds;
            LimitHit = limitHit;
            m_solutions = solutions ?? s_noSolutions;
            SolutionListState = solutionListState;
        }

        public LevelCheckStatus Status { get; }

        /// <summary>The whole status sentence, for example "Solvable in 4 moves." or "Not checked: ...".</summary>
        public string Text { get; }

        /// <summary>The sentence's reason part without prefix or final period; null for Solvable.</summary>
        public string Reason { get; }

        /// <summary>Player moves in the found solution: consecutive steps of one block merge into one move (DEC-122).</summary>
        public int MoveCount { get; }

        /// <summary>The found one-cell steps and exits in order; empty unless Solvable.</summary>
        public IReadOnlyList<LevelSolutionStep> Steps => m_steps;

        /// <summary>Distinct positions the search stored; 0 when no search ran. The listing's positions are not counted.</summary>
        public int VisitedPositions { get; }

        /// <summary>The check's own time; the listing's time is not counted.</summary>
        public double ElapsedSeconds { get; }

        /// <summary>The budget that stopped an Unknown check; None for every other status.</summary>
        public LevelCheckLimit LimitHit { get; }

        /// <summary>
        /// The listed distinct solutions, at most LevelCheckOptions.MaxListedSolutions; empty unless a listing ran.
        /// Entry 0 is always the found solution (Steps), and no two entries share an exit order.
        /// </summary>
        public IReadOnlyList<LevelSolution> Solutions => m_solutions;

        /// <summary>How the listing ended; NotListed when none ran.</summary>
        public LevelSolutionListState SolutionListState { get; }

        /// <summary>"1 solution", "12 solutions", "20+ solutions" or "at least 3 solutions"; empty when not listed.</summary>
        public string SolutionCountText => LevelDesignerText.FormatSolutionCount(m_solutions.Length, SolutionListState);

        /// <summary>
        /// A listed result reads its sentence without the final period, a middle dot and the count, for example
        /// "Solvable in 5 moves · 12 solutions"; any other result reads its sentence (Text).
        /// </summary>
        public string SummaryText => SolutionListState == LevelSolutionListState.NotListed
            ? Text
            : LevelDesignerText.FormatSummary(ListedSentence, SolutionCountText);

        // Entries run from the fewest moves (DEC-181), so a listed summary names entry #1's count, which can be lower
        // than the check's own step-shortest solution (DEC-144 D5).
        private string ListedSentence
        {
            get
            {
                if (Status != LevelCheckStatus.Solvable || m_solutions.Length == 0 || m_solutions[0].MoveCount >= MoveCount)
                {
                    return Text;
                }

                int moves = m_solutions[0].MoveCount;
                return k_solvablePrefix + moves + (moves == 1 ? " move." : " moves.");
            }
        }

        internal static LevelCheckResult CreateSolvable(LevelSolutionStep[] steps, int visitedPositions,
            double elapsedSeconds)
        {
            int moveCount = CountMoves(steps);
            string text = k_solvablePrefix + moveCount + (moveCount == 1 ? " move." : " moves.");
            return new LevelCheckResult(LevelCheckStatus.Solvable, text, null, moveCount, steps, visitedPositions,
                elapsedSeconds, LevelCheckLimit.None);
        }

        internal static LevelCheckResult CreateUnsolvable(string reason, int visitedPositions, double elapsedSeconds)
        {
            return new LevelCheckResult(LevelCheckStatus.Unsolvable, k_unsolvablePrefix + reason + ".", reason, 0, null,
                visitedPositions, elapsedSeconds, LevelCheckLimit.None);
        }

        /// <summary>limitText names the budget, for example "250,000 positions" or "3 seconds".</summary>
        internal static LevelCheckResult CreateUnknown(LevelCheckLimit limit, string limitText, int visitedPositions,
            double elapsedSeconds)
        {
            string reason = "the check stopped after " + limitText;
            return new LevelCheckResult(LevelCheckStatus.Unknown, k_unknownPrefix + limitText + ".", reason, 0, null,
                visitedPositions, elapsedSeconds, limit);
        }

        internal static LevelCheckResult CreateNotChecked(string reason, int visitedPositions, double elapsedSeconds)
        {
            return new LevelCheckResult(LevelCheckStatus.NotChecked, k_notCheckedPrefix + reason + ".", reason, 0, null,
                visitedPositions, elapsedSeconds, LevelCheckLimit.None);
        }

        /// <summary>Counts runs of consecutive steps by one block; an exit right after that block's slides joins their move.</summary>
        internal static int CountMoves(IReadOnlyList<LevelSolutionStep> steps)
        {
            int moves = 0;
            int previousBlock = -1;
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i].BlockIndex != previousBlock)
                {
                    moves++;
                    previousBlock = steps[i].BlockIndex;
                }
            }

            return moves;
        }

        /// <summary>The found solution as a listing entry; it shares this result's steps.</summary>
        internal LevelSolution CreateFoundSolution()
        {
            return new LevelSolution(m_steps);
        }

        /// <summary>This verdict unchanged (status, sentence, steps and counters) with a solution list.</summary>
        internal LevelCheckResult WithSolutions(LevelSolution[] solutions, LevelSolutionListState state)
        {
            return new LevelCheckResult(Status, Text, Reason, MoveCount, m_steps, VisitedPositions, ElapsedSeconds,
                LimitHit, solutions, state);
        }
    }
}
