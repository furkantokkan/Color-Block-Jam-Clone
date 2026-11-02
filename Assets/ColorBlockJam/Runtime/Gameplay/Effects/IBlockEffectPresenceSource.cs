using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// An effect state that can say which blocks carry its effect, so the booster restriction matrix can apply to it
    /// (DEC-171). Presence is null when the attempt has no block with the effect.
    /// </summary>
    public interface IBlockEffectPresenceSource
    {
        IBlockEffectPresence Presence { get; }
    }
}
