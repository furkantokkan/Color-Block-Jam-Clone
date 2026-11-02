using System;

namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The attempt's level countdown with its Time Freeze clock (B-8). A tick spends the freeze first
    /// and passes only the leftover time to the main countdown, so a freeze that ends inside a tick
    /// adds nothing and loses nothing. The main countdown signals expiry once. SetPaused and Stop hold
    /// both clocks, and Start clears any freeze, so the session's Suspend, Resume and Teardown steps
    /// need no second clock. It is plain data advanced only by the session's tick, so it allocates
    /// nothing and runs nothing on its own.
    /// </summary>
    public sealed class AttemptCountdown : ICountdown, IFreezeCountdown
    {
        private bool m_started;
        private bool m_paused;
        private bool m_expired;

        public float Remaining { get; private set; }

        public float FreezeRemaining { get; private set; }

        public bool IsFrozen => FreezeRemaining > 0f;

        public void Start(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "The countdown cannot start below zero.");
            }

            Remaining = seconds;
            FreezeRemaining = 0f;
            m_started = true;
            m_paused = false;
            m_expired = false;
        }

        public void SetPaused(bool paused)
        {
            m_paused = paused;
        }

        /// <summary>Stops both clocks and ends any freeze; the main time keeps its value for the result.</summary>
        public void Stop()
        {
            m_started = false;
            m_paused = false;
            FreezeRemaining = 0f;
        }

        public bool TryFreeze(float seconds)
        {
            if (!(seconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "A freeze lasts a positive time.");
            }

            if (!m_started || m_expired || IsFrozen)
            {
                return false;
            }

            FreezeRemaining = seconds;
            return true;
        }

        public void AddSeconds(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "Extra time cannot be negative.");
            }

            if (!m_started || m_expired)
            {
                throw new InvalidOperationException("Extra time needs a started countdown that has not run out.");
            }

            Remaining += seconds;
        }

        /// <summary>Advances the running clocks by deltaTime; true only on the tick the main countdown reaches zero.</summary>
        public bool Tick(float deltaTime)
        {
            if (!m_started || m_paused || m_expired)
            {
                return false;
            }

            float delta = deltaTime > 0f ? deltaTime : 0f;
            if (FreezeRemaining > 0f)
            {
                if (delta <= FreezeRemaining)
                {
                    FreezeRemaining -= delta;
                    return false;
                }

                delta -= FreezeRemaining;
                FreezeRemaining = 0f;
            }

            Remaining -= delta;
            if (Remaining > 0f)
            {
                return false;
            }

            Remaining = 0f;
            m_expired = true;
            return true;
        }
    }
}
