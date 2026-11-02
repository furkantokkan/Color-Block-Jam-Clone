using System;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Barrier's attempt state: the attempt's own rule, which alone flips it; presentation only reads it.</summary>
    public sealed class BarrierEffectState : IBlockEffectState
    {
        public BarrierEffectState(BarrierRule rule) { Rule = rule ?? throw new ArgumentNullException(nameof(rule)); }
        public string TypeId => "barrier";
        public BarrierRule Rule { get; }
    }
}
