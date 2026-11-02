using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Stage 3: a breadth-first search over positions (LevelStateKey) with one-cell slides and exits in a
    /// fixed order (block index ascending; +X, -X, +Y, -Y; then exit sides in Side order), bounded by a
    /// position and a time budget. It is shortest in one-cell steps. Proven unsolvable only when the queue
    /// is exhausted; a budget hit is Unknown. The move order is LevelSolverExpander's, shared with the solution
    /// listing (DEC-178). Nodes live in growable arrays that double as the queue, so an expansion allocates
    /// nothing beyond visited-set and queue growth.
    /// </summary>
    internal sealed class LevelSolverSearch
    {
        private const int k_timeCheckInterval = 1024;
        private const double k_progressInterval = 0.1;
        private const int k_initialCapacity = 4096;
        private const string k_exhaustedReason = "no sequence of moves clears every block";
        private const string k_cancelledReason = "the check was cancelled";

        private readonly LevelSolverTables m_tables;
        private readonly LevelCheckOptions m_options;
        private readonly ILevelCheckProgress m_progress;
        private readonly Stopwatch m_stopwatch;
        private readonly int m_blockCount;
        private readonly LevelSolverExpander m_expander;

        private readonly Dictionary<LevelStateKey, int> m_visited;
        private LevelStateKey[] m_keys;
        private int[] m_parents;
        private int[] m_moves;
        private int m_count;
        private int m_goal = -1;
        // The blocks that left in at least one stored position, one bit per block (DEC-176).
        private int m_everExited;

        private enum Expansion
        {
            Continue = 0,
            Found = 1,
            PositionLimit = 2
        }

        /// <summary>The stopwatch is the check's own, so the time budget covers the tables and pre-checks too.</summary>
        public LevelSolverSearch(LevelSolverTables tables, LevelCheckOptions options, ILevelCheckProgress progress,
            Stopwatch stopwatch)
        {
            m_tables = tables ?? throw new ArgumentNullException(nameof(tables));
            m_options = options ?? throw new ArgumentNullException(nameof(options));
            m_progress = progress ?? throw new ArgumentNullException(nameof(progress));
            m_stopwatch = stopwatch ?? throw new ArgumentNullException(nameof(stopwatch));
            m_blockCount = tables.BlockCount;
            m_expander = new LevelSolverExpander(tables, options.EagerExits);
            int capacity = Math.Max(1, Math.Min(k_initialCapacity, options.MaxPositions + 1));
            m_visited = new Dictionary<LevelStateKey, int>(capacity);
            m_keys = new LevelStateKey[capacity];
            m_parents = new int[capacity];
            m_moves = new int[capacity];
        }

        public LevelCheckResult Run()
        {
            LevelStateKey start = default;
            for (int block = 0; block < m_blockCount; block++)
            {
                start = start.WithSlot(block, m_tables.GetAuthoredSlot(block));
            }

            AddNode(start, -1, -1);
            if (m_blockCount == 0)
            {
                return LevelCheckResult.CreateSolvable(Array.Empty<LevelSolutionStep>(), m_count, Elapsed);
            }

            double lastPoll = double.NegativeInfinity;

            // Owner: this call. Stops: when the queue empties, a solution is found, a budget is hit or the owner cancels.
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
                            return LevelCheckResult.CreateNotChecked(k_cancelledReason, m_count, elapsed);
                        }
                    }

                    if (elapsed >= m_options.MaxSeconds)
                    {
                        return LevelCheckResult.CreateUnknown(LevelCheckLimit.Time,
                            LevelDesignerText.FormatSeconds(m_options.MaxSeconds), m_count, elapsed);
                    }
                }

                Expansion expansion = Expand(head);
                if (expansion == Expansion.Found)
                {
                    return LevelCheckResult.CreateSolvable(CreateSteps(), m_count, Elapsed);
                }

                if (expansion == Expansion.PositionLimit)
                {
                    return LevelCheckResult.CreateUnknown(LevelCheckLimit.Positions,
                        LevelDesignerText.FormatPositions(m_options.MaxPositions), m_count, Elapsed);
                }
            }

            return LevelCheckResult.CreateUnsolvable(k_exhaustedReason, m_count, Elapsed);
        }

        private double Elapsed => m_stopwatch.Elapsed.TotalSeconds;

        /// <summary>
        /// After Run: the blocks that left in at least one stored position, one bit per block. After an exhausted search
        /// the other blocks can never leave: moves are reversible and an exit only frees cells and thaws Ice, so any
        /// block that can leave somewhere can also leave after every other block that can (DEC-176).
        /// </summary>
        public int EverExitedMask => m_everExited;

        private Expansion Expand(int node)
        {
            LevelStateKey key = m_keys[node];
            SuccessorSink sink = new SuccessorSink(this, node, key);
            m_expander.Expand(key, ref sink);
            return sink.Result;
        }

        private Expansion AddSuccessor(int parent, LevelStateKey key, int block, int kind, bool isGoal)
        {
            if (m_visited.ContainsKey(key))
            {
                return Expansion.Continue;
            }

            int move = LevelSolverMoveCode.Encode(block, kind);
            if (isGoal)
            {
                m_goal = AddNode(key, parent, move);
                return Expansion.Found;
            }

            if (m_count >= m_options.MaxPositions)
            {
                return Expansion.PositionLimit;
            }

            AddNode(key, parent, move);
            return Expansion.Continue;
        }

        private int AddNode(LevelStateKey key, int parent, int move)
        {
            if (m_count == m_keys.Length)
            {
                int capacity = m_keys.Length * 2;
                Array.Resize(ref m_keys, capacity);
                Array.Resize(ref m_parents, capacity);
                Array.Resize(ref m_moves, capacity);
            }

            int node = m_count;
            m_keys[node] = key;
            m_parents[node] = parent;
            m_moves[node] = move;
            m_visited.Add(key, node);
            m_everExited |= key.ExitedMask;
            m_count++;
            return node;
        }

        // Walks the parent links from the goal and turns each stored move back into origins.
        private LevelSolutionStep[] CreateSteps()
        {
            int length = 0;
            for (int node = m_goal; m_parents[node] >= 0; node = m_parents[node])
            {
                length++;
            }

            LevelSolutionStep[] steps = new LevelSolutionStep[length];
            int index = length - 1;
            for (int node = m_goal; m_parents[node] >= 0; node = m_parents[node])
            {
                LevelSolverMoveCode.Decode(m_moves[node], out int block, out int kind);
                steps[index] = LevelSolverMoveCode.CreateStep(m_tables, block, kind,
                    m_keys[m_parents[node]].GetSlot(block), m_keys[node].GetSlot(block));
                index--;
            }

            return steps;
        }

        /// <summary>Turns each expander successor into this search's key and adds it; stops on Found or the limit.</summary>
        private struct SuccessorSink : ILevelSuccessorSink
        {
            private readonly LevelSolverSearch m_search;
            private readonly int m_node;
            private readonly LevelStateKey m_key;

            public SuccessorSink(LevelSolverSearch search, int node, LevelStateKey key)
            {
                m_search = search;
                m_node = node;
                m_key = key;
                Result = Expansion.Continue;
            }

            public Expansion Result { get; private set; }

            public bool Add(int block, int kind, int targetSlot, bool isGoal)
            {
                LevelStateKey successor = LevelSolverMoveCode.IsExit(kind)
                    ? m_key.WithExited(block)
                    : m_key.WithSlot(block, targetSlot);
                Result = m_search.AddSuccessor(m_node, successor, block, kind, isGoal);
                return Result == Expansion.Continue;
            }
        }
    }
}
