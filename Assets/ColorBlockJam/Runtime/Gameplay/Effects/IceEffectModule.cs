using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>BLK-03 registration: typed metadata, fresh Ice rules and its pooled scene view.</summary>
    public sealed class IceEffectModule : IBlockEffectModule
    {
        private readonly Material m_material;
        public IceEffectModule(Material material) { m_material = material; }
        public string TypeId => LevelEffectTypes.Ice;

        public IBlockEffectLevel Read(LevelData authored)
        {
            int[] counts = new int[authored.BlockCount];
            // The converter copies every authored cell into the model, so this sum is LevelModel.TotalCellCount:
            // the block IDs an attempt can issue, one slot per Rocket part (B-10).
            int capacity = 0;
            for (int block = 0; block < counts.Length; block++)
            {
                LevelBlockData data = authored.GetBlockAt(block);
                capacity += data.CellCount;
                if (data.TryGetEffect(out IceEffect ice)) counts[block] = ice.ClearsToThaw;
            }
            return new IceEffectLevel(counts, System.Math.Max(capacity, counts.Length));
        }

        public IBlockEffectState AddHooks(IBlockEffectLevel level, AttemptHooksBuilder hooks)
        {
            IceEffectLevel ice = level as IceEffectLevel
                ?? throw new System.ArgumentException("Ice needs its own typed level metadata.", nameof(level));
            // A Rocket part copies its parent's counts into its own slot (IceRules.OnFootprintChanged).
            IceRules rules = new IceRules(ice.CopyCounts(), ice.Capacity);
            bool active = false;
            for (int block = 0; block < ice.BlockCount; block++) active |= ice.GetCount(block) > 0;
            if (active)
            {
                hooks.Add((IDragGate)rules);
                hooks.Add((IExitGate)rules);
                hooks.Add((IBoardEventListener)rules);
            }
            return new IceEffectState(rules);
        }

        public IBlockEffectView CreateView(BlockEffectViewContext context)
            => new IceEffectView(context, m_material);
    }
}
