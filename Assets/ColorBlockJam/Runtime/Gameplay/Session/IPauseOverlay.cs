namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>What the pause presentation decides when the app returns from the background.</summary>
    public interface IPauseOverlay
    {
        /// <summary>True resumes a background pause on foreground; false keeps the session Paused.</summary>
        bool ResumesOnForeground { get; }
    }
}
