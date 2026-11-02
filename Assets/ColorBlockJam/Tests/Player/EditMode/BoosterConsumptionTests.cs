using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using Onity.Reactive;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the booster stock debit of B-14: publish only after commit, the shipped starting pair (ExtraTime and
    /// StartingRocket, DEC-167) as one atomic write and revision, cancellation before and after the write starts,
    /// serialization with purchases, invalid requests, the not-loaded case, no reseed on reload, and IsSaving while
    /// a debit is pending (AC-6.10, AC-BST-0.3).
    /// </summary>
    public sealed class BoosterConsumptionTests
    {
        private static readonly BoosterId[] s_rocket = { BoosterId.Rocket };

        // The shipped starting debit (DEC-167): the pre-level Rocket keeps its own stock, separate from the in-game
        // Rocket pool s_rocket debits.
        private static readonly BoosterId[] s_startingPair = { BoosterId.ExtraTime, BoosterId.StartingRocket };

        private PlayerTestFixtures m_fixtures;
        private CurrencyDefinition m_coin;
        private BoosterDefinition m_rocket;
        private CurrencyCatalog m_currencies;
        private BoosterCatalog m_boosters;
        private readonly List<ScriptedPlayerSnapshotStore> m_stores = new List<ScriptedPlayerSnapshotStore>();

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PlayerTestFixtures();
            CurrencyDefinition life = m_fixtures.CreateCurrency("life", "Lives", 5, 5, CurrencyDisplay.CountOnIcon);
            m_coin = m_fixtures.CreateCurrency("coin", "Coins", 0, 999999999);
            m_rocket = m_fixtures.CreateBooster(BoosterId.Rocket, 1, 10, m_coin);
            m_currencies = m_fixtures.CreateCatalog(life, m_coin);
            m_boosters = m_fixtures.CreateBoosterCatalog(
                m_fixtures.CreateBooster(BoosterId.TimeFreeze, 1, 10, m_coin),
                m_rocket,
                m_fixtures.CreateBooster(BoosterId.Hammer, 1, 10, m_coin),
                m_fixtures.CreateBooster(BoosterId.Vacuum, 1, 10, m_coin),
                m_fixtures.CreateBooster(BoosterId.ExtraTime, 1, 10, m_coin),
                m_fixtures.CreateBooster(BoosterId.StartingRocket, 1, 10, m_coin));
        }

        [TearDown]
        public void TearDown()
        {
            // A gated save ignores its token and holds the service lock, so every gate is released before the
            // fixtures dispose the services that depend on it.
            for (int index = 0; index < m_stores.Count; index++)
            {
                m_stores[index].Gate?.TrySetResult(true);
            }

            m_stores.Clear();
            m_fixtures.Dispose();
        }

        [UnityTest]
        public IEnumerator AGatedConsumePublishesStockAndRevisionOnlyAfterPersistence()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 2));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            IBoosterConsumer consumer = service;
            IBoosterShop shop = service;

            List<int> stocks = new List<int>();
            using (service.GetStock(BoosterId.Rocket).Subscribe(stock => stocks.Add(stock), false))
            {
                store.Gate = new TaskCompletionSource<bool>();
                Task<BoosterConsumeResult> consume = consumer.ConsumeAsync(s_rocket, CancellationToken.None);
                yield return null;

                Assert.That(consume.IsCompleted, Is.False);
                Assert.That(shop.IsSaving.Value, Is.True, "Buy is disabled while the debit is pending");
                Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(2), "pending: still the old stock");
                Assert.That(service.Committed.Revision, Is.EqualTo(3));
                Assert.That(store.Inner.SaveCount, Is.EqualTo(0), "nothing is durable yet");
                Assert.That(stocks, Is.Empty);

                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(consume);

                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed));
                Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
                Assert.That(service.Committed.Revision, Is.EqualTo(4));
                Assert.That(stocks, Is.EqualTo(new[] { 1 }));
                Assert.That(shop.IsSaving.Value, Is.False);
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20), "use leaves coins unchanged");
            }

            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.BoosterStock["Rocket"], Is.EqualTo(1));
            Assert.That(saved.Currencies["coin"], Is.EqualTo(20));
            Assert.That(saved.Revision, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator ACombinedStartingDebitIsOneWriteAndOneRevision()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, CancellationToken.None);
            yield return PlayerTestFixtures.Await(consume);

            Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed));
            Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1), "one store write for both charges");
            Assert.That(service.Committed.Revision, Is.EqualTo(4), "one revision for both charges");
            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(0));
            Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(0));
            Assert.That(service.GetStock(BoosterId.Hammer).Value, Is.EqualTo(1), "unlisted stock is untouched");
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1),
                "the in-game Rocket pool is a separate stock (DEC-167)");
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20));

            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.BoosterStock.ContainsKey("ExtraTime"), Is.True, "a spent charge stays a present key");
            Assert.That(saved.BoosterStock["ExtraTime"], Is.EqualTo(0));
            Assert.That(saved.BoosterStock["StartingRocket"], Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator InsufficientStockOnOneIdChangesNeither()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1, 0));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            List<int> stocks = new List<int>();
            using (service.GetStock(BoosterId.ExtraTime).Subscribe(stock => stocks.Add(stock), false))
            {
                Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, CancellationToken.None);
                yield return PlayerTestFixtures.Await(consume);

                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.InsufficientStock));
                Assert.That(stocks, Is.Empty, "no notification");
            }

            Assert.That(store.SaveAttempts, Is.EqualTo(writes), "no store call");
            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1), "the other charge is not spent");
            Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(0));
            Assert.That(service.Committed.Revision, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator AFailedSaveChangesNeither()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            string before = store.Inner.Json;

            List<int> stocks = new List<int>();
            using (service.GetStock(BoosterId.StartingRocket).Subscribe(stock => stocks.Add(stock), false))
            {
                store.FailSaves = true;
                Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, CancellationToken.None);
                yield return PlayerTestFixtures.Await(consume);

                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.SaveFailed));
                Assert.That(stocks, Is.Empty);
            }

            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1));
            Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1));
            Assert.That(service.Committed.Revision, Is.EqualTo(3));
            Assert.That(store.Inner.Json, Is.EqualTo(before), "the previous snapshot is kept");
        }

        [UnityTest]
        public IEnumerator ACancellationBeforeTheLockWritesNothing()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, cancelled.Token);
                yield return PlayerTestFixtures.Await(consume);

                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Cancelled));
                Assert.That(store.SaveAttempts, Is.EqualTo(writes), "a pre-cancelled debit never reaches the store");
            }

            // Cancelled while it waits behind another write: still nothing spent.
            store.Gate = new TaskCompletionSource<bool>();
            using (CancellationTokenSource attempt = new CancellationTokenSource())
            {
                try
                {
                    Task<PlayerSaveResult> other =
                        service.SetAsync(PlayerSettingId.Music, false, CancellationToken.None);
                    Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, attempt.Token);
                    yield return null;
                    Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1), "the debit waits for the lock");

                    attempt.Cancel();
                    yield return PlayerTestFixtures.Await(consume);
                    Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Cancelled));

                    store.Gate.SetResult(true);
                    yield return PlayerTestFixtures.Await(other);
                    Assert.That(other.Result, Is.EqualTo(PlayerSaveResult.Saved));
                }
                finally
                {
                    store.Gate.TrySetResult(true);
                }
            }

            Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1), "only the setting was written");
            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(1));
            Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(1));
            Assert.That(PlayerSnapshotJson.Read(store.Inner.Json).BoosterStock["StartingRocket"], Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ACancellationAfterTheWriteStartedStillEndsConsumedAndDurable()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            store.Gate = new TaskCompletionSource<bool>();
            using (CancellationTokenSource attempt = new CancellationTokenSource())
            {
                try
                {
                    Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_startingPair, attempt.Token);
                    yield return null;
                    Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1), "the store write has started");

                    // The attempt ends while the write is in progress.
                    attempt.Cancel();
                    store.Gate.SetResult(true);
                    yield return PlayerTestFixtures.Await(consume);

                    Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed));
                }
                finally
                {
                    store.Gate.TrySetResult(true);
                }
            }

            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(0));
            Assert.That(service.GetStock(BoosterId.StartingRocket).Value, Is.EqualTo(0));
            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.BoosterStock["ExtraTime"], Is.EqualTo(0), "the debit is durable");
            Assert.That(saved.BoosterStock["StartingRocket"], Is.EqualTo(0));
            Assert.That(saved.Revision, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator AConsumeAndAPurchaseStartedTogetherRunOneAtATime()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 0));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            store.Gate = new TaskCompletionSource<bool>();
            try
            {
                Task<BoosterPurchaseResult> purchase = service.PurchaseAsync(m_rocket, CancellationToken.None);
                Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_rocket, CancellationToken.None);
                yield return null;
                Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1), "the debit waits for the purchase write");

                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(Task.WhenAll(purchase, consume));

                Assert.That(purchase.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed),
                    "the debit reads the purchased charge");
            }
            finally
            {
                store.Gate.TrySetResult(true);
            }

            Assert.That(store.SaveAttempts, Is.EqualTo(writes + 2));
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(0));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(10));
            Assert.That(service.Committed.Revision, Is.EqualTo(5));

            // The reverse order: the debit finds no charge and writes nothing; the purchase still succeeds.
            ScriptedPlayerSnapshotStore reverseStore = CreateStore(Seeded(20, 1, 0));
            PlayerSaveService reverse = Create(reverseStore);
            yield return PlayerTestFixtures.Await(reverse.LoadAsync(CancellationToken.None));
            Task<BoosterConsumeResult> first = reverse.ConsumeAsync(s_rocket, CancellationToken.None);
            Task<BoosterPurchaseResult> second = reverse.PurchaseAsync(m_rocket, CancellationToken.None);
            yield return PlayerTestFixtures.Await(Task.WhenAll(first, second));

            Assert.That(first.Result, Is.EqualTo(BoosterConsumeResult.InsufficientStock));
            Assert.That(second.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
            Assert.That(reverseStore.SaveAttempts, Is.EqualTo(1));
            Assert.That(reverse.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
            Assert.That(reverse.GetBalance(m_coin).Value, Is.EqualTo(10));
            Assert.That(reverse.Committed.Revision, Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DuplicateUnknownAndEmptyRequestsAreInvalidWithNoWrite()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 2, 2));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            yield return AssertConsume(service, new[] { BoosterId.Rocket, BoosterId.Rocket },
                BoosterConsumeResult.InvalidRequest);
            yield return AssertConsume(service, new[] { BoosterId.Rocket, (BoosterId)99 },
                BoosterConsumeResult.InvalidRequest);
            yield return AssertConsume(service, new[] { BoosterId.None }, BoosterConsumeResult.InvalidRequest);
            yield return AssertConsume(service, new BoosterId[0], BoosterConsumeResult.InvalidRequest);
            yield return AssertConsume(service, null, BoosterConsumeResult.InvalidRequest);

            Assert.That(store.SaveAttempts, Is.EqualTo(writes), "no rejected debit writes");
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(2));
            Assert.That(service.GetStock(BoosterId.ExtraTime).Value, Is.EqualTo(2));
            Assert.That(service.Committed.Revision, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator AConsumeBeforeTheLoadIsSaveFailedWithNoStoreCall()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1));
            PlayerSaveService service = Create(store);

            Task<BoosterConsumeResult> consume = service.ConsumeAsync(s_rocket, CancellationToken.None);
            yield return PlayerTestFixtures.Await(consume);

            Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.SaveFailed));
            Assert.That(store.SaveAttempts, Is.EqualTo(0));
            Assert.That(store.Inner.SaveCount, Is.EqualTo(0));
            Assert.That(service.IsSaving.Value, Is.False);
        }

        [UnityTest]
        public IEnumerator ASpentChargeReloadsAsZeroAndIsNeverReseeded()
        {
            // An empty store: the load seeds the initial charge of 1 once.
            ScriptedPlayerSnapshotStore store = CreateStore(null);
            PlayerSaveService first = Create(store);
            yield return PlayerTestFixtures.Await(first.LoadAsync(CancellationToken.None));
            Assert.That(first.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));

            Task<BoosterConsumeResult> consume = first.ConsumeAsync(s_rocket, CancellationToken.None);
            yield return PlayerTestFixtures.Await(consume);
            Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed));
            int saves = store.Inner.SaveCount;

            PlayerSaveService second = Create(store);
            yield return PlayerTestFixtures.Await(second.LoadAsync(CancellationToken.None));

            Assert.That(store.Inner.SaveCount, Is.EqualTo(saves), "the reload writes nothing");
            Assert.That(second.GetStock(BoosterId.Rocket).Value, Is.EqualTo(0), "a spent charge stays spent");
            Assert.That(second.GetStock(BoosterId.Hammer).Value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TheDebitListIsCopiedBeforeTheFirstAwait()
        {
            ScriptedPlayerSnapshotStore store = CreateStore(Seeded(20, 1, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            // The lock is held by another write, so the debit is still pending when the caller reuses its buffer.
            store.Gate = new TaskCompletionSource<bool>();
            BoosterId[] buffer = { BoosterId.Rocket };
            try
            {
                Task<PlayerSaveResult> other = service.SetAsync(PlayerSettingId.Sound, false, CancellationToken.None);
                Task<BoosterConsumeResult> consume = service.ConsumeAsync(buffer, CancellationToken.None);
                buffer[0] = BoosterId.Hammer;
                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(Task.WhenAll(other, consume));

                Assert.That(consume.Result, Is.EqualTo(BoosterConsumeResult.Consumed));
            }
            finally
            {
                store.Gate.TrySetResult(true);
            }

            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(0));
            Assert.That(service.GetStock(BoosterId.Hammer).Value, Is.EqualTo(1));
        }

        private ScriptedPlayerSnapshotStore CreateStore(string json)
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(json);
            m_stores.Add(store);
            return store;
        }

        private PlayerSaveService Create(IPlayerSnapshotStore store)
        {
            return m_fixtures.CreateService(store, m_currencies, m_boosters);
        }

        private static IEnumerator AssertConsume(IBoosterConsumer consumer, IReadOnlyList<BoosterId> debits,
            BoosterConsumeResult expected)
        {
            Task<BoosterConsumeResult> consume = consumer.ConsumeAsync(debits, CancellationToken.None);
            yield return PlayerTestFixtures.Await(consume);
            Assert.That(consume.Result, Is.EqualTo(expected), debits == null ? "null list" : debits.Count + " IDs");
        }

        // Every catalog key is present, so the load writes nothing and the revision starts at PlayerTestFixtures.SeedRevision.
        private static string Seeded(int coins, int extraTimeStock, int rocketStock, int startingRocketStock = 1)
        {
            return PlayerTestFixtures.Seed(0,
                new Dictionary<string, int> { ["life"] = 5, ["coin"] = coins },
                new Dictionary<string, int>
                {
                    ["TimeFreeze"] = 1, ["Rocket"] = rocketStock, ["Hammer"] = 1, ["Vacuum"] = 1,
                    ["ExtraTime"] = extraTimeStock, ["StartingRocket"] = startingRocketStock
                });
        }
    }
}
