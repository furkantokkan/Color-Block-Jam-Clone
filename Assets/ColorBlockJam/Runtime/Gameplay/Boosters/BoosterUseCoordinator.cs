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
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Boosters
{
    /// <summary>
    /// Turns booster-bar presses and chosen targets into in-game booster uses (B-12, B-13, B-18): it refuses a
    /// press unless the attempt can take one, offers the purchase for an empty stock, switches the targeting mode,
    /// validates targets on the committed board, captures the intent, submits it to the session and owns the one
    /// stock debit that decides whether the intent applies. Selecting, switching, cancelling and refused targets
    /// spend nothing, and no click or target ever changes the board directly.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle); it attaches itself to BoosterTargeting as the target sink.
    /// Stop points: each save task ends after one debit result, reported once to the session; Dispose cancels the
    /// coordinator's token, detaches it and ends its state subscription.
    /// </remarks>
    public sealed class BoosterUseCoordinator : IBoosterTargetSink, IDisposable
    {
        private readonly LevelSession m_session;
        private readonly IBoosterConsumer m_consumer;
        private readonly IBoosterInventory m_inventory;
        private readonly IBoosterShop m_shop;
        private readonly BoosterCatalog m_catalog;
        private readonly BoosterTargeting m_targeting;
        private readonly IInGamePurchaseHost m_purchaseHost;
        private readonly CancellationTokenSource m_cancellation = new CancellationTokenSource();
        private readonly ReactiveProperty<bool> m_isBusy = new ReactiveProperty<bool>(false);

        // One debit list per booster ID, built once: ConsumeAsync copies the list before it first awaits (B-14).
        private readonly BoosterId[][] m_debits;

        // Hides the purchase presentation whenever play leaves the pause it belongs to. Owner: this coordinator;
        // disposed in Dispose.
        private readonly IDisposable m_stateSubscription;

        // The one debit in flight, stored so it is never discarded; a completed task while none runs.
        private Task m_save = Task.CompletedTask;
        private bool m_disposed;

        public BoosterUseCoordinator(LevelSession session, IBoosterConsumer consumer, IBoosterInventory inventory,
            IBoosterShop shop, BoosterCatalog catalog, BoosterTargeting targeting, IInGamePurchaseHost purchaseHost)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_shop = shop ?? throw new ArgumentNullException(nameof(shop));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            m_purchaseHost = purchaseHost ?? throw new ArgumentNullException(nameof(purchaseHost));
            m_debits = CreateDebits();
            m_targeting.Attach(this);
            m_stateSubscription = m_session.State.Subscribe(OnStateChanged);
        }

        /// <summary>True from an accepted use until its debit result reached the session; the HUD disables the bar.</summary>
        public IReadOnlyReactiveProperty<bool> IsBusy => m_isBusy;

        /// <summary>The debit in flight, or a completed task; tests and scene shutdown can await it.</summary>
        public Task PendingSave => m_save;

        /// <summary>
        /// Handles one booster-bar button. Only a Playing attempt outside the opening, with no booster or
        /// player-data write pending, takes a press. An empty stock ends targeting and offers the purchase;
        /// Time Freeze ends targeting and submits at once unless a freeze runs; Rocket, Hammer and Vacuum toggle or
        /// switch the targeting mode for free.
        /// </summary>
        public BoosterPressResult Press(BoosterId id)
        {
            if (!CanUse() || !TryGetInGame(id, out BoosterDefinition definition))
            {
                return BoosterPressResult.Refused;
            }

            if (m_inventory.GetStock(id).Value <= 0)
            {
                m_targeting.Cancel();
                if (!m_purchaseHost.TryShow(definition))
                {
                    return BoosterPressResult.Blocked;
                }

                if (!m_session.TryOpenPurchase())
                {
                    m_purchaseHost.Hide();
                    return BoosterPressResult.Refused;
                }

                return BoosterPressResult.PurchaseOpened;
            }

            if (id == BoosterId.TimeFreeze)
            {
                m_targeting.Cancel();
                if (!(definition is TimeFreezeBoosterDefinition freeze) || m_session.IsFrozen.Value)
                {
                    return BoosterPressResult.Refused;
                }

                BoosterIntent intent = BoosterIntent.ForTimeFreeze(m_session.Attempt.Generation, freeze.DurationSeconds);
                return Submit(intent) ? BoosterPressResult.Submitted : BoosterPressResult.Refused;
            }

            if (!BoosterTargeting.IsTargeted(id))
            {
                return BoosterPressResult.Refused;
            }

            return m_targeting.Toggle(id) ? BoosterPressResult.TargetingStarted : BoosterPressResult.TargetingCancelled;
        }

        /// <summary>
        /// Validates a tapped cell on the committed board with the shared target rules (B-11), builds the booster's
        /// command, captures the intent and submits it. Runs in the pointer step of the Playing tick; the save it
        /// starts first yields out of that tick.
        /// </summary>
        public BoosterTargetResult TrySubmitTarget(BoosterId id, LevelAttempt attempt, Cell cell)
        {
            if (attempt == null || !ReferenceEquals(attempt, m_session.Attempt) || !CanUse()
                || !TryGetInGame(id, out BoosterDefinition _) || m_inventory.GetStock(id).Value <= 0)
            {
                return BoosterTargetResult.Rejected;
            }

            IBoardReader board = attempt.Simulation.Board;
            int occupant = board.GetOccupant(cell);
            if (occupant < 0)
            {
                return BoosterTargetResult.Empty;
            }

            GameCommand command;
            switch (id)
            {
                case BoosterId.Rocket:
                    // The cell is a block's, so a refusal is its final cell (AC-BST-3.3).
                    if (!BoosterTargets.CanRemoveCell(board, cell, out int block, out CommandOutcome _))
                    {
                        return BoosterTargetResult.Rejected;
                    }

                    command = GameCommand.RemoveCell(block, board.GetOrigin(block), cell);
                    break;
                case BoosterId.Hammer:
                    if (!BoosterTargets.CanRemoveBlock(board, occupant))
                    {
                        return BoosterTargetResult.Rejected;
                    }

                    command = GameCommand.RemoveBlock(occupant, board.GetOrigin(occupant));
                    break;
                case BoosterId.Vacuum:
                    if (!BoosterTargets.CanRemoveColor(board, occupant))
                    {
                        return BoosterTargetResult.Rejected;
                    }

                    command = GameCommand.RemoveColor(occupant, board.GetOrigin(occupant), board.GetFootprint(occupant).Color);
                    break;
                default:
                    return BoosterTargetResult.Rejected;
            }

            BoosterIntent intent = BoosterIntent.ForBoard(attempt.Generation, id, command);
            return Submit(intent) ? BoosterTargetResult.Accepted : BoosterTargetResult.Rejected;
        }

        /// <summary>Hides the purchase presentation and resumes play unless backgrounding latched the pause.</summary>
        public bool ClosePurchase()
        {
            if (m_disposed)
            {
                return false;
            }

            m_purchaseHost.Hide();
            return m_session.ClosePurchase();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_stateSubscription.Dispose();
            m_targeting.Detach(this);
            m_cancellation.Cancel();
            m_cancellation.Dispose();
            m_isBusy.Dispose();
        }

        private bool CanUse()
        {
            return !m_disposed && m_session.Attempt != null && m_session.State.Value == SessionState.Playing
                && !m_session.IsOpening.Value && !m_session.IsBoosterWritePending && !m_session.HasCommittedBooster
                && !m_isBusy.Value && !m_shop.IsSaving.Value;
        }

        private bool TryGetInGame(BoosterId id, out BoosterDefinition definition)
        {
            return m_catalog.TryGet(id, out definition) && (definition.UseContexts & BoosterUseContext.InGame) != 0;
        }

        private bool Submit(in BoosterIntent intent)
        {
            if (!m_session.TrySubmitBooster(intent))
            {
                return false;
            }

            m_isBusy.SetValue(true);

            // Owner: this coordinator (m_save). Stops: after one debit result; Dispose cancels its token.
            m_save = SaveAsync(intent.Generation, m_debits[(int)intent.BoosterId], m_cancellation.Token);
            return true;
        }

        // The debit of one accepted use (B-13, B-14). The yield leaves the accepting tick before any store work,
        // because ConsumeAsync can complete on the caller's stack. The token only ends the wait for the write
        // lock: a write that started completes and reports its own result. A fault is logged and counts as a
        // failed save, so the session resumes without the effect; after Dispose nothing is reported.
        private async Task SaveAsync(int generation, BoosterId[] debit, CancellationToken token)
        {
            bool committed = false;
            try
            {
                await Task.Yield();
                if (!token.IsCancellationRequested)
                {
                    BoosterConsumeResult result = await m_consumer.ConsumeAsync(debit, token);
                    committed = result == BoosterConsumeResult.Consumed;
                }
            }
            catch (OperationCanceledException)
            {
                // Dispose ended the wait; nothing was spent.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            if (m_disposed)
            {
                return;
            }

            m_isBusy.SetValue(false);
            m_session.CompleteBoosterCommit(generation, committed);
        }

        private void OnStateChanged(SessionState state)
        {
            if (state != SessionState.Paused && m_purchaseHost.IsShowing)
            {
                m_purchaseHost.Hide();
            }
        }

        private static BoosterId[][] CreateDebits()
        {
            BoosterId[] ids = (BoosterId[])Enum.GetValues(typeof(BoosterId));
            int highest = 0;
            for (int index = 0; index < ids.Length; index++)
            {
                highest = Math.Max(highest, (int)ids[index]);
            }

            BoosterId[][] debits = new BoosterId[highest + 1][];
            for (int index = 0; index < ids.Length; index++)
            {
                debits[(int)ids[index]] = new[] { ids[index] };
            }

            return debits;
        }
    }
}
