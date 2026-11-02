using System;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The starting boosters an attempt applies once, after their combined debit committed on Home (B-16), and the
    /// timing of their opening: an intro of Playing time dims the screen and shows the boosters, the dim fades, then
    /// Extra Time's bonus seconds and the sampled Rocket command apply in one command phase, and a hold keeps the
    /// countdown, the pointer and the HUD boosters off while the Rocket flies (user request of 2026-09-28: the Rocket is
    /// thrown after the dim ends, and input opens after it). An empty plan (the default) opens nothing.
    /// </summary>
    public readonly struct OpeningPlan
    {
        /// <summary>The Playing seconds of the intro: the dimmed screen with the booster icons, then its fade.</summary>
        public const float k_defaultIntroSeconds = 1.2f;

        /// <summary>
        /// The last part of the intro, in which the dim fades out; equal to the opening layer's opacity transition in
        /// GameplayHud.uss, so the boosters apply once the screen is clear.
        /// </summary>
        public const float k_dimFadeSeconds = 0.2f;

        /// <summary>The Playing seconds after the boosters apply, covering the 0.75 s Rocket cast, before input opens.</summary>
        public const float k_defaultHoldSeconds = 0.9f;

        public OpeningPlan(bool hasExtraTime, int bonusSeconds, bool hasRocket, GameCommand rocket,
            float holdSeconds = k_defaultHoldSeconds, float introSeconds = k_defaultIntroSeconds)
        {
            if (hasExtraTime && bonusSeconds < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bonusSeconds), "Extra Time adds at least one second.");
            }

            if (hasRocket && rocket.Kind != CommandKind.RemoveCell)
            {
                throw new ArgumentException("The starting Rocket is a RemoveCell command.", nameof(rocket));
            }

            if (!(holdSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(holdSeconds), "The opening hold cannot be negative.");
            }

            if (!(introSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(introSeconds), "The opening intro cannot be negative.");
            }

            HasExtraTime = hasExtraTime;
            BonusSeconds = hasExtraTime ? bonusSeconds : 0;
            HasRocket = hasRocket;
            Rocket = hasRocket ? rocket : default;
            HoldSeconds = holdSeconds;
            IntroSeconds = introSeconds;
        }

        /// <summary>No starting booster: Play starts the attempt normally.</summary>
        public static OpeningPlan None => default;

        public bool HasExtraTime { get; }

        public int BonusSeconds { get; }

        public bool HasRocket { get; }

        /// <summary>The Rocket's RemoveCell command, captured on Home against the prepared board.</summary>
        public GameCommand Rocket { get; }

        /// <summary>The Playing seconds after the boosters apply, before input opens.</summary>
        public float HoldSeconds { get; }

        /// <summary>The Playing seconds before the boosters apply: the dimmed screen with their icons, then its fade.</summary>
        public float IntroSeconds { get; }

        /// <summary>The whole opening: intro plus hold.</summary>
        public float TotalSeconds => IntroSeconds + HoldSeconds;

        public bool IsEmpty => !HasExtraTime && !HasRocket;

        /// <summary>True when the opening applies this booster; the opening layer shows one icon per included booster.</summary>
        public bool Includes(BoosterId id)
        {
            switch (id)
            {
                case BoosterId.ExtraTime:
                    return HasExtraTime;
                case BoosterId.StartingRocket:
                    return HasRocket;
                default:
                    return false;
            }
        }
    }
}
