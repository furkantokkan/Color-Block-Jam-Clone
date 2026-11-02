using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Lists distinct solutions of a Solvable level after the check: two solutions are distinct when the blocks leave
    /// in a different order. Entry 0 is always the check's own solution. The others come from a breadth-first search
    /// over positions plus the exit order so far (LevelOrderedStateKey), expanded by the same LevelSolverExpander as
    /// LevelSolverSearch (same tables and move rules, same fixed move order, same eager-exit rule; DEC-178), so they
    /// arrive fewest one-cell steps first. A successor that clears the board is recorded once per exit order and
    /// never queued. The listing has its own position and time budget and never changes the verdict.
    /// </summary>
    internal sealed class LevelSolutionListing
    {
        // The poll cadence is LevelSolverSearch's.
        private const int k_timeCheckInterval = 1024;
        private const double k_progressInterval = 0.1;
        private const int k_initialCapacity = 4096;

        private readonly LevelSolverTables m_tables;
        private readonly LevelCheckOptions m_options;
        private readonly ILevelCheckProgress m_progress;
        private readonly Stopwatch m_stopwatch;
        private readonly int m_blockCount;
        private readonly LevelSolverExpander m_expander;

        private readonly Dictionary<LevelOrderedStateKey, int> m_visited;
        private readonly HashSet<ulong> m_listedOrders = new HashSet<ulong>();
        private readonly List<LevelSolution> m_solutions = new List<LevelSolution>();
        private LevelOrderedStateKey[] m_keys;
        private int[] m_parents;
        private int[] m_moves;
        private int m_count;

        private enum Expansion
        {
            Continue = 0,
            More = 1,
            PositionLimit = 2
        }

        private LevelSolutionListing(LevelSolverTables tables, LevelCheckResult verdict, LevelCheckOptions options,
            ILevelCheckProgress progress, Stopwatch stopwatch)
        {
            m_stopwatch = stopwatch;
            m_tables = tables;
            m_options = options;
            m_progress = progress;
            m_blockCount = tables.BlockCount;
            m_expander = new LevelSolverExpander(tables, options.EagerExits);
            int capacity = Math.Max(1, Math.Min(k_initialCapacity, options.MaxListPositions + 1));
            m_visited = new Dictionary<LevelOrderedStateKey, int>(capacity);
            m_keys = new LevelOrderedStateKey[capacity];
            m_parents = new int[capacity];
            m_moves = new int[capacity];

            LevelSolution found = verdict.CreateFoundSolution();
            if (!IsOrderOfEveryBlock(found.ExitOrder, m_blockCount))
            {
                throw new ArgumentException("The verdict's solution does not clear this level's blocks.", nameof(verdict));
            }

            m_solutions.Add(found);
            m_listedOrders.Add(LevelOrderedStateKey.PackOrder(found.ExitOrder));
        }

        /// <summary>
        /// The verdict with its solution list; a verdict that is not Solvable comes back unchanged. The verdict must be
        /// the check's answer for the level the tables were built from. The stopwatch is the listing's time budget
        /// clock: it runs from the listing's start, or from just before its tables were built again for a kept verdict,
        /// and the first progress report, before any expansion, gives that time.
        /// </summary>
        public static LevelCheckResult Run(LevelSolverTables tables, LevelCheckResult verdict, LevelCheckOptions options,
            ILevelCheckProgress progress, Stopwatch stopwatch)
        {
            if (tables == null || verdict == null || options == null || progress == null || stopwatch == null)
            {
                throw new ArgumentNullException(tables == null ? nameof(tables)
                    : verdict == null ? nameof(verdict) : options == null ? nameof(options)
                    : progress == null ? nameof(progress) : nameof(stopwatch));
            }

            if (verdict.Status != LevelCheckStatus.Solvable)
            {
                return verdict;
            }

            LevelSolutionListing listing = new LevelSolutionListing(tables, verdict, options, progress, stopwatch);
            LevelSolutionListState state = listing.Search();
            return verdict.WithSolutions(SortByMoves(listing.m_solutions.ToArray()), state);
        }

        // The panel lists entries from the fewest player moves to the most (user order of 2026-09-28, DEC-181). The sort
        // is stable, so entries with equal moves keep the search's order: fewest one-cell steps first. At most 20 entries.
        private static LevelSolution[] SortByMoves(LevelSolution[] solutions)
        {
            for (int index = 1; index < solutions.Length; index++)
            {
                LevelSolution entry = solutions[index];
                int target = index - 1;
                while (target >= 0 && solutions[target].MoveCount > entry.MoveCount)
                {
                    solutions[target + 1] = solutions[target];
                    target--;
                }

                solutions[target + 1] = entry;
            }

            return solutions;
        }

        private double Elapsed => m_stopwatch.Elapsed.TotalSeconds;

        private LevelSolutionListState Search()
        {
            LevelStateKey start = default;
            for (int block = 0; block < m_blockCount; block++)
            {
                start = start.WithSlot(block, m_tables.GetAuthoredSlot(block));
            }

            AddNode(new LevelOrderedStateKey(start, 0UL), -1, -1);
            double lastPoll = double.NegativeInfinity;

            // Owner: this call. Stops: when the queue empties, one more order is reached with the list full, a budget
            // is hit or the owner cancels.
            for (int head = 0; head < m_count; head++)
            {
                if (head % k_timeCheckInterval == 0)
                {
                    double elapsed = Elapsed;
                    if (elapsed - lastPoll >= k_progressInterval)
                    {
                        lastPoll = elapsed;
                        m_progress.Report(m_count, elapsed);
                        if (m_progress.IsCancelRequested)
                        {
                            return LevelSolutionListState.Partial;
                        }
                    }

                    if (elapsed >= m_options.MaxListSeconds)
                    {
                        return LevelSolutionListState.Partial;
                    }
                }

                Expansion expansion = Expand(head);
                if (expansion == Expansion.More)
                {
                    return LevelSolutionListState.More;
                }

                if (expansion == Expansion.PositionLimit)
                {
                    return LevelSolutionListState.Partial;
                }
            }

            return LevelSolutionListState.Complete;
        }

        private Expansion Expand(int node)
        {
            LevelOrderedStateKey key = m_keys[node];
            SuccessorSink sink = new SuccessorSink(this, node, key, CountExited(key.Position));
            m_expander.Expand(key.Position, ref sink);
            return sink.Result;
        }

        // The number of blocks that already left, which places a new exit in the order.
        private int CountExited(in LevelStateKey position)
        {
            int count = 0;
            for (int block = 0; block < m_blockCount; block++)
            {
                if (position.IsExited(block))
                {
                    count++;
                }
            }

            return count;
        }

        private Expansion AddSuccessor(int parent, LevelOrderedStateKey key, int block, int kind, bool isGoal)
        {
            int move = LevelSolverMoveCode.Encode(block, kind);
            if (isGoal)
            {
                return AddGoal(parent, move, key.ExitOrder);
            }

            if (m_visited.ContainsKey(key))
            {
                return Expansion.Continue;
            }

            if (m_count >= m_options.MaxListPositions)
            {
                return Expansion.PositionLimit;
            }

            AddNode(key, parent, move);
            return Expansion.Continue;
        }

        // A cleared board is listed once per exit order and never queued; with the list full, a new order means More.
        private Expansion AddGoal(int parent, int move, ulong exitOrder)
        {
            if (m_listedOrders.Contains(exitOrder))
            {
                return Expansion.Continue;
            }

            if (m_solutions.Count >= LevelCheckOptions.MaxListedSolutions)
            {
                return Expansion.More;
            }

            m_listedOrders.Add(exitOrder);
            m_solutions.Add(new LevelSolution(CreateSteps(parent, move)));
            return Expansion.Continue;
        }

        private void AddNode(LevelOrderedStateKey key, int parent, int move)
        {
            if (m_count == m_keys.Length)
            {
                int capacity = m_keys.Length * 2;
                Array.Resize(ref m_keys, capacity);
                Array.Resize(ref m_parents, capacity);
                Array.Resize(ref m_moves, capacity);
            }

            m_keys[m_count] = key;
            m_parents[m_count] = parent;
            m_moves[m_count] = move;
            m_visited.Add(key, m_count);
            m_count++;
        }

        // Walks the parent links from the goal's parent and turns each stored move back into origins; the goal's own
        // move, the last block's exit, ends the list.
        private LevelSolutionStep[] CreateSteps(int parent, int goalMove)
        {
            int length = 1;
            for (int node = parent; m_parents[node] >= 0; node = m_parents[node])
            {
                length++;
            }

            LevelSolutionStep[] steps = new LevelSolutionStep[length];
            steps[length - 1] = CreateStep(parent, goalMove, m_keys[parent].Position);
            int index = length - 2;
            for (int node = parent; m_parents[node] >= 0; node = m_parents[node])
            {
                steps[index] = CreateStep(m_parents[node], m_moves[node], m_keys[node].Position);
                index--;
            }

            return steps;
        }

        // One stored move from a node's position to the target position; an exit keeps From as To.
        private LevelSolutionStep CreateStep(int from, int move, in LevelStateKey target)
        {
            LevelSolverMoveCode.Decode(move, out int block, out int kind);
            return LevelSolverMoveCode.CreateStep(m_tables, block, kind, m_keys[from].Position.GetSlot(block),
                target.GetSlot(block));
        }

        // True when the order names every block of the level exactly once.
        private static bool IsOrderOfEveryBlock(IReadOnlyList<int> order, int blockCount)
        {
            if (order.Count != blockCount)
            {
                return false;
            }

            bool[] seen = new bool[blockCount];
            for (int i = 0; i < order.Count; i++)
            {
                int block = order[i];
                if (block < 0 || block >= blockCount || seen[block])
                {
                    return false;
                }

                seen[block] = true;
            }

            return true;
        }

        /// <summary>Turns each expander successor into this listing's ordered key and adds it; stops on More or the limit.</summary>
        private struct SuccessorSink : ILevelSuccessorSink
        {
            private readonly LevelSolutionListing m_listing;
            private readonly int m_node;
            private readonly LevelOrderedStateKey m_key;
            private readonly int m_exitedCount;

            public SuccessorSink(LevelSolutionListing listing, int node, LevelOrderedStateKey key, int exitedCount)
            {
                m_listing = listing;
                m_node = node;
                m_key = key;
                m_exitedCount = exitedCount;
                Result = Expansion.Continue;
            }

            public Expansion Result { get; private set; }

            public bool Add(int block, int kind, int targetSlot, bool isGoal)
            {
                LevelOrderedStateKey successor = LevelSolverMoveCode.IsExit(kind)
                    ? m_key.WithExited(block, m_exitedCount)
                    : m_key.WithSlot(block, targetSlot);
                Result = m_listing.AddSuccessor(m_node, successor, block, kind, isGoal);
                return Result == Expansion.Continue;
            }
        }
    }
}
