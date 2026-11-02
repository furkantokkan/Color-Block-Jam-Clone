using System.Collections.Generic;
using ColorBlockJam.Domain.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>A countdown that journals its calls and reaches zero on every tick while ExpireOnTick is set.</summary>
    public sealed class FakeCountdown : ICountdown
    {
        private readonly List<string> m_journal;

        public FakeCountdown(List<string> journal)
        {
            m_journal = journal;
        }

        public bool ExpireOnTick { get; set; }

        public int TickCount { get; private set; }

        public float Remaining { get; private set; }

        public void Start(int seconds)
        {
            Remaining = seconds;
            m_journal.Add("countdown.Start");
        }

        public void SetPaused(bool paused)
        {
            m_journal.Add(paused ? "countdown.Pause" : "countdown.Resume");
        }

        public void Stop()
        {
            m_journal.Add("countdown.Stop");
        }

        public bool Tick(float deltaTime)
        {
            TickCount++;
            return ExpireOnTick;
        }
    }
}
