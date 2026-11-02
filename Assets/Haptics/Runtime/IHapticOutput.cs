namespace Haptics
{
    /// <summary>Plays one platform-neutral haptic style or stops cancellable playback.</summary>
    public interface IHapticOutput
    {
        void Play(HapticStyle style);

        void Stop();
    }
}
