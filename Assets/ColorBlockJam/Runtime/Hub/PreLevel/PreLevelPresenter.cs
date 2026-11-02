using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using Cysharp.Text;
using Onity.Reactive;

namespace ColorBlockJam.Hub.PreLevel
{
    /// <summary>
    /// Drives the pre-level popup (references 09 and 10): the level title, one selector per pre-level booster with its
    /// committed stock, free selection toggling, a purchase request for an empty stock, and one launch of the Game scene
    /// with the chosen level and starting boosters (design 9.1, 9.3, AC-1.1, AC-1.1a, AC-1.9).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle, bound to itself only); <c>HubScreen</c> binds it, registers it with the
    /// navigator as the PreLevel overlay and connects <see cref="PurchaseRequested"/> to the purchase overlay, which
    /// covers this popup (<see cref="SetCovered"/>) while it shows. Selection is presenter state only: opening,
    /// toggling, covering and closing spend nothing; covering keeps it and Hide clears it. Stock subscriptions
    /// live in one CompositeDisposable cleared by <see cref="Unbind"/>. The launch await uses a token linked to the host
    /// token passed to <see cref="Bind"/>, cancelled by <see cref="Unbind"/> (<c>HubScreen.OnDisable</c>) and
    /// <see cref="Dispose"/> (Hub unload). Starting boosters are only carried in the request (design 9.3).
    /// </remarks>
    public sealed class PreLevelPresenter : IHubOverlay, ICoverableOverlay, IDisposable
    {
        public const string TitlePrefix = "LEVEL ";

        private readonly IPlayerProgress m_progress;
        private readonly IBoosterInventory m_inventory;
        private readonly BoosterCatalog m_catalog;
        private readonly IHubNavigator m_navigator;
        private readonly ILevelLauncher m_launcher;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly List<SelectorBinding> m_selectors = new List<SelectorBinding>(2);
        private readonly Action m_onClosePressed;
        private readonly Action m_onPlayPressed;
        private readonly Action<int> m_onSelectorPressed;

        private IPreLevelView m_view;
        private CancellationTokenSource m_bindCancellation;
        private int m_levelCount;
        private int m_levelIndex = -1;
        private bool m_isShown;
        private bool m_isCovered;
        private bool m_isLaunching;
        private bool m_disposed;

        public PreLevelPresenter(IPlayerProgress progress, IBoosterInventory inventory, BoosterCatalog catalog,
            IHubNavigator navigator, ILevelLauncher launcher)
        {
            m_progress = progress ?? throw new ArgumentNullException(nameof(progress));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            m_launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
            m_onClosePressed = OnClosePressed;
            m_onPlayPressed = OnPlayPressed;
            m_onSelectorPressed = OnSelectorPressed;
        }

        /// <summary>
        /// Raised when an empty-stock selector is tapped: the host opens the purchase popup for that booster (DEC-85).
        /// The selector is not selected.
        /// </summary>
        public event Action<BoosterDefinition> PurchaseRequested;

        public bool IsBound => m_view != null;

        /// <summary>True between Show and Hide, also while covered by the purchase popup.</summary>
        public bool IsShown => m_isShown;

        /// <summary>True while the purchase popup covers this popup; it is then hidden but still open.</summary>
        public bool IsCovered => m_isCovered;

        /// <summary>The number of selectors built from the catalog's pre-level boosters.</summary>
        public int SelectorCount => m_selectors.Count;

