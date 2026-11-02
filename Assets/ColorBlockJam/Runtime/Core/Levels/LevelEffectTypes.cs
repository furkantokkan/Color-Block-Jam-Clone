namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// Effect type IDs read as raw string literals outside the LevelEffectRegistry table (AD-14), so a
    /// misspelled ID cannot silently fail to match its registry entry, Gameplay module or Editor tool.
    /// LevelEffectTypesTests pins each constant to its registered definition.
    /// </summary>
    public static class LevelEffectTypes
    {
        public const string Arrow = "arrow";
        public const string Ice = "ice";
        public const string Barrier = "barrier";
    }
}
