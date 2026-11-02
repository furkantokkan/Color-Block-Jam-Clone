namespace ColorBlockJam.Player
{
    /// <summary>
    /// The Settings toggles the player snapshot stores. Each is saved under its own JSON key, so the numbers are
    /// not persisted; keep the names stable.
    /// </summary>
    public enum PlayerSettingId
    {
        Music = 0,
        Sound = 1,
        Vibration = 2
    }
}
