namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// Receives the search's progress at most every 100 ms and lets its owner cancel the check. The
    /// Level Editor shows a cancelable progress bar through it; tests pass a silent or scripted one.
    /// </summary>
    public interface ILevelCheckProgress
    {
        /// <summary>True once the owner wants the check to stop; the check then returns Not checked.</summary>
        bool IsCancelRequested { get; }

        void Report(int positions, double elapsedSeconds);
    }
}
