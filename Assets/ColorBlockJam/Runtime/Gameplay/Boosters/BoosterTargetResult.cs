namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>What a tapped target did to the targeting mode (B-12).</summary>
    public enum BoosterTargetResult
    {
        /// <summary>The use was captured and submitted; the session suspended into Paused(Saving).</summary>
        Accepted = 0,

        /// <summary>An on-board block the booster cannot take, such as a one-cell block for Rocket: the mode stays.</summary>
        Rejected = 1,

        /// <summary>No block holds the cell: the targeting mode is cancelled for free.</summary>
        Empty = 2
    }
}
