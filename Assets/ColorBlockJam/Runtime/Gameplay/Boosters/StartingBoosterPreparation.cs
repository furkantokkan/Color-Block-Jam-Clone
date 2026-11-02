using ColorBlockJam.Player;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// The result of preparing the starting boosters on Home: the plan to pass to LevelSession.Play, which is
    /// empty unless the combined debit committed, and why.
    /// </summary>
    public readonly struct StartingBoosterPreparation
    {
        public StartingBoosterPreparation(OpeningPlan plan, StartingBoosterOutcome outcome,
            BoosterConsumeResult consumeResult)
        {
            Plan = plan;
            Outcome = outcome;
            ConsumeResult = consumeResult;
        }

        public OpeningPlan Plan { get; }

        public StartingBoosterOutcome Outcome { get; }

        /// <summary>The debit's result when a debit ran; Consumed otherwise, as nothing was asked.</summary>
        public BoosterConsumeResult ConsumeResult { get; }
    }
}
