using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Currencies;
using Onity.Reactive;

namespace ColorBlockJam.Player.Lives
{
    /// <summary>
    /// Exposes the catalog's <c>life</c> currency as <see cref="ILives"/> (DEC-174). It holds no state of its own:
    /// counts, refill timers and every write stay with the single <see cref="PlayerSaveService"/>.
    /// </summary>
    /// <remarks>Owner: the project container (AsSingle), next to the save service; nothing to stop.</remarks>
    public sealed class LivesService : ILives
    {
        public const string LifeCurrencyId = "life";

        private readonly PlayerSaveService m_save;

        public LivesService(PlayerSaveService save, CurrencyCatalog catalog)
        {
            m_save = save ?? throw new ArgumentNullException(nameof(save));
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryGet(LifeCurrencyId, out CurrencyDefinition life))
            {
                throw new InvalidOperationException("Lives: the currency catalog has no '" + LifeCurrencyId
                                                    + "' currency. Add Data/Currencies/Life.asset to the catalog.");
            }

            Currency = life;
        }

        public CurrencyDefinition Currency { get; }

        public IReadOnlyReactiveProperty<int> Count => m_save.GetBalance(Currency);

        public int GetAvailable()
        {
            return m_save.GetRefillState(Currency).Amount;
        }

        public long GetSecondsUntilNext()
        {
            return m_save.GetSecondsUntilRefill(Currency);
        }

        public Task<PlayerSaveResult> RefreshAsync(CancellationToken cancellationToken)
        {
            return m_save.RefreshRefillsAsync(cancellationToken);
        }

        public Task<PlayerSaveResult> LoseOneAsync(CancellationToken cancellationToken)
        {
            return m_save.TakeAsync(Currency, 1, cancellationToken);
        }

        public Task<CurrencyRefillResult> RefillAsync(CancellationToken cancellationToken)
        {
            return m_save.BuyRefillAsync(Currency, cancellationToken);
        }
    }
}
