using System;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// OBJ-05 registration: the level's barriers read on Home, a fresh cell gate and clear listener per attempt, and the
    /// view that moves the play area's barrier pieces. It is listed after the block effects (DEC-MECH-3: blocks before
    /// board objects) and takes the scene's EnvironmentView as IceEffectModule takes its material.
    /// </summary>
    public sealed class BarrierEffectModule : IBlockEffectModule
    {
        private readonly EnvironmentView m_environment;

        /// <param name="environment">The scene's play-area view, which shows the barriers this module's view moves.</param>
        public BarrierEffectModule(EnvironmentView environment)
        {
            if (environment == null) throw new ArgumentNullException(nameof(environment));
            m_environment = environment;
        }

        public string TypeId => "barrier";

        public IBlockEffectLevel Read(LevelData authored) => BarrierEffectLevel.Create(authored);

        public IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks)
        {
            BarrierEffectLevel barriers = level as BarrierEffectLevel
                ?? throw new ArgumentException("Barrier needs its own typed level metadata.", nameof(level));
            if (hooks == null) throw new ArgumentNullException(nameof(hooks));
            // One rule per attempt, so Restart returns every barrier to its start state.
            BarrierRule rule = barriers.CreateRule();
            if (rule.BarrierCount > 0)
            {
                hooks.Add((ICellGate)rule);
                hooks.Add((IBoardEventListener)rule);
            }

            return new BarrierEffectState(rule);
        }

        public IBlockEffectView CreateView(BlockEffectViewContext context) => new BarrierEffectView(m_environment);
    }
}
