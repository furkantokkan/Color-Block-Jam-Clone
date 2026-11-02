namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// What the life-cost confirmation's green button does, and so what it reads: Retry restarts the level, Leave
    /// returns to Hub from a level in progress (DEC-189). Both show the same "You will lose 1 life!" popup.
    /// </summary>
    public enum LifeConfirmAction
    {
        Retry = 0,
        Leave = 1
    }
}
