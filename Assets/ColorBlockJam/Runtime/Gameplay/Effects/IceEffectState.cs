using System;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Ice's attempt rules; presentation reads counts without owning mutations.</summary>
    public sealed class IceEffectState : IBlockEffectState, IBlockEffectPresenceSource
    {
        public IceEffectState(IceRules rules) { Rules = rules ?? throw new ArgumentNullException(nameof(rules)); }
        public string TypeId => LevelEffectTypes.Ice;
        public IceRules Rules { get; }

        // A block carries Ice for booster restrictions only while frozen (DEC-171).
        public IBlockEffectPresence Presence => Rules;
    }
}
