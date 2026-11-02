namespace ColorBlockJam.Environment
{
    /// <summary>What an environment rule expects in one neighbouring quadrant.</summary>
    public enum EnvironmentNeighbor
    {
        // The neighbour is not checked.
        Any = 0,
        // The neighbour quadrant is part of the board floor.
        Floor = 1,
        // The neighbour quadrant is not floor, including anything outside the board.
        Void = 2
    }
}
