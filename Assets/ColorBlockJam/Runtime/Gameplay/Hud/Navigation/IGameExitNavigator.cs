namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// The single Game-to-Hub exit (pause HOME, fail popup close, no-life Retry, Level Complete Home): ends the
    /// attempt and returns to Hub once (Game HUD design 4, AC-8).
    /// </summary>
    public interface IGameExitNavigator
    {
        /// <summary>True from an accepted exit until it fails; a completed exit keeps it set.</summary>
        bool IsExiting { get; }

        /// <summary>
        /// Ends the attempt and starts the return to Hub; false, changing nothing, while an exit runs, when the session
        /// refuses Home (a pending booster write) or after disposal.
        /// </summary>
        bool TryGoHome();
    }
}
