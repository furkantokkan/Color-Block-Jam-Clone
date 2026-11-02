using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Lives;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using Onity.Reactive;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Verifies the Hub life refill popup (DEC-174, TASK-20260928-124512603-1f645a0d AC-4 and AC-5): it opens as the
    /// "life-refill" menu showing the life count, "Next life in mm:ss" and the 1-coin price, refreshes text only when
    /// the second changes, closes after a refill, keeps a "Not enough coins" message open, and offers no refill at
    /// full lives.
    /// </summary>
    public sealed class LifeRefillOverlayTests
    {
        private readonly List<Object> m_assets = new List<Object>();
        private CurrencyDefinition m_life;
        private CurrencyDefinition m_coin;
        private HubNavigator m_navigator;
        private FakeLives m_lives;
        private RecordingPurchaseView m_view;
        private LifeRefillOverlay m_overlay;

        [SetUp]
        public void SetUp()
        {
            m_coin = CreateCurrency("coin", "Coins");
            m_life = CreateCurrency("life", "Lives");
            var serialized = new SerializedObject(m_life);
            serialized.FindProperty("m_maxAmount").intValue = 5;
            serialized.FindProperty("m_refillSeconds").intValue = 1800;
            serialized.FindProperty("m_refillPrice").intValue = 1;
            serialized.FindProperty("m_refillPriceCurrency").objectReferenceValue = m_coin;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            m_navigator = new HubNavigator();
            m_lives = new FakeLives(m_life) { Available = 0, SecondsUntilNext = 1799 };
            m_view = new RecordingPurchaseView();
            m_overlay = new LifeRefillOverlay(m_lives, m_navigator, m_navigator);
            m_overlay.Bind(m_view, CancellationToken.None);
            m_navigator.RegisterMenu(LifeRefillOverlay.MenuId, m_overlay);
        }

        [TearDown]
        public void TearDown()
        {
            m_overlay.Dispose();
            m_navigator.Dispose();
            for (int i = 0; i < m_assets.Count; i++)
            {
                Object.DestroyImmediate(m_assets[i]);
            }

            m_assets.Clear();
        }

        [Test]
        public void Open_ShowsTheLifeCountTheTimeToTheNextLifeAndThePrice()
        {
            Assert.That(m_overlay.Open(), Is.True);

            Assert.That(m_view.IsVisible, Is.True);
            Assert.That(m_navigator.OpenCount, Is.EqualTo(1));
            Assert.That(m_view.Title, Is.EqualTo(LifeRefillOverlay.Title));
            Assert.That(m_view.Amount, Is.EqualTo("0"));
            Assert.That(m_view.Description, Is.EqualTo("Next life in 29:59"));
            Assert.That(m_view.Price, Is.EqualTo("1"));
            Assert.That(m_view.BuyInteractable, Is.True);
            Assert.That(m_overlay.Open(), Is.False, "an open popup does not open twice");
        }

        [Test]
        public void Tick_WritesTextOnlyWhenTheShownSecondChanges()
        {
            m_overlay.Open();
            int offers = m_view.OfferWrites;

            m_overlay.Tick();
            Assert.That(m_view.OfferWrites, Is.EqualTo(offers));

            m_lives.SecondsUntilNext = 1798;
            m_overlay.Tick();
            Assert.That(m_view.OfferWrites, Is.EqualTo(offers + 1));
            Assert.That(m_view.Description, Is.EqualTo("Next life in 29:58"));
        }

        [Test]
        public void Buy_ThatRefills_ClosesThePopup()
        {
            m_overlay.Open();
            m_lives.Result = CurrencyRefillResult.Refilled;

            m_view.PressBuy();

            Assert.That(m_overlay.PendingRefill.IsCompleted, Is.True);
            Assert.That(m_lives.RefillCalls, Is.EqualTo(1));
            Assert.That(m_view.IsVisible, Is.False);
            Assert.That(m_navigator.OpenCount, Is.Zero);
        }

        [Test]
        public void Buy_WithoutEnoughCoins_ShowsAMessageAndStaysOpen()
        {
            m_overlay.Open();
            m_lives.Result = CurrencyRefillResult.InsufficientFunds;

            m_view.PressBuy();

            Assert.That(m_view.IsVisible, Is.True);
            Assert.That(m_view.Message, Is.EqualTo("Not enough coins"));
            Assert.That(m_view.BuyInteractable, Is.True, "Buy works again after the refused refill");
        }

        [Test]
        public void FullLives_OfferNoRefill()
        {
            m_lives.Available = 5;
            m_lives.SecondsUntilNext = 0;

            m_overlay.Open();

            Assert.That(m_view.Description, Is.EqualTo(LifeRefillOverlay.FullText));
            Assert.That(m_view.BuyInteractable, Is.False);
        }

        [Test]
        public void Close_HidesThePopupThroughTheNavigator()
        {
            m_overlay.Open();

            m_view.PressClose();

            Assert.That(m_view.IsVisible, Is.False);
            Assert.That(m_navigator.OpenCount, Is.Zero);
        }

        private CurrencyDefinition CreateCurrency(string id, string displayName)
        {
            var definition = ScriptableObject.CreateInstance<CurrencyDefinition>();
            m_assets.Add(definition);
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("m_id").stringValue = id;
            serialized.FindProperty("m_displayName").stringValue = displayName;
            serialized.FindProperty("m_maxAmount").intValue = 999;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return definition;
        }

        private sealed class RecordingPurchaseView : IBoosterPurchaseView
        {
            public event Action BuyPressed;

            public event Action CloseRequested;

            public bool IsVisible { get; private set; }

            public string Title { get; private set; }

            public string Amount { get; private set; }

            public string Description { get; private set; }

            public string Price { get; private set; }

            public string Message { get; private set; } = string.Empty;

            public bool BuyInteractable { get; private set; }

            public int OfferWrites { get; private set; }

            public void SetVisible(bool visible)
            {
                IsVisible = visible;
            }

            public void SetOffer(string title, Sprite icon, string amount, string description, Sprite currencyIcon,
                string price)
            {
                OfferWrites++;
                Title = title;
                Amount = amount;
                Description = description;
                Price = price;
            }

            public void SetMessage(string text)
            {
                Message = text ?? string.Empty;
            }

            public void SetBuyInteractable(bool interactable)
            {
                BuyInteractable = interactable;
            }

            public void PressBuy()
            {
                BuyPressed?.Invoke();
            }

            public void PressClose()
            {
                CloseRequested?.Invoke();
            }
        }

        private sealed class FakeLives : ILives
        {
            public FakeLives(CurrencyDefinition currency)
            {
                Currency = currency;
            }

            public CurrencyDefinition Currency { get; }

            public IReadOnlyReactiveProperty<int> Count => null;

            public int Available { get; set; }

            public long SecondsUntilNext { get; set; }

            public CurrencyRefillResult Result { get; set; } = CurrencyRefillResult.Refilled;

            public int RefillCalls { get; private set; }

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
                return Task.FromResult(PlayerSaveResult.Saved);
            }

            public Task<PlayerSaveResult> LoseOneAsync(CancellationToken cancellationToken)
            {
                return Task.FromResult(PlayerSaveResult.Saved);
            }

            public Task<CurrencyRefillResult> RefillAsync(CancellationToken cancellationToken)
            {
                RefillCalls++;
                return Task.FromResult(Result);
            }
        }
    }
}
