namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Immutable metadata returned by one registered effect, in attempt block order.</summary>
    public interface IBlockEffectLevel
    {
        string TypeId { get; }
        int BlockCount { get; }
    }
}
