namespace ColorBlockJam.Domain.Commands
{
    /// <summary>
    /// What executing a command did: Applied, or the first reason it was refused. A refused command
    /// changes nothing, so a replay or a bot can report the reason and stop or try another command.
    /// </summary>
    public enum CommandOutcome
    {
        Applied = 0,

        /// <summary>No handler is registered for the command's kind.</summary>
        UnknownKind = 1,

        /// <summary>A field holds a value the kind does not define, such as an Option that is no side.</summary>
        Malformed = 2,

        UnknownBlock = 3,

        /// <summary>The block has already left the board.</summary>
        NotOnBoard = 4,

        /// <summary>The block does not stand on the command's From origin.</summary>
        StaleOrigin = 5,

        /// <summary>A drag gate (XP-1) holds the block in place.</summary>
        DragRefused = 6,

        /// <summary>The move or the exit side leaves the block's allowed axes (XP-2).</summary>
        AxisRefused = 7,

        /// <summary>The move neither changes the origin nor exits; it would not be a move (DEC-113).</summary>
        NoChange = 8,

        /// <summary>No path of single validated cell steps leads from From to To (RULE-10, XP-3).</summary>
        Unreachable = 9,

        /// <summary>The block does not fit a door on the exit side at To (RULE-12..14, XP-4).</summary>
        ExitRefused = 10,

        /// <summary>Rocket's cell is the block's only cell; Rocket never removes a whole block (AC-BST-3.3).</summary>
        FinalCell = 11,

        /// <summary>Rocket's cell is not a cell of the command's block, or of any block.</summary>
        NotBlockCell = 12,

        /// <summary>Vacuum's colour is not the tapped block's colour.</summary>
        ColorMismatch = 13,

        /// <summary>A booster restriction (XP-6, DEC-171) keeps this booster off the block, such as Rocket on Ice.</summary>
        EffectRestricted = 14
    }
}
