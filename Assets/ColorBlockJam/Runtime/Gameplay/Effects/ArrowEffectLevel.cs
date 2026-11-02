using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>Immutable Arrow axes in converted block order: one optional axis per authored block (BLK-01).</summary>
    public sealed class ArrowEffectLevel : IBlockEffectLevel
    {
        private readonly LevelEffectAxis?[] m_axes;

        /// <param name="axes">Per authored block, its Arrow axis (Horizontal or Vertical), or null for no Arrow.</param>
        public ArrowEffectLevel(LevelEffectAxis?[] axes)
        {
            m_axes = axes != null ? (LevelEffectAxis?[])axes.Clone() : throw new ArgumentNullException(nameof(axes));
            for (int block = 0; block < m_axes.Length; block++)
            {
                LevelEffectAxis? axis = m_axes[block];
                if (axis.HasValue && axis.Value != LevelEffectAxis.Horizontal && axis.Value != LevelEffectAxis.Vertical)
                    throw new ArgumentException("An Arrow axis is Horizontal or Vertical.", nameof(axes));
                HasArrow |= axis.HasValue;
            }
        }

        public string TypeId => LevelEffectTypes.Arrow;
        public int BlockCount => m_axes.Length;

        /// <summary>True when some authored block has an Arrow axis.</summary>
        public bool HasArrow { get; }

        public bool TryGetAxis(int block, out LevelEffectAxis axis)
        {
            LevelEffectAxis? value = m_axes[block];
            axis = value.GetValueOrDefault();
            return value.HasValue;
        }

        // The Domain hooks' axes: Horizontal or Vertical for an Arrow block, Both for any other.
        internal AxisMask[] CreateAxisMasks()
        {
            AxisMask[] masks = new AxisMask[m_axes.Length];
            for (int block = 0; block < masks.Length; block++)
            {
                LevelEffectAxis? axis = m_axes[block];
                masks[block] = !axis.HasValue ? AxisMask.Both
                    : axis.Value == LevelEffectAxis.Horizontal ? AxisMask.Horizontal : AxisMask.Vertical;
            }

            return masks;
        }
    }
}
