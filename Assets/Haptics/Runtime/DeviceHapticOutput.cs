using System;

namespace Haptics
{
    /// <summary>
    /// Plays haptic styles through Android's Vibrator or iOS's UIKit feedback generators; other platforms stay silent.
    /// </summary>
    /// <remarks>
    /// The caller constructs and disposes this output on Unity's main thread. Android looks up the Vibrator and
    /// prebuilds its effects at construction, so a play costs one
    /// JNI call and no managed allocation. Nothing it starts outlives a play: every style is a one-shot pattern the
    /// OS ends by itself.
    /// </remarks>
    public sealed class DeviceHapticOutput : IHapticOutput, IDisposable
    {
        private bool m_disposed;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly AndroidVibratorHaptics m_android = AndroidVibratorHaptics.Create();
#endif

        public void Play(HapticStyle style)
        {
            if (m_disposed || style <= HapticStyle.None || style > HapticStyle.Failure)
            {
                return;
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            m_android?.Play(style);
#elif UNITY_IOS && !UNITY_EDITOR
            IosFeedbackHaptics.Play(style);
#endif
        }

        public void Stop()
        {
            if (m_disposed)
            {
                return;
            }

            // iOS feedback generators play one-shot effects that cannot be cancelled, so only Android stops.
#if UNITY_ANDROID && !UNITY_EDITOR
            m_android?.Stop();
#endif
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Stop();
            m_disposed = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            m_android?.Dispose();
#endif
        }
    }
}
