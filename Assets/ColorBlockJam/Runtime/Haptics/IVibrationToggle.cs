using Onity.Reactive;

namespace ColorBlockJam.Haptics
{
    /// <summary>
    /// The player's Vibration on/off switch that every haptic obeys. The haptics domain owns this port; the player data
    /// side implements it, so haptics never depend on how settings are stored (DEC-186).
    /// </summary>
    public interface IVibrationToggle
    {
        /// <summary>The committed setting: a toggle whose save failed never changes it.</summary>
        IReadOnlyReactiveProperty<bool> IsOn { get; }
    }
}
