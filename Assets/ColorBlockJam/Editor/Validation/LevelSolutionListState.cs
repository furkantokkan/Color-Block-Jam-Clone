namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>How the solution listing of a Solvable check ended, which decides how its count reads.</summary>
    public enum LevelSolutionListState
    {
        /// <summary>No listing ran: the check was not asked for one, or the level is not Solvable.</summary>
        NotListed = 0,

        /// <summary>
        /// The listing visited every position, so the count is exact ("K solutions") under the check's eager-exit rule:
        /// an open exit is always taken, so an order in which a block waits at an open gate is not counted.
        /// </summary>
        Complete = 1,

        /// <summary>The list is full and one more distinct exit order exists ("20+ solutions").</summary>
        More = 2,

        /// <summary>The listing's budget or a cancel stopped it; more may exist ("at least K solutions").</summary>
        Partial = 3
    }
}
