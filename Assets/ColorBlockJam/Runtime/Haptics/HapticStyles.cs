using global::Haptics;

namespace ColorBlockJam.Haptics
{
    /// <summary>The single table of which vibration style each game moment plays (DEC-156, DEC-186).</summary>
    /// <remarks>
    /// Kept from the Nice Vibrations presets DEC-156 chose, so the feel did not change when the plugin was replaced.
    /// Strength, weakest to strongest: Selection, Light, Medium, Rigid, Heavy; Success and Failure are patterns.
    /// CBJ Template reference: gate exit and booster hit Hard (0.8), booster press Medium (0.6).
    /// </remarks>
    public static class HapticStyles
    {
        public static HapticStyle For(HapticFeedback feedback)
        {
            // Adjustable defaults: a light tick for the frequent touch, a crisp hit per exit (several can follow each
            // other), the strongest for a booster hit, and distinct patterns for the two results.
            switch (feedback)
            {
                case HapticFeedback.BlockTouch:
                    return HapticStyle.Light;
                case HapticFeedback.GateExit:
                    return HapticStyle.Rigid;
                case HapticFeedback.BoosterApplied:
                    return HapticStyle.Heavy;
                case HapticFeedback.LevelWon:
                    return HapticStyle.Success;
                case HapticFeedback.LevelFailed:
                    return HapticStyle.Failure;
                case HapticFeedback.ButtonTap:
                    return HapticStyle.Selection;
                case HapticFeedback.ButtonConfirm:
                    return HapticStyle.Medium;
                default:
                    return HapticStyle.None;
            }
        }
    }
}
