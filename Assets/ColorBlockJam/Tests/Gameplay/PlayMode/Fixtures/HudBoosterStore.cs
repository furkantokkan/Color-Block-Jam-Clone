using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Tests.Gameplay.PlayMode.Fixtures
{
    /// <summary>
    /// In-memory booster stock for HUD and loader PlayMode tests: one charge of every booster, a debit that commits at
    /// once (or, with a Gate, when the test opens it) and counts its calls, and a purchase that reports the test's chosen
    /// result (Purchased grants the booster's bundle) at once or, with a PurchaseGate, holds IsSaving true until the test
    /// opens it. Only a gated purchase or SetSaving raises IsSaving.
    /// </summary>
    public sealed class HudBoosterStore : IBoosterInventory, IBoosterShop, IBoosterConsumer, IDisposable
    {
        private readonly ReactiveProperty<int>[] m_stock;
        private readonly ReactiveProperty<bool> m_isSaving = new ReactiveProperty<bool>(false);

        public HudBoosterStore(int initialStock = 1)
        {
            BoosterId[] ids = (BoosterId[])Enum.GetValues(typeof(BoosterId));
            int highest = 0;
            for (int index = 0; index < ids.Length; index++)
            {
                highest = Math.Max(highest, (int)ids[index]);
            }

            m_stock = new ReactiveProperty<int>[highest + 1];
            for (int index = 0; index < m_stock.Length; index++)
            {
                m_stock[index] = new ReactiveProperty<int>(initialStock);
            }
        }

        public int ConsumeCount { get; private set; }

        /// <summary>
        /// When set, a debit waits for it: true commits, false fails the save. Like PlayerSaveService's write lock, the
        /// caller's token withdraws the debit only while the gate is still closed, and nothing is then spent.
        /// </summary>
        public TaskCompletionSource<bool> Gate { get; set; }

        public IReadOnlyReactiveProperty<bool> IsSaving => m_isSaving;

        public IReadOnlyReactiveProperty<int> GetStock(BoosterId id) => m_stock[(int)id];

        public void SetStock(BoosterId id, int stock)
        {
            m_stock[(int)id].SetValue(stock);
        }

        /// <summary>The result the next purchases report; only Purchased adds stock.</summary>
        public BoosterPurchaseResult NextPurchase { get; set; } = BoosterPurchaseResult.Purchased;

        /// <summary>When set, a purchase holds IsSaving true and waits for it: true reports NextPurchase, false SaveFailed.</summary>
        public TaskCompletionSource<bool> PurchaseGate { get; set; }

        public int PurchaseCount { get; private set; }

        public void SetSaving(bool saving)
        {
            m_isSaving.SetValue(saving);
        }

        public Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken)
        {
            PurchaseCount++;
            if (PurchaseGate != null)
            {
                return PurchaseAfterGateAsync(booster, PurchaseGate.Task);
            }

            return Task.FromResult(Purchase(booster, NextPurchase));
        }

        public Task<BoosterConsumeResult> ConsumeAsync(IReadOnlyList<BoosterId> debits, CancellationToken cancellationToken)
        {
            ConsumeCount++;
            for (int index = 0; index < debits.Count; index++)
            {
                if (m_stock[(int)debits[index]].Value < 1)
                {
                    return Task.FromResult(BoosterConsumeResult.InsufficientStock);
                }
            }

            BoosterId[] ids = new BoosterId[debits.Count];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = debits[index];
            }

            if (Gate != null)
            {
                return ConsumeAfterGateAsync(ids, Gate.Task, cancellationToken);
            }

            Debit(ids);
            return Task.FromResult(BoosterConsumeResult.Consumed);
        }

        private async Task<BoosterConsumeResult> ConsumeAfterGateAsync(BoosterId[] ids, Task<bool> gate,
            CancellationToken cancellationToken)
        {
            Task withdrawn = Task.Delay(Timeout.Infinite, cancellationToken);
            if (await Task.WhenAny(gate, withdrawn) != gate)
            {
                return BoosterConsumeResult.Cancelled;
            }

            if (!gate.Result)
            {
                return BoosterConsumeResult.SaveFailed;
            }

            Debit(ids);
            return BoosterConsumeResult.Consumed;
        }

        // Like PlayerSaveService, the committed stock is published before IsSaving clears and the task completes.
        private async Task<BoosterPurchaseResult> PurchaseAfterGateAsync(BoosterDefinition booster, Task<bool> gate)
        {
            m_isSaving.SetValue(true);
            try
            {
                bool committed = await gate;
                return Purchase(booster, committed ? NextPurchase : BoosterPurchaseResult.SaveFailed);
            }
            finally
            {
                m_isSaving.SetValue(false);
            }
        }

        private BoosterPurchaseResult Purchase(BoosterDefinition booster, BoosterPurchaseResult result)
        {
            if (result == BoosterPurchaseResult.Purchased)
            {
                ReactiveProperty<int> stock = m_stock[(int)booster.Id];
                stock.SetValue(stock.Value + Math.Max(1, booster.PurchaseAmount));
            }

            return result;
        }

        private void Debit(BoosterId[] ids)
        {
            for (int index = 0; index < ids.Length; index++)
            {
                ReactiveProperty<int> stock = m_stock[(int)ids[index]];
                stock.SetValue(stock.Value - 1);
            }
        }

        public void Dispose()
        {
            for (int index = 0; index < m_stock.Length; index++)
            {
                m_stock[index].Dispose();
            }

            m_isSaving.Dispose();
        }
    }
}
