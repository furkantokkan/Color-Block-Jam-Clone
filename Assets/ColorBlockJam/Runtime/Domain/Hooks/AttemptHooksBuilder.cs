using System;
using System.Collections.Generic;

namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Collects hooks in registration order while an attempt is built on Home and freezes them into
    /// AttemptHooks. The block effect modules (IceEffectModule, ArrowEffectModule, BarrierEffectModule,
    /// BoosterRestrictionHooks) register through it (DEC-72, DEC-158); it is never used during play, so
    /// its lists may allocate.
    /// </summary>
    public sealed class AttemptHooksBuilder
    {
        private readonly List<IDragGate> m_dragGates = new List<IDragGate>();
        private readonly List<IAxisConstraint> m_axisConstraints = new List<IAxisConstraint>();
        private readonly List<ICellGate> m_cellGates = new List<ICellGate>();
        private readonly List<IExitGate> m_exitGates = new List<IExitGate>();
        private readonly List<IBoardEventListener> m_listeners = new List<IBoardEventListener>();
        private readonly List<IBoosterTargetGate> m_boosterTargetGates = new List<IBoosterTargetGate>();

        public AttemptHooksBuilder Add(IDragGate gate)
        {
            m_dragGates.Add(gate ?? throw new ArgumentNullException(nameof(gate)));
            return this;
        }

        public AttemptHooksBuilder Add(IAxisConstraint constraint)
        {
            m_axisConstraints.Add(constraint ?? throw new ArgumentNullException(nameof(constraint)));
            return this;
        }

        public AttemptHooksBuilder Add(ICellGate gate)
        {
            m_cellGates.Add(gate ?? throw new ArgumentNullException(nameof(gate)));
            return this;
        }

        public AttemptHooksBuilder Add(IExitGate gate)
        {
            m_exitGates.Add(gate ?? throw new ArgumentNullException(nameof(gate)));
            return this;
        }

        public AttemptHooksBuilder Add(IBoardEventListener listener)
        {
            m_listeners.Add(listener ?? throw new ArgumentNullException(nameof(listener)));
            return this;
        }

        public AttemptHooksBuilder Add(IBoosterTargetGate gate)
        {
            m_boosterTargetGates.Add(gate ?? throw new ArgumentNullException(nameof(gate)));
            return this;
        }

        public AttemptHooks Build()
        {
            return new AttemptHooks(m_dragGates.ToArray(), m_axisConstraints.ToArray(), m_cellGates.ToArray(),
                m_exitGates.ToArray(), m_listeners.ToArray(), m_boosterTargetGates.ToArray());
        }
    }
}
