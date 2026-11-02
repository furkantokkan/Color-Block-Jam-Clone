namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>The top bar's two menu buttons as requests to the in-game menu flow (Game HUD design 4).</summary>
    public interface IGameMenu
    {
        /// <summary>The Pause button: opens the pause menu when the session state allows it.</summary>
        void RequestPause();

        /// <summary>The Retry button: opens the Retry confirmation when the session state allows it.</summary>
        void RequestRestart();
    }
}
