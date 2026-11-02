namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// The four board sides. The ordinals equal Runtime's BoardSide so the level converter casts
    /// between them without a lookup.
    /// </summary>
    public enum Side
    {
        Top = 0,
        Bottom = 1,
        Left = 2,
        Right = 3
    }
}
