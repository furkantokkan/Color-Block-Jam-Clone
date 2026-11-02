using System;
using System.Threading;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Player;
using ColorBlockJam.Presentation.UI;
using ColorBlockJam.UI.Settings;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Shows the shared Settings popup with HOME as the in-game pause menu (DEC-135, DEC-136, reference 20): toggles save
    /// through the shared presenter and the project's player settings, the four placeholder buttons give press
    /// feedback only, and Close (button or scrim) and HOME go to the menu flow as requests.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). It creates the shared presenter and binds it, with the
    /// view over the HUD's popup instance, on the first <see cref="Show"/> (a transition into Paused or a terminal
    /// state, never a steady Playing tick). It adds no scrim callback: the view already raises CloseRequested for a
    /// scrim tap. Stops: <see cref="Hide"/> hides the popup; <see cref="Dispose"/> hides it, removes the presenter
    /// handlers, disposes the presenter (which cancels its pending saves), the view, the safe-area padding and the
    /// lifetime token. Nothing here runs per frame.
    /// </remarks>
    public sealed class PauseMenuHost : IPauseMenu, IDisposable
    {
        private readonly SettingsPopupPresenter m_presenter;
        private readonly GameplayHudContext m_context;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private readonly Action m_onCloseRequested;
        private readonly Action m_onHomeRequested;

        private SettingsPopupView m_view;
        private Button m_closeButton;
        private Button m_homeButton;
        private SafeAreaPadding m_safeArea;
        private bool m_actionsEnabled = true;
        private bool m_bindFailed;
        private bool m_disposed;

        /// <summary>The Game scene's host: the popup instance lives in the HUD document of <paramref name="context"/>.</summary>
        [Inject]
        public PauseMenuHost(GameplayHudContext context, IPlayerSettings settings)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_presenter = new SettingsPopupPresenter(settings);
            m_presenter.SetHomeVisible(true);
            m_onCloseRequested = OnCloseRequested;
            m_onHomeRequested = OnHomeRequested;
        }

        public event Action CloseRequested;

        public event Action HomeRequested;

        public bool IsShowing => !m_disposed && m_presenter.IsVisible;

        public bool Show()
        {
            if (m_disposed || !TryBind())
            {
                return false;
            }

            m_presenter.Show();
            return true;
        }

        public void Hide()
        {
            if (!m_disposed)
            {
                m_presenter.Hide();
            }
        }

        public void SetActionsEnabled(bool enabled)
        {
            m_actionsEnabled = enabled;
            m_closeButton?.SetEnabled(enabled);
            m_homeButton?.SetEnabled(enabled);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            // The view does not hide itself on Dispose, so the popup is hidden first.
            m_presenter.Hide();
            m_disposed = true;
            m_presenter.CloseRequested -= m_onCloseRequested;
            m_presenter.HomeRequested -= m_onHomeRequested;
            m_presenter.Dispose();
            m_view?.Dispose();
            m_view = null;
            m_safeArea?.Dispose();
            m_safeArea = null;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
            CloseRequested = null;
            HomeRequested = null;
        }

        // Runs once, on the first show; later calls return the bound state. A HUD without the popup instance, or a popup
        // missing a named element, is an authoring error: it is logged once and Pause then does nothing.
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

            VisualElement root = m_context.Document.rootVisualElement;
            if (root == null)
            {
                return false;
            }

            VisualElement screen = root.Q<VisualElement>(SettingsPopupView.ScreenName);
            VisualElement safeArea = screen?.Q<VisualElement>(SettingsPopupView.SafeAreaName);
            Button closeButton = screen?.Q<Button>(SettingsPopupView.CloseButtonName);
            Button homeButton = screen?.Q<Button>(SettingsPopupView.HomeButtonName);
            if (safeArea == null || closeButton == null || homeButton == null)
            {
                m_bindFailed = true;
                Debug.LogError("Pause menu: the HUD shows no Settings popup (settings-screen with its safe area, close "
                               + "and HOME buttons); Pause does nothing.");
                return false;
            }

            SettingsPopupView view;
            try
            {
                view = new SettingsPopupView(screen);
            }
            catch (InvalidOperationException exception)
            {
                // A Settings popup missing one of its named elements: the flow then resumes instead of pausing
                // without a UI.
                m_bindFailed = true;
                Debug.LogError("Pause menu: " + exception.Message + " Pause does nothing.");
                return false;
            }

            m_view = view;
            m_presenter.Bind(m_view, m_lifetime.Token);
            m_presenter.CloseRequested += m_onCloseRequested;
            m_presenter.HomeRequested += m_onHomeRequested;
            m_closeButton = closeButton;
            m_homeButton = homeButton;
            SetActionsEnabled(m_actionsEnabled);

            // The popup centres inside its safe area, padded on every edge from the full-screen root (Hub's pattern).
            m_safeArea = new SafeAreaPadding(root, safeArea, SafeAreaEdges.All);
            m_safeArea.Apply();
            return true;
        }

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke();
        }

        private void OnHomeRequested()
        {
            HomeRequested?.Invoke();
        }
    }
}
