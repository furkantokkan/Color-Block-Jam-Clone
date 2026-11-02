using System.Collections.Generic;
using ColorBlockJam.Domain.Session;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// A real AttemptCountdown that also journals its time-booster calls, so a test can order Extra Time against the
    /// board events of the same command phase.
    /// </summary>
    public sealed class RecordingFreezeCountdown : ICountdown, IFreezeCountdown
    {
        private readonly AttemptCountdown m_inner = new AttemptCountdown();
        private readonly List<string> m_journal;

        public RecordingFreezeCountdown(List<string> journal)
        {
            m_journal = journal;
        }

        public float Remaining => m_inner.Remaining;

        public bool IsFrozen => m_inner.IsFrozen;

        public float FreezeRemaining => m_inner.FreezeRemaining;

        public int TickCount { get; private set; }

        public void Start(int seconds)
        {
            m_inner.Start(seconds);
        }

        public void SetPaused(bool paused)
        {
            m_inner.SetPaused(paused);
        }

        public void Stop()
        {
            m_inner.Stop();
        }

        public bool Tick(float deltaTime)
        {
            TickCount++;
            return m_inner.Tick(deltaTime);
        }

        public bool TryFreeze(float seconds)
        {
            m_journal.Add("clock.TryFreeze");
            return m_inner.TryFreeze(seconds);
        }

        public void AddSeconds(int seconds)
        {
            m_journal.Add("clock.AddSeconds");
            m_inner.AddSeconds(seconds);
        }
    }
}