        /// <summary>The last launch started by Play; a completed task when none ran. It never faults.</summary>
        public Task PendingLaunch { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Binds the view and builds the selectors from the catalog's pre-level boosters in catalog order; the popup
        /// starts hidden. A <paramref name="levelCount"/> of 0 leaves Play disabled.
        /// </summary>
        /// <param name="hostToken">The host's lifetime token; the launch wait of this bind is linked to it.</param>
        public void Bind(IPreLevelView view, int levelCount, CancellationToken hostToken)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(PreLevelPresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Pre-level presenter: already bound; unbind first.");
            }

            m_view = view;
            m_levelCount = Math.Max(0, levelCount);
            m_bindCancellation = CancellationTokenSource.CreateLinkedTokenSource(hostToken);
            m_isShown = false;
            m_isCovered = false;
            m_isLaunching = false;
            m_view.ClosePressed += m_onClosePressed;
            m_view.PlayPressed += m_onPlayPressed;
            m_view.SelectorPressed += m_onSelectorPressed;
            m_view.SetVisible(false);
            BindSelectors();
        }

        /// <summary>Cancels a pending launch wait, disposes subscriptions, detaches the view and removes the selectors.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_view.ClosePressed -= m_onClosePressed;
            m_view.PlayPressed -= m_onPlayPressed;
            m_view.SelectorPressed -= m_onSelectorPressed;
            m_subscriptions.Clear();
            m_selectors.Clear();
            m_view.ClearSelectors();
            m_view = null;
            m_isShown = false;
            m_isCovered = false;
            m_isLaunching = false;

            // Detached first: a launch that completes inside Cancel() finds no view and changes nothing.
            CancellationTokenSource cancellation = m_bindCancellation;
            m_bindCancellation = null;
            cancellation.Cancel();
            cancellation.Dispose();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            PurchaseRequested = null;
            m_disposed = true;
        }

        /// <summary>True when the selector in <paramref name="slot"/> is selected.</summary>
        public bool IsSelected(int slot)
        {
            return m_selectors[slot].IsSelected;
        }

        /// <summary>Called by the navigator when the popup opens: title from the playable level, nothing selected.</summary>
        public void Show()
        {
            if (m_view == null)
            {
                return;
            }

            // The same rule as the Home level button, so both show the same N (AC-1.1a).
            m_levelIndex = PlayerProgress.GetPlayableLevelIndex(m_progress.SavedLevelIndex.Value, m_levelCount);
            m_view.SetTitle(m_levelIndex >= 0 ? ZString.Concat(TitlePrefix, m_levelIndex + 1) : TitlePrefix);
            ClearSelection();
            m_view.SetPlayInteractable(m_levelIndex >= 0 && !m_isLaunching && !m_launcher.IsLaunching);
            m_isShown = true;
            m_isCovered = false;
            m_view.SetVisible(true);
        }

        /// <summary>Called by the navigator when the popup closes (close button or Back): clears the selection.</summary>
        public void Hide()
        {
            m_isShown = false;
            m_isCovered = false;
            if (m_view == null)
            {
                return;
            }

            // Close or Back clears the selection and spends nothing (AC-1.1a).
            ClearSelection();
            m_view.SetVisible(false);
        }

        /// <summary>
        /// Called by the purchase overlay: hides the open popup while the purchase popup shows and shows it again
        /// afterwards, keeping the title and selection (user feedback 2026-09-27: the popups must not overlap).
        /// </summary>
        public void SetCovered(bool covered)
        {
            m_isCovered = covered && m_isShown;
            if (m_view != null)
            {
                m_view.SetVisible(m_isShown && !m_isCovered);
            }
        }

        private void BindSelectors()
        {
            for (int i = 0; i < m_catalog.Count; i++)
            {
                BoosterDefinition definition = m_catalog.GetAt(i);
                if (definition == null || (definition.UseContexts & BoosterUseContext.PreLevel) == 0)
                {
                    continue;
                }

                int slot = m_view.AddSelector(definition.Icon, definition.DisplayName);
                var selector = new SelectorBinding(this, definition, slot);
                m_selectors.Add(selector);

                // Emits the committed stock at once, then after each committed change (a purchase).
                m_subscriptions.Add(m_inventory.GetStock(definition.Id).Subscribe(selector.OnStockChanged));
            }
        }

        private void ClearSelection()
        {
            for (int i = 0; i < m_selectors.Count; i++)
            {
                m_selectors[i].SetSelected(false);
            }
        }

