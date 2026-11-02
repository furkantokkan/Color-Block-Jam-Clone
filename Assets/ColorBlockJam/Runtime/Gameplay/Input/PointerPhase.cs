namespace ColorBlockJam.Gameplay.Input
{
    /// <summary>The primary pointer's phase in the current frame, as the gameplay tick reads it.</summary>
    public enum PointerPhase
    {
        None = 0,
        Down = 1,
        Held = 2,
        Up = 3,
        Cancel = 4
    }
}
