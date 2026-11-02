namespace Haptics
{
    /// <summary>
    /// A platform-neutral vibration: each device output turns it into its own primitive. The values are
    /// fixed because the iOS native code switches on them.
    /// </summary>
    public enum HapticStyle
    {
        None = 0,

        /// <summary>A light selection tick.</summary>
        Selection = 1,

        /// <summary>A faint, short impact.</summary>
        Light = 2,

        /// <summary>A medium impact.</summary>
        Medium = 3,

        /// <summary>A strong, short, crisp impact.</summary>
        Rigid = 4,

        /// <summary>The strongest, longer impact.</summary>
        Heavy = 5,

        /// <summary>A soft tick followed by a strong one.</summary>
        Success = 6,

        /// <summary>Three pulses.</summary>
        Failure = 7
    }
}
