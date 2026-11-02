using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.Hub
{
    public sealed class DummyCurrencyRewardTests
    {
        private const string k_homePath = "Assets/ColorBlockJam/UI/Hub/Home.uxml";
        private const string k_catalogPath = "Assets/ColorBlockJam/Data/Currencies/CurrencyCatalog.asset";
        private VisualElement m_home;
        private FakeGrant m_grant;
        private DummyCurrencyReward m_reward;

        [SetUp]
        public void SetUp()
        {
            m_home = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_homePath).CloneTree();
            CurrencyCatalog catalog = AssetDatabase.LoadAssetAtPath<CurrencyCatalog>(k_catalogPath);
            Assert.That(catalog, Is.Not.Null);
            m_grant = new FakeGrant();
            m_reward = new DummyCurrencyReward(m_grant, catalog);
            m_reward.Attach(m_home);
        }

        [TearDown]
        public void TearDown()
        {
            m_reward?.Dispose();
        }

        [TestCase(CurrencyIds.Life, 1)]
        [TestCase(CurrencyIds.Coin, 100)]
        public void PlusGrantsOnlyItsHolderCurrency(string currencyId, int amount)
        {
            Click(Plus(currencyId));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { currencyId + ":" + amount }));
        }

        [Test]
        public void FullLifeDoesNotGrantCoins()
        {
            m_grant.Lives = 5;
            Click(Plus(CurrencyIds.Life));
            Assert.That(m_grant.Lives, Is.EqualTo(5));
            Assert.That(m_grant.Coins, Is.Zero);
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "life:1" }));
        }

        [Test]
        public void RoutingUsesHolderIdsAndIgnoresUnrelatedPlusButtons()
        {
            m_home.Q<VisualElement>("currency-holder-coin").SendToBack();
            var unrelated = new Button { name = "plus-button" };
            m_home.Add(unrelated);
            m_reward.Attach(m_home);
            Click(unrelated);
            Click(Plus(CurrencyIds.Coin));
            Click(Plus(CurrencyIds.Life));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "coin:100", "life:1" }));
        }

        [Test]
        public void ReattachDoesNotDuplicateAndDetachRemovesBothCallbacks()
        {
            m_reward.Attach(m_home);
            Click(Plus(CurrencyIds.Life));
            Click(Plus(CurrencyIds.Coin));
            m_reward.Detach();
            Click(Plus(CurrencyIds.Life));
            Click(Plus(CurrencyIds.Coin));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "life:1", "coin:100" }));
        }

        [Test]
        public void DisposeCancelsPendingGrantAndCannotReattach()
        {
            m_grant.Pending = new TaskCompletionSource<PlayerSaveResult>();
            Click(Plus(CurrencyIds.Life));
            m_reward.Dispose();
            Assert.That(m_grant.Token.IsCancellationRequested, Is.True);
            m_reward.Attach(m_home);
            Click(Plus(CurrencyIds.Coin));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "life:1" }));
            m_grant.Pending.SetResult(PlayerSaveResult.Cancelled);
        }

        [Test]
        public async Task PendingGrantBlocksBothButtonsUntilSaveCompletes()
        {
            m_grant.Pending = new TaskCompletionSource<PlayerSaveResult>();
            Click(Plus(CurrencyIds.Coin));
            Click(Plus(CurrencyIds.Coin));
            Click(Plus(CurrencyIds.Life));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "coin:100" }));
            m_grant.Pending.SetResult(PlayerSaveResult.Saved);
            await Task.Yield();
            m_grant.Pending = null;
            Click(Plus(CurrencyIds.Life));
            Assert.That(m_grant.Calls, Is.EqualTo(new[] { "coin:100", "life:1" }));
        }

        private Button Plus(string currencyId)
        {
            return m_home.Q<VisualElement>("currency-holder-" + currencyId).Q<Button>("plus-button");
        }

        private static void Click(Button button)
        {
            // Invoke UI Toolkit's click dispatch on the authored button, without a scene or player save.
            MethodInfo invoke = typeof(Clickable).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic,
                null, new[] { typeof(EventBase) }, null);
            Assert.That(invoke, Is.Not.Null);
            invoke.Invoke(button.clickable, new object[] { null });
        }

        private sealed class FakeGrant : ICurrencyGrant
        {
            public readonly List<string> Calls = new List<string>();
            public TaskCompletionSource<PlayerSaveResult> Pending;
            public CancellationToken Token;
            public int Lives;
            public int Coins;

            public Task<PlayerSaveResult> AddAsync(CurrencyDefinition currency, int amount, CancellationToken cancellationToken)
            {
                Calls.Add(currency.Id + ":" + amount);
                Token = cancellationToken;
                if (currency.Id == CurrencyIds.Life)
                {
                    Lives = Math.Min(5, Lives + amount);
                }
                else if (currency.Id == CurrencyIds.Coin)
                {
                    Coins += amount;
                }

                return Pending?.Task ?? Task.FromResult(PlayerSaveResult.Saved);
            }
        }
    }
}
