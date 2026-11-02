namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Decides when the black fade may lift after a scene change (DEC-198): the new scene has finished building, its
    /// first frames have settled, and a scene that never finishes cannot keep the screen black.
    /// </summary>
    public static class SceneRevealPolicy
    {
        /// <summary>
        /// True when the fade may start fading the new scene in. <see cref="ActiveSceneReadiness"/> calls it once per
        /// frame, before it waits for the next frame, and reveals on the first true.
        /// </summary>
        /// <param name="isSceneReady">
        /// The active scene's SceneContext finished its build: Home bound in Hub; the level pack, board, pools and HUD
        /// in Game. A failed or destroyed build counts as finished.
        /// </param>
        /// <param name="framesSinceReady">Frames started since readiness was first seen: 0 on that frame.</param>
        /// <param name="secondsWaited">Unscaled seconds since the scene change finished loading.</param>
        public static bool ShouldReveal(bool isSceneReady, int framesSinceReady, float secondsWaited)
        {
            // TODO: the reveal policy. Placeholder until then: reveal on the frame readiness is first seen.
            return isSceneReady;
        }
    }
}
