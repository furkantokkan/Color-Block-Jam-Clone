using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using Onity.DI;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Player
{
    /// <summary>
    /// The single owner of the committed player snapshot and of every player-data write (DEC-92, design 4.4). It
    /// loads once, seeds missing catalog keys once, and for each write prepares the next snapshot, awaits the store,
    /// and only then commits and publishes; a failed save publishes nothing. Writes run one at a time. Timed-refill
    /// currencies (Lives, DEC-174) advance by the clock at load and in every write that reads them.
    /// </summary>
    public sealed class PlayerSaveService : IWallet, IPlayerProgress, IBoosterInventory, IPlayerSettings, IBoosterShop,
        IBoosterConsumer, ILevelWinRecorder, ICurrencyGrant, IDisposable
    {
        public const int MaxBoosterStock = PlayerEconomyRules.MaxBoosterStock;

        private readonly IPlayerSnapshotStore m_store;
        private readonly CurrencyCatalog m_currencies;
        private readonly BoosterCatalog m_boosters;
        private readonly IUtcClock m_clock;

        // Owner: this service. Stops: Dispose, when the project container ends the app scope; it cancels waits.
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();

        // One writer at a time (rule 3). Not disposed: it never creates a wait handle, and a write that already
        // started still releases it after the service is disposed.
        private readonly SemaphoreSlim m_writeLock = new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, ReactiveProperty<int>> m_balances =
            new Dictionary<string, ReactiveProperty<int>>(StringComparer.Ordinal);
        private readonly Dictionary<BoosterId, ReactiveProperty<int>> m_stock = new Dictionary<BoosterId, ReactiveProperty<int>>();
        private readonly ReactiveProperty<bool>[] m_settings;
        private readonly ReactiveProperty<int> m_savedLevelIndex = new ReactiveProperty<int>();
        private readonly ReactiveProperty<bool> m_isSaving = new ReactiveProperty<bool>();

        private PlayerSnapshot m_committed = PlayerSnapshot.Empty;
        private int m_pendingWrites;
        private bool m_loadStarted;
        private bool m_loaded;
        private bool m_writesRefused;
        private bool m_disposed;

        /// <summary>A service on the device clock (tests and tools).</summary>
        public PlayerSaveService(IPlayerSnapshotStore store, CurrencyCatalog currencies, BoosterCatalog boosters)
            : this(store, currencies, boosters, new SystemUtcClock())
        {
        }

        [Inject]
        public PlayerSaveService(IPlayerSnapshotStore store, CurrencyCatalog currencies, BoosterCatalog boosters,
            IUtcClock clock)
        {
            m_store = store ?? throw new ArgumentNullException(nameof(store));
            m_currencies = currencies ?? throw new ArgumentNullException(nameof(currencies));
            m_boosters = boosters ?? throw new ArgumentNullException(nameof(boosters));
            m_clock = clock ?? throw new ArgumentNullException(nameof(clock));

            for (int index = 0; index < currencies.Count; index++)
            {
                CurrencyDefinition currency = currencies.GetAt(index);
                if (!(currency is null) && currency.Id != null && !m_balances.ContainsKey(currency.Id))
                {
                    m_balances.Add(currency.Id, new ReactiveProperty<int>());
                }
            }

            for (int index = 0; index < boosters.Count; index++)
            {
                BoosterDefinition booster = boosters.GetAt(index);
                if (!(booster is null) && !m_stock.ContainsKey(booster.Id))
                {
                    m_stock.Add(booster.Id, new ReactiveProperty<int>());
                }
            }

            PlayerSettings defaults = PlayerSettings.Default;
            m_settings = new[]
            {
                new ReactiveProperty<bool>(defaults.Music),
                new ReactiveProperty<bool>(defaults.Sound),
                new ReactiveProperty<bool>(defaults.Vibration)
            };
        }

        /// <summary>The committed snapshot; the load and each successful write replace it.</summary>
        public PlayerSnapshot Committed => m_committed;

        /// <summary>True after LoadAsync finished.</summary>
        public bool IsLoaded => m_loaded;

        /// <summary>True when the save came from a newer format or could not be loaded, so nothing may overwrite it.</summary>
        public bool WritesRefused => m_writesRefused;

        public IReadOnlyReactiveProperty<int> SavedLevelIndex => m_savedLevelIndex;

        public IReadOnlyReactiveProperty<bool> IsSaving => m_isSaving;

        public IReadOnlyReactiveProperty<int> GetBalance(CurrencyDefinition currency)
        {
            if (currency is null || currency.Id == null || !m_balances.TryGetValue(currency.Id, out ReactiveProperty<int> balance))
            {
                throw new ArgumentException("The currency is not in the currency catalog.", nameof(currency));
            }

            return balance;
        }

        public bool CanAfford(CurrencyDefinition currency, int amount)
        {
            if (currency is null || currency.Id == null || amount < 0)
            {
                return false;
            }

            return m_committed.TryGetCurrency(currency.Id, out int balance) && balance >= amount;
        }

        public IReadOnlyReactiveProperty<int> GetStock(BoosterId id)
        {
            if (!m_stock.TryGetValue(id, out ReactiveProperty<int> stock))
            {
                throw new ArgumentException($"Booster {id} is not in the booster catalog.", nameof(id));
            }

            return stock;
        }

        public IReadOnlyReactiveProperty<bool> Get(PlayerSettingId id)
        {
            int index = (int)id;
            if (index < 0 || index >= m_settings.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown player setting.");
            }

            return m_settings[index];
        }

        /// <summary>
        /// Loads the snapshot once and seeds every catalog currency and booster the save lacks, writing once only
        /// when something was seeded (rule 1). Owner: the project scope build callback. Stops: the build token or
        /// service disposal. Awaits without ConfigureAwait(false) and calls no main-thread-only Unity API.
        /// </summary>
        public async Task LoadAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (m_loadStarted)
            {
                throw new InvalidOperationException("The player data loads once per app scope.");
            }

            m_loadStarted = true;
            CancellationToken lifetime = m_lifetime.Token;
            using (CancellationTokenSource linked =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime))
            {
                await m_writeLock.WaitAsync(linked.Token);
                try
                {
                    await LoadLockedAsync(linked.Token, lifetime);
                }
                finally
                {
                    m_writeLock.Release();
                }
            }
        }

        public async Task<PlayerSaveResult> SetAsync(PlayerSettingId id, bool value, CancellationToken cancellationToken)
        {
            if ((int)id < 0 || (int)id >= m_settings.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown player setting.");
            }

            return await WriteAsync(committed => committed.Settings.Get(id) == value
                ? committed
                : committed.WithSettings(committed.Settings.With(id, value)), cancellationToken);
        }

        public async Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken)
        {
            BoosterPurchaseResult check = BoosterPurchaseResult.SaveFailed;
            PlayerSaveResult saved = await WriteAsync(committed =>
            {
                check = CheckPurchase(committed, booster, out PlayerSnapshot next);
                return next;
            }, cancellationToken);

            switch (saved)
            {
                case PlayerSaveResult.Cancelled:
                    return BoosterPurchaseResult.Cancelled;
                case PlayerSaveResult.SaveFailed:
                    return BoosterPurchaseResult.SaveFailed;
                default:
                    return check;
            }
        }

        /// <summary>
        /// Debits one charge per listed booster as one snapshot, one store write and one revision (B-14). Owner: the
        /// caller that awaits the returned task. Stops: the caller token cancels only the wait for the write lock
        /// and the pre-write check; a started store write gets only the service lifetime token, so an attempt
        /// ending cannot cancel a committed debit (PROJECT.md Ownership).
        /// </summary>
        public async Task<BoosterConsumeResult> ConsumeAsync(IReadOnlyList<BoosterId> debits,
            CancellationToken cancellationToken)
        {
            if (m_disposed)
            {
                return BoosterConsumeResult.Cancelled;
            }

            // Copy before the first await: callers may reuse their buffer while this write waits for the lock.
            BoosterId[] ids = CopyDebits(debits);
            BoosterConsumeResult check = BoosterConsumeResult.SaveFailed;
            PlayerSaveResult saved = await WriteAsync(committed =>
            {
                check = CheckConsume(committed, ids, out PlayerSnapshot next);
                return next;
            }, cancellationToken);

            switch (saved)
            {
                case PlayerSaveResult.Cancelled:
                    return BoosterConsumeResult.Cancelled;
                case PlayerSaveResult.SaveFailed:
                    return BoosterConsumeResult.SaveFailed;
                default:
                    return check;
            }
        }

        /// <summary>
        /// Records one win (AC-4.9, AC-6.14): the reward and the progression commit as one snapshot and one revision.
        /// The snapshot is prepared from the committed one inside the lock, so a retry after a failed write prepares
        /// the same values again; the caller never repeats a call that returned Saved.
        /// </summary>
        public async Task<PlayerSaveResult> RecordWinAsync(int wonLevelIndex, CurrencyDefinition currency, int reward,
            CancellationToken cancellationToken)
        {
            if (wonLevelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(wonLevelIndex), wonLevelIndex, "The level index cannot be negative.");
            }

            if (reward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reward), reward, "The reward cannot be negative.");
            }

            if (currency is null || currency.Id == null || !m_balances.ContainsKey(currency.Id))
            {
                throw new ArgumentException("The currency is not in the currency catalog.", nameof(currency));
            }

            return await WriteAsync(committed =>
                PlayerEconomyRules.RecordWin(committed, wonLevelIndex, currency, reward), cancellationToken);
        }

        public async Task<PlayerSaveResult> AddAsync(CurrencyDefinition currency, int amount,
            CancellationToken cancellationToken)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "The amount cannot be negative.");
            }

            if (currency is null || currency.Id == null || !m_balances.ContainsKey(currency.Id))
            {
                throw new ArgumentException("The currency is not in the currency catalog.", nameof(currency));
            }

            return await WriteAsync(committed =>
                PlayerEconomyRules.AddCurrency(committed, currency, amount, m_currencies, m_clock.UtcNowSeconds),
                cancellationToken);
        }

        /// <summary>
        /// The balance of a timed-refill currency as of now, with refills already due by the clock but not yet
        /// committed; a currency without a timed refill returns its committed balance (DEC-174).
        /// </summary>
        public RefillState GetRefillState(CurrencyDefinition currency)
        {
            RequireCatalogCurrency(currency);
            return Advance(m_committed, currency, m_clock.UtcNowSeconds);
        }

        /// <summary>Whole seconds until the currency's next timed unit; 0 at its maximum or without a timed refill.</summary>
        public long GetSecondsUntilRefill(CurrencyDefinition currency)
        {
            RequireCatalogCurrency(currency);
            long now = m_clock.UtcNowSeconds;
            return CurrencyRefill.GetSecondsUntilNext(Advance(m_committed, currency, now), currency.RefillSeconds, now);
        }

        /// <summary>Commits every timed refill that is due; Saved without a write when none is (DEC-174).</summary>
        public Task<PlayerSaveResult> RefreshRefillsAsync(CancellationToken cancellationToken)
        {
            return WriteAsync(committed => WithRefills(committed, m_clock.UtcNowSeconds), cancellationToken);
        }

        /// <summary>
        /// Removes up to <paramref name="amount"/> (never below 0) after committing due refills, as one snapshot and one
        /// revision; a timed currency that drops below its maximum starts refilling (a life loss, DEC-174).
        /// </summary>
        public Task<PlayerSaveResult> TakeAsync(CurrencyDefinition currency, int amount, CancellationToken cancellationToken)
        {
            RequireCatalogCurrency(currency);
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "The amount cannot be negative.");
            }

            return WriteAsync(committed =>
                PlayerEconomyRules.TakeCurrency(committed, currency, amount, m_currencies, m_clock.UtcNowSeconds),
                cancellationToken);
        }

        /// <summary>
        /// Pays the currency's refill price and fills it to its maximum in one snapshot and one revision; every other
        /// result changes nothing (DEC-174).
        /// </summary>
        public async Task<CurrencyRefillResult> BuyRefillAsync(CurrencyDefinition currency, CancellationToken cancellationToken)
        {
            RequireCatalogCurrency(currency);
            CurrencyRefillResult? check = null;
            PlayerSaveResult saved = await WriteAsync(committed =>
            {
                long now = m_clock.UtcNowSeconds;
                PlayerSnapshot current = WithRefills(committed, now);
                current.TryGetCurrency(currency.Id, out int balance);
                if (balance >= currency.MaxAmount)
                {
                    check = CurrencyRefillResult.AlreadyFull;
                    return null;
                }

                CurrencyDefinition priceCurrency = currency.RefillPriceCurrency;
                if (!currency.HasRefillOffer || priceCurrency.Id == null || !m_balances.ContainsKey(priceCurrency.Id))
                {
                    check = CurrencyRefillResult.InvalidOffer;
                    return null;
                }

                current.TryGetCurrency(priceCurrency.Id, out int funds);
                if (funds < currency.RefillPrice)
                {
                    check = CurrencyRefillResult.InsufficientFunds;
                    return null;
                }

                check = CurrencyRefillResult.Refilled;
                return WithRefills(current.WithCurrency(priceCurrency.Id, funds - currency.RefillPrice)
                    .WithCurrency(currency.Id, currency.MaxAmount), now);
            }, cancellationToken);

            switch (saved)
            {
                case PlayerSaveResult.Cancelled:
                    return CurrencyRefillResult.Cancelled;
                case PlayerSaveResult.SaveFailed:
                    return CurrencyRefillResult.SaveFailed;
                default:
                    return check ?? CurrencyRefillResult.SaveFailed;
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
            foreach (ReactiveProperty<int> balance in m_balances.Values)
            {
                balance.Dispose();
            }

            foreach (ReactiveProperty<int> stock in m_stock.Values)
            {
                stock.Dispose();
            }

            for (int index = 0; index < m_settings.Length; index++)
            {
                m_settings[index].Dispose();
            }

            m_savedLevelIndex.Dispose();
            m_isSaving.Dispose();
        }

        private async Task LoadLockedAsync(CancellationToken cancellationToken, CancellationToken lifetime)
        {
            PlayerSnapshot loaded;
            try
            {
                loaded = await m_store.LoadAsync(cancellationToken);
            }
            catch (PlayerSnapshotVersionException exception)
            {
                RefuseWrites(exception.Message + " Defaults are used in memory and nothing is saved, so the newer "
                             + "save is kept.");
                return;
            }
            catch (InvalidDataException exception)
            {
                Debug.LogWarning("The player save is invalid and the default player data is used: " + exception.Message);
                loaded = null;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                RefuseWrites("The player save could not be loaded (" + exception.Message + "). Defaults are used in "
                             + "memory and nothing is saved, so the existing save is kept.");
                return;
            }

            PlayerSnapshot seeded = Seed(loaded ?? PlayerSnapshot.Empty, out bool changed);

            // Refills earned while the app was closed, and a timer for a balance that is below its maximum without one
            // (a save from before DEC-174).
            PlayerSnapshot refilled = WithRefills(seeded, m_clock.UtcNowSeconds);
            changed |= !ReferenceEquals(refilled, seeded);
            seeded = refilled;
            if (!changed)
            {
                Publish(seeded);
                m_loaded = true;
                return;
            }

            PlayerSnapshot next = seeded.WithRevision(seeded.Revision + 1);
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // The build can cancel loading, but a seed save that started belongs to the service.
                await m_store.SaveAsync(next, lifetime);
                lifetime.ThrowIfCancellationRequested();
                Publish(next);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                // The seeded values stay in memory, so the next successful write persists them.
                lifetime.ThrowIfCancellationRequested();
                Debug.LogWarning("The seeded player data could not be saved and is kept in memory: " + exception.Message);
                Publish(seeded);
            }

            m_loaded = true;
        }

        private void RefuseWrites(string message)
        {
            Debug.LogError(message);
            m_writesRefused = true;
            Publish(Seed(PlayerSnapshot.Empty, out _));
            m_loaded = true;
        }

        // Adds the starting amount or initial charges for every catalog key the snapshot lacks; present keys are
        // never reseeded (AC-6.7, DEC-78).
        private PlayerSnapshot Seed(PlayerSnapshot snapshot, out bool changed)
        {
            changed = false;
            for (int index = 0; index < m_currencies.Count; index++)
            {
                CurrencyDefinition currency = m_currencies.GetAt(index);
                if (!(currency is null) && currency.Id != null && !snapshot.TryGetCurrency(currency.Id, out _))
                {
                    snapshot = snapshot.WithCurrency(currency.Id, currency.StartingAmount);
                    changed = true;
                }
            }

            for (int index = 0; index < m_boosters.Count; index++)
            {
                BoosterDefinition booster = m_boosters.GetAt(index);
                if (!(booster is null) && !snapshot.TryGetBoosterStock(booster.Id, out _))
                {
                    snapshot = snapshot.WithBoosterStock(booster.Id, booster.InitialCharges);
                    changed = true;
                }
            }

            return snapshot;
        }

        // Advances every timed-refill currency the snapshot holds to now; returns the same instance when nothing moves.
        private PlayerSnapshot WithRefills(PlayerSnapshot snapshot, long nowUtcSeconds)
        {
            return PlayerEconomyRules.WithRefills(snapshot, m_currencies, nowUtcSeconds);
        }

        private static RefillState Advance(PlayerSnapshot snapshot, CurrencyDefinition currency, long nowUtcSeconds)
        {
            return PlayerEconomyRules.GetRefillState(snapshot, currency, nowUtcSeconds);
        }

        // One locked write (rule 3): prepare reads the committed snapshot inside the lock and returns the next one, or
        // null or the committed instance when nothing changes. The caller token cancels only the wait and the pre-write
        // check; a started store write runs under the service lifetime, so a leaving caller cannot cancel it.
        private async Task<PlayerSaveResult> WriteAsync(Func<PlayerSnapshot, PlayerSnapshot> prepare,
            CancellationToken cancellationToken)
        {
            if (m_disposed)
            {
                return PlayerSaveResult.Cancelled;
            }

            CancellationToken lifetime = m_lifetime.Token;
            BeginWrite();
            try
            {
                using (CancellationTokenSource linked =
                       CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime))
                {
                    if (!await TryEnterAsync(linked.Token))
                    {
                        return PlayerSaveResult.Cancelled;
                    }

                    try
                    {
                        if (!CanWrite())
                        {
                            return PlayerSaveResult.SaveFailed;
                        }

                        PlayerSnapshot next = prepare(m_committed);
                        if (next is null || ReferenceEquals(next, m_committed))
                        {
                            return PlayerSaveResult.Saved;
                        }

                        if (linked.IsCancellationRequested)
                        {
                            return PlayerSaveResult.Cancelled;
                        }

                        return await CommitAsync(next.WithRevision(m_committed.Revision + 1), lifetime);
                    }
                    finally
                    {
                        m_writeLock.Release();
                    }
                }
            }
            finally
            {
                EndWrite();
            }
        }

        private void RequireCatalogCurrency(CurrencyDefinition currency)
        {
            if (currency is null || currency.Id == null || !m_balances.ContainsKey(currency.Id))
            {
                throw new ArgumentException("The currency is not in the currency catalog.", nameof(currency));
            }
        }

        private BoosterPurchaseResult CheckPurchase(PlayerSnapshot snapshot, BoosterDefinition booster, out PlayerSnapshot next)
        {
            next = null;
            if (booster is null || !m_stock.ContainsKey(booster.Id))
            {
                return BoosterPurchaseResult.InvalidOffer;
            }

            CurrencyDefinition currency = booster.PurchaseCurrency;
            if (currency is null || currency.Id == null || !m_balances.ContainsKey(currency.Id))
            {
                return BoosterPurchaseResult.InvalidOffer;
            }

            return PlayerEconomyRules.Purchase(snapshot, booster, out next);
        }

        // Every ID is validated before any snapshot is built, so a combined debit is all or nothing. A spent charge
        // is written as a present 0, never removed, so a reload never reseeds it (AC-6.7).
        private BoosterConsumeResult CheckConsume(PlayerSnapshot snapshot, BoosterId[] ids, out PlayerSnapshot next)
        {
            next = null;
            if (ids == null)
            {
                return BoosterConsumeResult.InvalidRequest;
            }

            for (int index = 0; index < ids.Length; index++)
            {
                if (!m_stock.ContainsKey(ids[index]))
                {
                    return BoosterConsumeResult.InvalidRequest;
                }
            }

            return PlayerEconomyRules.Consume(snapshot, ids, out next);
        }

        // Returns a private copy of the debit list, or null for a null, empty, None or duplicate entry; every use
        // costs exactly one charge, so a duplicate is a caller error rather than a count (DEC-78).
        private static BoosterId[] CopyDebits(IReadOnlyList<BoosterId> debits)
        {
            if (debits == null || debits.Count == 0)
            {
                return null;
            }

            BoosterId[] ids = new BoosterId[debits.Count];
            for (int index = 0; index < ids.Length; index++)
            {
                BoosterId id = debits[index];
                if (id == BoosterId.None)
                {
                    return null;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (ids[previous] == id)
                    {
                        return null;
                    }
                }

                ids[index] = id;
            }

            return ids;
        }

        // Prepare, commit, publish (rule 2): the committed snapshot and the reactive values change only after the
        // store reports the snapshot durable. The await keeps the caller's context, so UI callers publish on the main
        // thread (rule 6). A write that already started completes; after disposal its result is ignored (rule 5).
        private async Task<PlayerSaveResult> CommitAsync(PlayerSnapshot next, CancellationToken cancellationToken)
        {
            try
            {
                await m_store.SaveAsync(next, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return PlayerSaveResult.Cancelled;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("The player data could not be saved; nothing changed: " + exception.Message);
                return PlayerSaveResult.SaveFailed;
            }

            if (m_disposed)
            {
                return PlayerSaveResult.Cancelled;
            }

            Publish(next);
            return PlayerSaveResult.Saved;
        }

        private void Publish(PlayerSnapshot snapshot)
        {
            m_committed = snapshot;
            foreach (KeyValuePair<string, ReactiveProperty<int>> balance in m_balances)
            {
                snapshot.TryGetCurrency(balance.Key, out int amount);
                balance.Value.SetValue(amount);
            }

            foreach (KeyValuePair<BoosterId, ReactiveProperty<int>> stock in m_stock)
            {
                snapshot.TryGetBoosterStock(stock.Key, out int count);
                stock.Value.SetValue(count);
            }

            m_settings[(int)PlayerSettingId.Music].SetValue(snapshot.Settings.Music);
            m_settings[(int)PlayerSettingId.Sound].SetValue(snapshot.Settings.Sound);
            m_settings[(int)PlayerSettingId.Vibration].SetValue(snapshot.Settings.Vibration);
            m_savedLevelIndex.SetValue(snapshot.CurrentLevelIndex);
        }

        private async Task<bool> TryEnterAsync(CancellationToken cancellationToken)
        {
            try
            {
                await m_writeLock.WaitAsync(cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        private bool CanWrite()
        {
            return m_loaded && !m_writesRefused && !m_disposed;
        }

        private void BeginWrite()
        {
            m_pendingWrites++;
            m_isSaving.SetValue(true);
        }

        private void EndWrite()
        {
            m_pendingWrites--;
            if (!m_disposed)
            {
                m_isSaving.SetValue(m_pendingWrites > 0);
            }
        }

        private void ThrowIfDisposed()
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerSaveService));
            }
        }
    }
}
