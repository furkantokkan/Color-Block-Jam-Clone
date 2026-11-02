using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Arrow's attempt state: it holds no changing values, because an Arrow block's axis is fixed for the attempt
    /// (DEC-MECH-4) and its hooks read it from the level metadata. Presence names the Arrow blocks for booster
    /// restrictions (DEC-171), or is null when the level has none.
    /// </summary>
    public sealed class ArrowEffectState : IBlockEffectState, IBlockEffectPresenceSource
    {
        public ArrowEffectState(IBlockEffectPresence presence)
        {
            Presence = presence;
        }

        public string TypeId => LevelEffectTypes.Arrow;

        public IBlockEffectPresence Presence { get; }
    }
}
