namespace ColorBlockJam.Domain.Board
{
    /// <summary>Whether a block still occupies board cells or has left through a door or a booster.</summary>
    public enum BlockStatus
    {
        OnBoard = 0,
        Exited = 1,
        Removed = 2
    }
}
