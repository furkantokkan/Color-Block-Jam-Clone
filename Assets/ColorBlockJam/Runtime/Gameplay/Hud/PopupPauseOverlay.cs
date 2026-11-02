using ColorBlockJam.Gameplay.Session;

namespace ColorBlockJam.Gameplay.Hud
{
    /// <summary>
    /// The pause overlay of a Game scene with the HUD: the session does not resume a background pause by itself,
    /// because GameMenuFlow decides on the foreground return: it resumes unless the player's pause menu or Retry
    /// confirmation is up (DEC-182).
    /// </summary>
    /// <remarks>
    /// A class of its own, not the menu flow, so LevelSession (which needs an IPauseOverlay) and the flow (which needs
    /// the session) form no constructor cycle. A HUD-less scene keeps <see cref="NoPauseOverlay"/>.
    /// </remarks>
    public sealed class PopupPauseOverlay : IPauseOverlay
    {
        public bool ResumesOnForeground => false;
    }
}
