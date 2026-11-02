using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Exit;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// Turns the one pointer drag of an attempt into a move command (DEC-122). It steps a preview of
    /// the held block cell by cell toward the pointer and detects contact with a fitting door (RULE-9..14,
    /// DEC-164, DEC-112), but it never writes the board: a release that moved the block, or that contact,
    /// becomes one MoveBlock command for the executor. Every preview origin is validated, so no pointer
    /// jump can tunnel through anything, and the preview origin is always an integer cell.
    /// </summary>
    public sealed class DragResolver
    {
        private const int k_noBlock = -1;

        // X sides first, so an equal push toward a corner's two doors picks the X side, as the drag steps do.
        private static readonly Side[] s_contactOrder = { Side.Left, Side.Right, Side.Top, Side.Bottom };

        private readonly BoardState m_board;
        private readonly AttemptHooks m_hooks;
        private readonly DragSettings m_settings;
        private readonly PreviewBoard m_preview;

        private bool m_active;
        private bool m_consumed;
        private int m_block = k_noBlock;
        private Cell m_start;
        private Cell m_current;
        private BoardPoint m_grab;
        private AxisMask m_axes;
        private bool m_hasPending;
        private GameCommand m_pending;

        public DragResolver(BoardState board, AttemptHooks hooks, DragSettings settings)
        {
            m_board = board ?? throw new ArgumentNullException(nameof(board));
            m_hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_settings = settings;
            m_preview = new PreviewBoard(board);
        }

        public bool IsActive => m_active;

        /// <summary>The held block index, or -1 when no drag is active.</summary>
        public int Block => m_block;

        /// <summary>True after door contact produced its exit command: the pointer owns nothing until it is released.</summary>
        public bool IsConsumed => m_consumed;

        public Cell StartOrigin => m_start;

        /// <summary>The pointer's offset from the block origin at press time, kept for the whole drag (AC-3.20).</summary>
        public BoardPoint GrabOffset => m_grab;

        /// <summary>The axes read from the hooks at drag start and fixed until release (DEC-MECH-25).</summary>
        public AxisMask Axes => m_axes;

        /// <summary>True while an interrupted drag's move waits for the next command phase (DEC-113).</summary>
        public bool HasPending => m_hasPending;

        /// <summary>
        /// The board as the player sees it: the committed board with the held or interrupted block at
        /// its preview origin. The view reads it; rules only ever change the committed board.
        /// </summary>
        public IBoardReader Preview => m_preview;

        /// <summary>
        /// Starts a drag of an on-board block that every drag gate allows. A refusal changes nothing
        /// and leaves the resolver inactive (design section 0.2).
        /// </summary>
        public bool TryBegin(int block, BoardPoint pointer)
        {
            if (m_hasPending)
            {
                throw new InvalidOperationException("The interrupted move must run before a new drag.");
            }

            if (m_active || m_board.GetStatus(block) != BlockStatus.OnBoard || !m_hooks.AllowsDrag(m_board, block))
            {
                return false;
            }

            m_axes = m_hooks.GetAxes(m_board, block);
            m_block = block;
            m_start = m_board.GetOrigin(block);
            m_current = m_start;
            m_grab = pointer - m_start;
            m_consumed = false;
            m_active = true;
            m_preview.Show(block, m_start);
            return true;
        }

        /// <summary>
        /// Moves the preview toward the pointer, one validated cell at a time. Once the held block's cell
        /// touches a door it fits (flush, colour and span, DEC-164, DEC-197), with or without an outward
        /// push, it returns ExitReady with the command
        /// that moves the block there and exits; the press is then consumed. Contact is checked before the
        /// first step and after each one.
        /// </summary>
        public DragResult Update(BoardPoint pointer, out GameCommand exit)
        {
            exit = default;
            if (!m_active || m_consumed)
            {
                return DragResult.Unchanged;
            }

            BlockFootprint footprint = m_board.GetFootprint(m_block);
            BoardPoint wanted = pointer - m_grab;

            // At most one cell beyond flush on each side: enough to rank the touched doors a corner block points at.
            float desiredX = Clamp(wanted.X, -footprint.MinX - 1, m_board.Width - footprint.MaxX);
            float desiredY = Clamp(wanted.Y, -footprint.MinY - 1, m_board.Height - footprint.MaxY);
            if ((m_axes & AxisMask.Horizontal) == 0)
            {
                desiredX = m_current.X;
            }

            if ((m_axes & AxisMask.Vertical) == 0)
            {
                desiredY = m_current.Y;
            }

            if (TryExitOnContact(desiredX, desiredY, out exit))
            {
                return DragResult.ExitReady;
            }

            Cell target = new Cell(RoundHalfAwayFromZero(desiredX), RoundHalfAwayFromZero(desiredY));
            int maxSteps = m_settings.MaxStepsPerTick > 0 ? m_settings.MaxStepsPerTick : m_board.Width + m_board.Height;
            DragResult result = DragResult.Unchanged;

            // Owner: this call. Stops: at the target, when every wanted axis is blocked, or after maxSteps.
            for (int step = 0; step < maxSteps; step++)
            {
                int dx = Math.Sign(target.X - m_current.X);
                int dy = Math.Sign(target.Y - m_current.Y);
                if (dx == 0 && dy == 0)
                {
                    break;
                }

                // The dominant axis moves first and a tie goes to X; a blocked axis slides on the other.
                bool primaryX = Math.Abs(desiredX - m_current.X) >= Math.Abs(desiredY - m_current.Y);
                Cell primary = primaryX ? new Cell(dx, 0) : new Cell(0, dy);
                Cell secondary = primaryX ? new Cell(0, dy) : new Cell(dx, 0);
                if (primary == Cell.Zero)
                {
                    primary = secondary;
                    secondary = Cell.Zero;
                }

                if (TryStep(primary) || (secondary != Cell.Zero && TryStep(secondary)))
                {
                    if (TryExitOnContact(desiredX, desiredY, out exit))
                    {
                        return DragResult.ExitReady;
                    }

                    result = DragResult.Moved;
                    continue;
                }

                break;
            }

            return result;
        }

        /// <summary>
        /// Ends the drag on pointer Up or Cancel. True with the move when the block left its start and
        /// did not push out; a drag back to its start is no move (DEC-113).
        /// </summary>
        public bool TryEnd(out GameCommand move)
        {
            move = default;
            if (!m_active)
            {
                return false;
            }

            bool moved = !m_consumed && m_current != m_start;
            if (moved)
            {
                move = GameCommand.Move(m_block, m_start, m_current);
            }

            m_preview.Clear();
            Clear();
            return moved;
        }

        /// <summary>
        /// Ends the drag for Suspend or Teardown without running anything: commands run only in the
        /// command phase while Playing (RULE-30), so a changed board's move waits for TryTakePending
        /// (DEC-113). The preview keeps showing the block where the player left it.
        /// </summary>
        public void Interrupt()
        {
            if (!m_active)
            {
                return;
            }

            if (!m_consumed && m_current != m_start)
            {
                m_pending = GameCommand.Move(m_block, m_start, m_current);
                m_hasPending = true;
            }
            else
            {
                m_preview.Clear();
            }

            Clear();
        }

        /// <summary>Hands over an interrupted drag's move; the session runs it first in the next command phase.</summary>
        public bool TryTakePending(out GameCommand move)
        {
            move = m_pending;
            if (!m_hasPending)
            {
                return false;
            }

            m_hasPending = false;
            m_preview.Clear();
            return true;
        }

        /// <summary>The held block's visual lead from its preview cell toward the pointer; zero when nothing is held.</summary>
        public BoardPoint GetViewOffset(BoardPoint pointer)
        {
            if (!m_active || m_consumed)
            {
                return default;
            }

            return FollowOffset.Compute(m_preview, m_hooks, m_block, m_current, pointer - m_grab, m_axes,
                m_settings.LeadCells);
        }

        private bool TryStep(Cell direction)
        {
            Cell next = m_current + direction;
            if (!MoveRules.CanOccupy(m_preview, m_hooks, m_block, next))
            {
                return false;
            }

            m_current = next;
            m_preview.Show(m_block, next);
            return true;
        }

        // Exits through a fitting door as soon as the preview cell touches it (DEC-164, DEC-197): no push, and
        // no drawn line-up or distance, so a held block can never be left resting in front of its own gate.
        // The view slides the block into line and on through the gate.
        // When a corner block touches two, the side the pointer pushes toward most wins; a tie goes to X.
        private bool TryExitOnContact(float desiredX, float desiredY, out GameCommand exit)
        {
            exit = default;
            float pushX = desiredX - m_current.X;
            float pushY = desiredY - m_current.Y;
            bool found = false;
            Side best = Side.Top;
            float bestPush = 0f;
            for (int i = 0; i < s_contactOrder.Length; i++)
            {
                Side side = s_contactOrder[i];

                // An exit leaves along an allowed axis only, as MoveBlockHandler requires. ExitRules.Evaluate
                // checks flushness itself, so no separate IsFlush call is needed here.
                AxisMask exitAxis = SideMath.GetExitAxis(side);
                if ((m_axes & exitAxis) == 0
                    || ExitRules.Evaluate(m_preview, m_hooks, m_block, m_current, side, out ExitCandidate _) != ExitVerdict.Fits)
                {
                    continue;
                }

                Cell outward = SideMath.GetOutward(side);
                float push = outward.X * pushX + outward.Y * pushY;
                if (!found || push > bestPush)
                {
                    found = true;
                    best = side;
                    bestPush = push;
                }
            }

            if (!found)
            {
                return false;
            }

            // The executor commits the exit in this command phase (RULE-13), so the press owns nothing more.
            exit = GameCommand.MoveAndExit(m_block, m_start, m_current, best);
            m_consumed = true;
            m_preview.Clear();
            return true;
        }

        private void Clear()
        {
            m_active = false;
            m_consumed = false;
            m_block = k_noBlock;
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }

        // Half away from zero, so the preview origin changes exactly when the offset reaches +-0.5 (DEC-112).
        private static int RoundHalfAwayFromZero(float value)
        {
            return value >= 0f ? (int)Math.Floor(value + 0.5) : -(int)Math.Floor(-value + 0.5);
        }
    }
}
