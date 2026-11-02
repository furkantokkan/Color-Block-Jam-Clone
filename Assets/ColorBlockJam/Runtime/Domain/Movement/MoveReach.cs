using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// Decides whether a block can travel between two origins the way a drag does: in single cell
    /// steps along its allowed axes, each step being the drag's own check, MoveRules.CanOccupy with the
    /// block shown where it stands (RULE-10, XP-2, XP-3). A breadth-first search over the block's
    /// origins in a fixed neighbour order; its buffers are allocated with the attempt, so a search
    /// allocates nothing and the same board always gives the same answer (DEC-122).
    /// </summary>
    public sealed class MoveReach
    {
        private const int k_stepCount = 4;

        private readonly PreviewBoard m_view;
        private readonly int m_width;
        private readonly int m_height;

        // One slot per origin whose footprint box fits the board; a slot is visited when its stamp
        // equals the current search's stamp, so a new search never clears the array.
        private readonly int[] m_visited;
        private readonly int[] m_queue;
        private int m_stamp;

        public MoveReach(BoardState board)
        {
            m_view = new PreviewBoard(board);
            m_width = board.Width;
            m_height = board.Height;
            m_visited = new int[m_width * m_height];
            m_queue = new int[m_width * m_height];
        }

        /// <summary>
        /// True when the on-board block can travel from start, its current origin, to target. A step
        /// that one origin refuses may still be allowed from another (a cell gate can let a block keep
        /// a cell it already covers), so an origin is only marked once a step into it succeeded.
        /// </summary>
        public bool CanReach(AttemptHooks hooks, int block, Cell start, Cell target, AxisMask axes)
        {
            BlockFootprint footprint = m_view.GetFootprint(block);
            if (!TryGetSlot(footprint, start, out int startSlot) || !TryGetSlot(footprint, target, out int _))
            {
                return false;
            }

            if (start == target)
            {
                return true;
            }

            if (m_stamp == int.MaxValue)
            {
                Array.Clear(m_visited, 0, m_visited.Length);
                m_stamp = 0;
            }

            m_stamp++;
            m_visited[startSlot] = m_stamp;
            m_queue[0] = startSlot;
            int head = 0;
            int tail = 1;

            // Owner: this call. Stops: when the target is found or the queue empties; each origin is
            // queued at most once, so the loop runs at most width * height times.
            while (head < tail)
            {
                Cell origin = GetOrigin(footprint, m_queue[head]);
                head++;
                m_view.Show(block, origin);
                for (int i = 0; i < k_stepCount; i++)
                {
                    Cell step = GetStep(i);
                    if ((step.X != 0 && (axes & AxisMask.Horizontal) == 0) || (step.Y != 0 && (axes & AxisMask.Vertical) == 0))
                    {
                        continue;
                    }

                    Cell next = origin + step;
                    if (!TryGetSlot(footprint, next, out int slot) || m_visited[slot] == m_stamp
                        || !MoveRules.CanOccupy(m_view, hooks, block, next))
                    {
                        continue;
                    }

                    if (next == target)
                    {
                        m_view.Clear();
                        return true;
                    }

                    m_visited[slot] = m_stamp;
                    m_queue[tail] = slot;
                    tail++;
                }
            }

            m_view.Clear();
            return false;
        }

        // An origin maps to a slot when the block's footprint box lies inside the board; any other
        // origin puts a cell off the board, which CanOccupy would refuse anyway.
        private bool TryGetSlot(in BlockFootprint footprint, Cell origin, out int slot)
        {
            int left = origin.X + footprint.MinX;
            int bottom = origin.Y + footprint.MinY;
            if (left < 0 || bottom < 0 || origin.X + footprint.MaxX >= m_width || origin.Y + footprint.MaxY >= m_height)
            {
                slot = -1;
                return false;
            }

            slot = bottom * m_width + left;
            return true;
        }

        private Cell GetOrigin(in BlockFootprint footprint, int slot)
        {
            return new Cell(slot % m_width - footprint.MinX, slot / m_width - footprint.MinY);
        }

        // The fixed neighbour order +X, -X, +Y, -Y keeps every search deterministic.
        private static Cell GetStep(int index)
        {
            switch (index)
            {
                case 0: return new Cell(1, 0);
                case 1: return new Cell(-1, 0);
                case 2: return new Cell(0, 1);
                default: return new Cell(0, -1);
            }
        }
    }
}
