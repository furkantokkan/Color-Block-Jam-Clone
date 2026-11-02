using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>One effect's metadata, attempt rules and optional scene-lifetime view.</summary>
    public interface IBlockEffectModule
    {
        string TypeId { get; }
        IBlockEffectLevel Read(LevelData authored);
        IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks);
        IBlockEffectView CreateView(BlockEffectViewContext context);
    }
}
