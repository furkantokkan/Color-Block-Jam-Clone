using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using Onity.Reactive;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.SharedUI
{
    /// <summary>
    /// In-memory booster and currency definitions for the purchase popup tests; every object it makes is destroyed by
    /// <see cref="Dispose"/>.
    /// </summary>
    internal sealed class PurchaseTestAssets : IDisposable
    {
        private const float k_timeoutSeconds = 5f;

        private readonly List<Object> m_objects = new List<Object>();

        public CurrencyDefinition CreateCoin()
        {
            var coin = ScriptableObject.CreateInstance<CurrencyDefinition>();
            m_objects.Add(coin);
            var serialized = new SerializedObject(coin);
            serialized.FindProperty("m_id").stringValue = "coin";
            serialized.FindProperty("m_displayName").stringValue = "Coins";
            serialized.FindProperty("m_icon").objectReferenceValue = CreateSprite("Coin_Single");
            serialized.FindProperty("m_maxAmount").intValue = 999999999;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return coin;
        }

        public BoosterDefinition CreateBooster(BoosterId id, string displayName, CurrencyDefinition currency, int price)
        {
            var booster = ScriptableObject.CreateInstance<TargetedBoosterDefinition>();
            m_objects.Add(booster);
            var serialized = new SerializedObject(booster);
            serialized.FindProperty("m_id").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(BoosterId)), id);
            serialized.FindProperty("m_displayName").stringValue = displayName;
            serialized.FindProperty("m_description").stringValue = displayName + " description.";
            serialized.FindProperty("m_icon").objectReferenceValue = CreateSprite(displayName);
            serialized.FindProperty("m_useContexts").intValue = (int)(BoosterUseContext.PreLevel | BoosterUseContext.InGame);
            serialized.FindProperty("m_purchasePrice").intValue = price;
            serialized.FindProperty("m_purchaseCurrency").objectReferenceValue = currency;
            serialized.FindProperty("m_purchaseAmount").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return booster;
        }

        public void Dispose()
        {
            for (int i = m_objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(m_objects[i]);
            }

            m_objects.Clear();
        }

        /// <summary>Yields editor frames until the task completes, so awaited continuations run; fails on timeout.</summary>
        public static IEnumerator Await(Task task)
        {
            float deadline = Time.realtimeSinceStartup + k_timeoutSeconds;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True, "the task did not complete in time");
        }

        private Sprite CreateSprite(string name)
        {
            var texture = new Texture2D(2, 2);
            m_objects.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            m_objects.Add(sprite);
            return sprite;
        }
    }

    /// <summary>
    /// Shop and stock port pair: PurchaseAsync waits until the test completes it; Purchased publishes the new stock
    /// before the result returns, as the real service does (design 4.4). IsSaving follows the pending purchase.
    /// </summary>
    internal sealed class FakeBoosterShop : IBoosterShop, IBoosterInventory, IDisposable
    {
        private readonly ReactiveProperty<bool> m_isSaving = new ReactiveProperty<bool>(false);
        private readonly ReactiveProperty<int> m_stock;
        private TaskCompletionSource<BoosterPurchaseResult> m_pending;

        public FakeBoosterShop(int stock)
        {
            m_stock = new ReactiveProperty<int>(stock);
        }

        public IReadOnlyReactiveProperty<bool> IsSaving => m_isSaving;

        public int Calls { get; private set; }

        public BoosterDefinition LastBooster { get; private set; }

        public CancellationToken LastToken { get; private set; }

        public int Stock => m_stock.Value;

        /// <summary>Another writer (a Settings toggle, a reward) holds the save owner.</summary>
        public void SetSaving(bool saving)
        {
            m_isSaving.Value = saving;
        }

        public IReadOnlyReactiveProperty<int> GetStock(BoosterId id)
        {
            return m_stock;
        }

        public Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken)
        {
            Calls++;
            LastBooster = booster;
            LastToken = cancellationToken;
            m_pending = new TaskCompletionSource<BoosterPurchaseResult>();
            m_isSaving.Value = true;
            cancellationToken.Register(() => m_pending.TrySetCanceled());
            return m_pending.Task;
        }

        public void Complete(BoosterPurchaseResult result)
        {
            if (result == BoosterPurchaseResult.Purchased)
            {
                m_stock.Value += 1;
            }

            m_isSaving.Value = false;
            m_pending.TrySetResult(result);
        }

        public void Fail(Exception exception)
        {
            m_isSaving.Value = false;
            m_pending.TrySetException(exception);
        }

        public void Dispose()
        {
            m_isSaving.Dispose();
            m_stock.Dispose();
        }
    }

    /// <summary>Records what the presenter writes into the purchase popup and raises its clicks.</summary>
    internal sealed class FakeBoosterPurchaseView : IBoosterPurchaseView
    {
        public event Action BuyPressed;

        public event Action CloseRequested;

        public bool IsVisible { get; private set; }

        public string Title { get; private set; }

        public Sprite Icon { get; private set; }

        public string Amount { get; private set; }

        public string Description { get; private set; }

        public Sprite CurrencyIcon { get; private set; }

        public string Price { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public bool BuyInteractable { get; private set; }

        public int BuyHandlers => BuyPressed?.GetInvocationList().Length ?? 0;

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
        }

        public void SetOffer(string title, Sprite icon, string amount, string description, Sprite currencyIcon, string price)
        {
            Title = title;
            Icon = icon;
            Amount = amount;
            Description = description;
            CurrencyIcon = currencyIcon;
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
}
