namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// What Android Back does during Playing when no booster targeting mode takes it: the in-game menu flow opens the
    /// pause menu (Game HUD design 5). The router calls it from the tick's pointer step, so a scene without one keeps
    /// Back doing nothing outside targeting.
    /// </summary>
    public interface IGameplayBackFallback
    {
        /// <summary>
        /// Handles one Back press read by the router; true when it left Playing, and the router then stops its step,
        /// as an accepted booster target does.
        /// </summary>
        bool OnBack();
    }
}
