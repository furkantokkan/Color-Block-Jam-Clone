namespace ColorBlockJam.Haptics
{
    /// <summary>Plays one short vibration for a game moment when the player's Vibration setting is on (DEC-156).</summary>
    public interface IHaptics
    {
        void Play(HapticFeedback feedback);
    }
}
