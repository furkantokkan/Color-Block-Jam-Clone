namespace ColorBlockJam.Hub.Launch
{
    /// <summary>The outcome of one Play press on the pre-level popup (design 9.3).</summary>
    public enum LevelLaunchResult
    {
        /// <summary>The Game scene loaded; this launcher accepts no further launch.</summary>
        Completed = 0,

        /// <summary>A launch was already in flight or completed, or the launcher was disposed; nothing started.</summary>
        Ignored = 1,

        /// <summary>The Hub scene ended or the caller cancelled while waiting.</summary>
        Cancelled = 2,

        /// <summary>The scene load threw; the error was logged and Play can be pressed again.</summary>
        Failed = 3
    }
}
