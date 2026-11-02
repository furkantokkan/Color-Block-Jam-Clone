#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;

namespace Haptics
{
    /// <summary>
    /// iOS side of <see cref="DeviceHapticOutput"/>: forwards a style to iOS/Haptics.mm for UIKit feedback.
    /// </summary>
    internal static class IosFeedbackHaptics
    {
        public static void Play(HapticStyle style)
        {
            Haptics_Play((int)style);
        }

        [DllImport("__Internal")]
        private static extern void Haptics_Play(int style);
    }
}
#endif
