namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The designer's answer when the Level Editor would leave a level with unsaved changes: keep editing it, save it
    /// first, or drop its changes.
    /// </summary>
    internal enum LevelLeaveChoice
    {
        Cancel = 0,
        Save = 1,
        Discard = 2
    }
}
