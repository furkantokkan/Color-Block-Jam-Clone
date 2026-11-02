using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the win save (AC-4.9, AC-6.14, DEC-92, DEC-173): the coin reward and the next saved level commit as one
    /// write and one revision, a failed write changes nothing and its retry adds the reward once, the saved level never
    /// moves back and keeps counting past the last database level, and the reward stops at the currency maximum.
    /// </summary>
    public sealed class LevelWinRecorderTests
    {
        private PlayerTestFixtures m_fixtures;
        private CurrencyDefinition m_coin;
        private CurrencyCatalog m_currencies;
        private BoosterCatalog m_boosters;

        [SetUp]
        public void SetUp()
        {
            m_fixtures = new PlayerTestFixtures();
            m_coin = m_fixtures.CreateCurrency("coin", "Coins", 0, 1000);
            m_currencies = m_fixtures.CreateCatalog(m_coin);
            m_boosters = m_fixtures.CreateBoosterCatalog(m_fixtures.CreateBooster(BoosterId.Rocket, 1, 10, m_coin));
        }

        [TearDown]
        public void TearDown()
        {
            m_fixtures.Dispose();
        }

        [UnityTest]
        public IEnumerator AWinAddsTheRewardAndTheNextLevelInOneWrite()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(coins: 40, level: 2));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));
            long revision = service.Committed.Revision;
            int saves = store.Inner.SaveCount;

            Task<PlayerSaveResult> win = service.RecordWinAsync(2, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(win);

            Assert.That(win.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(store.Inner.SaveCount, Is.EqualTo(saves + 1), "one write");
            Assert.That(service.Committed.Revision, Is.EqualTo(revision + 1), "one revision");
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(50));
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(3));
        }

        [UnityTest]
        public IEnumerator AFailedWinChangesNothingAndItsRetryAddsTheRewardOnce()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(coins: 40, level: 0));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            store.FailSaves = true;
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("could not be saved"));
            Task<PlayerSaveResult> failed = service.RecordWinAsync(0, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(failed);

            Assert.That(failed.Result, Is.EqualTo(PlayerSaveResult.SaveFailed));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(40), "a failed write publishes nothing");
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(0));

            store.FailSaves = false;
            Task<PlayerSaveResult> retry = service.RecordWinAsync(0, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(retry);

            Assert.That(retry.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(50), "the retry adds the reward once");
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReplayingAnEarlierLevelNeverMovesTheSavedLevelBack()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(coins: 0, level: 4));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Task<PlayerSaveResult> win = service.RecordWinAsync(1, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(win);

            Assert.That(win.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(4));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(10));
        }

        [UnityTest]
        public IEnumerator TheSavedLevelKeepsCountingPastTheLastDatabaseLevel()
        {
            // Level 5 of the five-level case: Hub then offers Level 6, which plays the first level again (DEC-173).
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(coins: 40, level: 4));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Task<PlayerSaveResult> win = service.RecordWinAsync(4, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(win);

            Assert.That(win.Result, Is.EqualTo(PlayerSaveResult.Saved));
            Assert.That(service.SavedLevelIndex.Value, Is.EqualTo(5));
            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator TheRewardStopsAtTheCurrencyMaximum()
        {
            ScriptedPlayerSnapshotStore store = new ScriptedPlayerSnapshotStore(Json(coins: 995, level: 0));
            PlayerSaveService service = Create(store);
            yield return PlayerTestFixtures.Await(service.LoadAsync(CancellationToken.None));

            Task<PlayerSaveResult> win = service.RecordWinAsync(0, m_coin, 10, CancellationToken.None);
            yield return PlayerTestFixtures.Await(win);

            Assert.That(service.GetBalance(m_coin).Value, Is.EqualTo(1000));
        }

        [Test]
        public void ACurrencyOutsideTheCatalogIsRejected()
        {
            PlayerSaveService service = Create(new ScriptedPlayerSnapshotStore());
            CurrencyDefinition gem = m_fixtures.CreateCurrency("gem", "Gems", 0, 10);

            Task<PlayerSaveResult> win = service.RecordWinAsync(0, gem, 10, CancellationToken.None);

            Assert.That(win.IsFaulted, Is.True);
            Assert.That(win.Exception.InnerException, Is.TypeOf<System.ArgumentException>());
        }

        private PlayerSaveService Create(ScriptedPlayerSnapshotStore store)
        {
            return m_fixtures.CreateService(store, m_currencies, m_boosters);
        }

        private static string Json(int coins, int level)
        {
            return PlayerTestFixtures.Seed(level,
                new Dictionary<string, int> { ["coin"] = coins },
                new Dictionary<string, int> { ["Rocket"] = 1 });
        }
    }
}
