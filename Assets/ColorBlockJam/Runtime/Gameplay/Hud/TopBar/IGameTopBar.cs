using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Hud.TopBar
{
    /// <summary>The Game HUD's top bar: Level badge, Retry, Pause and coin holder (Game HUD design 3-4).</summary>
    public interface IGameTopBar
    {
        /// <summary>Prepares the bar for the level on Home: binds on the first call and shows the level's number.</summary>
        void Show(AttemptLevel level);
    }
}
