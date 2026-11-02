using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Player;
using ColorBlockJam.Presentation.UI;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.UI.Purchase;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// Shows the shared booster purchase popup (ColorBlockJam.UI, DEC-136) inside the gameplay HUD for a booster with no
    /// stock (AC-12, B-18, B-32): <see cref="TryShow"/> shows one offer while the use coordinator pauses the session into
    /// Paused(Purchase); Buy runs through <see cref="IBoosterShop"/> inside the shared presenter and never casts or
    /// selects; the close button, a scrim tap and Android Back hide the popup and close the purchase pause, which
    /// resumes play unless backgrounding latched it.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, bound only with the HUD document). It creates the shared presenter and
    /// binds it, with the view over the HUD's popup instance, on the first <see cref="TryShow"/>: a transition into
    /// Paused, never a Playing tick. Stop points: <see cref="Hide"/> hides the popup and disables the Back action; the
    /// use coordinator hides it whenever the state leaves Paused (Home, Restart, Teardown, a foreground resume);
    /// <see cref="Dispose"/> unbinds the presenter, which cancels a pending purchase wait (a late result then shows
    /// nothing), removes the scrim callback, disposes the Back action, the view, the safe-area padding and the lifetime
    /// token. The Back action is a private <see cref="OverlayBackInput"/> (Android Back), not the shared AsSingle
    /// instance <c>GameMenuFlow</c> owns (DEC-154's one-handler rule), enabled only while the popup shows, because the
    /// gameplay map and its polled Back are disabled under Paused. Nothing here runs per frame.
    /// </remarks>
    public sealed class InGamePurchaseHost : IInGamePurchaseHost, IDisposable
    {
        private readonly LevelSession m_session;
        private readonly BoosterPurchasePresenter m_presenter;
        private readonly GameplayHudContext m_context;
        private readonly VisualElement m_root;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private readonly Action m_onCloseRequested;
        private readonly EventCallback<ClickEvent> m_onScrimClicked;
        private readonly Action m_onBackPressed;

        private BoosterPurchaseView m_view;
        private VisualElement m_scrim;
        private SafeAreaPadding m_safeArea;
        private OverlayBackInput m_back;
        private bool m_bindFailed;
        private bool m_disposed;

        /// <summary>The Game scene's host: the popup instance lives in the HUD document of <paramref name="context"/>.</summary>
        [Inject]
        public InGamePurchaseHost(GameplayHudContext context, LevelSession session, IBoosterShop shop,
            IBoosterInventory inventory)
            : this(session, shop, inventory)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>A host over an already built HUD tree (a GameplayHud.uxml clone or any ancestor of its popup instance).</summary>
        public InGamePurchaseHost(VisualElement hudRoot, LevelSession session, IBoosterShop shop, IBoosterInventory inventory)
            : this(session, shop, inventory)
        {
            m_root = hudRoot ?? throw new ArgumentNullException(nameof(hudRoot));
        }

        private InGamePurchaseHost(LevelSession session, IBoosterShop shop, IBoosterInventory inventory)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_presenter = new BoosterPurchasePresenter(shop, inventory);
            m_onCloseRequested = OnCloseRequested;
            m_onScrimClicked = OnScrimClicked;
            m_onBackPressed = OnBackPressed;
        }

        /// <summary>True once the presenter is bound to the HUD's popup view.</summary>
        public bool IsBound => m_view != null;

        public bool IsShowing => !m_disposed && m_presenter.IsVisible;

        /// <summary>The booster the popup offers while it shows; null otherwise.</summary>
        public BoosterDefinition Booster => m_disposed ? null : m_presenter.Booster;

        /// <summary>True while the Back action is enabled, that is while the popup shows.</summary>
        public bool IsBackEnabled => m_back != null && m_back.IsEnabled;

        /// <summary>The last purchase Buy started, or a completed task; it never faults. Tests await it.</summary>
        public Task PendingPurchase => m_presenter.PendingPurchase;

        /// <summary>
        /// Shows the purchase offer of <paramref name="definition"/> and enables Back; false, changing nothing, when the
        /// host is disposed, already showing, or the HUD has no popup instance (one error is logged).
        /// </summary>
        public bool TryShow(BoosterDefinition definition)
        {
            if (m_disposed || definition == null || m_presenter.IsVisible || !TryBind())
            {
                return false;
            }

            if (!m_presenter.Show(definition))
            {
                return false;
            }

            m_back.Enable();
            return true;
        }

        /// <summary>Hides the popup and disables Back; the session is left as it is.</summary>
        public void Hide()
        {
            if (m_disposed)
            {
                return;
            }

            m_back?.Disable();
            m_presenter.Hide();
        }

        /// <summary>
        /// The close button, a scrim tap and Back: hides the popup and closes the purchase pause. True when play resumed;
        /// after backgrounding the pause stays for the ordinary background rules. False when nothing shows.
        /// </summary>
        public bool Close()
        {
            if (!IsShowing)
            {
                return false;
            }

            Hide();
            return m_session.ClosePurchase();
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            if (m_back != null)
            {
                // Dispose already unsubscribes Pressed and disables the action.
                m_back.Dispose();
                m_back = null;
            }

            m_scrim?.UnregisterCallback(m_onScrimClicked);
            m_scrim = null;
            m_presenter.CloseRequested -= m_onCloseRequested;

            // Unbind cancels the purchase wait linked to the lifetime token before the view goes away.
            m_presenter.Dispose();
            m_view?.Dispose();
            m_view = null;
            m_safeArea?.Dispose();
            m_safeArea = null;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
        }

        // Runs once, on the first show; later calls return the bound state. A HUD without the popup instance is an
        // authoring error: it is logged once and every zero-stock press then plays the blocked feedback.
        private bool TryBind()
        {
            if (m_view != null)
            {
                return true;
            }

            if (m_bindFailed)
            {
                return false;
            }

            VisualElement root = m_root ?? m_context.Document.rootVisualElement;
            if (root == null)
            {
                return false;
            }

            VisualElement screen = root.Q<VisualElement>(BoosterPurchaseView.ScreenName);
            VisualElement scrim = screen?.Q<VisualElement>(BoosterPurchaseView.ScrimName);
            VisualElement safeArea = screen?.Q<VisualElement>(BoosterPurchaseView.SafeAreaName);
            if (scrim == null || safeArea == null)
            {
                m_bindFailed = true;
                Debug.LogError("In-game purchase host: the HUD shows no booster purchase popup (booster-purchase-screen with "
                               + "its scrim and safe area); a zero-stock booster plays its blocked feedback instead.");
                return false;
            }

            m_view = new BoosterPurchaseView(screen);
            m_presenter.Bind(m_view, m_lifetime.Token);
            m_presenter.CloseRequested += m_onCloseRequested;
            m_scrim = scrim;
            m_scrim.RegisterCallback(m_onScrimClicked);

            // The popup centres inside its safe area, padded on every edge (the Hub's pattern, DEC-136).
            m_safeArea = new SafeAreaPadding(root, safeArea, SafeAreaEdges.All);
            m_safeArea.Apply();

            // Owner: this host, a private instance (DEC-154's one-handler rule). Enabled while the popup shows;
            // disposed in Dispose.
            m_back = new OverlayBackInput();
            m_back.Pressed += m_onBackPressed;
            return true;
        }

        private void OnCloseRequested()
        {
            Close();
        }

        private void OnScrimClicked(ClickEvent evt)
        {
            Close();
        }

        private void OnBackPressed()
        {
            Close();
        }
    }
}
