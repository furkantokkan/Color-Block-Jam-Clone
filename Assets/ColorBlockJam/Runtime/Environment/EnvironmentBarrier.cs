namespace ColorBlockJam.Environment
{
    /// <summary>
    /// One composed Barrier board object (catalog OBJ-05): its level cell-object ID, the range of its
    /// pieces in the barrier placement list and its authored start state.
    /// </summary>
    public readonly struct EnvironmentBarrier
    {
        public readonly string Id;
        public readonly int FirstPiece;
        public readonly int PieceCount;
        public readonly bool StartsOpen;

        public EnvironmentBarrier(string id, int firstPiece, int pieceCount, bool startsOpen)
        {
            Id = id;
            FirstPiece = firstPiece;
            PieceCount = pieceCount;
            StartsOpen = startsOpen;
        }
    }
}
