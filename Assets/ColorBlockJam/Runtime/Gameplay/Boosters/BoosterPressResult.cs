namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>What a booster-bar button press did (B-12, B-13, B-18); the HUD turns it into feedback.</summary>
    public enum BoosterPressResult
    {
        /// <summary>Nothing happened: not Playing, the opening runs, a booster or player-data write is pending, or Time Freeze already runs.</summary>
        Refused = 0,

        /// <summary>No stock and no purchase presentation is available: the button plays its blocked feedback.</summary>
        Blocked = 1,

        /// <summary>No stock: the purchase presentation opened under Paused(Purchase).</summary>
        PurchaseOpened = 2,

        /// <summary>Rocket, Hammer or Vacuum now waits for a target; any other mode was switched off for free.</summary>
        TargetingStarted = 3,

        /// <summary>The same targeted button ended its own mode for free.</summary>
        TargetingCancelled = 4,

        /// <summary>Time Freeze was captured and submitted; its stock debit is being written.</summary>
        Submitted = 5
    }
}
