using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Hub.Lives;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Currencies;
using NUnit.Framework;
using Onity.Reactive;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Proves the Home presenter behaviour of design 7 with fake view, wallet, progress and navigator
    /// (contract scenarios 5, 6 and 8, AC-1.2, AC-1.7, AC-1.8).
    /// </summary>
    public sealed class HomePresenterTests
    {
        private readonly List<Object> m_assets = new List<Object>();
        private CurrencyDefinition m_life;
        private CurrencyDefinition m_coin;
        private CurrencyDefinition m_hidden;
        private FakeWallet m_wallet;
        private FakeProgress m_progress;
        private FakeNavigator m_navigator;
        private FakeHomeView m_view;

        [SetUp]
        public void SetUp()
        {
            m_life = CreateCurrency("life", CurrencyDisplay.CountOnIcon, 5, "Full", true);
            m_hidden = CreateCurrency("gem", CurrencyDisplay.Amount, 999, string.Empty, false);
            m_coin = CreateCurrency("coin", CurrencyDisplay.Amount, 999999999, string.Empty, true);
            m_wallet = new FakeWallet();
            m_wallet.Set(m_life, 5);
            m_wallet.Set(m_hidden, 3);
            m_wallet.Set(m_coin, 0);
            m_progress = new FakeProgress();
            m_navigator = new FakeNavigator();
            // "life" and "coin" mirror the two holders Home.uxml authors (DEC-143); "gem" is deliberately
            // unauthored so a Show On Home currency without a matching holder can be tested.
            m_view = new FakeHomeView(3, "life", "coin");
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < m_assets.Count; i++)
            {
                Object.DestroyImmediate(m_assets[i]);
            }

            m_assets.Clear();
            m_wallet.Dispose();
            m_progress.Dispose();
        }

        [Test]
        public void Bind_SavedIndexZeroOfFive_ReadsLevel1()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            Assert.That(m_view.LevelLabel, Is.EqualTo("Level 1"));
            Assert.That(m_view.LevelInteractable, Is.True);
        }

        [Test]
        public void Bind_SavedIndexPastLastLevel_KeepsCounting()
        {
            m_progress.Index.Value = 7;
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            Assert.That(m_view.LevelLabel, Is.EqualTo("Level 8"), "Level 8 replays the third level (DEC-173)");
        }

        [Test]
        public void SavedIndexChange_RebuildsLabelOnlyWhenThePlayableLevelChanges()
        {
            m_progress.Index.Value = 7;
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);
            int calls = m_view.SetLevelCalls;

            m_progress.Index.Value = 7;
            Assert.That(m_view.SetLevelCalls, Is.EqualTo(calls), "the same level writes nothing");

            m_progress.Index.Value = 2;
            Assert.That(m_view.SetLevelCalls, Is.EqualTo(calls + 1));
            Assert.That(m_view.LevelLabel, Is.EqualTo("Level 3"));
        }

        [Test]
        public void Route_ShowsCurrentThenNextLevelsLockedPastTheLastDatabaseLevel()
        {
            m_progress.Index.Value = 3;
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            Assert.That(m_view.RouteNodes[0], Is.EqualTo(new RouteNode("4", true, false)));
            Assert.That(m_view.RouteNodes[1], Is.EqualTo(new RouteNode("5", true, true)));
            Assert.That(m_view.RouteNodes[2], Is.EqualTo(new RouteNode("6", true, true)),
                "Level 6 replays the first level (DEC-173)");
        }

        [Test]
        public void Bind_NoLevels_ShowsDashDisabledAndIgnoresPress()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 0);
            m_view.PressLevel();

            Assert.That(m_view.LevelLabel, Is.EqualTo("Level -"));
            Assert.That(m_view.LevelInteractable, Is.False);
            Assert.That(m_view.RouteNodes[0].Visible, Is.False);
            Assert.That(m_navigator.PreLevelRequests, Is.Zero);
        }

        [Test]
        public void Bind_AddsOneHolderPerShowOnHomeCurrencyInCatalogOrder()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_hidden, m_coin);

            presenter.Bind(m_view, 5);

            Assert.That(m_view.Holders.Count, Is.EqualTo(2));
            Assert.That(m_view.Holders[0].CountOnIcon, Is.True, "life first");
            Assert.That(m_view.Holders[1].CountOnIcon, Is.False, "coin second");
        }

        [Test]
        public void Bind_FindsEachAuthoredHolderByCurrencyIdAndShowsIt()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            FakeHolder life = m_view.GetAuthoredHolder("life");
            FakeHolder coin = m_view.GetAuthoredHolder("coin");
            Assert.That(life.Visible, Is.True);
            Assert.That(life.CountOnIcon, Is.True, "life is CountOnIcon");
            Assert.That(coin.Visible, Is.True);
            Assert.That(coin.CountOnIcon, Is.False, "coin is Amount");
        }

        [Test]
        public void Bind_ShowOnHomeCurrencyWithNoAuthoredHolder_LogsOneErrorAndSkipsIt()
        {
            CurrencyDefinition missing = CreateCurrency("gem", CurrencyDisplay.Amount, 999, string.Empty, true);
            using HomePresenter presenter = CreatePresenter(m_life, missing, m_coin);

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("gem.*Home\\.uxml"));
            presenter.Bind(m_view, 5);

            Assert.That(m_view.Holders.Count, Is.EqualTo(2), "life and coin still bind; gem has no authored holder");
        }

        [Test]
        public void Bind_AuthoredHolderNotClaimedByAnyCurrency_StaysHidden()
        {
            using HomePresenter presenter = CreatePresenter(m_life);

            presenter.Bind(m_view, 5);

            Assert.That(m_view.GetAuthoredHolder("life").Visible, Is.True);
            Assert.That(m_view.GetAuthoredHolder("coin").Visible, Is.False, "coin is not in this catalog");
        }

        [Test]
        public void Rebind_ReusesTheSameAuthoredHolderInstances()
        {
            HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);
            FakeHolder life = m_view.GetAuthoredHolder("life");
            FakeHolder coin = m_view.GetAuthoredHolder("coin");

            presenter.Unbind();
            presenter.Bind(m_view, 5);

            Assert.That(m_view.GetAuthoredHolder("life"), Is.SameAs(life), "the authored holder is reused, not recreated");
            Assert.That(m_view.GetAuthoredHolder("coin"), Is.SameAs(coin));
            Assert.That(life.Visible, Is.True);
            Assert.That(coin.Visible, Is.True);
            presenter.Dispose();
        }

        [Test]
        public void CoinHolder_ShowsCompactBalances()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);
            FakeHolder coin = m_view.Holders[1];

            Assert.That(coin.Text, Is.EqualTo("0"));
            int[] balances = { 950, 1520, 12500, 1520000 };
            string[] expected = { "950", "1.52k", "12.5k", "1.52m" };
            for (int i = 0; i < balances.Length; i++)
            {
                m_wallet.Set(m_coin, balances[i]);
                Assert.That(coin.Text, Is.EqualTo(expected[i]));
            }
        }

        [Test]
        public void LifeHolder_AtMaximum_ShowsFullWithCountOnIcon()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            FakeHolder life = m_view.Holders[0];
            Assert.That(life.Text, Is.EqualTo("Full"));
            Assert.That(life.IconCount, Is.EqualTo("5"));
        }

        [Test]
        public void LifeHolder_BelowMaximum_ShowsTheCountInTheHolderAndOnTheIcon()
        {
            m_wallet.Set(m_life, 3);
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);

            presenter.Bind(m_view, 5);

            FakeHolder life = m_view.Holders[0];
            Assert.That(life.Text, Is.EqualTo("3"));
            Assert.That(life.IconCount, Is.EqualTo("3"));
        }

        [Test]
        public void Holder_TextChangesOnlyWhenTheBalanceChanges()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);
            FakeHolder coin = m_view.Holders[1];
            int writes = coin.TextWrites;

            m_wallet.Set(m_coin, 0);
            Assert.That(coin.TextWrites, Is.EqualTo(writes));

            m_wallet.Set(m_coin, 10);
            Assert.That(coin.TextWrites, Is.EqualTo(writes + 1));
        }

        [Test]
        public void Presses_AskTheNavigator()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);

            m_view.PressLevel();
            m_view.PressSettings();

            Assert.That(m_navigator.PreLevelRequests, Is.EqualTo(1));
            Assert.That(m_navigator.SettingsRequests, Is.EqualTo(1));
        }

        [Test]
        public void Presses_WithRealNavigatorAndNoOverlays_OpenNothingAndLogNothing()
        {
            using var navigator = new HubNavigator();
            using var presenter = new HomePresenter(m_wallet, m_progress, CreateCatalog(m_life, m_coin), navigator);
            presenter.Bind(m_view, 5);

            m_view.PressLevel();
            m_view.PressSettings();

            Assert.That(navigator.OpenCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Unbind_DisposesSubscriptionsAndDetachesTheViewButLeavesAuthoredHoldersInPlace()
        {
            m_progress.Index.Value = 1;
            HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);
            FakeHolder coin = m_view.Holders[1];
            int textWrites = coin.TextWrites;
            int levelCalls = m_view.SetLevelCalls;

            presenter.Unbind();
            m_wallet.Set(m_coin, 500);
            m_progress.Index.Value = 3;
            m_view.PressLevel();

            Assert.That(coin.TextWrites, Is.EqualTo(textWrites));
            Assert.That(m_view.SetLevelCalls, Is.EqualTo(levelCalls));
            Assert.That(m_navigator.PreLevelRequests, Is.Zero);
            // DEC-143: Unbind detaches bindings only; the authored holders are never removed.
            Assert.That(m_view.GetAuthoredHolder("coin"), Is.SameAs(coin));
            Assert.That(coin.Visible, Is.True, "unbind does not hide an authored holder");
            Assert.That(m_wallet.SubscriberFreeAfterUnbind(m_coin), Is.True);
            presenter.Dispose();
        }

        [Test]
        public void Bind_Twice_Throws()
        {
            using HomePresenter presenter = CreatePresenter(m_life, m_coin);
            presenter.Bind(m_view, 5);

            Assert.Throws<InvalidOperationException>(() => presenter.Bind(new FakeHomeView(3), 5));
        }

        [Test]
        public void Level_AtZeroLives_OpensTheRefillPopupInsteadOfThePreLevelPopup()
        {
            m_wallet.Set(m_life, 0);
            var lives = new FakeLives(m_life, 0);
            var menus = new FakeMenuNavigator();
            using var presenter = new HomePresenter(m_wallet, m_progress, CreateCatalog(m_life, m_coin), m_navigator,
                lives, menus);
            presenter.Bind(m_view, 5);

            m_view.PressLevel();

            Assert.That(menus.Opened, Is.EqualTo(new[] { LifeRefillOverlay.MenuId }), "DEC-174: no level starts at 0 lives");
            Assert.That(m_navigator.PreLevelRequests, Is.Zero);
            lives.Dispose();
        }

        [Test]
        public void Level_WithALifeAvailable_OpensThePreLevelPopup()
        {
            m_wallet.Set(m_life, 0);
            // The committed count is 0, but a refill is already due by the clock.
            var lives = new FakeLives(m_life, 0) { Available = 1 };
            var menus = new FakeMenuNavigator();
            using var presenter = new HomePresenter(m_wallet, m_progress, CreateCatalog(m_life, m_coin), m_navigator,
                lives, menus);
            presenter.Bind(m_view, 5);

            m_view.PressLevel();

            Assert.That(m_navigator.PreLevelRequests, Is.EqualTo(1));
            Assert.That(menus.Opened, Is.Empty);
            lives.Dispose();
        }

        [Test]
        public void Tick_BelowTheMaximum_ShowsTheTimeToTheNextLifeAndFullAtTheMaximum()
        {
            m_wallet.Set(m_life, 3);
            var lives = new FakeLives(m_life, 3) { SecondsUntilNext = 1500 };
            using var presenter = new HomePresenter(m_wallet, m_progress, CreateCatalog(m_life, m_coin), m_navigator,
                lives, new FakeMenuNavigator());
            presenter.Bind(m_view, 5);
            FakeHolder life = m_view.GetAuthoredHolder("life");

            presenter.Tick();
            Assert.That(life.Text, Is.EqualTo("25:00"));
            Assert.That(life.IconCount, Is.EqualTo("3"));

            int writes = life.TextWrites;
            presenter.Tick();
            Assert.That(life.TextWrites, Is.EqualTo(writes), "the same second writes no text");

            lives.SecondsUntilNext = 1499;
            presenter.Tick();
            Assert.That(life.Text, Is.EqualTo("24:59"));

            m_wallet.Set(m_life, 5);
            Assert.That(life.Text, Is.EqualTo("Full"));
            lives.Dispose();
        }

        [Test]
        public void Tick_WithALifeDue_CommitsOneRefreshAtATime()
        {
            m_wallet.Set(m_life, 3);
            var lives = new FakeLives(m_life, 3) { Available = 4, SecondsUntilNext = 900 };
            using var presenter = new HomePresenter(m_wallet, m_progress, CreateCatalog(m_life, m_coin), m_navigator,
                lives, new FakeMenuNavigator());
            presenter.Bind(m_view, 5);

            presenter.Tick();
            presenter.Tick();

            Assert.That(lives.RefreshCalls, Is.EqualTo(1), "a pending refresh is not started twice");
            lives.CompleteRefresh();
            Assert.That(presenter.PendingLifeRefresh.IsCompleted, Is.True);
            lives.Dispose();
        }

        private HomePresenter CreatePresenter(params CurrencyDefinition[] currencies)
        {
            return new HomePresenter(m_wallet, m_progress, CreateCatalog(currencies), m_navigator);
        }

        private CurrencyCatalog CreateCatalog(params CurrencyDefinition[] currencies)
        {
            var catalog = ScriptableObject.CreateInstance<CurrencyCatalog>();
            m_assets.Add(catalog);
            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("m_definitions");
            list.arraySize = currencies.Length;
            for (int i = 0; i < currencies.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = currencies[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        private CurrencyDefinition CreateCurrency(string id, CurrencyDisplay display, int max, string fullLabel, bool showOnHome)
        {
            var definition = ScriptableObject.CreateInstance<CurrencyDefinition>();
            m_assets.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("m_id").stringValue = id;
            serialized.FindProperty("m_displayName").stringValue = id;
            serialized.FindProperty("m_maxAmount").intValue = max;
            serialized.FindProperty("m_homeDisplay").enumValueIndex = (int)display;
            serialized.FindProperty("m_fullLabel").stringValue = fullLabel;
            serialized.FindProperty("m_showOnHome").boolValue = showOnHome;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private readonly struct RouteNode : IEquatable<RouteNode>
        {
            public readonly string Label;
            public readonly bool Visible;
            public readonly bool Locked;

            public RouteNode(string label, bool visible, bool locked)
            {
                Label = label;
                Visible = visible;
                Locked = locked;
            }

            public bool Equals(RouteNode other)
            {
                return Label == other.Label && Visible == other.Visible && Locked == other.Locked;
            }

            public override bool Equals(object obj)
            {
                return obj is RouteNode other && Equals(other);
            }

            public override int GetHashCode()
            {
                return (Label ?? string.Empty).GetHashCode() ^ (Visible ? 2 : 0) ^ (Locked ? 4 : 0);
            }

            public override string ToString()
            {
                return $"({Label}, visible {Visible}, locked {Locked})";
            }
        }

        /// <summary>
        /// Fakes the authored holders of Home.uxml (DEC-143): <paramref name="authoredHolderIds"/> names the
        /// holders "present" in Home.uxml, one persistent <see cref="FakeHolder"/> per id, found by
        /// <see cref="TryGetCurrencyHolder"/> and never created or destroyed by a bind.
        /// </summary>
        private sealed class FakeHomeView : IHomeView
        {
            private readonly Dictionary<string, FakeHolder> m_authoredHolders = new Dictionary<string, FakeHolder>();

            public FakeHomeView(int routeNodeCount, params string[] authoredHolderIds)
            {
                RouteNodes = new RouteNode[routeNodeCount];
                for (int i = 0; i < authoredHolderIds.Length; i++)
                {
                    m_authoredHolders.Add(authoredHolderIds[i], new FakeHolder());
                }
            }

            public event Action LevelPressed;

            public event Action SettingsPressed;

            public int RouteNodeCount => RouteNodes.Length;

            public RouteNode[] RouteNodes { get; }

            /// <summary>The holders shown by the most recent bind, in catalog order.</summary>
            public List<FakeHolder> Holders { get; } = new List<FakeHolder>();

            public string LevelLabel { get; private set; }

            public bool LevelInteractable { get; private set; }

            public int SetLevelCalls { get; private set; }

            public void SetLevel(string label, bool interactable)
            {
                SetLevelCalls++;
                LevelLabel = label;
                LevelInteractable = interactable;
            }

            public void SetRouteNode(int slot, string label, bool visible, bool locked)
            {
                RouteNodes[slot] = new RouteNode(label, visible, locked);
            }

            public void HideCurrencyHolders()
            {
                Holders.Clear();
                foreach (FakeHolder holder in m_authoredHolders.Values)
                {
                    holder.Visible = false;
                }
            }

            public bool TryGetCurrencyHolder(string currencyId, Sprite icon, bool countOnIcon, out ICurrencyHolderView holder)
            {
                if (!m_authoredHolders.TryGetValue(currencyId, out FakeHolder found))
                {
                    holder = null;
                    return false;
                }

                found.Configure(icon, countOnIcon);
                found.Visible = true;
                Holders.Add(found);
                holder = found;
                return true;
            }

            /// <summary>The persistent fake for one authored holder id, for tests to assert on directly.</summary>
            public FakeHolder GetAuthoredHolder(string id)
            {
                return m_authoredHolders[id];
            }

            public void PressLevel()
            {
                LevelPressed?.Invoke();
            }

            public void PressSettings()
            {
                SettingsPressed?.Invoke();
            }
        }

        private sealed class FakeHolder : ICurrencyHolderView
        {
            public bool CountOnIcon { get; private set; }

            public Sprite Icon { get; private set; }

            public bool Visible { get; set; }

            public string Text { get; private set; }

            public string IconCount { get; private set; }

            public int TextWrites { get; private set; }

            public void Configure(Sprite icon, bool countOnIcon)
            {
                Icon = icon;
                CountOnIcon = countOnIcon;
            }

            public void SetText(string text)
            {
                TextWrites++;
                Text = text;
            }

            public void SetIconCount(string text)
            {
                IconCount = text;
            }
        }

        private sealed class FakeWallet : IWallet, IDisposable
        {
            private readonly Dictionary<CurrencyDefinition, CountingProperty> m_balances =
                new Dictionary<CurrencyDefinition, CountingProperty>();

            public void Set(CurrencyDefinition currency, int value)
            {
                if (!m_balances.TryGetValue(currency, out CountingProperty property))
                {
                    property = new CountingProperty(value);
                    m_balances.Add(currency, property);
                    return;
                }

                property.Source.Value = value;
            }

            public bool SubscriberFreeAfterUnbind(CurrencyDefinition currency)
            {
                return m_balances[currency].LiveSubscriptions == 0;
            }

            public IReadOnlyReactiveProperty<int> GetBalance(CurrencyDefinition currency)
            {
                return m_balances[currency];
            }

            public bool CanAfford(CurrencyDefinition currency, int amount)
            {
                return m_balances[currency].Value >= amount;
            }

            public void Dispose()
            {
                foreach (CountingProperty property in m_balances.Values)
                {
                    property.Source.Dispose();
                }
            }
        }

        /// <summary>A ReactiveProperty wrapper that counts live subscriptions, to prove Unbind disposes them.</summary>
        private sealed class CountingProperty : IReadOnlyReactiveProperty<int>
        {
            public CountingProperty(int initial)
            {
                Source = new ReactiveProperty<int>(initial);
            }

            public ReactiveProperty<int> Source { get; }

            public int LiveSubscriptions { get; private set; }

            public int Value => Source.Value;

            public IDisposable Subscribe(Observer<int> observer, bool emitCurrentValue = true)
            {
                IDisposable inner = Source.Subscribe(observer, emitCurrentValue);
                LiveSubscriptions++;
                return new Subscription(this, inner);
            }

            private sealed class Subscription : IDisposable
            {
                private readonly CountingProperty m_owner;
                private IDisposable m_inner;

                public Subscription(CountingProperty owner, IDisposable inner)
                {
                    m_owner = owner;
                    m_inner = inner;
                }

                public void Dispose()
                {
                    if (m_inner == null)
                    {
                        return;
                    }

                    m_inner.Dispose();
                    m_inner = null;
                    m_owner.LiveSubscriptions--;
                }
            }
        }

        private sealed class FakeProgress : IPlayerProgress, IDisposable
        {
            public ReactiveProperty<int> Index { get; } = new ReactiveProperty<int>(0);

            public IReadOnlyReactiveProperty<int> SavedLevelIndex => Index;

            public void Dispose()
            {
                Index.Dispose();
            }
        }

        private sealed class FakeNavigator : IHubNavigator
        {
            public int SettingsRequests { get; private set; }

            public int PreLevelRequests { get; private set; }

            public int OpenCount => 0;

            public bool OpenSettings()
            {
                SettingsRequests++;
                return false;
            }

            public bool OpenPreLevel()
            {
                PreLevelRequests++;
                return false;
            }

            public bool Open(HubOverlayId id)
            {
                return false;
            }

            public bool Close(HubOverlayId id)
            {
                return false;
            }

            public bool CloseTop()
            {
                return false;
            }

            public bool IsOpen(HubOverlayId id)
            {
                return false;
            }
        }

        private sealed class FakeMenuNavigator : IHubMenuNavigator
        {
            public List<string> Opened { get; } = new List<string>();

            public bool HasMenu(string menuId)
            {
                return true;
            }

            public bool OpenMenu(string menuId)
            {
                Opened.Add(menuId);
                return true;
            }
        }

        /// <summary>Lives with a settable available count and countdown; a refresh stays pending until completed.</summary>
        private sealed class FakeLives : ILives, IDisposable
        {
            private readonly ReactiveProperty<int> m_count;
            private TaskCompletionSource<PlayerSaveResult> m_refresh;

            public FakeLives(CurrencyDefinition currency, int count)
            {
                Currency = currency;
                m_count = new ReactiveProperty<int>(count);
                Available = count;
            }

            public CurrencyDefinition Currency { get; }

            public IReadOnlyReactiveProperty<int> Count => m_count;

            public int Available { get; set; }

            public long SecondsUntilNext { get; set; }

            public int RefreshCalls { get; private set; }

            public int GetAvailable()
            {
                return Available;
            }

            public long GetSecondsUntilNext()
            {
                return SecondsUntilNext;
            }

            public Task<PlayerSaveResult> RefreshAsync(CancellationToken cancellationToken)
            {
                RefreshCalls++;
                m_refresh = new TaskCompletionSource<PlayerSaveResult>();
                return m_refresh.Task;
            }

            public void CompleteRefresh()
            {
                m_refresh?.TrySetResult(PlayerSaveResult.Saved);
            }

            public Task<PlayerSaveResult> LoseOneAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(PlayerSaveResult.Saved);
            }

            public Task<CurrencyRefillResult> RefillAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(CurrencyRefillResult.Refilled);
            }

            public void Dispose()
            {
                m_count.Dispose();
            }
        }
    }
}
