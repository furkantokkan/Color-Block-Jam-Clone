namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>The four outcomes of the solvability check (PRD 7.9).</summary>
    public enum LevelCheckStatus
    {
        /// <summary>A sequence of moves that clears every block was found.</summary>
        Solvable = 0,

        /// <summary>A static pre-check failed, or the search visited every reachable position.</summary>
        Unsolvable = 1,

        /// <summary>The search hit its position or time budget first; nothing is proven.</summary>
        Unknown = 2,

        /// <summary>The check did not run: an unsupported mechanic, too many blocks, a cancel or an unreadable level.</summary>
        NotChecked = 3
    }
}
