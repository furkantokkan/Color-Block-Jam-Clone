using System;

namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The whole seconds a clock shows (B-9): the remaining time rounded up, so a clock shows 0 only
    /// when it has run out. Views use it to pick a prebuilt string and change text only when the value
    /// changes; it allocates nothing.
    /// </summary>
    public static class DisplaySeconds
    {
        // Frame deltas leave float drift, such as 6.00001 after four seconds from 10; within this
        // margin of a whole second the clock already shows that second.
        private const float k_tolerance = 1e-4f;

        public static int Calculate(float remaining)
        {
            if (!(remaining > k_tolerance))
            {
                return 0;
            }

            return (int)Math.Ceiling(remaining - k_tolerance);
        }
    }
}
