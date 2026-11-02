using ColorBlockJam.Domain.Commands;

namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// Read-only access to one attempt's board for rules, hooks, views and the Level Editor solver
    /// (AR-10, DEC-144). GetOccupant returns a block ID, Occupant.k_empty or Occupant.k_offBoard. A block ID is
    /// the authored index for an authored block and a later ID for a Rocket part (B-2); rules read a
    /// block's cells through GetFootprint, never through Model.GetBlock (B-3).
    /// </summary>
    public interface IBoardReader
    {
        LevelModel Model { get; }

        int Width { get; }

        int Height { get; }

        /// <summary>The block IDs issued so far: the authored blocks, then every Rocket part. IDs are never reused.</summary>
        int BlockCount { get; }

        /// <summary>
        /// The number of blocks still required to win; every block is required (RULE-29), so a Rocket
        /// split adds one for each new part.
        /// </summary>
        int RequiredRemaining { get; }

        Cell GetOrigin(int block);

        BlockStatus GetStatus(int block);

        /// <summary>The block's current cells, extents, colour and authored source.</summary>
        BlockFootprint GetFootprint(int block);

        int GetOccupant(Cell cell);

        bool IsFloor(Cell cell);

        /// <summary>
        /// False when the attempt's booster restrictions (XP-6, DEC-171) keep a board booster of this kind off the
        /// block, for example Rocket on a frozen block. Every booster target check reads it through BoosterTargets.
        /// </summary>
        bool AllowsBoosterTarget(CommandKind kind, int block);
    }
}
