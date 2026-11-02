using Cysharp.Text;

namespace ColorBlockJam.Hub.Tabs
{
    /// <summary>
    /// The one rule that turns a tab definition and the player's saved level into what the tab bar shows: its status
    /// (hidden, unlocked or locked) and its label (DEC-169). Plain C# with no view or service, so EditMode tests pin it.
    /// </summary>
    public static class HubTabRules
    {
        private const string k_levelLabelPrefix = "Lvl ";

        /// <param name="savedLevelIndex">
        /// <c>IPlayerProgress.SavedLevelIndex</c>: the 0-based index of the level the player plays next. It is not
        /// clamped to the level database, so after the last level it can equal the level count (DEC-8, DEC-159).
        /// </param>
        public static HubTabStatus GetStatus(HubTabDefinition tab, int savedLevelIndex)
        {
            switch (tab.Availability)
            {
                case HubTabAvailability.Available:
                    return HubTabStatus.Unlocked;
                case HubTabAvailability.Locked:
                    return HubTabStatus.Locked;
                case HubTabAvailability.LevelGated:
                    return GetLevelGatedStatus(tab.UnlockLevel, savedLevelIndex);
                default:
                    return HubTabStatus.Hidden;
            }
        }

        /// <summary>
        /// Open tabs show their label. A locked tab shows a label only when its entry asks for one: "Lvl N" for a
        /// Level Gated tab, its own label for a Locked one. An empty string keeps the label's line height, so the icon
        /// above never shifts (DEC-141).
        /// </summary>
        public static string GetLabel(HubTabDefinition tab, HubTabStatus status)
        {
            if (status == HubTabStatus.Unlocked)
            {
                return tab.Label;
            }

            if (status != HubTabStatus.Locked || !tab.ShowLabelWhileLocked)
            {
                return string.Empty;
            }

            return tab.Availability == HubTabAvailability.LevelGated
                ? ZString.Concat(k_levelLabelPrefix, tab.UnlockLevel)
                : tab.Label;
        }

        /// <summary>
        /// Decides when a Level Gated tab opens. "Lvl 50" on the tab is the player-facing promise this must keep.
        /// </summary>
        /// <param name="unlockLevel">The 1-based level number from the catalog (1 or more).</param>
        /// <param name="savedLevelIndex">The 0-based index of the level the player plays next (see GetStatus).</param>
        private static HubTabStatus GetLevelGatedStatus(int unlockLevel, int savedLevelIndex)
        {
            // TODO(user): open the tab once the player has "reached" unlockLevel. Decide what reaching means here:
            // the level the player now plays is savedLevelIndex + 1 (so a fresh save sits on level 1), or should the
            // tab wait until that level is completed? Also decide how a negative (damaged) index is treated.
            // Until then every Level Gated tab stays locked, which keeps today's "Lvl 50" look.
            return HubTabStatus.Locked;
        }
    }
}
