using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// The hook arrays of one attempt (PRD 8.3, XP-1..XP-6), fixed when the attempt is built and
    /// run with plain for loops, so a check or a publish allocates nothing. With no registrations
    /// every array is empty and the core rules run unchanged (AC-EXT-10).
    /// </summary>
    public sealed class AttemptHooks
    {
        private readonly IDragGate[] m_dragGates;
        private readonly IAxisConstraint[] m_axisConstraints;
        private readonly ICellGate[] m_cellGates;
        private readonly IExitGate[] m_exitGates;
        private readonly IBoardEventListener[] m_listeners;
        private readonly IBoosterTargetGate[] m_boosterTargetGates;

        internal AttemptHooks(IDragGate[] dragGates, IAxisConstraint[] axisConstraints, ICellGate[] cellGates,
            IExitGate[] exitGates, IBoardEventListener[] listeners, IBoosterTargetGate[] boosterTargetGates)
        {
            m_dragGates = dragGates;
            m_axisConstraints = axisConstraints;
            m_cellGates = cellGates;
            m_exitGates = exitGates;
            m_listeners = listeners;
            m_boosterTargetGates = boosterTargetGates;
        }

        /// <summary>Hooks with no registrations: the plain colour-door rules.</summary>
        public static AttemptHooks CreateEmpty()
        {
            return new AttemptHooks(Array.Empty<IDragGate>(), Array.Empty<IAxisConstraint>(),
                Array.Empty<ICellGate>(), Array.Empty<IExitGate>(), Array.Empty<IBoardEventListener>(),
                Array.Empty<IBoosterTargetGate>());
        }

        public bool AllowsDrag(IBoardReader board, int block)
        {
            for (int i = 0; i < m_dragGates.Length; i++)
            {
                if (!m_dragGates[i].AllowsDrag(board, block))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Starts from both axes and intersects every constraint.</summary>
        public AxisMask GetAxes(IBoardReader board, int block)
        {
            AxisMask axes = AxisMask.Both;
            for (int i = 0; i < m_axisConstraints.Length; i++)
            {
                axes &= m_axisConstraints[i].GetAxes(board, block);
            }

            return axes;
        }

        public bool AllowsCell(IBoardReader board, int block, Cell cell)
        {
            for (int i = 0; i < m_cellGates.Length; i++)
            {
                if (!m_cellGates[i].AllowsCell(board, block, cell))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Every gate must allow the exit; the outcome starts as Leave and any gate may set Stay.</summary>
        public bool AllowsExit(IBoardReader board, in ExitQuery query, out ExitOutcome outcome)
        {
            outcome = ExitOutcome.Leave;
            for (int i = 0; i < m_exitGates.Length; i++)
            {
                if (!m_exitGates[i].AllowsExit(board, query, ref outcome))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Every gate must allow a board booster of this kind on the block (XP-6, DEC-171).</summary>
        public bool AllowsBoosterTarget(IBoardReader board, CommandKind kind, int block)
        {
            for (int i = 0; i < m_boosterTargetGates.Length; i++)
            {
                if (!m_boosterTargetGates[i].AllowsBoosterTarget(board, kind, block))
                {
                    return false;
                }
            }

            return true;
        }

        public void Publish(in BlockExitedEvent e)
        {
            for (int i = 0; i < m_listeners.Length; i++)
            {
                m_listeners[i].OnBlockExited(e);
            }
        }

        public void Publish(in BlockRemovedEvent e)
        {
            for (int i = 0; i < m_listeners.Length; i++)
            {
                m_listeners[i].OnBlockRemoved(e);
            }
        }

        public void Publish(in MoveCommittedEvent e)
        {
            for (int i = 0; i < m_listeners.Length; i++)
            {
                m_listeners[i].OnMoveCommitted(e);
            }
        }

        public void Publish(in FootprintChangedEvent e)
        {
            for (int i = 0; i < m_listeners.Length; i++)
            {
                m_listeners[i].OnFootprintChanged(e);
            }
        }
    }
}
