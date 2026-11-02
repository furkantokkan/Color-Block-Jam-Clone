using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>A pointer source that returns the sample the test set for the next tick and records enablement.</summary>
    public sealed class ScriptedPointerSource : IPointerSource
    {
        private PointerSample m_next;

        public bool IsEnabled { get; private set; }

        public void Set(PointerPhase phase, Vector2 screenPosition)
        {
            m_next = new PointerSample(phase, screenPosition);
        }

        public void Enable()
        {
            IsEnabled = true;
        }

        public void Disable()
        {
            IsEnabled = false;
        }

        public PointerSample Read() => m_next;
    }
}
