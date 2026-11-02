namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The production seed source: the system tick count when a seed is asked for, read once per attempt on Home.
    /// It keeps no state, so nothing survives between attempts.
    /// </summary>
    public sealed class TickCountSeedSource : ISeedSource
    {
        public int NextSeed() => System.Environment.TickCount;
    }
}
