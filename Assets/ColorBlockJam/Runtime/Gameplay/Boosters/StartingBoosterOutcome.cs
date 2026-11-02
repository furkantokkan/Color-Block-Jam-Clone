namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>Why a starting-booster preparation opens, or does not open, the attempt with boosters (B-16, B-29).</summary>
    public enum StartingBoosterOutcome
    {
        /// <summary>No starting booster was selected: nothing is spent.</summary>
        NoSelection = 0,

        /// <summary>Nothing selected can apply (no stock, no definition, no Rocket candidate): nothing is spent.</summary>
        NothingEligible = 1,

        /// <summary>The combined debit committed; the plan applies once after Play.</summary>
        Committed = 2,

        /// <summary>The combined debit did not commit: nothing is spent and the attempt starts without starting boosters.</summary>
        CommitFailed = 3
    }
}
