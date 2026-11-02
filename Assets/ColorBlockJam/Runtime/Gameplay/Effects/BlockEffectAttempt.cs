using System;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>A successfully built attempt: immutable registration order and fresh effect state.</summary>
    public sealed class BlockEffectAttempt
    {
        private readonly IBlockEffectState[] m_states;

        internal BlockEffectAttempt(BlockEffectContext context, int generation,
            AttemptHooks hooks, IBlockEffectState[] states)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Generation = generation;
            Hooks = hooks ?? throw new ArgumentNullException(nameof(hooks));
            m_states = (IBlockEffectState[])states.Clone();
        }

        public BlockEffectContext Context { get; }
        public int Generation { get; }
        public AttemptHooks Hooks { get; }
        public int Count => m_states.Length;
        public IBlockEffectState GetAt(int index) => m_states[index];
    }
}
