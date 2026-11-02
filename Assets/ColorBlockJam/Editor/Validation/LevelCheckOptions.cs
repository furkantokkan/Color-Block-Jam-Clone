using System;
using ColorBlockJam.Runtime.Board;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The solvability check's settings. The budget defaults live here and nowhere else (DEC-175: the check's
    /// position count is unlimited — the 15-second clock is the only budget, user order of 2026-09-29 — and the
    /// solution listing keeps 1,000,000 positions and 10 seconds, raised from DEC-144's 250,000 and 3 seconds once
    /// Validate Level ran off the main thread); the Level Editor has no override UI yet.
    /// </summary>
    public sealed class LevelCheckOptions
    {
        public const int DefaultMaxPositions = int.MaxValue;
        public const double DefaultMaxSeconds = 15.0;

        /// <summary>The solution listing's own position budget.</summary>
        public const int DefaultMaxListPositions = 1000000;

        /// <summary>The solution listing's own time budget, measured from the listing's start.</summary>
        public const double DefaultMaxListSeconds = 10.0;

        /// <summary>The most solutions a listing keeps; one more distinct exit order reads "20+ solutions".</summary>
        public const int MaxListedSolutions = 20;

        /// <summary>The most blocks the state key holds (16 origins of 7 bits plus 16 exited bits).</summary>
        public const int MaxBlocks = LevelStateKey.MaxBlocks;

        public LevelCheckOptions()
        {
            MaxPositions = DefaultMaxPositions;
            MaxSeconds = DefaultMaxSeconds;
            EagerExits = true;
            MaxListPositions = DefaultMaxListPositions;
            MaxListSeconds = DefaultMaxListSeconds;
        }

        /// <summary>The search stops with Unknown once it has stored this many positions.</summary>
        public int MaxPositions { get; set; }

        /// <summary>The search stops with Unknown once this much time has passed.</summary>
        public double MaxSeconds { get; set; }

        /// <summary>
        /// When an exit is available, expand only the available exits. Exits only free cells and lower Ice
        /// counts, so this never loses a solution; false runs plain breadth-first search (the oracle).
        /// </summary>
        public bool EagerExits { get; set; }

        /// <summary>Overrides how whole blocks are named in reasons; when set, ShapeNames is not used.</summary>
        public LevelBlockDescriber BlockDescriber { get; set; }

        /// <summary>
        /// Names a block's piece in reasons by its ShapeKey, for example with the piece library's template names and
        /// the level's own piece names (DEC-193); null, or a null or blank answer, reads "{n}-cell". The check calls it
        /// on its own thread (DEC-175), so it must read a snapshot built on the main thread, never a Unity object.
        /// </summary>
        public Func<ShapeKey, string> ShapeNames { get; set; }

        /// <summary>The listing stops as Partial once it has stored this many positions.</summary>
        public int MaxListPositions { get; set; }

        /// <summary>
        /// The listing stops as Partial once this much time has passed since the listing started; for a verdict listed
        /// on its own (LevelSolvabilityCheck.ListSolutions), since just before its tables were built again.
        /// </summary>
        public double MaxListSeconds { get; set; }
    }
}
