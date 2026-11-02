namespace ColorBlockJam.Domain.Board
{
    /// <summary>
    /// The non-block results of IBoardReader.GetOccupant. They belong to the reader contract, so rules
    /// and every reader implementation share them without depending on BoardState.
    /// </summary>
    public static class Occupant
    {
        /// <summary>A free floor or void cell inside the board.</summary>
        public const int k_empty = -1;

        /// <summary>A cell outside the board.</summary>
        public const int k_offBoard = -2;
    }
}
