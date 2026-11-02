namespace ColorBlockJam.Gameplay.Hud
{
    /// <summary>
    /// Hears OS backgrounding after the level session has: the in-game menu flow resumes play on the foreground return
    /// unless the player's own menu is up (DEC-182). A Paused(Saving) or Paused(Purchase) session latching to
    /// Background raises no state notification, so this call is where the return is handled.
    /// </summary>
    public interface IApplicationPauseListener
    {
        /// <summary>True when the app went to the background, false when it came back.</summary>
        void OnApplicationPauseChanged(bool paused);
    }
}
