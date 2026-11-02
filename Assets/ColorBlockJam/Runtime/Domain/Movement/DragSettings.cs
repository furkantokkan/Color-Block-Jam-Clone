
namespace ColorBlockJam.Domain.Movement
{
    /// <summary>
    /// The drag tunables: step budget per tick and view lead (DEC-112). A fitting door takes a held block on
    /// contact, with no push and no line-up distance (DEC-164, DEC-197).
    /// </summary>
    public readonly struct DragSettings
    {
        public DragSettings(int maxStepsPerTick, float leadCells)
        {
            MaxStepsPerTick = maxStepsPerTick;
            LeadCells = leadCells;
        }

        /// <summary>Cell steps allowed in one update; 0 means board width + height.</summary>
        public int MaxStepsPerTick { get; }

        /// <summary>
        /// How far the view may lead the logical cell toward a free neighbour. 0.5 equals the rounding
        /// point of the logical step, so the view stays continuous across a step (DEC-112).
        /// </summary>
        public float LeadCells { get; }

        public static DragSettings Default => new DragSettings(0, 0.5f);
    }
}
