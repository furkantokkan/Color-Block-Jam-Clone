namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Where one authored door's pieces sit in the composer's placement list: its Door_1x1 slots
    /// followed by its arrow, contiguous and in authored door order. EnvironmentView turns each span
    /// into one EnvironmentGate on Home.
    /// </summary>
    public readonly struct EnvironmentGateSpan
    {
        public readonly int DoorIndex;
        public readonly int FirstPlacement;
        public readonly int PlacementCount;

        public EnvironmentGateSpan(int doorIndex, int firstPlacement, int placementCount)
        {
            DoorIndex = doorIndex;
            FirstPlacement = firstPlacement;
            PlacementCount = placementCount;
        }
    }
}
