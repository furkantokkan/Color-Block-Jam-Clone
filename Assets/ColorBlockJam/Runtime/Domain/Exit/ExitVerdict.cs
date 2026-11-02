namespace ColorBlockJam.Domain.Exit
{
    /// <summary>Why an exit is refused, or Fits when every exit condition holds.</summary>
    public enum ExitVerdict
    {
        Fits = 0,
        NotFlush = 1,
        NoDoor = 2,
        TooNarrow = 3,
        WrongColor = 4,
        PathBlocked = 5,
        Vetoed = 6
    }
}
