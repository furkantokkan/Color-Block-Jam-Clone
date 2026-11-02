namespace ColorBlockJam.Domain.Hooks
{
    /// <summary>Which of the four board events a BoardEvent entry records.</summary>
    public enum BoardEventKind
    {
        Exited = 0,
        Removed = 1,
        MoveCommitted = 2,
        FootprintChanged = 3
    }
}
