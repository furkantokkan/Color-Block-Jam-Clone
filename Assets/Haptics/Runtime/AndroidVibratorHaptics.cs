#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using UnityEngine;

namespace Haptics
{
    /// <summary>
    /// Android side of <see cref="DeviceHapticOutput"/>: plays each <see cref="HapticStyle"/> through the Vibrator
    /// service with an effect built once.
    /// </summary>
    /// <remarks>
    /// <see cref="Create"/> runs once, on the main thread. It looks up the Vibrator, prebuilds one VibrationEffect per
    /// style (API 26+) and keeps them and the method ids as JNI references, so <see cref="Play"/> is one JNI call with
    /// a reused argument array and allocates nothing managed. The durations and amplitudes are fixed presets; a motor
    /// without amplitude control plays them at its default strength. API 22-25 plays the
    /// first pulse as a plain duration. A failed Java call (for example a missing VIBRATE permission) turns haptics off
    /// with one warning instead of throwing into the caller. Dispose deletes the global references.
    /// </remarks>
    internal sealed class AndroidVibratorHaptics : IDisposable
    {
        private const int k_effectApi = 26;

        // Per HapticStyle value: waveform timings in ms (off, on, off, on ...) and amplitudes 0-255 (0 = off).
        // Index 0 (None) is never played. Keep these indices aligned with HapticStyle.
        private static readonly long[][] s_timings =
        {
            new long[] { 0 },
            new long[] { 0, 40 },
            new long[] { 0, 40 },
            new long[] { 0, 80 },
            new long[] { 0, 40 },
            new long[] { 0, 160 },
            new long[] { 0, 40, 80, 60 },
            new long[] { 0, 60, 80, 60, 80, 60 }
        };

        private static readonly int[][] s_amplitudes =
        {
            new[] { 0 },
            new[] { 0, 120 },
            new[] { 0, 120 },
            new[] { 0, 120 },
            new[] { 0, 255 },
            new[] { 0, 255 },
            new[] { 0, 120, 0, 255 },
            new[] { 0, 255, 0, 191, 0, 128 }
        };

        private readonly IntPtr m_vibrator;
        private readonly IntPtr m_vibrateEffect;
        private readonly IntPtr m_vibrateMillis;
        private readonly IntPtr m_cancel;
        private readonly IntPtr[] m_effects;
        private readonly jvalue[] m_argument = new jvalue[1];
        private readonly jvalue[] m_noArguments = new jvalue[0];
        private bool m_failed;
        private bool m_disposed;

        private AndroidVibratorHaptics(IntPtr vibrator, IntPtr vibrateEffect, IntPtr vibrateMillis, IntPtr cancel,
            IntPtr[] effects)
        {
            m_vibrator = vibrator;
            m_vibrateEffect = vibrateEffect;
            m_vibrateMillis = vibrateMillis;
            m_cancel = cancel;
            m_effects = effects;
        }

        /// <summary>Returns null when the device has no vibrator or the lookup fails; haptics then stay silent.</summary>
        public static AndroidVibratorHaptics Create()
        {
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (AndroidJavaObject vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator"))
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    if (vibrator == null || !vibrator.Call<bool>("hasVibrator"))
                    {
                        return null;
                    }

                    IntPtr type = vibrator.GetRawClass();
                    IntPtr cancel = AndroidJNI.GetMethodID(type, "cancel", "()V");
                    if (version.GetStatic<int>("SDK_INT") >= k_effectApi)
                    {
                        IntPtr vibrateEffect = AndroidJNI.GetMethodID(type, "vibrate", "(Landroid/os/VibrationEffect;)V");
                        return new AndroidVibratorHaptics(AndroidJNI.NewGlobalRef(vibrator.GetRawObject()), vibrateEffect,
                            IntPtr.Zero, cancel, CreateEffects());
                    }

                    IntPtr vibrateMillis = AndroidJNI.GetMethodID(type, "vibrate", "(J)V");
                    return new AndroidVibratorHaptics(AndroidJNI.NewGlobalRef(vibrator.GetRawObject()), IntPtr.Zero,
                        vibrateMillis, cancel, null);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning($"Haptics are off: the Android Vibrator could not be set up ({error.Message}).");
                return null;
            }
        }

        public void Play(HapticStyle style)
        {
            int index = (int)style;
            if (m_failed || m_disposed || index <= 0 || index >= s_timings.Length)
            {
                return;
            }

            if (m_effects != null)
            {
                m_argument[0].l = m_effects[index];
                AndroidJNI.CallVoidMethod(m_vibrator, m_vibrateEffect, m_argument);
            }
            else
            {
                m_argument[0].j = s_timings[index][1];
                AndroidJNI.CallVoidMethod(m_vibrator, m_vibrateMillis, m_argument);
            }

            CheckFailure("vibrate");
        }

        public void Stop()
        {
            if (m_failed || m_disposed)
            {
                return;
            }

            AndroidJNI.CallVoidMethod(m_vibrator, m_cancel, m_noArguments);
            CheckFailure("cancel");
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            if (m_effects != null)
            {
                for (int i = 0; i < m_effects.Length; i++)
                {
                    if (m_effects[i] != IntPtr.Zero)
                    {
                        AndroidJNI.DeleteGlobalRef(m_effects[i]);
                    }
                }
            }

            AndroidJNI.DeleteGlobalRef(m_vibrator);
        }

        private static IntPtr[] CreateEffects()
        {
            var effects = new IntPtr[s_timings.Length];
            using (var type = new AndroidJavaClass("android.os.VibrationEffect"))
            {
                for (int style = 1; style < s_timings.Length; style++)
                {
                    using (AndroidJavaObject effect = type.CallStatic<AndroidJavaObject>("createWaveform",
                               s_timings[style], s_amplitudes[style], -1))
                    {
                        effects[style] = AndroidJNI.NewGlobalRef(effect.GetRawObject());
                    }
                }
            }

            return effects;
        }

        // A Java exception left pending would crash the next JNI call, so it is cleared here and haptics turn off.
        private void CheckFailure(string call)
        {
            IntPtr exception = AndroidJNI.ExceptionOccurred();
            if (exception == IntPtr.Zero)
            {
                return;
            }

            AndroidJNI.ExceptionClear();
            AndroidJNI.DeleteLocalRef(exception);
            m_failed = true;
            Debug.LogWarning($"Haptics are off: Vibrator.{call} failed on this device.");
        }
    }
}
#endif
