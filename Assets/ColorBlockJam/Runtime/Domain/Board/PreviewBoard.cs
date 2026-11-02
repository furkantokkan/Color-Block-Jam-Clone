using System;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// The board as it reads with one block shown at another origin, without writing the board: that
    /// block's cells at the shown origin, its real cells empty, every other cell as it is. It answers
    /// every IBoardReader question exactly as BoardState would after Move(block, origin), so the drag
    /// preview and the move search ask the rules today's questions while only command handlers change
    /// the board (DEC-122). With nothing shown it reads exactly as the board.
    /// </summary>
    public sealed class PreviewBoard : IBoardReader
    {
        private const int k_none = -1;

        private readonly BoardState m_board;

        // A cell belongs to the shown footprint when its mark equals the current stamp, so showing
        // another origin never clears the array.
        private readonly int[] m_mark;
        private int m_stamp;
        private int m_block = k_none;
        private Cell m_origin;

        public PreviewBoard(BoardState board)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_mark = new int[board.Width * board.Height];
        }

        /// <summary>The block shown away from its board origin, or -1 when the preview reads as the board.</summary>
        public int ShownBlock => m_block;

        public LevelModel Model => m_board.Model;

        public int Width => m_board.Width;

        public int Height => m_board.Height;

        public int BlockCount => m_board.BlockCount;

        public int RequiredRemaining => m_board.RequiredRemaining;

        /// <summary>Shows an on-board block at origin; cells outside the board are simply not marked.</summary>
        public void Show(int block, Cell origin)
        {
            if (m_stamp == int.MaxValue)
            {
                Array.Clear(m_mark, 0, m_mark.Length);
                m_stamp = 0;
            }

            m_stamp++;
            m_block = block;
            m_origin = origin;
            BlockFootprint footprint = m_board.GetFootprint(block);
            for (int i = 0; i < footprint.CellCount; i++)
            {
                Cell cell = origin + footprint.GetCell(i);
                if (cell.X >= 0 && cell.Y >= 0 && cell.X < Width && cell.Y < Height)
                {
                    m_mark[cell.Y * Width + cell.X] = m_stamp;
                }
            }
        }

        /// <summary>Stops showing a block, so the preview reads exactly as the board again.</summary>
        public void Clear()
        {
            m_block = k_none;
        }

        public Cell GetOrigin(int block) => block == m_block ? m_origin : m_board.GetOrigin(block);

        public BlockStatus GetStatus(int block) => m_board.GetStatus(block);

        /// <summary>The board's footprint: a preview shows a block elsewhere, never with other cells.</summary>
        public BlockFootprint GetFootprint(int block) => m_board.GetFootprint(block);

        public bool IsFloor(Cell cell) => m_board.IsFloor(cell);

        // Restrictions depend on a block's effects, never on where it is shown.
        public bool AllowsBoosterTarget(CommandKind kind, int block) => m_board.AllowsBoosterTarget(kind, block);

        public int GetOccupant(Cell cell)
        {
            int occupant = m_board.GetOccupant(cell);
            if (m_block == k_none || occupant == Occupant.k_offBoard)
            {
                return occupant;
            }

            if (m_mark[cell.Y * Width + cell.X] == m_stamp)
            {
                return m_block;
            }

            return occupant == m_block ? Occupant.k_empty : occupant;
        }
    }
}
