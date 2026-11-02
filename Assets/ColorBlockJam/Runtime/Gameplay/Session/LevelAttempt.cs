using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Levels;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// Everything one attempt owns: the level it plays, its board simulation and its lifetime.
    /// Teardown drops it as a unit, so no value survives into the next attempt (RULE-8, V-E11).
    /// </summary>
    public sealed class LevelAttempt
    {
        public LevelAttempt(AttemptLevel level, BoardSimulation simulation, int generation)
        {
            Level = level ?? throw new ArgumentNullException(nameof(level));
            Simulation = simulation ?? throw new ArgumentNullException(nameof(simulation));
            Generation = generation;
            Lifetime = new AttemptLifetime();
        }

        public AttemptLevel Level { get; }

        public BoardSimulation Simulation { get; }

        public AttemptLifetime Lifetime { get; }

        /// <summary>Counts attempts in this session; late callbacks compare it before touching the attempt.</summary>
        public int Generation { get; }

        /// <summary>
        /// True once the player's press took a block in this attempt: the countdown waits for it, and HOME leaves an
        /// untouched attempt for free (DEC-189). A booster alone never sets it.
        /// </summary>
        public bool HasTouchedBlock { get; private set; }

        /// <summary>The pointer's first press on a block; later calls change nothing.</summary>
        public void MarkBlockTouched()
        {
            HasTouchedBlock = true;
        }
    }
}
