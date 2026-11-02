using System;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// One accepted in-game booster use as immutable data (B-13): the attempt it belongs to, the booster and either
    /// the board command it applies (Rocket, Hammer, Vacuum) or the freeze time it starts (Time Freeze, whose Command
    /// kind is None). It is captured before Paused(Saving) and applied once after the stock debit commits, so the
    /// charge and the effect always describe the same use; it allocates nothing.
    /// </summary>
    public readonly struct BoosterIntent
    {
        private BoosterIntent(int generation, BoosterId boosterId, GameCommand command, float freezeSeconds)
        {
            Generation = generation;
            BoosterId = boosterId;
            Command = command;
            FreezeSeconds = freezeSeconds;
        }

        /// <summary>The generation of the attempt that accepted the use; a later attempt refuses it as stale.</summary>
        public int Generation { get; }

        public BoosterId BoosterId { get; }

        /// <summary>The board command a board booster applies; Kind None for a time booster.</summary>
        public GameCommand Command { get; }

        /// <summary>The Playing seconds a Time Freeze holds the main countdown; 0 for a board booster.</summary>
        public float FreezeSeconds { get; }

        /// <summary>True when the intent applies a board command.</summary>
        public bool IsBoard => Command.Kind != CommandKind.None;

        /// <summary>True when the intent starts a Time Freeze.</summary>
        public bool IsTimeFreeze => Command.Kind == CommandKind.None && FreezeSeconds > 0f;

        /// <summary>A Rocket, Hammer or Vacuum use that applies command once.</summary>
        public static BoosterIntent ForBoard(int generation, BoosterId boosterId, in GameCommand command)
        {
            if (boosterId == BoosterId.None)
            {
                throw new ArgumentException("A booster intent needs a booster.", nameof(boosterId));
            }

            if (command.Kind == CommandKind.None)
            {
                throw new ArgumentException("A board booster intent needs a board command.", nameof(command));
            }

            return new BoosterIntent(generation, boosterId, command, 0f);
        }

        /// <summary>A Time Freeze use that holds the main countdown for seconds of Playing time.</summary>
        public static BoosterIntent ForTimeFreeze(int generation, float seconds)
        {
            if (!(seconds > 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), "A freeze lasts a positive time.");
            }

            return new BoosterIntent(generation, BoosterId.TimeFreeze, default, seconds);
        }
    }
}
