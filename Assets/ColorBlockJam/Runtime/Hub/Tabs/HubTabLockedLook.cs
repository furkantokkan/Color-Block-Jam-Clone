namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// What a locked tab looks like (DEC-86, DEC-141). Stored by value in the navigation catalog, so a value is never
    /// renumbered.
    /// </summary>
    public enum HubTabLockedLook
    {
        /// <summary>The tab's own icon, dimmed, with ic_lock.png on top (Shop and Collection).</summary>
        DimmedIconWithLock = 0,

        /// <summary>ic_lock.png alone in place of the icon, not dimmed (the reserved end slots).</summary>
        LockOnly = 1
    }
}
