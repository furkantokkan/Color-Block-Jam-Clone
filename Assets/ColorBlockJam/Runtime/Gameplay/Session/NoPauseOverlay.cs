namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// The pause overlay of a scene without the Game HUD (tests, HUD-less scenes): it resumes a background
    /// pause on foreground when configured to (DEC-116); the HUD binds PopupPauseOverlay instead (DEC-154).
    /// </summary>
    public sealed class NoPauseOverlay : IPauseOverlay
    {
        public NoPauseOverlay(bool resumesOnForeground)
        {
            ResumesOnForeground = resumesOnForeground;
        }

        public bool ResumesOnForeground { get; }
    }
}
