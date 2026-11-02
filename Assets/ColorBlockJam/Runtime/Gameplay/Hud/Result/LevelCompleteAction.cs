namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>What the Level Complete popup's one button does now (AC-4.9, AC-4.10, AC-6.14, DEC-173).</summary>
    public enum LevelCompleteAction
    {
        /// <summary>Continue to the next level; shows the reward row.</summary>
        Continue = 0,

        /// <summary>Retry the failed win save with the same reward and progression.</summary>
        Retry = 1,

        /// <summary>Back to Hub: after the last database level, or a level outside the active database (DEC-8).</summary>
        Home = 2
    }
}
