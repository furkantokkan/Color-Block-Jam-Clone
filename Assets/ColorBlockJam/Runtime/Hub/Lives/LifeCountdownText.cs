using Cysharp.Text;

namespace ColorBlockJam.Hub.Lives
{
    /// <summary>
    /// Formats the time to the next life as "mm:ss" for the Home life holder and the refill popup (DEC-174). Hub text
    /// outside any gameplay tick; callers format only when the shown second changes.
    /// </summary>
    public static class LifeCountdownText
    {
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
