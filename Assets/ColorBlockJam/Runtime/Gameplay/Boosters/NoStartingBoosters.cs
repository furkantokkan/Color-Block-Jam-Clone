namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The starting booster source while no launch request exists (B-17): direct Game Play, Editor test play and
    /// every attempt until the Hub pre-level launch binds its own source start with no selection.
    /// </summary>
    public sealed class NoStartingBoosters : IStartingBoosterSource
    {
        public bool TryTake(out StartingBoosterSelection selection)
        {
            selection = StartingBoosterSelection.None;
            return false;
        }
    }
}