        private void OnSelectorPressed(int slot)
        {
            SelectorBinding selector = FindSelector(slot);
            if (!m_isShown || m_isCovered || m_isLaunching || selector == null)
            {
                return;
            }

            // An empty stock is bought, never selected (AC-1.9, DEC-85).
            if (selector.Stock <= 0)
            {
                PurchaseRequested?.Invoke(selector.Definition);
                return;
            }

            selector.SetSelected(!selector.IsSelected);
        }

        private void OnClosePressed()
        {
            m_navigator.Close(HubOverlayId.PreLevel);
        }

        private void OnPlayPressed()
        {
            if (!m_isShown || m_isCovered || m_isLaunching || m_levelIndex < 0 || m_launcher.IsLaunching)
            {
                return;
            }

            // Selector order is catalog order, so the request lists Extra Time before Rocket.
            var startingBoosters = new List<BoosterId>(m_selectors.Count);
            for (int i = 0; i < m_selectors.Count; i++)
            {
                if (m_selectors[i].IsSelected)
                {
                    startingBoosters.Add(m_selectors[i].Definition.Id);
                }
            }

            m_isLaunching = true;
            m_view.SetPlayInteractable(false);
            PendingLaunch = LaunchAsync(new LevelLaunchRequest(m_levelIndex, startingBoosters), m_bindCancellation.Token);
        }

        /// <summary>
        /// Owner: this presenter, which keeps the task in <see cref="PendingLaunch"/>. Stops: when the launcher returns,
        /// or the bind's token (Unbind or Hub unload, which the completed transition itself causes). A failed or
        /// cancelled launch enables Play again.
        /// </summary>
        private async Task LaunchAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            LevelLaunchResult result = await m_launcher.LaunchAsync(request, cancellationToken);
            if (result == LevelLaunchResult.Completed || cancellationToken.IsCancellationRequested || m_view == null)
            {
                return;
            }

            m_isLaunching = false;
            m_view.SetPlayInteractable(m_isShown && m_levelIndex >= 0 && !m_launcher.IsLaunching);
        }

        private SelectorBinding FindSelector(int slot)
        {
            for (int i = 0; i < m_selectors.Count; i++)
            {
                if (m_selectors[i].Slot == slot)
                {
                    return m_selectors[i];
                }
            }

            return null;
        }

        /// <summary>
        /// One selector's booster, committed stock and selection; it redraws the selector only when its look changes.
        /// </summary>
        private sealed class SelectorBinding
        {
            private readonly PreLevelPresenter m_owner;
            private bool m_hasShown;
            private PreLevelSelectorState m_shownState;
            private int m_shownStock;

            public SelectorBinding(PreLevelPresenter owner, BoosterDefinition definition, int slot)
            {
                m_owner = owner;
                Definition = definition;
                Slot = slot;
                OnStockChanged = SetStock;
            }

            public BoosterDefinition Definition { get; }

            public int Slot { get; }

            public int Stock { get; private set; }

            public bool IsSelected { get; private set; }

            public Observer<int> OnStockChanged { get; }

            public void SetSelected(bool selected)
            {
                IsSelected = selected && Stock > 0;
                Redraw();
            }

            private void SetStock(int stock)
            {
                Stock = stock;
                if (stock <= 0)
                {
                    IsSelected = false;
                }

                Redraw();
            }

            private void Redraw()
            {
                IPreLevelView view = m_owner.m_view;
                if (view == null)
                {
                    return;
                }

                PreLevelSelectorState state = Stock <= 0
                    ? PreLevelSelectorState.OutOfStock
                    : IsSelected ? PreLevelSelectorState.Selected : PreLevelSelectorState.Available;
                if (m_hasShown && state == m_shownState && Stock == m_shownStock)
                {
                    return;
                }

                m_hasShown = true;
                m_shownState = state;
                m_shownStock = Stock;
                view.SetSelector(Slot, state, ZString.Concat(Stock));
            }
        }
    }
}
