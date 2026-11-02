namespace ColorBlockJam.Domain.Picking
{
    /// <summary>A screen-space position in pixels, the Domain's stand-in for a Unity Vector2 (DEC-115).</summary>
    public readonly struct ScreenPoint
    {
        // Far enough that no real on-screen point can be nearer, so a projector can mark a point
        // behind the camera without depending on the device's screen resolution.
        private const float k_offscreen = 1e9f;

        /// <summary>The shared sentinel for a point that projects behind the camera (DEC-115).</summary>
        public static readonly ScreenPoint Offscreen = new ScreenPoint(k_offscreen, k_offscreen);

        public readonly float X;
        public readonly float Y;

        public ScreenPoint(float x, float y)
        {
            X = x;
            Y = y;
        }

        /// <summary>True for the <see cref="Offscreen"/> sentinel or any point at least as far away.</summary>
        public bool IsOffscreen => !(X < k_offscreen) || !(Y < k_offscreen);
    }
}
