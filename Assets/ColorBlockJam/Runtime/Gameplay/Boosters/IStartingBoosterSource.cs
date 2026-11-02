namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// Where the Game scene reads the starting booster selection of the attempt it opens (B-17). The Hub pre-level
    /// launch maps its launch request onto it; TryTake hands the selection over once, so Restart, Next Level and a
    /// second build never reuse it.
    /// </summary>
    public interface IStartingBoosterSource
    {
        /// <summary>False when no selection is waiting; otherwise hands it over and forgets it.</summary>
        bool TryTake(out StartingBoosterSelection selection);
    }
}
