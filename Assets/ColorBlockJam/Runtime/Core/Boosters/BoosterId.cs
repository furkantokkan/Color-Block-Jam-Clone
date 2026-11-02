using System;

namespace ColorBlockJam.Runtime.Boosters
{
    /// <summary>
    /// Stable IDs shared by authored definitions and persistent inventory data.
    /// Never renumber existing values.
    /// </summary>
    public enum BoosterId
    {
        None = 0,
        TimeFreeze = 1,
        Rocket = 2,
        Hammer = 3,
        Vacuum = 4,
        ExtraTime = 5,

        /// <summary>
        /// The pre-level Rocket (DEC-167): the same one-cell effect as <see cref="Rocket"/>, but its own stock, so
        /// starting and in-game Rockets are never counted in one pool.
        /// </summary>
        StartingRocket = 6
    }

    [Flags]
    public enum BoosterUseContext
    {
        None = 0,
        PreLevel = 1,
        InGame = 2
    }

    public enum BoosterTargetKind
    {
        None = 0,
        Cell = 1,
        BlockOrObstacle = 2,
        ColorFromBlock = 3,

        /// <summary>A whole block, whatever its footprint or Ice (Hammer, B-26).</summary>
        Block = 4
    }
}
