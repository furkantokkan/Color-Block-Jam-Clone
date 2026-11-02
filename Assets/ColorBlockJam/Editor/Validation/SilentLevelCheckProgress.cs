namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>A progress receiver that shows nothing and never cancels; the check's default.</summary>
    public sealed class SilentLevelCheckProgress : ILevelCheckProgress
    {
        /// <summary>Stateless, so one shared instance is safe.</summary>
        public static readonly SilentLevelCheckProgress Instance = new SilentLevelCheckProgress();

        private SilentLevelCheckProgress()
        {
        }

        public bool IsCancelRequested => false;

        public void Report(int positions, double elapsedSeconds)
        {
        }
    }
}
