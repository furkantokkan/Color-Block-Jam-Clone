namespace ColorBlockJam.Domain.Movement
{
    /// <summary>What one drag update did to the held block's preview.</summary>
    public enum DragResult
    {
        Unchanged = 0,
        Moved = 1,

        /// <summary>The held block touched a door it fits (DEC-164); the update returned the exit command.</summary>
        ExitReady = 2
    }
}
