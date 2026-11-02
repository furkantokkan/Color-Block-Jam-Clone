using System;
using global::Haptics;
using Onity.Reactive;

namespace ColorBlockJam.Haptics
{
    /// <summary>
    /// Plays haptic moments only while the Vibration setting is on, so the Settings toggle switches every vibration in
    /// the game from one place (DEC-156, DEC-186).
    /// </summary>
    /// <remarks>
    /// Owner: the project container (AsSingle), which disposes it at the app scope's end. Its one subscription watches
    /// the setting: turning it off stops a playing pattern, and turning it on plays one ButtonTap so the Vibration
    /// switch itself confirms the change (its own button tap ran while the setting was still off and stayed silent).
    /// Dispose ends that subscription and stops playback. The output is a separate container singleton with its own lifetime.
    /// </remarks>
    public sealed class HapticsService : IHaptics, IDisposable
    {
        private readonly IHapticOutput m_output;
        private readonly IReadOnlyReactiveProperty<bool> m_isOn;
        private readonly IDisposable m_subscription;

        public HapticsService(IVibrationToggle toggle, IHapticOutput output)
        {
            if (toggle == null)
            {
                throw new ArgumentNullException(nameof(toggle));
            }

            m_output = output ?? throw new ArgumentNullException(nameof(output));
            m_isOn = toggle.IsOn;
            m_subscription = m_isOn.Subscribe(OnToggleChanged, emitCurrentValue: false);
        }

        public void Play(HapticFeedback feedback)
        {
            HapticStyle style = HapticStyles.For(feedback);
            if (m_isOn.Value && style != HapticStyle.None)
            {
                m_output.Play(style);
            }
        }

        public void Dispose()
        {
            m_subscription.Dispose();
            m_output.Stop();
        }

        private void OnToggleChanged(bool isOn)
        {
            if (isOn)
            {
                m_output.Play(HapticStyles.For(HapticFeedback.ButtonTap));
            }
            else
            {
                m_output.Stop();
            }
        }
    }
}
