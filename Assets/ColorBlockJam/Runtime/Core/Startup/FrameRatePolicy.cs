using UnityEngine;

namespace ColorBlockJam.Runtime.Startup
{
    /// <summary>
    /// Sets the player's frame rate once, before the first scene loads, so every entry point (Init, direct Play of
    /// Hub or Game) runs at the 60 fps target of NFR-2 (DEC-28). Without it Android and iOS fall back to Unity's
    /// mobile default of 30 fps, where they ignore vSyncCount, and a dragged block visibly steps (DEC-183).
    /// </summary>
    public static class FrameRatePolicy
    {
        /// <summary>The frame rate the board and HUD are budgeted for (NFR-2, NFR-P4).</summary>
        public const int TargetFrameRate = 60;

        // Owner: the engine calls this once per player launch and domain reload. Stops: it only writes a setting.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            Application.targetFrameRate = SelectTargetFrameRate(Screen.currentResolution.refreshRateRatio.value);
        }

        /// <summary>
        /// Chooses the frame rate for a display that refreshes <paramref name="displayRefreshRate"/> times per second.
        /// Mobile players run at this rate; desktop and Editor runs with vSyncCount above 0 follow vsync instead.
        /// </summary>
        public static int SelectTargetFrameRate(double displayRefreshRate)
        {
            return TargetFrameRate;
        }
    }
}
