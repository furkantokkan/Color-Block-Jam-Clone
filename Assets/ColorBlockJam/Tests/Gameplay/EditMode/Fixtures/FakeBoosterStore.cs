using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;

namespace ColorBlockJam.Tests.Gameplay.Fixtures
{
    /// <summary>
    /// The player-data ports a booster use reads and writes, in memory: stock per booster, the IsSaving flag and a
    /// debit that records each call. A test chooses the next debit result, a fault, or a gate that holds the debit
    /// open until the test releases it. A Consumed debit lowers the stock only after the gate opens.
    /// </summary>
    public sealed class FakeBoosterStore : IBoosterInventory, IBoosterShop, IBoosterConsumer, IDisposable
    {
        private readonly ReactiveProperty<int>[] m_stock;
        private readonly ReactiveProperty<bool> m_isSaving = new ReactiveProperty<bool>(false);

        public FakeBoosterStore(int initialStock = 1)
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

        /// <summary>The result of the next debits; Consumed spends one charge of each listed booster.</summary>
        public BoosterConsumeResult NextResult { get; set; } = BoosterConsumeResult.Consumed;

        /// <summary>When set, the next debit faults with it once.</summary>
        public Exception NextFault { get; set; }

        /// <summary>When set, the next debits wait for it before they resolve.</summary>
        public TaskCompletionSource<bool> Gate { get; set; }

        public int ConsumeCount { get; private set; }

        /// <summary>A copy of every debit list, in call order.</summary>
        public List<BoosterId[]> Debits { get; } = new List<BoosterId[]>();

        public IReadOnlyReactiveProperty<bool> IsSaving => m_isSaving;

        public void SetStock(BoosterId id, int stock)
        {
            m_stock[(int)id].SetValue(stock);
        }

        public void SetSaving(bool saving)
        {
            m_isSaving.SetValue(saving);
        }

        public IReadOnlyReactiveProperty<int> GetStock(BoosterId id) => m_stock[(int)id];

        public Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken)
        {
            throw new NotSupportedException("Booster use tests do not purchase.");
        }

        public Task<BoosterConsumeResult> ConsumeAsync(IReadOnlyList<BoosterId> debits, CancellationToken cancellationToken)
        {
            ConsumeCount++;
            BoosterId[] copy = new BoosterId[debits.Count];
            for (int index = 0; index < copy.Length; index++)
            {
                copy[index] = debits[index];
            }

            Debits.Add(copy);
            if (NextFault != null)
            {
                Exception fault = NextFault;
                NextFault = null;
                return Task.FromException<BoosterConsumeResult>(fault);
            }

            if (Gate != null)
            {
                return ConsumeAfterGateAsync(Gate.Task, copy);
            }

            return Task.FromResult(Apply(copy));
        }

        public void Dispose()
        {
            for (int index = 0; index < m_stock.Length; index++)
            {
                m_stock[index].Dispose();
            }

            m_isSaving.Dispose();
        }

        private async Task<BoosterConsumeResult> ConsumeAfterGateAsync(Task gate, BoosterId[] debits)
        {
            await gate;
            return Apply(debits);
        }

        private BoosterConsumeResult Apply(BoosterId[] debits)
        {
            BoosterConsumeResult result = NextResult;
            if (result != BoosterConsumeResult.Consumed)
            {
                return result;
            }

            for (int index = 0; index < debits.Length; index++)
            {
                if (m_stock[(int)debits[index]].Value < 1)
                {
                    return BoosterConsumeResult.InsufficientStock;
                }
            }

            for (int index = 0; index < debits.Length; index++)
            {
                ReactiveProperty<int> stock = m_stock[(int)debits[index]];
                stock.SetValue(stock.Value - 1);
            }

            return BoosterConsumeResult.Consumed;
        }
    }
}
