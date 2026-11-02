using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The targeting banner's instruction per booster (B-23), in the reference wording. The definitions' Description
    /// stays the purchase popup's text, and only Hammer.asset is an editable definition, so the copy lives here.
    /// </summary>
    public static class BoosterInstructions
    {
        public const string Rocket = "Tap and destroy one unit of a block!";
        public const string Hammer = "Tap any block to destroy it!";
        public const string Vacuum = "Tap and vacuum blocks with the same color!";

        /// <summary>The instruction of a targeted booster; empty for a booster that takes no target.</summary>
        public static string Get(BoosterId id)
        {
            switch (id)
            {
                case BoosterId.Rocket:
                    return Rocket;
                case BoosterId.Hammer:
                    return Hammer;
                case BoosterId.Vacuum:
                    return Vacuum;
                default:
                    return string.Empty;
            }
        }
    }
}
