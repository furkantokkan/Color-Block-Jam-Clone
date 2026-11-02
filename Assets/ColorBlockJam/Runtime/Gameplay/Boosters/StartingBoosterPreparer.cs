using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// Prepares the starting boosters of a prepared attempt on Home (B-16): it keeps each selected booster that has a
    /// definition, stock and, for Rocket, a candidate cell sampled from the seed, commits their combined debit in one
    /// ConsumeAsync and returns the plan LevelSession.Play applies once. When nothing is selected or eligible, or the
    /// debit does not commit, nothing is spent and the plan is empty (B-29: the Game scene cannot offer a retry, so
    /// the attempt starts without starting boosters).
    /// </summary>
    /// <remarks>
    /// Owner of its one await: the caller (the Game scene's attempt loader), whose token ends the wait for the write
    /// lock. It keeps no state between calls and allocates only on Home.
    /// </remarks>
    public sealed class StartingBoosterPreparer
    {
        private readonly IBoosterConsumer m_consumer;
        private readonly IBoosterInventory m_inventory;
        private readonly BoosterCatalog m_catalog;

        public StartingBoosterPreparer(IBoosterConsumer consumer, IBoosterInventory inventory, BoosterCatalog catalog)
        {
            m_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
        }

        /// <summary>
        /// Builds and commits the opening of session's prepared attempt. Extra Time takes its definition's bonus;
        /// Rocket takes one cell of StartingRocketCandidates on the prepared board and is dropped, unspent, when there
        /// is none. The session must be on Home with a prepared attempt.
        /// </summary>
        public async Task<StartingBoosterPreparation> PrepareAsync(LevelSession session, StartingBoosterSelection selection,
            int seed, CancellationToken cancellationToken)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (selection.IsEmpty)
            {
                return new StartingBoosterPreparation(OpeningPlan.None, StartingBoosterOutcome.NoSelection,
                    BoosterConsumeResult.Consumed);
            }

            LevelAttempt attempt = session.Attempt;
            if (attempt == null || session.State.Value != SessionState.Home)
            {
                throw new InvalidOperationException("Starting boosters are prepared on Home for a prepared attempt.");
            }

            bool extraTime = false;
            int bonusSeconds = 0;
            if (selection.HasExtraTime && session.SupportsTimeBoosters
                && TryGetStarting(BoosterId.ExtraTime, out BoosterDefinition extraDefinition)
                && extraDefinition is ExtraTimeBoosterDefinition extra && HasStock(BoosterId.ExtraTime))
            {
                extraTime = true;
                bonusSeconds = extra.BonusSeconds;
            }

            bool rocket = false;
            GameCommand rocketCommand = default;
            if (selection.HasRocket && TryGetStarting(BoosterId.StartingRocket, out BoosterDefinition _)
                && HasStock(BoosterId.StartingRocket))
            {
                rocket = TrySampleRocket(attempt.Simulation.Board, seed, out rocketCommand);
            }

            if (!extraTime && !rocket)
            {
                return new StartingBoosterPreparation(OpeningPlan.None, StartingBoosterOutcome.NothingEligible,
                    BoosterConsumeResult.Consumed);
            }

            BoosterId[] debit = extraTime && rocket
                ? new[] { BoosterId.ExtraTime, BoosterId.StartingRocket }
                : new[] { extraTime ? BoosterId.ExtraTime : BoosterId.StartingRocket };

            BoosterConsumeResult result;
            try
            {
                result = await m_consumer.ConsumeAsync(debit, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                result = BoosterConsumeResult.SaveFailed;
            }

            if (result != BoosterConsumeResult.Consumed)
            {
                return new StartingBoosterPreparation(OpeningPlan.None, StartingBoosterOutcome.CommitFailed, result);
            }

            return new StartingBoosterPreparation(new OpeningPlan(extraTime, bonusSeconds, rocket, rocketCommand),
                StartingBoosterOutcome.Committed, result);
        }

        private bool TryGetStarting(BoosterId id, out BoosterDefinition definition)
        {
            return m_catalog.TryGet(id, out definition) && (definition.UseContexts & BoosterUseContext.PreLevel) != 0;
        }

        private bool HasStock(BoosterId id) => m_inventory.GetStock(id).Value >= 1;

        // One seeded sample over every cell Rocket accepts, split-causing cells included (B-11).
        private static bool TrySampleRocket(IBoardReader board, int seed, out GameCommand command)
        {
            Cell[] candidates = new Cell[board.Model.TotalCellCount];
            int count = StartingRocketCandidates.Collect(board, candidates);
            if (!StartingRocketCandidates.TrySample(candidates, count, seed, out Cell cell))
            {
                command = default;
                return false;
            }

            int block = board.GetOccupant(cell);
            command = GameCommand.RemoveCell(block, board.GetOrigin(block), cell);
            return true;
        }
    }
}
