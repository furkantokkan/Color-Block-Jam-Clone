namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>
    /// The Android Back button (Escape on desktop) of the gameplay input map, polled once per Playing tick (B-12).
    /// Reading it allocates nothing; it reports false while the map is disabled.
    /// </summary>
    public interface IBackButtonSource
    {
        /// <summary>True only in the frame Back was pressed.</summary>
        bool WasBackPressed();
    }
}
