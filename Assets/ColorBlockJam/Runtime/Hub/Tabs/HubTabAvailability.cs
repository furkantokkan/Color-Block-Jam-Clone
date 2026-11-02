namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// How a designer makes one Home tab available (DEC-169). Stored by value in the navigation catalog, so a value
    /// is never renumbered.
    /// </summary>
    public enum HubTabAvailability
    {
        /// <summary>Shown and open: its menu opens on press.</summary>
        Available = 0,

        /// <summary>Shown locked until a designer opens it; a press gives feedback only.</summary>
        Locked = 1,

        /// <summary>Shown locked until the player reaches the tab's unlock level, then open without a restart.</summary>
        LevelGated = 2,

        /// <summary>Turned off: the slot is not shown and the other tabs share the row.</summary>
        Hidden = 3
    }
}
