using Onity.Reactive;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// Reads the committed saved level index; readers turn it into the playable level and its database entry with
    /// PlayerProgress (DEC-173).
    /// </summary>
    public interface IPlayerProgress
    {
        IReadOnlyReactiveProperty<int> SavedLevelIndex { get; }
    }
}
