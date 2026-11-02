using System;
using System.Globalization;
using ColorBlockJam.Runtime.Currencies;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The HUD's prebuilt strings (B-9): "mm:ss" clock texts and whole numbers, built on Home so a Playing tick only
    /// picks one by index and allocates nothing (Performance Rule 8; Gameplay has no ZString). Values outside a table
    /// clamp to its ends instead of throwing.
    /// </summary>
    /// <remarks>Owner: the gameplay HUD, which reserves the clock table for each level on Home.</remarks>
    public sealed class HudText
    {
        private readonly string[] m_numbers;
        private string[] m_clock = Array.Empty<string>();

        /// <param name="maxNumber">The largest number the HUD shows, such as the booster stock limit.</param>
        public HudText(int maxNumber)
        {
            if (maxNumber < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumber));
            }

            m_numbers = new string[maxNumber + 1];
            for (int value = 0; value < m_numbers.Length; value++)
            {
                m_numbers[value] = value.ToString(CultureInfo.InvariantCulture);
            }

            ReserveClock(0);
        }

        /// <summary>The largest number with its own string.</summary>
        public int MaxNumber => m_numbers.Length - 1;

        /// <summary>The largest clock second with its own string.</summary>
        public int MaxClockSeconds => m_clock.Length - 1;

        /// <summary>
        /// Builds the clock strings from 0 up to maxSeconds once, on Home; a table that already covers it is kept, so a
        /// Restart or a shorter level builds nothing.
        /// </summary>
        public void ReserveClock(int maxSeconds)
        {
            if (maxSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSeconds));
            }

            if (maxSeconds < m_clock.Length)
            {
                return;
            }

            string[] clock = new string[maxSeconds + 1];
            for (int seconds = 0; seconds < clock.Length; seconds++)
            {
                clock[seconds] = FormatClock(seconds);
            }

            m_clock = clock;
        }

        /// <summary>The "mm:ss" text of whole seconds, such as "01:05".</summary>
        public string GetClock(int seconds)
        {
            return m_clock[Clamp(seconds, m_clock.Length - 1)];
        }

        public string GetNumber(int value)
        {
            return m_numbers[Clamp(value, m_numbers.Length - 1)];
        }

        /// <summary>
        /// Formats whole seconds as "mm:ss" ("02:55", reference ref-hud-top-bar-level29, RULE-21 as revised on
        /// 2026-09-28) through the shared <see cref="ClockText"/>; used only while building the table.
        /// </summary>
        public static string FormatClock(int seconds)
        {
            return ClockText.Format(seconds);
        }

        private static int Clamp(int value, int max)
        {
            return value < 0 ? 0 : value > max ? max : value;
        }
    }
}
