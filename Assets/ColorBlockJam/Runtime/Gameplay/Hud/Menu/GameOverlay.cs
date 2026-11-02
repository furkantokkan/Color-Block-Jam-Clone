namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>The one in-game overlay the menu flow shows at a time (Game HUD design 4).</summary>
    public enum GameOverlay
    {
        None = 0,
        PauseMenu = 1,
        RestartConfirm = 2,

        /// <summary>The Retry confirmation reading Leave, opened by HOME after a block was touched (DEC-189).</summary>
        LeaveConfirm = 3
    }
}
