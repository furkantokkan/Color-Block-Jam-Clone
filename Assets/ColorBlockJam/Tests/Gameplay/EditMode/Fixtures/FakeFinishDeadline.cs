using System;
using ColorBlockJam.Gameplay.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A manually completed session deadline; it never advances with the Unity frame loop.</summary>
    public sealed class FakeFinishDeadline : IFinishDeadline
    {
        private Action m_completed;

        public float Seconds { get; private set; }
        public bool IsPending => m_completed != null;

        public void Start(float seconds, Action completed)
        {
            Seconds = seconds;
            m_completed = completed;
        }

        public void Cancel()
        {
            m_completed = null;
        }

        public void Complete()
        {
            Action completed = m_completed;
            m_completed = null;
            completed?.Invoke();
        }
    }
}
