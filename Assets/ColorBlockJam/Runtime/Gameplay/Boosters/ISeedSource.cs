namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// Supplies the seed of the starting Rocket's target sample (B-11, B-17), so production draws a fresh seed per
    /// attempt and tests choose a repeatable one.
    /// </summary>
    public interface ISeedSource
    {
        int NextSeed();
    }
}
