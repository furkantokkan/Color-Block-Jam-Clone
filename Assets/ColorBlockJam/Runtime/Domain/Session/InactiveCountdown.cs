namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// A countdown that never runs out. It holds the timer phase slot until the countdown task binds
    /// the real timer, so the tick order stays testable now (design H).
    /// </summary>
    public sealed class InactiveCountdown : ICountdown
    {
        public float Remaining { get; private set; }

        public void Start(int seconds)
        {
            Remaining = seconds;
        }

        public void SetPaused(bool paused)
        {
        }

        public void Stop()
        {
        }

        public bool Tick(float deltaTime) => false;
    }
}
