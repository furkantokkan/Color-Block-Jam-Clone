using System;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Presentation.UI;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.Menu
{
    /// <summary>
    /// Shows the Retry confirmation popup inside the gameplay HUD (Game HUD design 6): "Level N" on the header, the
    /// heart with "-1", the life copy and a green Retry, or Leave when HOME opened it (DEC-189); the green button, Close
    /// and a scrim tap go to the menu flow as requests. The flow charges the attempt's life (DEC-174); this host only
    /// shows the copy.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). It creates the view over the HUD's popup instance on the
    /// first <see cref="Show"/> (a transition into Paused or Failed, never a steady Playing tick). Stops:
    /// <see cref="Hide"/> hides the popup; <see cref="Dispose"/> hides it and disposes the view (its click handlers)
    /// and the safe-area padding. Nothing here runs per frame.
    /// </remarks>
    public sealed class RestartConfirmHost : IRestartConfirm, IDisposable
    {
        private const string k_retryLabel = "Retry";
        private const string k_leaveLabel = "Leave";

        private readonly GameplayHudContext m_context;
        private readonly Action m_onConfirmRequested;
        private readonly Action m_onCloseRequested;

        private RestartConfirmView m_view;
        private SafeAreaPadding m_safeArea;
        private bool m_isShowing;
        private bool m_actionsEnabled = true;
        private bool m_bindFailed;
        private bool m_disposed;

        /// <summary>The Game scene's host: the popup instance lives in the HUD document of <paramref name="context"/>.</summary>
        [Inject]
        public RestartConfirmHost(GameplayHudContext context)
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            m_onConfirmRequested = OnConfirmRequested;
            m_onCloseRequested = OnCloseRequested;
        }

        public event Action CloseRequested;

        public event Action ConfirmRequested;

        public bool IsShowing => !m_disposed && m_isShowing;

        public bool Show(string title, LifeConfirmAction action)
        {
            if (m_disposed || !TryBind())
            {
                return false;
            }

            m_view.SetTitle(title);
            m_view.SetConfirmLabel(action == LifeConfirmAction.Leave ? k_leaveLabel : k_retryLabel);
            m_view.SetConfirmEnabled(true);
            m_view.SetVisible(true);
            m_isShowing = true;
            return true;
        }

        public void Hide()
        {
            if (m_disposed)
            {
                return;
            }

            m_isShowing = false;
            m_view?.SetVisible(false);
        }

        public void SetActionsEnabled(bool enabled)
        {
            m_actionsEnabled = enabled;
            m_view?.SetActionsEnabled(enabled);
        }

        public void SetConfirmEnabled(bool enabled)
        {
            if (!m_disposed)
            {
                m_view?.SetConfirmEnabled(enabled);
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Hide();
            m_disposed = true;
            if (m_view != null)
            {
                m_view.ConfirmRequested -= m_onConfirmRequested;
                m_view.CloseRequested -= m_onCloseRequested;
                m_view.Dispose();
                m_view = null;
            }

            m_safeArea?.Dispose();
            m_safeArea = null;
            ConfirmRequested = null;
            CloseRequested = null;
        }

        // Runs once, on the first show; later calls return the bound state. A HUD without the popup instance, or a popup
        // missing a named element, is an authoring error: it is logged once and Retry then does nothing.
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

            VisualElement screen = root.Q<VisualElement>(RestartConfirmView.ScreenName);
            if (screen == null)
            {
                m_bindFailed = true;
                Debug.LogError("Retry confirmation: the HUD shows no RestartConfirmPopup.uxml (restart-confirm-screen); "
                               + "Retry does nothing.");
                return false;
            }

            RestartConfirmView view;
            try
            {
                view = new RestartConfirmView(screen);
            }
            catch (InvalidOperationException exception)
            {
                // A popup missing one of its named elements: the flow then resumes instead of pausing without a UI.
                m_bindFailed = true;
                Debug.LogError("Retry confirmation: " + exception.Message + " Retry does nothing.");
                return false;
            }

            m_view = view;
            m_view.ConfirmRequested += m_onConfirmRequested;
            m_view.CloseRequested += m_onCloseRequested;
            m_view.SetActionsEnabled(m_actionsEnabled);

            // The popup centres inside its safe area, padded on every edge from the full-screen root (Hub's pattern).
            m_safeArea = new SafeAreaPadding(root, m_view.SafeArea, SafeAreaEdges.All);
            m_safeArea.Apply();
            return true;
        }

        private void OnConfirmRequested()
        {
            ConfirmRequested?.Invoke();
        }

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke();
        }
    }
}
