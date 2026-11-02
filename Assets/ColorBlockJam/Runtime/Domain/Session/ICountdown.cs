namespace ColorBlockJam.Domain.Session
{
    /// <summary>
    /// The level countdown port that fills the timer phase of the tick (RULE-18). The session starts,
    /// pauses and stops it; Tick returns true on the tick it reaches zero.
    /// </summary>
    public interface ICountdown
    {
        float Remaining { get; }

        void Start(int seconds);

        void SetPaused(bool paused);

        void Stop();

        bool Tick(float deltaTime);
    }
}
