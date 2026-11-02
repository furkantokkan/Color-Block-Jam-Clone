namespace ColorBlockJam.UI
{
    /// <summary>
    /// The player-facing copy shared by the popups in this assembly (Settings, booster purchase, life refill), kept
    /// in one place because four sites had drifted to two wordings of the same save-failure line (DEC-136).
    /// </summary>
    public static class PopupMessages
    {
        public const string SaveFailed = "Couldn't save. Try again.";
        public const string InvalidOffer = "This offer is not available.";
        public const string NotEnoughPrefix = "Not enough ";
    }
}
