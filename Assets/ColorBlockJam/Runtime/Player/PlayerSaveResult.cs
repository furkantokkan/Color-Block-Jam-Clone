namespace ColorBlockJam.Player
{
    /// <summary>The expected outcomes of a player-data write, returned instead of thrown (design 4.4 rule 4).</summary>
    public enum PlayerSaveResult
    {
        Saved = 0,
        SaveFailed = 1,
        Cancelled = 2
    }
}
