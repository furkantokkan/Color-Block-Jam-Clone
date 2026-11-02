using System;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Currencies;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.TopBar
{
    /// <summary>
    /// The Game HUD's top bar composition (Game HUD design 3-4): on the first <see cref="Show"/> it wraps the HUD
    /// document's top bar in its view and binds the presenter; every Show then shows the attempt's level number, which
    /// the attempt loader set on Home, on the badge (AC-2, DEC-173). Booster's HUD keeps the timer and freeze counter
    /// that sit inside the same bar.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch), registered after the session and the menu flow, so it is
    /// disposed first. Stops: <see cref="Dispose"/> disposes the presenter (its subscriptions) and the view (its click
    /// handlers). Plain C#, no Update, coroutine or tween of its own.
    /// </remarks>
    public sealed class GameTopBar : IGameTopBar, IDisposable
    {
        /// <summary>The catalog ID of the currency the holder shows (Data/Currencies/Coin.asset, DEC-128).</summary>
        public const string CoinCurrencyId = "coin";

        private readonly GameplayHudContext m_context;
        private readonly VisualElement m_root;
        private readonly AttemptLevelNumber m_levelNumber;
        private readonly TopBarPresenter m_presenter;

        private TopBarView m_view;
        private bool m_bindFailed;
        private bool m_disposed;

        /// <summary>The Game scene's top bar: its elements live in the HUD document of <paramref name="context"/>.</summary>
        [Inject]
        public GameTopBar(GameplayHudContext context, IGameSessionControl session, IGameMenu menu, IWallet wallet,
            CurrencyCatalog currencies, AttemptLevelNumber levelNumber)
            : this(session, menu, wallet, currencies, levelNumber)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>A top bar over an already built HUD tree (a GameplayHud.uxml clone or any ancestor of the bar).</summary>
        public GameTopBar(VisualElement hudRoot, IGameSessionControl session, IGameMenu menu, IWallet wallet,
            CurrencyCatalog currencies, AttemptLevelNumber levelNumber)
            : this(session, menu, wallet, currencies, levelNumber)
        {
            m_root = hudRoot ?? throw new ArgumentNullException(nameof(hudRoot));
        }

        private GameTopBar(IGameSessionControl session, IGameMenu menu, IWallet wallet, CurrencyCatalog currencies,
            AttemptLevelNumber levelNumber)
        {
            m_levelNumber = levelNumber ?? throw new ArgumentNullException(nameof(levelNumber));
            if (currencies == null)
            {
                throw new ArgumentNullException(nameof(currencies));
            }

            if (!currencies.TryGet(CoinCurrencyId, out CurrencyDefinition coin))
            {
                Debug.LogWarning("Game top bar: the currency catalog has no '" + CoinCurrencyId
                                 + "' currency; the coin holder is hidden.");
                coin = null;
            }

            m_presenter = new TopBarPresenter(session, menu, wallet, coin);
        }

        public bool IsBound => m_view != null;

        /// <summary>The bound view, or null before the first successful Show; for tests and captures.</summary>
        public TopBarView View => m_view;

        public TopBarPresenter Presenter => m_presenter;

        public void Show(AttemptLevel level)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(GameTopBar));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            // Home only: the loader set the number for this attempt before showing the bar.
            m_presenter.ShowLevel(m_levelNumber.NumberText);
            if (!IsBound)
            {
                TryBind();
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_presenter.Dispose();
            m_view?.Dispose();
            m_view = null;
        }

        // Runs once; a HUD without the top bar is an authoring error that is logged once, and play continues.
        private void TryBind()
        {
            if (m_bindFailed)
            {
                return;
            }

            VisualElement root = m_root ?? m_context.Document.rootVisualElement;
            if (root?.Q<VisualElement>(TopBarView.BarName) == null)
            {
                m_bindFailed = true;
                Debug.LogError("Game top bar: the HUD shows no GameTopBar.uxml (game-top-bar); the Level badge, Retry, "
                               + "Pause and coin holder are not shown.");
                return;
            }

            m_view = new TopBarView(root);
            m_presenter.Bind(m_view);
        }
    }
}
