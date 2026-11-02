using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>BLK-01 registration: typed axes on Home, the stateless axis and exit hooks, and the marker view.</summary>
    public sealed class ArrowEffectModule : IBlockEffectModule
    {
        public string TypeId => LevelEffectTypes.Arrow;

        public IBlockEffectLevel Read(LevelData authored)
        {
            if (authored == null) throw new ArgumentNullException(nameof(authored));
            LevelEffectAxis?[] axes = new LevelEffectAxis?[authored.BlockCount];
            for (int block = 0; block < axes.Length; block++)
            {
                if (ArrowMarkerRun.TryGetAxis(authored.GetBlockAt(block), out LevelEffectAxis axis)) axes[block] = axis;
            }

            return new ArrowEffectLevel(axes);
        }

        public IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks)
        {
            ArrowEffectLevel arrow = level as ArrowEffectLevel
                ?? throw new ArgumentException("Arrow needs its own typed level metadata.", nameof(level));
            if (!arrow.HasArrow)
            {
                return new ArrowEffectState(null);
            }

            // Every Arrow hook keys its axes by the authored source, so a Rocket part keeps its parent's (B-10).
            AxisMask[] axes = arrow.CreateAxisMasks();
            hooks.Add(new ArrowAxisConstraint(axes));
            hooks.Add(new ArrowExitGate(axes));
            return new ArrowEffectState(new ArrowEffectPresence(axes));
        }

        public IBlockEffectView CreateView(BlockEffectViewContext context) => new ArrowEffectView(context);
    }
}
