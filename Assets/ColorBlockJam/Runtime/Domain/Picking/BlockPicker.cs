using System;
using ColorBlockJam.Domain.Board;

namespace ColorBlockJam.Domain.Picking
{
    /// <summary>
    /// Chooses the block a press selects, once per pointer-down (AC-3.19, AC-3.20): a direct hit on a
    /// projected occupied cell, otherwise the on-board block whose projected occupied footprint is
    /// nearest in screen space, with no radius cutoff. Ties go to the lower block ID (DEC-115): authored
    /// blocks keep their authored index and Rocket parts take later IDs, so an authored block wins a
    /// tie against a part (B-2). The only allocation is the grid-corner buffer made in the constructor.
    /// </summary>
    public sealed class BlockPicker
    {
        private const int k_none = -1;

        private readonly int m_width;
        private readonly int m_height;
        private readonly ScreenPoint[] m_corner;

        public BlockPicker(int width, int height)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The board needs a positive size.");
            }

            m_width = width;
            m_height = height;
            m_corner = new ScreenPoint[(width + 1) * (height + 1)];
        }

        /// <summary>Returns the selected block index, or -1 when no block is on the board.</summary>
        public int Pick(ScreenPoint press, IBoardReader board, IFootprintProjector projector)
        {
            if (board.Width != m_width || board.Height != m_height)
            {
                throw new ArgumentException("The board size differs from the picker's size.", nameof(board));
            }

            // Grid corner (gx, gy) sits at board point (gx - 0.5, gy - 0.5); cell (x, y) spans corners x..x+1, y..y+1.
            int stride = m_width + 1;
            for (int gy = 0; gy <= m_height; gy++)
            {
                for (int gx = 0; gx <= m_width; gx++)
                {
                    m_corner[gy * stride + gx] = projector.Project(gx - 0.5f, gy - 0.5f);
                }
            }

            int best = k_none;
            float bestDistanceSq = float.PositiveInfinity;
            for (int block = 0; block < board.BlockCount; block++)
            {
                if (board.GetStatus(block) != BlockStatus.OnBoard)
                {
                    continue;
                }

                Cell origin = board.GetOrigin(block);
                BlockFootprint footprint = board.GetFootprint(block);
                for (int i = 0; i < footprint.CellCount; i++)
                {
                    Cell cell = origin + footprint.GetCell(i);
                    int corner = cell.Y * stride + cell.X;
                    float distanceSq = GetDistanceSq(press, m_corner[corner], m_corner[corner + 1],
                        m_corner[corner + stride + 1], m_corner[corner + stride]);

                    // A direct hit of the lowest index cannot be beaten: later blocks only win with a strictly smaller distance.
                    if (distanceSq == 0f)
                    {
                        return block;
                    }

                    if (distanceSq < bestDistanceSq)
                    {
                        bestDistanceSq = distanceSq;
                        best = block;
                    }
                }
            }

            return best;
        }

        private static float GetDistanceSq(ScreenPoint p, ScreenPoint q0, ScreenPoint q1, ScreenPoint q2, ScreenPoint q3)
        {
            float c0 = Cross(q0, q1, p);
            float c1 = Cross(q1, q2, p);
            float c2 = Cross(q2, q3, p);
            float c3 = Cross(q3, q0, p);
            bool inside = (c0 >= 0f && c1 >= 0f && c2 >= 0f && c3 >= 0f)
                || (c0 <= 0f && c1 <= 0f && c2 <= 0f && c3 <= 0f);
            if (inside)
            {
                return 0f;
            }

            float distanceSq = GetSegmentDistanceSq(p, q0, q1);
            distanceSq = Math.Min(distanceSq, GetSegmentDistanceSq(p, q1, q2));
            distanceSq = Math.Min(distanceSq, GetSegmentDistanceSq(p, q2, q3));
            return Math.Min(distanceSq, GetSegmentDistanceSq(p, q3, q0));
        }

        private static float Cross(ScreenPoint a, ScreenPoint b, ScreenPoint p)
        {
            return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
        }

        private static float GetSegmentDistanceSq(ScreenPoint p, ScreenPoint a, ScreenPoint b)
        {
            float abX = b.X - a.X;
            float abY = b.Y - a.Y;
            float lengthSq = abX * abX + abY * abY;
            float t = lengthSq > 0f ? ((p.X - a.X) * abX + (p.Y - a.Y) * abY) / lengthSq : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float dx = p.X - (a.X + abX * t);
            float dy = p.Y - (a.Y + abY * t);
            return dx * dx + dy * dy;
        }
    }
}
