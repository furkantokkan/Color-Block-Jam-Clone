using System;
using ColorBlockJam.Domain.Picking;
using ColorBlockJam.Gameplay.UI;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A reticle pool that records where each reticle was placed and how many show, without allocating after Reserve,
    /// so allocation tests can place reticles through it. Relayout raises GeometryChanged.
    /// </summary>
    public sealed class FakeReticleView : IReticleView
    {
        private ScreenPoint[] m_centers = new ScreenPoint[0];
        private float[] m_sizes = new float[0];

        public event Action GeometryChanged;

        public int Capacity => m_centers.Length;

        /// <summary>Reticles shown now: every index below it was placed since the last HideFrom below it.</summary>
        public int Shown { get; private set; }

        public int ShowCalls { get; private set; }

        public void Reserve(int capacity)
        {
            if (capacity <= m_centers.Length)
            {
                return;
            }

            Array.Resize(ref m_centers, capacity);
            Array.Resize(ref m_sizes, capacity);
        }

        public void Show(int index, ScreenPoint center, float size)
        {
            m_centers[index] = center;
            m_sizes[index] = size;
            ShowCalls++;
            if (index >= Shown)
            {
                Shown = index + 1;
            }
        }

        public void HideFrom(int index)
        {
            if (index < Shown)
            {
                Shown = index;
            }
        }

        public ScreenPoint GetCenter(int index) => m_centers[index];

        public float GetSize(int index) => m_sizes[index];

        public void Relayout()
        {
            GeometryChanged?.Invoke();
        }
    }
}
