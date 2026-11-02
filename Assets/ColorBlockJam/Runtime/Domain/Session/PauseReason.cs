namespace ColorBlockJam.Domain.Session
{
    /// <summary>Why the session is Paused; a reason, never a separate state (DEC-92).</summary>
    public enum PauseReason
    {
        None = 0,
        User = 1,
        Background = 2,
        Saving = 3,
        Purchase = 4
    }
}
