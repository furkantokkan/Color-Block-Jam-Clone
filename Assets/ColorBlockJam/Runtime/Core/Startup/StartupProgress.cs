using UnityEngine;

namespace ColorBlockJam.Runtime.Startup
{
    /// <summary>
    /// Maps the three startup phases onto one 0..1 loading bar: the project scope wait, which reports no measurable
    /// progress, then the Hub scene load, which does, then Hub's build of Home and of the level it prepares behind it,
    /// which does not (DEC-140, DEC-199).
    /// </summary>
    public static class StartupProgress
    {
        // Adjustable UX values: the project wait eases toward 60 % of the bar (the Hub load fills the real rest) and
        // covers about a third of it after 1 s, so a slow device never shows a frozen bar.
        private const float k_projectShare = 0.6f;
        private const float k_projectTimeConstant = 1.2f;

        /// <summary>Where the Hub load leaves the bar; the rest eases while Hub builds and prepares its level.</summary>
        public const float HubLoadEnd = 0.85f;

        // The build phase eases toward, and stays short of, the full bar: about 86 % of its share after 1 s.
        private const float k_buildReach = 0.95f;
        private const float k_buildTimeConstant = 0.5f;

        /// <summary>
        /// Bar value while the project scope loads (level JSON, shapes, player save). Its ReadyTask is one task with
        /// no progress of its own, so the value can depend only on the time waited.
        /// Contract: 0 at zero or negative time, never decreases as time grows, and stays below 1 however long the
        /// wait takes, because the Hub load still has to fill the rest of the bar.
        /// </summary>
        public static float ForProjectWait(float elapsedSeconds)
        {
            if (elapsedSeconds <= 0f)
            {
                return 0f;
            }

            return k_projectShare * (1f - Mathf.Exp(-elapsedSeconds / k_projectTimeConstant));
        }

        /// <summary>
        /// Bar value during the Hub load: fills from <paramref name="reached"/> up to <see cref="HubLoadEnd"/> in
        /// proportion to the scene load progress, so the bar never jumps backwards between phases.
        /// </summary>
        public static float ForHubLoad(float reached, float hubLoadProgress)
        {
            float start = Mathf.Clamp01(reached);
            float end = Mathf.Max(start, HubLoadEnd);
            return start + (end - start) * Mathf.Clamp01(hubLoadProgress);
        }

        /// <summary>
        /// Bar value while Hub builds Home and prepares the level it offers (DEC-199). That build reports no progress, so
        /// the value depends only on the time waited: it starts at <paramref name="reached"/>, never decreases and never
        /// reaches 1; the loader fills the bar once Hub is ready.
        /// </summary>
        public static float ForSceneBuild(float reached, float elapsedSeconds)
        {
            float start = Mathf.Clamp01(reached);
            if (elapsedSeconds <= 0f)
            {
                return start;
            }

            return start + (1f - start) * k_buildReach * (1f - Mathf.Exp(-elapsedSeconds / k_buildTimeConstant));
        }
    }
}
