using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Boosters;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Drives the booster bar and the targeting banner (B-12, B-22, B-23): one slot per in-game booster in catalog
    /// order, its stock count or plus badge, the selected slot of the targeting mode, the enabled rules and the banner
    /// of the selected booster. A click only calls BoosterUseCoordinator.Press or cancels targeting; nothing here
    /// changes the board.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD. Every subscription goes into one CompositeDisposable that <see cref="Unbind"/> clears;
    /// <see cref="Dispose"/> is the last stop point. Observers are cached, texts come from HudText, and the view is
    /// written only when a shown value changes, so a steady Playing tick does no work here.
    /// </remarks>
    public sealed class BoosterBarPresenter : IDisposable
    {
        private readonly LevelSession m_session;
        private readonly BoosterTargeting m_targeting;
        private readonly BoosterUseCoordinator m_coordinator;
        private readonly BoosterCatalog m_catalog;
        private readonly IBoosterInventory m_inventory;
        private readonly IBoosterShop m_shop;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Action<int> m_onSlotPressed;
        private readonly Action m_onBannerClosed;
        private readonly Observer<SessionState> m_onStateChanged;
        private readonly Observer<bool> m_onGateChanged;
        private readonly Observer<BoosterId> m_onModeChanged;

        private Slot[] m_slots = Array.Empty<Slot>();
        private int m_slotCount;
        private IGameplayHudView m_view;
        private HudText m_text;
        private bool m_disposed;

        public BoosterBarPresenter(LevelSession session, BoosterTargeting targeting, BoosterUseCoordinator coordinator,
            BoosterCatalog catalog, IBoosterInventory inventory, IBoosterShop shop)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_targeting = targeting ?? throw new ArgumentNullException(nameof(targeting));
            m_coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            m_shop = shop ?? throw new ArgumentNullException(nameof(shop));
            m_onSlotPressed = OnSlotPressed;
            m_onBannerClosed = OnBannerClosed;
            m_onStateChanged = OnStateChanged;
            m_onGateChanged = OnGateChanged;
            m_onModeChanged = OnModeChanged;
        }

        public bool IsBound => m_view != null;

        /// <summary>How many slots show a booster.</summary>
        public int SlotCount => m_slotCount;

        /// <summary>The booster a shown slot holds.</summary>
        public BoosterId GetSlotBooster(int slot)
        {
            if (slot < 0 || slot >= m_slotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            return m_slots[slot].Id;
        }

        /// <summary>
        /// Binds the view once, on Home: assigns the catalog's in-game boosters to the slots in catalog order, makes each
        /// banner title once, then follows stock, the session gates and the targeting mode.
        /// </summary>
        public void Bind(IGameplayHudView view, HudText text)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(BoosterBarPresenter));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Booster bar presenter: already bound; unbind first.");
            }

            m_view = view ?? throw new ArgumentNullException(nameof(view));
            m_text = text ?? throw new ArgumentNullException(nameof(text));
            CreateSlots();

            m_view.SlotPressed += m_onSlotPressed;
            m_view.BannerClosed += m_onBannerClosed;
            for (int slot = 0; slot < m_slotCount; slot++)
            {
                // Emits the current stock at once, then only on a committed change.
                m_subscriptions.Add(m_inventory.GetStock(m_slots[slot].Id).Subscribe(m_slots[slot].OnStockChanged));
            }

            m_subscriptions.Add(m_session.State.Subscribe(m_onStateChanged));
            m_subscriptions.Add(m_session.IsOpening.Subscribe(m_onGateChanged));
            m_subscriptions.Add(m_session.IsFrozen.Subscribe(m_onGateChanged));
            m_subscriptions.Add(m_coordinator.IsBusy.Subscribe(m_onGateChanged));
            m_subscriptions.Add(m_shop.IsSaving.Subscribe(m_onGateChanged));
            m_subscriptions.Add(m_targeting.Mode.Subscribe(m_onModeChanged));
        }

        /// <summary>Disposes every subscription and detaches the view's events.</summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_view.SlotPressed -= m_onSlotPressed;
            m_view.BannerClosed -= m_onBannerClosed;
            m_view = null;
            m_slots = Array.Empty<Slot>();
            m_slotCount = 0;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_disposed = true;
        }

        private void CreateSlots()
        {
            m_slots = new Slot[m_view.SlotCount];
            m_slotCount = 0;
            int unshown = 0;
            for (int index = 0; index < m_catalog.Count; index++)
            {
                BoosterDefinition definition = m_catalog.GetAt(index);
                if (definition == null || (definition.UseContexts & BoosterUseContext.InGame) == 0)
                {
                    continue;
                }

                if (m_slotCount == m_slots.Length)
                {
                    unshown++;
                    continue;
                }

                Slot slot = new Slot(this, m_slotCount, definition);
                m_slots[m_slotCount] = slot;
                m_view.ShowSlot(m_slotCount, definition.Icon);
                m_slotCount++;
            }

            for (int slot = m_slotCount; slot < m_slots.Length; slot++)
            {
                m_view.HideSlot(slot);
            }

            if (unshown > 0)
            {
                Debug.LogWarning($"Booster bar: the catalog has {unshown} more in-game booster(s) than the bar's "
                                 + $"{m_slots.Length} slots; they are not shown.");
            }
        }

        private void OnSlotPressed(int slot)
        {
            if (m_view == null || slot < 0 || slot >= m_slotCount)
            {
                return;
            }

            BoosterPressResult result = m_coordinator.Press(m_slots[slot].Id);
            if (result == BoosterPressResult.Blocked)
            {
                m_view.PlaySlotBlocked(slot);
            }
        }

        private void OnBannerClosed()
        {
            m_targeting.Cancel();
        }

        private void OnStateChanged(SessionState state)
        {
            RefreshEnabled();
        }

        private void OnGateChanged(bool value)
        {
            RefreshEnabled();
        }

        // A slot takes presses only while Playing, outside the opening hold, with no booster or player-data write
        // pending; Time Freeze also waits for a running freeze to end (B-22).
        private void RefreshEnabled()
        {
            if (m_view == null)
            {
                return;
            }

            bool open = m_session.State.Value == SessionState.Playing && !m_session.IsOpening.Value
                && !m_coordinator.IsBusy.Value && !m_shop.IsSaving.Value;
            bool frozen = m_session.IsFrozen.Value;
            for (int slot = 0; slot < m_slotCount; slot++)
            {
                m_slots[slot].SetEnabled(open && !(m_slots[slot].Id == BoosterId.TimeFreeze && frozen));
            }
        }

        private void OnModeChanged(BoosterId mode)
        {
            if (m_view == null)
            {
                return;
            }

            Slot selected = null;
            for (int slot = 0; slot < m_slotCount; slot++)
            {
                bool isSelected = mode != BoosterId.None && m_slots[slot].Id == mode;
                m_slots[slot].SetSelected(isSelected);
                if (isSelected)
                {
                    selected = m_slots[slot];
                }
            }

            if (selected != null)
            {
                m_view.ShowBanner(selected.Title, selected.Instruction, selected.Definition.Icon);
            }
            else
            {
                m_view.HideBanner();
            }
        }

        /// <summary>One slot's booster, its banner copy and what the view shows for it, written only on a change.</summary>
        private sealed class Slot
        {
            private readonly BoosterBarPresenter m_owner;
            private readonly int m_index;
            private bool m_hasStock;
            private int m_stock;
            private bool m_hasEnabled;
            private bool m_enabled;
            private bool m_selected;

            public Slot(BoosterBarPresenter owner, int index, BoosterDefinition definition)
            {
                m_owner = owner;
                m_index = index;
                Definition = definition;
                Id = definition.Id;

                // Made once on Home; the banner shows them as they are (B-23).
                Title = definition.DisplayName != null ? definition.DisplayName.ToUpperInvariant() : string.Empty;
                Instruction = BoosterInstructions.Get(definition.Id);
                OnStockChanged = SetStock;
            }

            public BoosterDefinition Definition { get; }

            public BoosterId Id { get; }

            public string Title { get; }

            public string Instruction { get; }

            public Observer<int> OnStockChanged { get; }

            public void SetEnabled(bool enabled)
            {
                if (m_hasEnabled && enabled == m_enabled)
                {
                    return;
                }

                m_hasEnabled = true;
                m_enabled = enabled;
                m_owner.m_view.SetSlotEnabled(m_index, enabled);
            }

            public void SetSelected(bool selected)
            {
                if (selected == m_selected)
                {
                    return;
                }

                m_selected = selected;
                m_owner.m_view.SetSlotSelected(m_index, selected);
            }

            private void SetStock(int stock)
            {
                if (m_owner.m_view == null || (m_hasStock && stock == m_stock))
                {
                    return;
                }

                m_hasStock = true;
                m_stock = stock;
                m_owner.m_view.SetSlotStock(m_index, m_owner.m_text.GetNumber(stock), stock <= 0);
            }
        }
    }
}
