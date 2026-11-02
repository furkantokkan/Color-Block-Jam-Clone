namespace ColorBlockJam.Haptics
{
    /// <summary>The game moments that vibrate; <see cref="HapticStyles"/> gives each one a style (DEC-156, DEC-186).</summary>
    public enum HapticFeedback
    {
        BlockTouch = 0,
        GateExit = 1,
        BoosterApplied = 2,
        LevelWon = 3,
        LevelFailed = 4,
        ButtonTap = 5,
        ButtonConfirm = 6
    }
}
