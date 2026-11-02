using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Receives one position's successors from LevelSolverExpander. A struct sink keeps the expansion free of
    /// allocations and of a successor buffer.
    /// </summary>
    internal interface ILevelSuccessorSink
    {
        /// <summary>
        /// One successor: the block and its LevelSolverMoveCode kind, the slot it slides to (-1 for an exit) and
        /// whether this exit clears the board. Returns false to stop the expansion.
        /// </summary>
        bool Add(int block, int kind, int targetSlot, bool isGoal);
    }

    /// <summary>
    /// Walks a position's successors in the one fixed order the check and the solution listing share (DEC-178): with
    /// eager exits, every available exit and nothing else when one exists; otherwise, per block in index order, its
    /// slides (+X, -X, +Y, -Y) and then, in plain search only, its exits in Side order. Frozen blocks have no move.
    /// Move legality is LevelSolverMoves'; the key type, goal rule and visited policy stay with each search.
    /// </summary>
    internal sealed class LevelSolverExpander
    {
        private readonly LevelSolverTables m_tables;
        private readonly bool m_eagerExits;
        private readonly int m_blockCount;

        // Scratch for the position being expanded.
        private readonly int[] m_slots;
        private readonly CellMask[] m_footprints;

        public LevelSolverExpander(LevelSolverTables tables, bool eagerExits)
        {
            m_tables = tables ?? throw new ArgumentNullException(nameof(tables));
            m_eagerExits = eagerExits;
            m_blockCount = tables.BlockCount;
            m_slots = new int[m_blockCount];
            m_footprints = new CellMask[m_blockCount];
        }

        public void Expand<TSink>(in LevelStateKey position, ref TSink sink) where TSink : struct, ILevelSuccessorSink
        {
            int exitedCount = 0;
            CellMask occupancy = CellMask.Empty;
            for (int block = 0; block < m_blockCount; block++)
            {
                if (position.IsExited(block))
                {
                    exitedCount++;
                    continue;
                }

                int slot = position.GetSlot(block);
                m_slots[block] = slot;
                m_footprints[block] = m_tables.GetFootprint(block, slot);
                occupancy = occupancy.Or(m_footprints[block]);
            }

            bool lastBlock = exitedCount + 1 == m_blockCount;
            if (m_eagerExits)
            {
                // Exits only free cells and lower Ice counts, so an available exit never loses a solution.
                bool anyExit = false;
                for (int block = 0; block < m_blockCount; block++)
                {
                    if (position.IsExited(block) || LevelSolverMoves.IsFrozen(m_tables, block, exitedCount))
                    {
                        continue;
                    }

                    CellMask others = occupancy.Without(m_footprints[block]);
                    for (int side = 0; side < LevelSolverTables.SideCount; side++)
                    {
                        if (!LevelSolverMoves.CanExit(m_tables, block, m_slots[block], (Side)side, others))
                        {
                            continue;
                        }

                        anyExit = true;
                        if (!sink.Add(block, LevelSolverMoveCode.ExitKindOffset + side, -1, lastBlock))
                        {
                            return;
                        }
                    }
                }

                if (anyExit)
                {
                    return;
                }
            }

            for (int block = 0; block < m_blockCount; block++)
            {
                if (position.IsExited(block) || LevelSolverMoves.IsFrozen(m_tables, block, exitedCount))
                {
                    continue;
                }

                int slot = m_slots[block];
                CellMask others = occupancy.Without(m_footprints[block]);
                for (int direction = 0; direction < LevelSolverTables.DirectionCount; direction++)
                {
                    int target = LevelSolverMoves.GetSlide(m_tables, block, slot, direction, others);
                    if (target < 0)
                    {
                        continue;
                    }

                    if (!sink.Add(block, direction, target, false))
                    {
                        return;
                    }
                }

                // With eager exits on, no exit was available, so only plain search asks again.
                for (int side = 0; !m_eagerExits && side < LevelSolverTables.SideCount; side++)
                {
                    if (!LevelSolverMoves.CanExit(m_tables, block, slot, (Side)side, others))
                    {
                        continue;
                    }

                    if (!sink.Add(block, LevelSolverMoveCode.ExitKindOffset + side, -1, lastBlock))
                    {
                        return;
                    }
                }
            }
        }
    }
}
