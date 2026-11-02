using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the life economy on the shared currency system (DEC-174, TASK-20260928-124512603-1f645a0d AC-5 to AC-7):
    /// the timed refill rule, a life loss that never goes below 0 and starts the timer, one life per 30 minutes up to
    /// 5 (also while the app was closed), the 1-coin refill as one save, failures that change nothing, and the optional
    /// refill field in the save JSON.
    /// </summary>
    public sealed class LivesTests
    {
        private const long k_now = 1800000000;
        private const int k_refillSeconds = 1800;

        private PlayerTestFixtures m_fixtures;
        private CurrencyDefinition m_life;
        private CurrencyDefinition m_coin;
        private CurrencyCatalog m_currencies;
        private BoosterCatalog m_boosters;
        private FakeClock m_clock;
        private readonly List<PlayerSaveService> m_services = new List<PlayerSaveService>();

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PlayerTestFixtures();
            m_life = m_fixtures.CreateCurrency("life", "Lives", 5, 5, CurrencyDisplay.CountOnIcon);
            m_coin = m_fixtures.CreateCurrency("coin", "Coins", 0, 999999999);
            SetRefill(m_life, k_refillSeconds, 1, m_coin);
            m_currencies = m_fixtures.CreateCatalog(m_life, m_coin);
            m_boosters = m_fixtures.CreateBoosterCatalog();
            m_clock = new FakeClock { UtcNowSeconds = k_now };
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < m_services.Count; index++)
            {
                m_services[index].Dispose();
            }

            m_services.Clear();
            m_fixtures.Dispose();
        }

        [Test]
        public void TheRefillRuleKeepsPartialProgressAndStopsAtTheMaximum()
        {
            RefillState full = CurrencyRefill.Advance(5, 5, k_refillSeconds, false, 0, k_now);
            Assert.That(full.Amount, Is.EqualTo(5));
            Assert.That(full.IsRefilling, Is.False);

            RefillState started = CurrencyRefill.Advance(3, 5, k_refillSeconds, false, 0, k_now);
            Assert.That(started.Amount, Is.EqualTo(3));
            Assert.That(started.IsRefilling, Is.True);
            Assert.That(started.StartUtcSeconds, Is.EqualTo(k_now));

            // 45 minutes after the start: one life, and the 15 minutes past it count towards the next one.
            RefillState partial = CurrencyRefill.Advance(3, 5, k_refillSeconds, true, k_now - 2700, k_now);
            Assert.That(partial.Amount, Is.EqualTo(4));
            Assert.That(partial.StartUtcSeconds, Is.EqualTo(k_now - 900));
            Assert.That(CurrencyRefill.GetSecondsUntilNext(partial, k_refillSeconds, k_now), Is.EqualTo(900));

            RefillState capped = CurrencyRefill.Advance(1, 5, k_refillSeconds, true, k_now - 3 * 3600, k_now);
            Assert.That(capped.Amount, Is.EqualTo(5));
            Assert.That(capped.IsRefilling, Is.False);

            // A clock moved back restarts the current unit instead of counting backwards.
            RefillState rewound = CurrencyRefill.Advance(2, 5, k_refillSeconds, true, k_now + 100, k_now);
            Assert.That(rewound.Amount, Is.EqualTo(2));
            Assert.That(rewound.StartUtcSeconds, Is.EqualTo(k_now));

            RefillState untimed = CurrencyRefill.Advance(2, 5, 0, false, 0, k_now);
            Assert.That(untimed.IsRefilling, Is.False);
            Assert.That(CurrencyRefill.GetSecondsUntilNext(untimed, 0, k_now), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LosingALifeStartsItsTimerAndNeverGoesBelowZero()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore();
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            LivesService lives = new LivesService(service, m_currencies);

            yield return PlayerTestFixtures.Await(lives.LoseOneAsync(CancellationToken.None));

            Assert.That(lives.Count.Value, Is.EqualTo(4));
            Assert.That(service.Committed.TryGetRefillStart("life", out long start), Is.True);
            Assert.That(start, Is.EqualTo(k_now));
            Assert.That(lives.GetSecondsUntilNext(), Is.EqualTo(k_refillSeconds));

            for (int loss = 0; loss < 5; loss++)
            {
                yield return PlayerTestFixtures.Await(lives.LoseOneAsync(CancellationToken.None));
            }

            Assert.That(lives.Count.Value, Is.EqualTo(0), "a loss at 0 lives keeps 0");
            Assert.That(lives.GetAvailable(), Is.EqualTo(0));
            Assert.That(PlayerSnapshotJson.Read(store.Inner.Json).Currencies["life"], Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OneLifeRefillsEveryThirtyMinutesUpToTheMaximum()
        {
            PlayerSaveService service = Create(new ScriptedPlayerSnapshotStore());
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            LivesService lives = new LivesService(service, m_currencies);
            yield return PlayerTestFixtures.Await(lives.LoseOneAsync(CancellationToken.None));
            yield return PlayerTestFixtures.Await(lives.LoseOneAsync(CancellationToken.None));
            Assert.That(lives.Count.Value, Is.EqualTo(3));

            m_clock.UtcNowSeconds = k_now + 2700;
            Assert.That(lives.GetAvailable(), Is.EqualTo(4), "one life is due after 30 minutes");
            Assert.That(lives.Count.Value, Is.EqualTo(3), "a due life is published only after its save");
            Assert.That(lives.GetSecondsUntilNext(), Is.EqualTo(900));

            yield return PlayerTestFixtures.Await(lives.RefreshAsync(CancellationToken.None));
            Assert.That(lives.Count.Value, Is.EqualTo(4));
            Assert.That(service.Committed.TryGetRefillStart("life", out long start), Is.True);
            Assert.That(start, Is.EqualTo(k_now + k_refillSeconds));

            m_clock.UtcNowSeconds = k_now + 10 * k_refillSeconds;
            yield return PlayerTestFixtures.Await(lives.RefreshAsync(CancellationToken.None));
            Assert.That(lives.Count.Value, Is.EqualTo(5));
            Assert.That(service.Committed.TryGetRefillStart("life", out _), Is.False, "no timer runs at the maximum");
            Assert.That(lives.GetSecondsUntilNext(), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator ARefillForOneCoinFillsEveryLifeInOneSave()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(life: 1, coin: 3, lifeStart: k_now));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            LivesService lives = new LivesService(service, m_currencies);
            long revision = service.Committed.Revision;
            int saves = store.SaveAttempts;

            var refill = lives.RefillAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(refill);

            Assert.That(refill.Result, Is.EqualTo(CurrencyRefillResult.Refilled));
            Assert.That(lives.Count.Value, Is.EqualTo(5));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(2));
            Assert.That(service.Committed.Revision, Is.EqualTo(revision + 1), "lives and coins commit together");
            Assert.That(store.SaveAttempts, Is.EqualTo(saves + 1));
            Assert.That(service.Committed.TryGetRefillStart("life", out _), Is.False);

            var again = lives.RefillAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(again);
            Assert.That(again.Result, Is.EqualTo(CurrencyRefillResult.AlreadyFull));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(2), "a full refill spends nothing");
            Assert.That(store.SaveAttempts, Is.EqualTo(saves + 1));
        }

        [UnityTest]
        public IEnumerator ARefillWithoutEnoughCoinsOrWithAFailedSaveChangesNothing()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(life: 0, coin: 0, lifeStart: k_now));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            LivesService lives = new LivesService(service, m_currencies);
            long revision = service.Committed.Revision;

            var poor = lives.RefillAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(poor);
            Assert.That(poor.Result, Is.EqualTo(CurrencyRefillResult.InsufficientFunds));
            Assert.That(lives.Count.Value, Is.EqualTo(0));
            Assert.That(service.Committed.Revision, Is.EqualTo(revision));

            yield return PlayerTestFixtures.Await(service.AddAsync(m_coin, 1, CancellationToken.None));
            revision = service.Committed.Revision;
            store.FailSaves = true;
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("could not be saved"));

            var failed = lives.RefillAsync(CancellationToken.None);
            yield return PlayerTestFixtures.Await(failed);
            Assert.That(failed.Result, Is.EqualTo(CurrencyRefillResult.SaveFailed));
            Assert.That(lives.Count.Value, Is.EqualTo(0));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(1));
            Assert.That(service.Committed.Revision, Is.EqualTo(revision));
        }

        [UnityTest]
        public IEnumerator AnOlderSaveStartsTheTimerAndTimeWhileClosedRefills()
        {
            ScriptedPlayerSnapshotStore older = new ScriptedPlayerSnapshotStore(Json(life: 3, coin: 0, lifeStart: null));
            PlayerSaveService first = Create(older);
            yield return PlayerTestFixtures.Await(first.LoadAsync(CancellationToken.None));

            Assert.That(first.GetBalance(m_life).Value, Is.EqualTo(3));
            Assert.That(first.Committed.TryGetRefillStart("life", out long start), Is.True);
            Assert.That(start, Is.EqualTo(k_now));
            Assert.That(PlayerSnapshotJson.Read(older.Inner.Json).RefillStarts["life"], Is.EqualTo(k_now),
                "the started timer is saved once at load");

            ScriptedPlayerSnapshotStore closed =
                new ScriptedPlayerSnapshotStore(Json(life: 1, coin: 0, lifeStart: k_now - 2 * 3600));
            PlayerSaveService second = Create(closed);
            yield return PlayerTestFixtures.Await(second.LoadAsync(CancellationToken.None));

            Assert.That(second.GetBalance(m_life).Value, Is.EqualTo(5), "two hours closed refill four lives");
            Assert.That(second.Committed.TryGetRefillStart("life", out _), Is.False);
        }

        [UnityTest]
        public IEnumerator AGrantThatReachesTheMaximumStopsTheTimer()
        {
            PlayerSaveService service = Create(new ScriptedPlayerSnapshotStore(Json(life: 4, coin: 0, lifeStart: k_now)));
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            yield return PlayerTestFixtures.Await(service.AddAsync(m_life, 1, CancellationToken.None));

            Assert.That(service.GetBalance(m_life).Value, Is.EqualTo(5));
            Assert.That(service.Committed.TryGetRefillStart("life", out _), Is.False);
        }

        [Test]
        public void RefillStartsSurviveTheSaveJsonAndStayOptional()
        {
            PlayerSnapshot snapshot = new PlayerSnapshot(4, 2, new Dictionary<string, int> { ["life"] = 2, ["coin"] = 7 },
                null, PlayerSettings.Default, new Dictionary<string, long> { ["life"] = k_now });

            PlayerSnapshot read = PlayerSnapshotJson.Read(PlayerSnapshotJson.Write(snapshot));
            Assert.That(read.RefillStarts["life"], Is.EqualTo(k_now));
            Assert.That(read.Currencies["life"], Is.EqualTo(2));

            string withoutTimer = PlayerSnapshotJson.Write(snapshot.WithRefillStart("life", null));
            StringAssert.DoesNotContain("currencyRefillStarts", withoutTimer, "a save without a timer keeps its old shape");
            Assert.That(PlayerSnapshotJson.Read(withoutTimer).RefillStarts.Count, Is.EqualTo(0));

            string negative = PlayerSnapshotJson.Write(snapshot).Replace(k_now.ToString(), "-5");
            Assert.Throws<InvalidDataException>(() => PlayerSnapshotJson.Read(negative));
        }

        private PlayerSaveService Create(IPlayerSnapshotStore store)
        {
            PlayerSaveService service = new PlayerSaveService(store, m_currencies, m_boosters, m_clock);
            m_services.Add(service);
            return service;
        }

        private static string Json(int life, int coin, long? lifeStart)
        {
            Dictionary<string, long> starts = new Dictionary<string, long>();
            if (lifeStart.HasValue)
            {
                starts["life"] = lifeStart.Value;
            }

            return PlayerSnapshotJson.Write(new PlayerSnapshot(1, 0,
                new Dictionary<string, int> { ["life"] = life, ["coin"] = coin }, null, PlayerSettings.Default, starts));
        }

        private static void SetRefill(CurrencyDefinition currency, int seconds, int price, CurrencyDefinition priceCurrency)
        {
            UnityEditor.SerializedObject serialized = new UnityEditor.SerializedObject(currency);
            serialized.FindProperty("m_refillSeconds").intValue = seconds;
            serialized.FindProperty("m_refillPrice").intValue = price;
            serialized.FindProperty("m_refillPriceCurrency").objectReferenceValue = priceCurrency;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class FakeClock : IUtcClock
        {
            public long UtcNowSeconds { get; set; }
        }
    }
}
