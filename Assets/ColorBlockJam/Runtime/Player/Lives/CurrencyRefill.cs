using System;

namespace ColorBlockJam.Player.Lives
{
    /// <summary>
    /// Plain C# timed-refill rule for a currency such as Lives (DEC-174): below its maximum a balance gains one unit
    /// per full refill interval of UTC wall-clock time, keeping the partial progress towards the next unit; at the
    /// maximum no timer runs. It reads no clock, so the save service and the tests pass "now" explicitly.
    /// </summary>
    public static class CurrencyRefill
    {
        /// <summary>
        /// Advances a balance to <paramref name="nowUtcSeconds"/>. A balance below the maximum without a start (or
        /// with a start in the future, after the device clock moved back) starts refilling now.
        /// </summary>
        public static RefillState Advance(int amount, int maxAmount, int refillSeconds, bool hasStart,
            long startUtcSeconds, long nowUtcSeconds)
        {
            if (refillSeconds <= 0 || amount >= maxAmount)
            {
                return new RefillState(amount, false, 0);
            }

            if (!hasStart || startUtcSeconds > nowUtcSeconds)
            {
                return new RefillState(amount, true, nowUtcSeconds);
            }

            long gained = (nowUtcSeconds - startUtcSeconds) / refillSeconds;
            if (gained >= maxAmount - amount)
            {
                return new RefillState(maxAmount, false, 0);
            }

            return new RefillState(amount + (int)gained, true, startUtcSeconds + gained * refillSeconds);
        }

        /// <summary>Whole seconds until the next unit; 0 when nothing is refilling.</summary>
        public static long GetSecondsUntilNext(RefillState state, int refillSeconds, long nowUtcSeconds)
        {
            if (!state.IsRefilling || refillSeconds <= 0)
            {
                return 0;
            }

            return Math.Max(0, state.StartUtcSeconds + refillSeconds - nowUtcSeconds);
        }
    }
}
