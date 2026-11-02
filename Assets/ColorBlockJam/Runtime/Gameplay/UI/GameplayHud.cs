using System;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Levels;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Haptics;
using ColorBlockJam.Player;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Presentation.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.UI.Haptics;
using Onity.Reactive;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The Game scene's HUD composition (B-21 to B-25): on the first <see cref="Show"/> it wraps the HUD document's tree
    /// in its view and reticle layer, pads the top band and the booster bar from the safe area and binds the bar,
    /// timer, opening and reticle presenters; every Show then prepares the level on Home (clock strings, reticle pool,
    /// frost quad). It also turns the session's frozen state into the Time Freeze frost, and tells the board presenter
    /// where the booster casts enter the screen: the top of its booster bar.
    /// </summary>
    /// <remarks>
    /// Plain C#, no Update, coroutine or tween of its own. Owner: the Game scene container (AsSingle), registered after
    /// the session and the use coordinator, so it is disposed first; <see cref="Dispose"/> unbinds every presenter and
    /// subscription, disposes the views and safe-area paddings and destroys the reticles' white copy. The HUD document
    /// is not disabled during the scene, so its tree outlives the binding.
    /// </remarks>
    public sealed class GameplayHud : IGameplayHud, IBoosterCastAnchor, IDisposable
    {
        private readonly GameplayHudContext m_context;
        private readonly LevelSession m_session;
        private readonly BoosterCatalog m_catalog;
        private readonly TimeFreezeFrostView m_frost;
        private readonly HudText m_text;
        private readonly BoosterBarPresenter m_bar;
        private readonly TimerPresenter m_timer;
        private readonly OpeningPresenter m_opening;
        private readonly RocketReticlePresenter m_reticles;
        private readonly CompositeDisposable m_subscriptions = new CompositeDisposable();
        private readonly Observer<bool> m_onFrozenChanged;

        private GameplayHudView m_view;
        private RocketReticleView m_reticleView;
        private SafeAreaPadding m_topSafeArea;
        private SafeAreaPadding m_barSafeArea;
        private Rect m_pinnedSafeArea;
        private Vector2 m_pinnedScreenSize;
        private bool m_hasPinnedSafeArea;
        private bool m_disposed;

        // Vibrates for haptic-class buttons anywhere in the HUD document (DEC-156); attached on bind, detached on
        // Dispose. Null while no haptics were set.
        private IHaptics m_haptics;
        private ButtonHaptics m_buttonHaptics;

        public GameplayHud(GameplayHudContext context, LevelSession session, BoosterTargeting targeting,
            BoosterUseCoordinator coordinator, BoosterCatalog catalog, IBoosterInventory inventory, IBoosterShop shop,
            IBoardProjector projector, TimeFreezeFrostView frost)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            m_frost = frost ?? throw new ArgumentNullException(nameof(frost));
            m_text = new HudText(PlayerSaveService.MaxBoosterStock);
            m_bar = new BoosterBarPresenter(session, targeting, coordinator, catalog, inventory, shop);
            m_timer = new TimerPresenter(session, catalog);
            m_opening = new OpeningPresenter(session, catalog);
            m_reticles = new RocketReticlePresenter(session, targeting, projector);
            m_onFrozenChanged = OnFrozenChanged;
        }

        /// <summary>True once the HUD found its tree and bound its presenters.</summary>
        public bool IsBound => m_view != null;

        /// <summary>The bound view, or null before the first successful Show; for tests and captures.</summary>
        public GameplayHudView View => m_view;

        /// <summary>The bound reticle layer, or null before the first successful Show.</summary>
        public RocketReticleView ReticleView => m_reticleView;

        public BoosterBarPresenter Bar => m_bar;

        /// <summary>
        /// Sets the haptics the HUD document's haptic-class buttons play once the HUD binds (DEC-156); null plays nothing.
        /// The Game scene sets it once at scene build, before the first Show.
        /// </summary>
        public void SetHaptics(IHaptics haptics)
        {
            m_haptics = haptics;
        }

        public RocketReticlePresenter Reticles => m_reticles;

        public void Show(AttemptLevel level)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(GameplayHud));
            }

            if (level == null)
            {
                throw new ArgumentNullException(nameof(level));
            }

            if (!IsBound && !TryBind())
            {
                return;
            }

            // Home only: everything the level's Playing ticks read is built here (B-9, B-24, B-25).
            int timerSeconds = level.Model.TimerSeconds;
            m_text.ReserveClock(timerSeconds + GetExtraTimeBonus());
            m_timer.ShowSeconds(timerSeconds);
            m_reticles.Prepare(level.Model.TotalCellCount);
            m_opening.SetPlan(OpeningPlan.None);
            m_frost.Prepare();
        }

        /// <summary>
        /// The booster bar's top centre in viewport space. The HUD panel covers the screen, so its root's size maps
        /// panel units onto the viewport; worldBound and layout are structs, so nothing is allocated.
        /// </summary>
        public bool TryGetLaunchViewportPoint(out Vector2 viewportPoint)
        {
            viewportPoint = default;
            VisualElement bar = m_view?.BoosterBar;
            IPanel panel = bar?.panel;
            if (panel == null)
            {
                return false;
            }

            Rect screen = panel.visualTree.worldBound;
            Rect bounds = bar.worldBound;
            if (!(screen.width > 0f) || !(screen.height > 0f) || !(bounds.height > 0f))
            {
                return false;
            }

            viewportPoint = new Vector2((bounds.center.x - screen.xMin) / screen.width,
                1f - (bounds.yMin - screen.yMin) / screen.height);
            return true;
        }

        public void SetOpening(in OpeningPlan plan)
        {
            if (!m_disposed)
            {
                m_opening.SetPlan(plan);
            }
        }

        /// <summary>
        /// Pins the safe area to an explicit surface instead of Screen, for captures or a simulated notch; applies now
        /// when bound and on a later bind (HubScreen.SetSafeAreaSource).
        /// </summary>
        public void SetSafeAreaSource(Rect safeArea, Vector2 screenSize)
        {
            m_pinnedSafeArea = safeArea;
            m_pinnedScreenSize = screenSize;
            m_hasPinnedSafeArea = true;
            if (IsBound)
            {
                m_topSafeArea.SetSource(safeArea, screenSize);
                m_barSafeArea.SetSource(safeArea, screenSize);
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_buttonHaptics?.Detach();
            m_buttonHaptics = null;
            m_subscriptions.Dispose();
            m_bar.Dispose();
            m_timer.Dispose();
            m_opening.Dispose();
            m_reticles.Dispose();
            m_topSafeArea?.Dispose();
            m_barSafeArea?.Dispose();
            m_reticleView?.Dispose();
            m_view?.Dispose();
            m_topSafeArea = null;
            m_barSafeArea = null;
            m_reticleView = null;
            m_view = null;
        }

        private bool TryBind()
        {
            VisualElement root = m_context.Document.rootVisualElement;
            VisualElement hud = root?.Q<VisualElement>(GameplayHudView.RootName);
            if (hud == null)
            {
                Debug.LogError("Gameplay HUD: its UIDocument shows no GameplayHud.uxml (no hud-root element); the booster "
                               + "bar and timer are not shown.", m_context.Document);
                return false;
            }

            m_view = new GameplayHudView(hud);
            m_reticleView = new RocketReticleView(m_view.TargetLayer, m_context.ReticleTexture);

            // The top band pads the top and sides; the bar's area reaches the bottom edge and pads its row (Hub's pattern).
            m_topSafeArea = new SafeAreaPadding(hud, m_view.TopArea, SafeAreaEdges.Left | SafeAreaEdges.Top | SafeAreaEdges.Right);
            m_barSafeArea = new SafeAreaPadding(hud, m_view.BoosterBar, SafeAreaEdges.Left | SafeAreaEdges.Right | SafeAreaEdges.Bottom);
            if (m_hasPinnedSafeArea)
            {
                m_topSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
                m_barSafeArea.SetSource(m_pinnedSafeArea, m_pinnedScreenSize);
            }
            else
            {
                m_topSafeArea.Apply();
                m_barSafeArea.Apply();
            }

            m_bar.Bind(m_view, m_text);
            m_timer.Bind(m_view, m_text);
            m_opening.Bind(m_view);
            m_reticles.Bind(m_reticleView);
            m_subscriptions.Add(m_session.IsFrozen.Subscribe(m_onFrozenChanged));

            // The whole document, so the top bar, pause menu and popups beside hud-root are covered too.
            if (m_haptics != null)
            {
                m_buttonHaptics = new ButtonHaptics(m_haptics);
                m_buttonHaptics.Attach(root);
            }

            return true;
        }

        // The largest time the main clock can show beyond the authored time: one Extra Time bonus (B-9, B-16).
        private int GetExtraTimeBonus()
        {
            return m_catalog.TryGet(BoosterId.ExtraTime, out BoosterDefinition definition)
                && definition is ExtraTimeBoosterDefinition extraTime
                    ? extraTime.BonusSeconds
                    : 0;
        }

        private void OnFrozenChanged(bool frozen)
        {
            m_frost.SetFrozen(frozen);
        }
    }
}
