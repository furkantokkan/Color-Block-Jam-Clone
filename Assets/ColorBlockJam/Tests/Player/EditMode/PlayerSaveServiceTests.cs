using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using Onity.Reactive;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the player save service: seed once, unknown keys kept, a newer format never overwritten,
    /// publish only after commit, failed saves change nothing, purchase outcomes and serialized purchases, and
    /// settings persistence (player-data-currencies AC-2, AC-3, AC-4, AC-6, AC-7, AC-8).
    /// </summary>
    public sealed class PlayerSaveServiceTests
    {
        private PlayerTestFixtures m_fixtures;
        private CurrencyDefinition m_life;
        private CurrencyDefinition m_coin;
        private BoosterDefinition m_rocket;
        private BoosterDefinition m_hammer;
        private CurrencyCatalog m_currencies;
        private BoosterCatalog m_boosters;

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PlayerTestFixtures();
            m_life = m_fixtures.CreateCurrency("life", "Lives", 5, 5, CurrencyDisplay.CountOnIcon);
            m_coin = m_fixtures.CreateCurrency("coin", "Coins", 0, 999999999);
            m_rocket = m_fixtures.CreateBooster(BoosterId.Rocket, 1, 10, m_coin);
            m_hammer = m_fixtures.CreateBooster(BoosterId.Hammer, 2, 10, m_coin);
            m_currencies = m_fixtures.CreateCatalog(m_life, m_coin);
            m_boosters = m_fixtures.CreateBoosterCatalog(m_rocket, m_hammer);
        }

        [TearDown]
        public void TearDown()
        {
            m_fixtures.Dispose();
        }

        [UnityTest]
        public IEnumerator AnEmptyStoreIsSeededWithOneWrite()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore();
            PlayerSaveService service = Create(store);

            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Assert.That(store.SaveAttempts, Is.EqualTo(1));
            Assert.That(store.Inner.SaveCount, Is.EqualTo(1));
            Assert.That(service.GetBalance(m_life).Value, Is.EqualTo(5));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(0));
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
            Assert.That(service.GetStock(BoosterId.Hammer).Value, Is.EqualTo(2));
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(0));
            Assert.That(service.Get(PlayerSettingId.Music).Value, Is.True);
            Assert.That(service.Get(PlayerSettingId.Sound).Value, Is.True);
            Assert.That(service.Get(PlayerSettingId.Vibration).Value, Is.True);
            Assert.That(service.Committed.Revision, Is.EqualTo(1));
            Assert.That(PlayerSnapshotJson.Read(store.Inner.Json).Currencies["life"], Is.EqualTo(5));
        }

        [UnityTest]
        public IEnumerator OnlyMissingKeysAreSeededOnceAndANewCurrencyNeedsNoCode()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(@"{
  ""formatVersion"": 1, ""revision"": 4, ""currentLevelIndex"": 3,
  ""currencies"": { ""coin"": 42 },
  ""boosterStock"": { ""Rocket"": 0 },
  ""settings"": { ""music"": false, ""sound"": true, ""vibration"": true }
}");

            // A third currency created at runtime and only listed in the catalog.
            CurrencyDefinition gem = m_fixtures.CreateCurrency("gem", "Gems", 3, 50);
            m_currencies = m_fixtures.CreateCatalog(m_life, m_coin, gem);
            PlayerSaveService first = Create(store);
            yield return PlayerTestFixtures.Await(first.LoadAsync(CancellationToken.None));

            Assert.That(store.Inner.SaveCount, Is.EqualTo(1), "one write seeds every missing key");
            Assert.That(first.GetBalance(m_coin).Value, Is.EqualTo(42), "present keys are never reseeded");
            Assert.That(first.GetBalance(m_life).Value, Is.EqualTo(5));
            Assert.That(first.GetBalance(gem).Value, Is.EqualTo(3));
            Assert.That(first.GetStock(BoosterId.Rocket).Value, Is.EqualTo(0), "a spent stock stays spent");
            Assert.That(first.GetStock(BoosterId.Hammer).Value, Is.EqualTo(2));
            Assert.That(first.SavedLevelIndex.Value, Is.EqualTo(3));
            Assert.That(first.Get(PlayerSettingId.Music).Value, Is.False);
            Assert.That(first.Committed.Revision, Is.EqualTo(5));

            PlayerSaveService second = Create(store);
            yield return PlayerTestFixtures.Await(second.LoadAsync(CancellationToken.None));

            Assert.That(store.Inner.SaveCount, Is.EqualTo(1), "a second load writes nothing");
            Assert.That(second.GetBalance(gem).Value, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator UnknownKeysSurviveALaterWrite()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(PlayerSnapshotJsonTests.UnknownKeysJson);
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Task<PlayerSaveResult> set = service.SetAsync(PlayerSettingId.Vibration, false, CancellationToken.None);
            yield return PlayerTestFixtures.Await(set);

            Assert.That(set.Result, Is.EqualTo(PlayerSaveResult.Saved));
            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.Currencies["gem"], Is.EqualTo(7));
            Assert.That(saved.BoosterStock["Magnet"], Is.EqualTo(9));
            Assert.That(saved.Settings.Vibration, Is.False);
        }

        [UnityTest]
        public IEnumerator ANewerFormatLoadsDefaultsInMemoryAndIsNeverOverwritten()
        {
            string newer = PlayerSnapshotJsonTests.UnknownKeysJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 2");
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(newer);
            PlayerSaveService service = Create(store);

            LogAssert.Expect(LogType.Error, new Regex("format version 2"));
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Assert.That(service.WritesRefused, Is.True);
            Assert.That(service.GetBalance(m_life).Value, Is.EqualTo(5));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(0));

            Task<PlayerSaveResult> set = service.SetAsync(PlayerSettingId.Music, false, CancellationToken.None);
            yield return PlayerTestFixtures.Await(set);

            Assert.That(set.Result, Is.EqualTo(PlayerSaveResult.SaveFailed));
            Assert.That(store.SaveAttempts, Is.EqualTo(0));
            Assert.That(store.Inner.Json, Is.EqualTo(newer), "the newer save is untouched");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator APurchasePublishesOnlyAfterItsSaveCommits()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            List<int> balances = new List<int>();
            using (service.GetBalance(m_coin).Subscribe(balance => balances.Add(balance), false))
            {
                store.Gate = new TaskCompletionSource<bool>();
                Task<BoosterPurchaseResult> purchase = service.PurchaseAsync(m_rocket, CancellationToken.None);
                yield return null;

                Assert.That(purchase.IsCompleted, Is.False);
                Assert.That(service.IsSaving.Value, Is.True);
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20), "pending: still the old balance");
                Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
                Assert.That(balances, Is.Empty);

                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(purchase);

                Assert.That(purchase.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(10));
                Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(2));
                Assert.That(balances, Is.EqualTo(new[] { 10 }));
                Assert.That(service.IsSaving.Value, Is.False);
            }

            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.Currencies["coin"], Is.EqualTo(10), "one snapshot holds the debit");
            Assert.That(saved.BoosterStock["Rocket"], Is.EqualTo(2), "and the grant");
        }

        [UnityTest]
        public IEnumerator AFailedPurchaseSavePublishesNothing()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            List<int> balances = new List<int>();
            using (service.GetBalance(m_coin).Subscribe(balance => balances.Add(balance), false))
            {
                store.FailSaves = true;
                Task<BoosterPurchaseResult> purchase = service.PurchaseAsync(m_rocket, CancellationToken.None);
                yield return PlayerTestFixtures.Await(purchase);

                Assert.That(purchase.Result, Is.EqualTo(BoosterPurchaseResult.SaveFailed));
                Assert.That(balances, Is.Empty);
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20));
                Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(1));
                Assert.That(service.Committed.Revision, Is.EqualTo(3));
            }
        }

        [UnityTest]
        public IEnumerator PurchaseOutcomesChangeNothingUnlessPurchased()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(5, 99));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            int writes = store.SaveAttempts;

            yield return AssertPurchase(service, m_rocket, BoosterPurchaseResult.InsufficientFunds);
            yield return AssertPurchase(service, m_fixtures.CreateBooster(BoosterId.Rocket, 1, 10, null),
                BoosterPurchaseResult.InvalidOffer);
            yield return AssertPurchase(service, m_fixtures.CreateBooster(BoosterId.Rocket, 1, 0, m_coin),
                BoosterPurchaseResult.InvalidOffer);
            yield return AssertPurchase(service, null, BoosterPurchaseResult.InvalidOffer);

            Assert.That(store.SaveAttempts, Is.EqualTo(writes), "no rejected purchase writes");
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(5));

            ScriptedPlayerSnapshotStore fullStore = new ScriptedPlayerSnapshotStore(WithCoins(500, 99));
            PlayerSaveService full = Create(fullStore);
            yield return PlayerTestFixtures.Await(full.LoadAsync(CancellationToken.None));
            yield return AssertPurchase(full, m_rocket, BoosterPurchaseResult.StockFull);
            Assert.That(full.GetBalance(m_coin).Value, Is.EqualTo(500));
            Assert.That(full.GetStock(BoosterId.Rocket).Value, Is.EqualTo(99));
        }

        [UnityTest]
        public IEnumerator PurchasesStartedTogetherRunOneAtATime()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(15, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            store.Gate = new TaskCompletionSource<bool>();
            Task<BoosterPurchaseResult> first = service.PurchaseAsync(m_rocket, CancellationToken.None);
            Task<BoosterPurchaseResult> second = service.PurchaseAsync(m_rocket, CancellationToken.None);
            yield return null;
            Assert.That(store.SaveAttempts, Is.EqualTo(1), "the second purchase waits for the first write");
            store.Gate.SetResult(true);
            yield return PlayerTestFixtures.Await(Task.WhenAll(first, second));

            Assert.That(first.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
            Assert.That(second.Result, Is.EqualTo(BoosterPurchaseResult.InsufficientFunds),
                "the second reads the committed balance of 5");
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(5));
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(2));

            ScriptedPlayerSnapshotStore richStore = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService rich = Create(richStore);
            yield return PlayerTestFixtures.Await(rich.LoadAsync(CancellationToken.None));
            Task<BoosterPurchaseResult> a = rich.PurchaseAsync(m_rocket, CancellationToken.None);
            Task<BoosterPurchaseResult> b = rich.PurchaseAsync(m_rocket, CancellationToken.None);
            yield return PlayerTestFixtures.Await(Task.WhenAll(a, b));

            Assert.That(a.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
            Assert.That(b.Result, Is.EqualTo(BoosterPurchaseResult.Purchased));
            Assert.That(rich.GetBalance(m_coin).Value, Is.EqualTo(0));
            Assert.That(rich.GetStock(BoosterId.Rocket).Value, Is.EqualTo(3));
            Assert.That(PlayerSnapshotJson.Read(richStore.Inner.Json).Currencies["coin"], Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ASettingPersistsAcrossServicesAndAFailedSaveKeepsTheCommittedValue()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore();
            PlayerSaveService first = Create(store);
            yield return PlayerTestFixtures.Await(first.LoadAsync(CancellationToken.None));

            Task<PlayerSaveResult> set = first.SetAsync(PlayerSettingId.Vibration, false, CancellationToken.None);
            yield return PlayerTestFixtures.Await(set);
            Assert.That(set.Result, Is.EqualTo(PlayerSaveResult.Saved));

            PlayerSaveService second = Create(store);
            yield return PlayerTestFixtures.Await(second.LoadAsync(CancellationToken.None));
            Assert.That(second.Get(PlayerSettingId.Vibration).Value, Is.False);
            Assert.That(second.Get(PlayerSettingId.Music).Value, Is.True);

            store.FailSaves = true;
            Task<PlayerSaveResult> failed = second.SetAsync(PlayerSettingId.Vibration, true, CancellationToken.None);
            yield return PlayerTestFixtures.Await(failed);

            Assert.That(failed.Result, Is.EqualTo(PlayerSaveResult.SaveFailed));
            Assert.That(second.Get(PlayerSettingId.Vibration).Value, Is.False);
            Assert.That(PlayerSnapshotJson.Read(store.Inner.Json).Settings.Vibration, Is.False);
        }

        [UnityTest]
        public IEnumerator ACancelledWriteReturnsCancelledAndChangesNothing()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            using (CancellationTokenSource cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Task<BoosterPurchaseResult> purchase = service.PurchaseAsync(m_rocket, cancelled.Token);
                yield return PlayerTestFixtures.Await(purchase);

                Assert.That(purchase.Result, Is.EqualTo(BoosterPurchaseResult.Cancelled));
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20));
            }
        }

        [UnityTest]
        public IEnumerator AStartedSettingWriteSurvivesCallerCancellation()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            long revision = service.Committed.Revision;

            yield return CancelCallerDuringWrite(store,
                token => service.SetAsync(PlayerSettingId.Music, false, token), PlayerSaveResult.Saved);

            Assert.That(service.Get(PlayerSettingId.Music).Value, Is.False);
            Assert.That(PlayerSnapshotJson.Read(store.Inner.Json).Settings.Music, Is.False);
            Assert.That(service.Committed.Revision, Is.EqualTo(revision + 1));
            Assert.That(service.IsSaving.Value, Is.False);
        }

        [UnityTest]
        public IEnumerator AStartedPurchaseSurvivesCallerCancellation()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            long revision = service.Committed.Revision;

            yield return CancelCallerDuringWrite(store,
                token => service.PurchaseAsync(m_rocket, token), BoosterPurchaseResult.Purchased);

            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.Currencies["coin"], Is.EqualTo(10));
            Assert.That(saved.BoosterStock["Rocket"], Is.EqualTo(2));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(10));
            Assert.That(service.GetStock(BoosterId.Rocket).Value, Is.EqualTo(2));
            Assert.That(saved.Revision, Is.EqualTo(revision + 1));
        }

        [UnityTest]
        public IEnumerator AStartedCurrencyGrantSurvivesCallerCancellation()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            long revision = service.Committed.Revision;

            yield return CancelCallerDuringWrite(store,
                token => service.AddAsync(m_coin, 7, token), PlayerSaveResult.Saved);

            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(27));
            PlayerSnapshot saved = PlayerSnapshotJson.Read(store.Inner.Json);
            Assert.That(saved.Currencies["coin"], Is.EqualTo(27));
            Assert.That(saved.Revision, Is.EqualTo(revision + 1));
        }

        [UnityTest]
        public IEnumerator CancellingAQueuedWriteKeepsTheActiveWriteAndSavesNothingExtra()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            store.Gate = new TaskCompletionSource<bool>();
            using CancellationTokenSource caller = new CancellationTokenSource();
            Task<PlayerSaveResult> active = service.SetAsync(PlayerSettingId.Music, false, CancellationToken.None);
            try
            {
                Task<PlayerSaveResult> queued = service.AddAsync(m_coin, 7, caller.Token);
                caller.Cancel();
                yield return PlayerTestFixtures.Await(queued);

                Assert.That(queued.Result, Is.EqualTo(PlayerSaveResult.Cancelled));
                Assert.That(store.SaveAttempts, Is.EqualTo(1));
                Assert.That(service.IsSaving.Value, Is.True, "The first write still owns the pending state.");
                Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(20));

                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(active);
                Assert.That(active.Result, Is.EqualTo(PlayerSaveResult.Saved));
                Assert.That(store.Inner.SaveCount, Is.EqualTo(1));
                Assert.That(service.IsSaving.Value, Is.False);
            }
            finally
            {
                store.Gate.TrySetResult(true);
            }
        }

        [UnityTest]
        public IEnumerator UnchangedSettingsAndCappedGrantsKeepTheRevision()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(m_coin.MaxAmount, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            PlayerSnapshot before = service.Committed;

            Task<PlayerSaveResult> setting = service.SetAsync(PlayerSettingId.Music, true, CancellationToken.None);
            yield return PlayerTestFixtures.Await(setting);
            Task<PlayerSaveResult> grant = service.AddAsync(m_coin, 7, CancellationToken.None);
            yield return PlayerTestFixtures.Await(grant);

            Assert.That(setting.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(grant.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(store.SaveAttempts, Is.Zero);
            Assert.That(service.Committed, Is.SameAs(before));
        }

        [UnityTest]
        public IEnumerator AStartedSeedSaveSurvivesCallerCancellation()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore
            {
                Gate = new TaskCompletionSource<bool>()
            };
            PlayerSaveService service = Create(store);
            using CancellationTokenSource caller = new CancellationTokenSource();
            Task load = service.LoadAsync(caller.Token);
            try
            {
                Assert.That(store.SaveAttempts, Is.EqualTo(1));
                caller.Cancel();
                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(load);

                Assert.That(store.Inner.SaveCount, Is.EqualTo(1));
                Assert.That(service.Committed.Revision, Is.EqualTo(1));
                Assert.That(service.GetBalance(m_life).Value, Is.EqualTo(5));
                PlayerSaveService reloaded = Create(store);
                yield return PlayerTestFixtures.Await(reloaded.LoadAsync(CancellationToken.None));
                Assert.That(store.SaveAttempts, Is.EqualTo(1), "A later load does not seed again.");
            }
            finally
            {
                store.Gate.TrySetResult(true);
            }
        }

        [UnityTest]
        public IEnumerator DisposingTheServiceCancelsAStartedWriteWithoutPublishing()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(WithCoins(20, 1));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            List<int> published = new List<int>();
            using var subscription = service.GetBalance(m_coin).Subscribe(value => published.Add(value), false);
            store.Gate = new TaskCompletionSource<bool>();
            Task<PlayerSaveResult> write = service.AddAsync(m_coin, 7, CancellationToken.None);
            service.Dispose();
            store.Gate.SetResult(true);
            yield return PlayerTestFixtures.Await(write);

            Assert.That(write.Result, Is.EqualTo(PlayerSaveResult.Cancelled));
            Assert.That(store.Inner.SaveCount, Is.Zero);
            Assert.That(published, Is.Empty);
        }

        // Hold the store after entry so cancellation occurs during the write, not before the lock is acquired.
        private static IEnumerator CancelCallerDuringWrite<T>(ScriptedPlayerSnapshotStore store,
            Func<CancellationToken, Task<T>> start, T expected)
        {
            int writes = store.SaveAttempts;
            store.Gate = new TaskCompletionSource<bool>();
            using CancellationTokenSource caller = new CancellationTokenSource();
            Task<T> pending = start(caller.Token);
            try
            {
                Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1));
                Assert.That(pending.IsCompleted, Is.False);
                caller.Cancel();
                store.Gate.SetResult(true);
                yield return PlayerTestFixtures.Await(pending);
                Assert.That(pending.Result, Is.EqualTo(expected));
                Assert.That(store.SaveAttempts, Is.EqualTo(writes + 1));
            }
            finally
            {
                store.Gate.TrySetResult(true);
            }
        }

        private PlayerSaveService Create(IPlayerSnapshotStore store)
        {
            return m_fixtures.CreateService(store, m_currencies, m_boosters);
        }

        private static IEnumerator AssertPurchase(PlayerSaveService service, BoosterDefinition booster,
            BoosterPurchaseResult expected)
        {
            Task<BoosterPurchaseResult> purchase = service.PurchaseAsync(booster, CancellationToken.None);
            yield return PlayerTestFixtures.Await(purchase);
            Assert.That(purchase.Result, Is.EqualTo(expected), booster == null ? "null booster" : booster.name);
        }

        private static string WithCoins(int coins, int rocketStock)
        {
            return PlayerTestFixtures.Seed(0,
                new Dictionary<string, int> { ["life"] = 5, ["coin"] = coins },
                new Dictionary<string, int> { ["Rocket"] = rocketStock, ["Hammer"] = 2 });
        }
    }
}
