using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// One attempt's frozen blocks; every other block that leaves the board, through a door or a booster removal, thaws them by one. A Rocket part inherits
    /// the counts of the block it split from, so the split never thaws or refreezes anything (B-10).
    /// </summary>
    public sealed class IceRules : IDragGate, IExitGate, IBoardEventListener, IBlockEffectPresence
    {
        // Mirrors LevelEffectRegistry's authored "clearsToThaw" range (Runtime), which is the range's authority;
        // Domain stays reference-free, so that range and IceCounterLabels (Presentation) keep their own literal 20.
        public const int k_maxCount = 20;

        private readonly int[] m_initial;
        private readonly int[] m_remaining;
        private readonly bool[] m_exited;

        /// <summary>
        /// Counts with room for the authored blocks only, for boards that never split. A board that can
        /// split passes LevelModel.TotalCellCount to the capacity overload, or its Rocket parts lose their Ice.
        /// </summary>
        public IceRules(int[] initial)
            : this(initial, initial?.Length ?? 0)
        {
        }

        /// <summary>
        /// Counts for the authored blocks with room for capacity block IDs (LevelModel.TotalCellCount),
        /// so every Rocket part has a slot; IDs past the authored counts start unfrozen.
        /// </summary>
        public IceRules(int[] initial, int capacity)
        {
            if (initial == null) throw new ArgumentNullException(nameof(initial));
            if (capacity < initial.Length)
                throw new ArgumentOutOfRangeException(nameof(capacity), "The capacity must hold every authored block.");
            m_initial = new int[capacity];
            m_remaining = new int[capacity];
            m_exited = new bool[capacity];
            for (int block = 0; block < initial.Length; block++)
            {
                if (initial[block] < 0 || initial[block] > k_maxCount)
                    throw new ArgumentOutOfRangeException(nameof(initial), $"Ice counts must be zero or 1 through {k_maxCount}.");
                m_initial[block] = initial[block];
                m_remaining[block] = initial[block];
            }
        }

        public int BlockCount => m_remaining.Length;
        public int Revision { get; private set; }
        public int GetInitial(int block) => m_initial[block];
        public int GetRemaining(int block) => m_remaining[block];

        // An ID past the capacity has no slot and reads as unfrozen, as its events are ignored below, so
        // rules sized below the board's IDs never throw inside the Playing tick.
        public bool AllowsDrag(IBoardReader board, int block) => IsThawed(block);
        public bool AllowsExit(IBoardReader board, in ExitQuery query, ref ExitOutcome outcome)
            => IsThawed(query.Block);

        // A block carries Ice for booster restrictions only while frozen; a thawed block is a plain block (DEC-171).
        public bool HasEffect(IBoardReader board, int block) => !IsThawed(block);

        public void OnBlockExited(in BlockExitedEvent e)
        {
            if (e.Outcome != ExitOutcome.Leave) return;
            Clear(e.Block);
        }

        // A booster removal (Hammer, Vacuum) clears a block from the board like a door exit, so it thaws too.
        public void OnBlockRemoved(in BlockRemovedEvent e) => Clear(e.Block);

        // Each block counts once: every other frozen block loses one.
        private void Clear(int cleared)
        {
            if (cleared < 0 || cleared >= m_exited.Length || m_exited[cleared]) return;
            m_exited[cleared] = true;
            bool changed = false;
            for (int block = 0; block < m_remaining.Length; block++)
            {
                if (block == cleared || m_remaining[block] == 0) continue;
                m_remaining[block]--;
                changed = true;
            }
            if (changed) Revision++;
        }
        public void OnMoveCommitted(in MoveCommittedEvent e) { }

        // A new part (Block != Parent) copies its parent's counts; the parent's own event changes nothing.
        public void OnFootprintChanged(in FootprintChangedEvent e)
        {
            if (e.Block == e.Parent || e.Block < 0 || e.Block >= m_remaining.Length || e.Parent < 0
                || e.Parent >= m_remaining.Length)
                return;
            m_initial[e.Block] = m_initial[e.Parent];
            m_remaining[e.Block] = m_remaining[e.Parent];
            m_exited[e.Block] = false;
            if (m_remaining[e.Block] > 0) Revision++;
        }

        private bool IsThawed(int block) => (uint)block >= (uint)m_remaining.Length || m_remaining[block] == 0;
    }
}
