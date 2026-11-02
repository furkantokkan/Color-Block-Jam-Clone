namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>
    /// Whether a permitted exit removes the block (Leave) or keeps it on the board (Stay, reserved for
    /// a future layer peel).
    /// </summary>
    public enum ExitOutcome
    {
        Leave = 0,
        Stay = 1
    }
}
