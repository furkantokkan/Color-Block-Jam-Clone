using System;
using ColorBlockJam.Domain.Picking;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The pooled Rocket crosshair layer as its presenter sees it (B-24): reticles are placed in screen pixels (origin
    /// bottom left, as IBoardProjector projects) and the view converts them to its panel.
    /// </summary>
    public interface IReticleView
    {
        /// <summary>How many reticles the pool holds.</summary>
        int Capacity { get; }

        /// <summary>The layer's geometry changed (resolution, orientation), so shown reticles must be placed again.</summary>
        event Action GeometryChanged;

        /// <summary>Grows the pool to capacity on Home; a pool that is large enough is kept.</summary>
        void Reserve(int capacity);

        /// <summary>Shows reticle index centred on a screen point with a width of size screen pixels.</summary>
        void Show(int index, ScreenPoint center, float size);

        /// <summary>Hides every reticle from index on.</summary>
        void HideFrom(int index);
    }
}
