using System;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// Builds registered effect hooks atomically, without effect-specific branches, then the booster restriction gate
    /// from the matrix (DEC-171).
    /// </summary>
    public sealed class EffectAttemptHookSource : IAttemptHookSource
    {
        private readonly BlockEffectContextSource m_source;
        private readonly BoosterRestrictionMatrix m_restrictions;

        /// <summary>Hooks with no booster restrictions.</summary>
        public EffectAttemptHookSource(BlockEffectContextSource source)
            : this(source, null)
        {
        }

        /// <param name="restrictions">The matrix read on every attempt; null restricts nothing. An invalid matrix throws here.</param>
        public EffectAttemptHookSource(BlockEffectContextSource source, BoosterRestrictionMatrix restrictions)
        {
            m_source = source ?? throw new ArgumentNullException(nameof(source));
            if (restrictions != null && !restrictions.Validate(out string error))
            {
                throw new ArgumentException($"Booster restriction matrix '{restrictions.name}': {error}", nameof(restrictions));
            }

            m_restrictions = restrictions;
        }

        public AttemptHooks Create(AttemptLevel level)
        {
            int generation = m_source.BeginAttempt(level);
            BlockEffectContext context = m_source.Context;
            AttemptHooksBuilder hooks = new AttemptHooksBuilder();
            IBlockEffectState[] states = new IBlockEffectState[m_source.Count];
            for (int index = 0; index < states.Length; index++)
            {
                IBlockEffectModule module = m_source.GetModule(index);
                IBlockEffectState state = module.AddHooks(context.GetAt(index), hooks);
                if (state == null || state.TypeId != module.TypeId)
                    throw new InvalidOperationException("An effect module returned mismatched attempt state.");
                states[index] = state;
            }
            BoosterRestrictionHooks.Add(m_restrictions, states, hooks);
            BlockEffectAttempt attempt = new BlockEffectAttempt(context, generation, hooks.Build(), states);
            m_source.Publish(attempt);
            return attempt.Hooks;
        }
    }
}
