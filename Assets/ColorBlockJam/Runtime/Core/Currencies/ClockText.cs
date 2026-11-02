using Cysharp.Text;

namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// Formats whole seconds as "mm:ss" (RULE-21), the one shared rule behind every clock and countdown so a format
    /// change is made once. It lives with the currency formatters so Player, Hub and Gameplay share it (DEC-137
    /// pattern, alongside CurrencyText).
    /// </summary>
    public static class ClockText
    {
        /// <summary>0 -> "00:00", 65 -> "01:05", negative seconds clamp to 0.</summary>
        public static string Format(long seconds)
        {
            if (seconds < 0)
            {
                seconds = 0;
            }

            return ZString.Format("{0:00}:{1:00}", seconds / 60, seconds % 60);
        }
    }
}
