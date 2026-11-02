using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Lives;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Lives;
using ColorBlockJam.Runtime.Currencies;
using Cysharp.Text;
using Onity.DI;
using Onity.Reactive;
using UnityEngine;

namespace ColorBlockJam.Hub.Home
{
    /// <summary>
    /// Drives the Home screen from player data: the level button and route from the saved level, one
    /// currency holder per Home currency, and level/settings presses forwarded to the navigator (design 7).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). Every subscription goes into one CompositeDisposable
    /// cleared by <see cref="Unbind"/>, which <c>HubScreen.OnDisable</c> calls; <see cref="Dispose"/> at
    /// Hub unload is the last stop point. Text is rebuilt only when a shown value changes (AC-1.2, AC-1.8).
    /// Lives (DEC-174): at 0 lives the Level button opens the life refill popup instead of the pre-level popup, and
    /// below the maximum the life holder shows the time to the next life. <see cref="Tick"/>, called by HubScreen's
    /// view-owned timer, updates that countdown and commits a refill once one is due (one owned task at a time).
    /// </remarks>
    public sealed class HomePresenter : IDisposable
    {
        private const string k_levelPrefix = "Level ";
        private const string k_levelUnavailable = "Level -";
        private const int k_noLevelShown = -1;

        private readonly IWallet m_wallet;
        private readonly IPlayerProgress m_progress;
        private readonly CurrencyCatalog m_catalog;
        private readonly IHubNavigator m_navigator;
        private readonly ILives m_lives;
        private readonly IHubMenuNavigator m_menus;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly List<CurrencyHolderBinding> m_holders = new List<CurrencyHolderBinding>(4);
        private readonly Action m_onLevelPressed;
        private readonly Action m_onSettingsPressed;
        private readonly Observer<int> m_onSavedLevelChanged;

        private IHomeView m_view;
        private CurrencyHolderBinding m_lifeHolder;
        private int m_levelCount;
        private int m_shownLevelIndex = k_noLevelShown;
        private bool m_disposed;

        /// <summary>A Home without lives (test harnesses): the Level button always opens the pre-level popup.</summary>
        public HomePresenter(IWallet wallet, IPlayerProgress progress, CurrencyCatalog catalog, IHubNavigator navigator)
            : this(wallet, progress, catalog, navigator, null, null)
        {
        }

        /// <param name="lives">The project's lives (DEC-174); null only in test harnesses without lives.</param>
        /// <param name="menus">Opens the life refill popup; null only together with <paramref name="lives"/>.</param>
        [Inject]
        public HomePresenter(IWallet wallet, IPlayerProgress progress, CurrencyCatalog catalog, IHubNavigator navigator,
            ILives lives, IHubMenuNavigator menus)
        {
            m_wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            m_progress = progress ?? throw new ArgumentNullException(nameof(progress));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_navigator = navigator ?? throw new ArgumentNullException(nameof(navigator));
            if (lives != null && menus == null)
            {
                throw new ArgumentNullException(nameof(menus), "Home presenter: lives need the menu navigator.");
            }

            m_lives = lives;
            m_menus = menus;
            m_onLevelPressed = OnLevelPressed;
            m_onSettingsPressed = OnSettingsPressed;
            m_onSavedLevelChanged = OnSavedLevelChanged;
        }

        public bool IsBound => m_view != null;

        /// <summary>The last refill commit started by <see cref="Tick"/>; a completed task when none ran. It never faults.</summary>
        public Task PendingLifeRefresh { get; private set; } = Task.CompletedTask;

