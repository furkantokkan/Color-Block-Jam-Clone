using System;
using System.Collections.Generic;
using ColorBlockJam.Haptics;
using ColorBlockJam.Player;
using ColorBlockJam.UI.Haptics;
using global::Haptics;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// Proves DEC-156 and DEC-186: the Settings Vibration toggle gates every haptic through the player's
    /// IVibrationToggle, turning it off stops playback, every moment has a style, and a button vibrates only through
    /// its haptic USS class while enabled.
    /// </summary>
    public sealed class HapticsTests
    {
        private FakePlayerSettings m_settings;
        private RecordingHapticOutput m_output;
        private HapticsService m_haptics;

        [SetUp]
        public void SetUp()
        {
            m_settings = new FakePlayerSettings(music: true, sound: true, vibration: true);
            m_output = new RecordingHapticOutput();
            m_haptics = new HapticsService(new PlayerVibrationToggle(m_settings), m_output);
        }

        [TearDown]
        public void TearDown()
        {
            m_haptics.Dispose();
            m_settings.Dispose();
        }

        [Test]
        public void Play_BlockTouchWithVibrationOn_UsesLightStyle()
        {
            m_haptics.Play(HapticFeedback.BlockTouch);

            Assert.That(HapticStyles.For(HapticFeedback.BlockTouch), Is.EqualTo(HapticStyle.Light));
            Assert.That(m_output.Played, Is.EqualTo(new[] { HapticStyle.Light }));
        }

        [Test]
        public void EveryMoment_HasAStyle()
        {
            foreach (HapticFeedback feedback in Enum.GetValues(typeof(HapticFeedback)))
            {
                Assert.That(HapticStyles.For(feedback), Is.Not.EqualTo(HapticStyle.None), feedback.ToString());
            }
        }

        [Test]
        public void Play_UnknownGameMoment_PlaysNothing()
        {
            m_haptics.Play((HapticFeedback)int.MaxValue);

            Assert.That(m_output.Played, Is.Empty);
        }

        [Test]
        public void Create_WithNoToggle_RejectsTheMissingDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new HapticsService(null, m_output));
        }

        [Test]
        public void Create_WithNoOutput_RejectsTheMissingDependency()
        {
            Assert.Throws<ArgumentNullException>(() => new HapticsService(new PlayerVibrationToggle(m_settings), null));
        }

        [Test]
        public void Play_WithVibrationOff_PlaysNothing()
        {
            m_settings.Property(PlayerSettingId.Vibration).Value = false;

            m_haptics.Play(HapticFeedback.BlockTouch);

            Assert.That(m_output.Played, Is.Empty);
        }

        [Test]
        public void TurningVibrationOff_StopsPlaybackOnce_AndOnConfirmsWithATap()
        {
            Assert.That(m_output.StopCount, Is.EqualTo(0), "construction stops nothing");
            Assert.That(m_output.Played, Is.Empty, "construction plays nothing");

            m_settings.Property(PlayerSettingId.Vibration).Value = false;
            m_settings.Property(PlayerSettingId.Vibration).Value = true;
            m_haptics.Play(HapticFeedback.BlockTouch);

            Assert.That(m_output.StopCount, Is.EqualTo(1));
            Assert.That(m_output.Played, Is.EqualTo(new[]
            {
                HapticStyles.For(HapticFeedback.ButtonTap), HapticStyles.For(HapticFeedback.BlockTouch)
            }), "turning on taps once, then moments play");
        }

        [Test]
        public void Dispose_StopsPlayback_AndEndsTheSubscription()
        {
            m_haptics.Dispose();
            m_settings.Property(PlayerSettingId.Vibration).Value = false;

            Assert.That(m_output.StopCount, Is.EqualTo(1), "only the Dispose stop, no later setting stop");
        }

        [Test]
        public void ButtonClick_PlaysTheNearestMarkedClass()
        {
            VisualElement root = new VisualElement();
            Button play = AddButton(root, ButtonHaptics.ConfirmClass);
            Button close = AddButton(root, ButtonHaptics.TapClass);
            Label playIcon = new Label();
            play.Add(playIcon);
            ButtonHaptics buttons = new ButtonHaptics(m_haptics);
            buttons.Attach(root);

            Assert.That(buttons.PlayFor(playIcon), Is.True, "a child of a marked button counts");
            Assert.That(buttons.PlayFor(close), Is.True);

            Assert.That(m_output.Played, Is.EqualTo(new[]
            {
                HapticStyles.For(HapticFeedback.ButtonConfirm), HapticStyles.For(HapticFeedback.ButtonTap)
            }));
            buttons.Detach();
        }

        [Test]
        public void ButtonClick_UnmarkedOrDisabled_PlaysNothing()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList(ButtonHaptics.TapClass);
            VisualElement screen = new VisualElement();
            root.Add(screen);
            Button plain = AddButton(screen, null);
            Button buy = AddButton(screen, ButtonHaptics.ConfirmClass);
            buy.SetEnabled(false);
            ButtonHaptics buttons = new ButtonHaptics(m_haptics);
            buttons.Attach(screen);

            Assert.That(buttons.PlayFor(plain), Is.False, "the walk stops at the attached root");
            Assert.That(buttons.PlayFor(buy), Is.False, "a disabled button stays silent");
            Assert.That(m_output.Played, Is.Empty);
            buttons.Detach();
        }

        private static Button AddButton(VisualElement parent, string hapticClass)
        {
            Button button = new Button();
            if (hapticClass != null)
            {
                button.AddToClassList(hapticClass);
            }

            parent.Add(button);
            return button;
        }

        private sealed class RecordingHapticOutput : IHapticOutput
        {
            public List<HapticStyle> Played { get; } = new List<HapticStyle>();

            public int StopCount { get; private set; }

            public void Play(HapticStyle style)
            {
                Played.Add(style);
            }

            public void Stop()
            {
                StopCount++;
            }
        }
    }
}
