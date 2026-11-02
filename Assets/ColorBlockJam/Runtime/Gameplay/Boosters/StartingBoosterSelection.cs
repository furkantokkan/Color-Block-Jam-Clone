using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The starting boosters the player selected before Play (B-17): Extra Time, Rocket, both or neither. Selecting
    /// spends nothing; StartingBoosterPreparer decides what the attempt can apply and debits it.
    /// </summary>
    public readonly struct StartingBoosterSelection
    {
        public StartingBoosterSelection(bool hasExtraTime, bool hasRocket)
        {
            HasExtraTime = hasExtraTime;
            HasRocket = hasRocket;
        }

        /// <summary>No starting booster selected.</summary>
        public static StartingBoosterSelection None => default;

        public bool HasExtraTime { get; }

        public bool HasRocket { get; }

        public bool IsEmpty => !HasExtraTime && !HasRocket;

        /// <summary>
        /// Reads a launch request's booster list; a repeated ID counts once. Any ID other than Extra Time or Starting
        /// Rocket (the in-game Rocket included, DEC-167) is not a starting booster and throws, so a wiring error is
        /// found on Home instead of being dropped.
        /// </summary>
        public static StartingBoosterSelection From(IReadOnlyList<BoosterId> boosters)
        {
            if (boosters == null)
            {
                throw new ArgumentNullException(nameof(boosters));
            }

            bool extraTime = false;
            bool rocket = false;
            for (int index = 0; index < boosters.Count; index++)
            {
                switch (boosters[index])
                {
                    case BoosterId.ExtraTime:
                        extraTime = true;
                        break;
                    case BoosterId.StartingRocket:
                        rocket = true;
                        break;
                    default:
                        throw new ArgumentException(boosters[index] + " is not a starting booster.", nameof(boosters));
                }
            }

            return new StartingBoosterSelection(extraTime, rocket);
        }
    }
}
