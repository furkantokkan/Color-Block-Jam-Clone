namespace ColorBlockJam.Presentation.Board
{
    /// <summary>
    /// Tuning for the gate-exit cube fragments, adapted from the CBJ Template's Block Destroy Particle
    /// (visual reference 15, DEC-131). Lengths are BoardRoot-local board units, where one cell spans one
    /// unit. ExitFragmentEmitter reads them once while it builds on Home.
    /// </summary>
    public static class ExitFragmentSettings
    {
        /// <summary>How long one fragment lives, in seconds (the reference's 0.52).</summary>
        public const float k_lifetimeSeconds = 0.52f;

        /// <summary>Launch speed away from the door, in units per second, before damping (the reference's 8.99).</summary>
        public const float k_speed = 9f;

        /// <summary>
        /// The share of the speed above the limit that each simulation step removes. With the limit at
        /// zero, fragments slow to a stop about two cells beyond the door (the reference's 0.08).
        /// </summary>
        public const float k_dampen = 0.08f;

        /// <summary>Smallest fragment edge; each fragment picks its size between this and k_maxSize (the reference's 0.1 to 0.4).</summary>
        public const float k_minSize = 0.1f;

        public const float k_maxSize = 0.4f;

        /// <summary>Fragments per second for each lane the exiting block covers along the door (the reference emits 40 per lane).</summary>
        public const float k_ratePerLane = 40f;

        /// <summary>Largest spin around each axis, in degrees per second; each fragment tumbles at its own rate.</summary>
        public const float k_spinDegreesPerSecond = 90f;

        /// <summary>The emitting box is as tall as the block body and thin across the door line.</summary>
        public const float k_boxHeight = 0.45f;

        public const float k_boxDepth = 0.2f;

        /// <summary>
        /// Live fragments one emitter can hold. A six-lane block at 40 per lane and 0.52 s keeps at most
        /// 125 alive; above the cap Unity simply emits no more.
        /// </summary>
        public const int k_maxParticles = 160;

        /// <summary>
        /// Exits whose fragments can show at the same time. An emitter is busy for the exit motion (at most
        /// 0.6 s) plus one lifetime, about 1.1 s, so a busy pool skips the effect rather than growing.
        /// </summary>
        public const int k_maxEmitters = 4;
    }
}
