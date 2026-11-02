namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// The one look the tab bar view draws for a shown tab; the presenter picks it from the tab's status and locked
    /// look, so the view decides nothing.
    /// </summary>
    public enum HubTabLook
    {
        /// <summary>The catalog icon at full colour and the open label.</summary>
        Open = 0,

        /// <summary>The catalog icon dimmed with ic_lock.png on top (USS class <c>tab--locked</c>).</summary>
        LockedDimmed = 1,

        /// <summary>ic_lock.png alone in place of the icon (USS class <c>tab--lock-only</c>).</summary>
        LockedLockOnly = 2
    }
}
