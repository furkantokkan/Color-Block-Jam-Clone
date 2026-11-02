using System;
using ColorBlockJam.Haptics;
using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>Gives the haptics domain the committed Vibration setting as its on/off switch (DEC-156, DEC-186).</summary>
    public sealed class PlayerVibrationToggle : IVibrationToggle
    {
        public PlayerVibrationToggle(IPlayerSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            IsOn = settings.Get(PlayerSettingId.Vibration);
        }

        public IReadOnlyReactiveProperty<bool> IsOn { get; }
    }
}
