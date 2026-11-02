namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// The kinds of gameplay command. The executor finds a command's handler by its kind, so a new
    /// command adds its kind here and registers its handler without editing the executor.
    /// Logs store these values, so a kind is only ever appended, never renumbered.
    /// </summary>
    public enum CommandKind
    {
        None = 0,

        /// <summary>One player move: a block travels to a cell and may leave through a door there (DEC-122).</summary>
        MoveBlock = 1,

        /// <summary>Rocket: removes one cell of a block; the remaining parts become separate blocks (B-4, B-6).</summary>
        RemoveCell = 2,

        /// <summary>Hammer: removes one whole block, whatever its footprint or Ice (B-6).</summary>
        RemoveBlock = 3,

        /// <summary>Vacuum: removes every on-board block of the tapped block's colour (B-6).</summary>
        RemoveColor = 4
    }
}
