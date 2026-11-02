using System.Collections.Generic;
using ColorBlockJam.Gameplay.Input;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// Returns the sample a test sets and journals Enable and Disable, so session tests control every tick's pointer.
    /// PressBack makes the next WasBackPressed report one Back press, as the real map does for one frame.
    /// </summary>
    public sealed class FakePointerSource : IPointerSource, IBackButtonSource
    {
        private readonly List<string> m_journal;
        private PointerSample m_sample;
        private bool m_backPressed;

        public FakePointerSource(List<string> journal)
        {
            m_journal = journal;
        }

        public bool IsEnabled { get; private set; }

        public void Set(PointerPhase phase, Vector2 screenPosition)
        {
            m_sample = new PointerSample(phase, screenPosition);
        }

        public void Enable()
        {
            IsEnabled = true;
            m_journal.Add("pointer.Enable");
        }

        public void Disable()
        {
            IsEnabled = false;
            m_journal.Add("pointer.Disable");
        }

        public PointerSample Read() => m_sample;

        public void PressBack()
        {
            m_backPressed = true;
        }

        public bool WasBackPressed()
        {
            bool pressed = m_backPressed;
            m_backPressed = false;
            return pressed;
        }
    }
}
