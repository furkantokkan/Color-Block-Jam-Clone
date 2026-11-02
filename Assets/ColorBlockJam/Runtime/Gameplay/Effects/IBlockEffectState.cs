namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>The state of one registered effect for one attempt; its Domain rules own mutations.</summary>
    public interface IBlockEffectState
    {
        string TypeId { get; }
    }
}
