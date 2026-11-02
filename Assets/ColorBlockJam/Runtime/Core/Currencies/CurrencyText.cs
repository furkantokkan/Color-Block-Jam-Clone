using Cysharp.Text;

namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// Formats a currency amount compactly for any screen: below 1,000 as is, then three significant digits with
    /// a k, m or b suffix, truncated so a value never rounds up across a unit (design 7, AC-1.8). It lives with the
    /// currency definitions so Hub, shared popups and the Game HUD share one rule (DEC-137).
    /// </summary>
    public static class CurrencyText
    {
        private const int k_thousand = 1000;
        private const int k_million = 1000000;
        private const int k_billion = 1000000000;

        /// <summary>
        /// 0 -> "0", 950 -> "950", 1520 -> "1.52k", 12500 -> "12.5k", 125000 -> "125k",
        /// 1520000 -> "1.52m", 999999 -> "999k". Trailing zero decimals are dropped (1000 -> "1k").
        /// </summary>
        public static string FormatCompact(int value)
        {
            if (value < k_thousand)
            {
                return ZString.Concat(value);
            }

            int unit;
            char suffix;
            if (value >= k_billion)
            {
                unit = k_billion;
                suffix = 'b';
            }
            else if (value >= k_million)
            {
                unit = k_million;
                suffix = 'm';
            }
            else
            {
                unit = k_thousand;
                suffix = 'k';
            }

            int whole = value / unit;
            if (whole >= 100)
            {
                return ZString.Concat(whole, suffix);
            }

            if (whole >= 10)
            {
                int tenths = value / (unit / 10) % 10;
                return tenths == 0
                    ? ZString.Concat(whole, suffix)
                    : ZString.Concat(whole, '.', tenths, suffix);
            }

            int hundredths = value / (unit / 100) % 100;
            if (hundredths == 0)
            {
                return ZString.Concat(whole, suffix);
            }

            if (hundredths % 10 == 0)
            {
                return ZString.Concat(whole, '.', hundredths / 10, suffix);
            }

            return hundredths < 10
                ? ZString.Concat(whole, ".0", hundredths, suffix)
                : ZString.Concat(whole, '.', hundredths, suffix);
        }
    }
}
