using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.UI.Purchase;
using NUnit.Framework;
using Onity.Reactive;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Hub
{
    /// <summary>
    /// Shared assets and fakes for the pre-level, purchase overlay and launcher tests: booster and currency definitions
    /// made in memory, a catalog, fake player-data ports, fake views, a fake launcher and a frame-yielding task wait.
    /// </summary>
    internal sealed class PreLevelTestFixtures : IDisposable
    {
        private const float k_timeoutSeconds = 5f;

        private readonly List<Object> m_assets = new List<Object>();

        public CurrencyDefinition CreateCoin()
        {
            var coin = ScriptableObject.CreateInstance<CurrencyDefinition>();
            m_assets.Add(coin);
            var serialized = new SerializedObject(coin);
            serialized.FindProperty("m_id").stringValue = "coin";
            serialized.FindProperty("m_displayName").stringValue = "Coins";
            serialized.FindProperty("m_icon").objectReferenceValue = CreateSprite("Coin_Single");
            serialized.FindProperty("m_maxAmount").intValue = 999999999;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return coin;
        }

        public BoosterDefinition CreateBooster(BoosterId id, string displayName, BoosterUseContext contexts,
            CurrencyDefinition currency = null, int price = 0)
        {
            var booster = ScriptableObject.CreateInstance<TargetedBoosterDefinition>();
            m_assets.Add(booster);
            var serialized = new SerializedObject(booster);
            serialized.FindProperty("m_id").enumValueIndex = Array.IndexOf(Enum.GetValues(typeof(BoosterId)), id);
            serialized.FindProperty("m_displayName").stringValue = displayName;
            serialized.FindProperty("m_description").stringValue = displayName + " description.";
            serialized.FindProperty("m_icon").objectReferenceValue = CreateSprite(displayName);
            serialized.FindProperty("m_useContexts").intValue = (int)contexts;
            serialized.FindProperty("m_purchasePrice").intValue = price;
            serialized.FindProperty("m_purchaseCurrency").objectReferenceValue = currency;
            serialized.FindProperty("m_purchaseAmount").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return booster;
        }

        public BoosterCatalog CreateCatalog(params BoosterDefinition[] boosters)
        {
            var catalog = ScriptableObject.CreateInstance<BoosterCatalog>();
            m_assets.Add(catalog);
            var serialized = new SerializedObject(catalog);
            SerializedProperty list = serialized.FindProperty("m_definitions");
            list.arraySize = boosters.Length;
            for (int i = 0; i < boosters.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = boosters[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return catalog;
        }

        public void Dispose()
        {
            for (int i = m_assets.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(m_assets[i]);
            }

            m_assets.Clear();
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
            m_assets.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            m_assets.Add(sprite);
            return sprite;
        }
    }

    /// <summary>Committed stock per booster; tests change it as the save service would after a commit.</summary>
    internal sealed class FakeInventory : IBoosterInventory, IDisposable
    {
        private readonly Dictionary<BoosterId, CountingProperty> m_stock = new Dictionary<BoosterId, CountingProperty>();

        public void Set(BoosterId id, int stock)
        {
            if (m_stock.TryGetValue(id, out CountingProperty property))
            {
                property.Source.Value = stock;
                return;
            }

            m_stock.Add(id, new CountingProperty(stock));
        }

        public int LiveSubscriptions(BoosterId id)
        {
            return m_stock[id].LiveSubscriptions;
        }

        public IReadOnlyReactiveProperty<int> GetStock(BoosterId id)
        {
            return m_stock[id];
        }

        public void Dispose()
        {
            foreach (CountingProperty property in m_stock.Values)
            {
                property.Source.Dispose();
            }
        }
    }

    /// <summary>A ReactiveProperty wrapper that counts live subscriptions, to prove Unbind disposes them.</summary>
    internal sealed class CountingProperty : IReadOnlyReactiveProperty<int>
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

    internal sealed class FakeProgress : IPlayerProgress, IDisposable
    {
        public ReactiveProperty<int> Index { get; } = new ReactiveProperty<int>(0);

        public IReadOnlyReactiveProperty<int> SavedLevelIndex => Index;

        public void Dispose()
        {
            Index.Dispose();
        }
    }

    /// <summary>A shop whose purchases complete when the test says so; a Purchased result raises the stock first.</summary>
    internal sealed class FakeShop : IBoosterShop, IDisposable
    {
        private readonly ReactiveProperty<bool> m_isSaving = new ReactiveProperty<bool>(false);
        private readonly FakeInventory m_inventory;
        private TaskCompletionSource<BoosterPurchaseResult> m_pending;
        private BoosterDefinition m_booster;

        public FakeShop(FakeInventory inventory)
        {
            m_inventory = inventory;
        }

        public IReadOnlyReactiveProperty<bool> IsSaving => m_isSaving;

        public int Calls { get; private set; }

        public Task<BoosterPurchaseResult> PurchaseAsync(BoosterDefinition booster, CancellationToken cancellationToken)
        {
            Calls++;
            m_booster = booster;
            m_pending = new TaskCompletionSource<BoosterPurchaseResult>();
            m_isSaving.Value = true;
            cancellationToken.Register(() => m_pending.TrySetCanceled());
            return m_pending.Task;
        }

        public void Complete(BoosterPurchaseResult result)
        {
            if (result == BoosterPurchaseResult.Purchased)
            {
                m_inventory.Set(m_booster.Id, m_inventory.GetStock(m_booster.Id).Value + 1);
            }

            m_isSaving.Value = false;
            m_pending.TrySetResult(result);
        }

        public void Dispose()
        {
            m_isSaving.Dispose();
        }
    }

    /// <summary>A launcher that records requests and finishes when the test says so.</summary>
    internal sealed class FakeLauncher : ILevelLauncher
    {
        private TaskCompletionSource<LevelLaunchResult> m_pending;

        public List<LevelLaunchRequest> Requests { get; } = new List<LevelLaunchRequest>();

        public bool IsLaunching { get; private set; }

        public Task<LevelLaunchResult> LaunchAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            IsLaunching = true;
            m_pending = new TaskCompletionSource<LevelLaunchResult>();
            return m_pending.Task;
        }

        public void Complete(LevelLaunchResult result)
        {
            IsLaunching = result == LevelLaunchResult.Completed;
            m_pending.TrySetResult(result);
        }
    }

    /// <summary>An overlay that only records whether the navigator shows it.</summary>
    internal sealed class FakeOverlay : IHubOverlay
    {
        public bool IsShown { get; private set; }

        public void Show()
        {
            IsShown = true;
        }

        public void Hide()
        {
            IsShown = false;
        }
    }

    internal sealed class FakePreLevelView : IPreLevelView
    {
        public event Action ClosePressed;

        public event Action PlayPressed;

        public event Action<int> SelectorPressed;

        public string Title { get; private set; }

        public bool IsShown { get; private set; }

        public bool PlayInteractable { get; private set; }

        public List<string> Labels { get; } = new List<string>();

        public List<Sprite> Icons { get; } = new List<Sprite>();

        public List<PreLevelSelectorState> States { get; } = new List<PreLevelSelectorState>();

        public List<string> Counts { get; } = new List<string>();

        public int SetSelectorCalls { get; private set; }

        public int Handlers => (ClosePressed?.GetInvocationList().Length ?? 0) + (PlayPressed?.GetInvocationList().Length ?? 0)
                               + (SelectorPressed?.GetInvocationList().Length ?? 0);

        public void SetVisible(bool visible)
        {
            IsShown = visible;
        }

        public void SetTitle(string title)
        {
            Title = title;
        }

        public int AddSelector(Sprite icon, string label)
        {
            Labels.Add(label);
            Icons.Add(icon);
            States.Add(PreLevelSelectorState.Available);
            Counts.Add(string.Empty);
            return Labels.Count - 1;
        }

        public void ClearSelectors()
        {
            Labels.Clear();
            Icons.Clear();
            States.Clear();
            Counts.Clear();
        }

        public void SetSelector(int slot, PreLevelSelectorState state, string count)
        {
            SetSelectorCalls++;
            States[slot] = state;
            Counts[slot] = count;
        }

        public void SetPlayInteractable(bool interactable)
        {
            PlayInteractable = interactable;
        }

        public void PressSelector(int slot)
        {
            SelectorPressed?.Invoke(slot);
        }

        public void PressPlay()
        {
            PlayPressed?.Invoke();
        }

        public void PressClose()
        {
            ClosePressed?.Invoke();
        }
    }

    /// <summary>Records what the shared purchase presenter writes and raises its clicks, for the Hub overlay tests.</summary>
    internal sealed class FakePurchaseView : IBoosterPurchaseView
    {
        public event Action BuyPressed;

        public event Action CloseRequested;

        public bool IsVisible { get; private set; }

        public string Title { get; private set; }

        public string Message { get; private set; } = string.Empty;

        public void SetVisible(bool visible)
        {
            IsVisible = visible;
        }

        public void SetOffer(string title, Sprite icon, string amount, string description, Sprite currencyIcon, string price)
        {
            Title = title;
        }

        public void SetMessage(string text)
        {
            Message = text ?? string.Empty;
        }

        public void SetBuyInteractable(bool interactable)
        {
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