        /// <summary>
        /// Binds the view once per show. A <paramref name="levelCount"/> of 0 (failed project scope or empty
        /// database) shows "Level -" with the level button disabled and no route.
        /// </summary>
        public void Bind(IHomeView view, int levelCount)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(HomePresenter));
            }

            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            if (m_view != null)
            {
                throw new InvalidOperationException("Home presenter: already bound; unbind first.");
            }

            m_view = view;
            m_levelCount = Math.Max(0, levelCount);
            m_shownLevelIndex = k_noLevelShown;

            m_view.LevelPressed += m_onLevelPressed;
            m_view.SettingsPressed += m_onSettingsPressed;

            BindCurrencies();
            BindLevel();
        }

        /// <summary>
        /// Disposes every subscription and detaches view events. The authored currency holders (DEC-143) are
        /// left exactly as they are; the next <see cref="Bind"/> hides and re-shows them.
        /// </summary>
        public void Unbind()
        {
            if (m_view == null)
            {
                return;
            }

            m_subscriptions.Clear();
            m_holders.Clear();
            m_lifeHolder = null;
            m_view.LevelPressed -= m_onLevelPressed;
            m_view.SettingsPressed -= m_onSettingsPressed;
            m_view = null;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Unbind();
            m_subscriptions.Dispose();
            m_lifetime.Cancel();
            m_lifetime.Dispose();
            m_disposed = true;
        }

        /// <summary>
        /// HubScreen's view-owned timer (a few times a second while Home is bound): shows the time to the next life in
        /// the life holder and commits a refill once one is due. Text changes only when the shown second changes.
        /// </summary>
        public void Tick()
        {
            if (m_view == null || m_lives == null || m_disposed)
            {
                return;
            }

            m_lifeHolder?.SetCountdown(m_lives.GetSecondsUntilNext());
            if (m_lives.GetAvailable() > m_lives.Count.Value && PendingLifeRefresh.IsCompleted)
            {
                // Owner: this presenter (PendingLifeRefresh). Stops: when the save returns; Dispose cancels its wait.
                PendingLifeRefresh = m_lives.RefreshAsync(m_lifetime.Token);
            }
        }

        private void BindCurrencies()
        {
            // Hides every authored holder first, so a holder no Show On Home currency claims stays hidden
            // (a currency removed from the catalog, or turned off, leaves no holder shown for it).
            m_view.HideCurrencyHolders();

            for (int i = 0; i < m_catalog.Count; i++)
            {
                CurrencyDefinition definition = m_catalog.GetAt(i);
                if (definition == null || !definition.ShowOnHome)
                {
                    continue;
                }

                bool countOnIcon = definition.HomeDisplay == CurrencyDisplay.CountOnIcon;
                if (!m_view.TryGetCurrencyHolder(definition.Id, definition.Icon, countOnIcon, out ICurrencyHolderView holder))
                {
                    Debug.LogError(ZString.Concat("Home presenter: currency '", definition.Id,
                        "' has no authored holder in Home.uxml (expected an element named 'currency-holder-",
                        definition.Id, "')."));
                    continue;
                }

                var binding = new CurrencyHolderBinding(definition, holder);
                m_holders.Add(binding);
                if (m_lives != null && definition == m_lives.Currency)
                {
                    m_lifeHolder = binding;
                }

                // Emits the current balance at once, then only on a committed change.
                m_subscriptions.Add(m_wallet.GetBalance(definition).Subscribe(binding.OnBalanceChanged));
            }
        }

        private void BindLevel()
        {
            if (m_levelCount == 0)
            {
                m_view.SetLevel(k_levelUnavailable, false);
                for (int slot = 0; slot < m_view.RouteNodeCount; slot++)
                {
                    m_view.SetRouteNode(slot, string.Empty, false, false);
                }

                return;
            }

            m_subscriptions.Add(m_progress.SavedLevelIndex.Subscribe(m_onSavedLevelChanged));
        }

        private void OnSavedLevelChanged(int savedIndex)
        {
            int index = PlayerProgress.GetPlayableLevelIndex(savedIndex, m_levelCount);
            if (index == m_shownLevelIndex)
            {
                return;
            }

            m_shownLevelIndex = index;
            m_view.SetLevel(ZString.Concat(k_levelPrefix, index + 1), true);

            // Slot 0 is the current level; later slots preview the next levels, locked. Numbers keep counting past the
            // last database level, whose levels play again, so every slot shows (DEC-173).
            for (int slot = 0; slot < m_view.RouteNodeCount; slot++)
            {
                m_view.SetRouteNode(slot, ZString.Concat(index + slot + 1), true, slot > 0);
            }
        }

        private void OnLevelPressed()
        {
            if (m_levelCount == 0)
            {
                return;
            }

            // Level entry needs a life (DEC-174): with none available the refill popup opens instead; the level never
            // starts at 0 lives, even when that popup is not registered.
            if (m_lives != null && m_lives.GetAvailable() <= 0)
            {
                m_menus.OpenMenu(LifeRefillOverlay.MenuId);
                return;
            }

            // Task 4 registers the pre-level popup; until then the press shows feedback only.
            m_navigator.OpenPreLevel();
        }

        private void OnSettingsPressed()
        {
            // Task 3 registers Settings; until then the press shows feedback only.
            m_navigator.OpenSettings();
        }

        /// <summary>
        /// Shows one currency's balance in its holder, formatting only when the balance or the countdown changes.
        /// </summary>
        private sealed class CurrencyHolderBinding
        {
            private readonly CurrencyDefinition m_definition;
            private readonly ICurrencyHolderView m_holder;
            private bool m_hasShown;
            private int m_shownBalance;
            private long m_countdownSeconds;

            public CurrencyHolderBinding(CurrencyDefinition definition, ICurrencyHolderView holder)
            {
                m_definition = definition;
                m_holder = holder ?? throw new ArgumentNullException(nameof(holder));
                OnBalanceChanged = Show;
            }

            public Observer<int> OnBalanceChanged { get; }

            /// <summary>Seconds to the next timed unit (Lives, DEC-174); 0 shows no countdown.</summary>
            public void SetCountdown(long seconds)
            {
                if (seconds == m_countdownSeconds)
                {
                    return;
                }

                m_countdownSeconds = seconds;
                if (m_hasShown)
                {
                    Render();
                }
            }

            private void Show(int balance)
            {
                if (m_hasShown && balance == m_shownBalance)
                {
                    return;
                }

                m_hasShown = true;
                m_shownBalance = balance;
                Render();
            }

            private void Render()
            {
                string amount = CurrencyText.FormatCompact(m_shownBalance);
                if (m_definition.HomeDisplay == CurrencyDisplay.CountOnIcon)
                {
                    // The icon always carries the count; the holder says "Full" at the maximum (reference 06) and, below
                    // it, the time to the next unit while one is refilling (DEC-174).
                    m_holder.SetIconCount(amount);
                    if (m_shownBalance >= m_definition.MaxAmount)
                    {
                        m_holder.SetText(m_definition.FullLabel);
                    }
                    else
                    {
                        m_holder.SetText(m_countdownSeconds > 0 ? LifeCountdownText.Format(m_countdownSeconds) : amount);
                    }

                    return;
                }

                m_holder.SetText(amount);
            }
        }
    }
}
