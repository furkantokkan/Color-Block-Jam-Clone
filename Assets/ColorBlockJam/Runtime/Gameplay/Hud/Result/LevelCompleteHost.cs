using System;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Presentation.UI;
using Onity.DI;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>
    /// Shows the Level Complete popup inside the gameplay HUD (reference 05, AC-4.9), bursts its confetti when it opens
    /// (DEC-173) and forwards its one button's presses to <see cref="LevelCompleteFlow"/> as requests.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). Onity calls <see cref="Initialize"/> once when the scene
    /// container builds, before any attempt, and the confetti stage is created then. The view is created over the HUD's
    /// popup instance on the first <see cref="Show"/> (after Won's Finish and Teardown, never a Playing tick). Stops:
    /// <see cref="Hide"/> hides the popup and ends the confetti; <see cref="Dispose"/> hides it and disposes the view
    /// (its click handler), the confetti stage and the safe-area padding. Nothing here runs per frame; the confetti's
    /// own show ends by itself (<see cref="UiParticleOverlay"/>).
    /// </remarks>
    public sealed class LevelCompleteHost : ILevelCompletePopup, IOnityInitializable, IDisposable
    {
        /// <summary>
        /// The world units of the confetti that fill the screen's height: Epic Toon FX's blast (about 2.4 units across
        /// its radius) then spreads over most of the screen, from behind the coin pile.
        /// </summary>
        public const float k_confettiViewHeight = 7f;

        /// <summary>
        /// Set on the HUD's root while the popup shows, so GameplayHud.uss can restyle the HUD under the result (the
        /// booster bar fades further, as in reference 05).
        /// </summary>
        public const string ResultClassName = "hud--result";

        private readonly GameplayHudContext m_context;
        private readonly VisualElement m_root;
        private readonly UiParticleOverlay m_confetti;
        private readonly Action m_onPrimaryRequested;

        private VisualElement m_hudRoot;
        private LevelCompleteView m_view;
        private SafeAreaPadding m_safeArea;
        private bool m_isShowing;
        private bool m_bindFailed;
        private bool m_disposed;

        /// <summary>
        /// The Game scene's host: the popup instance lives in the HUD document of <paramref name="context"/>, and the
        /// confetti stage is created under that document's GameObject, so it unloads with the scene.
        /// </summary>
        [Inject]
        public LevelCompleteHost(GameplayHudContext context, LevelCompleteConfettiContext confetti)
            : this()
        {
            m_context = context ?? throw new ArgumentNullException(nameof(context));
            if (confetti == null)
            {
                throw new ArgumentNullException(nameof(confetti));
            }

            m_confetti = new UiParticleOverlay(confetti.Prefab, context.Document.transform, k_confettiViewHeight);
        }

        /// <summary>A host over an already built HUD tree (a GameplayHud.uxml clone or any ancestor of its popup).</summary>
        public LevelCompleteHost(VisualElement hudRoot)
            : this()
        {
            m_root = hudRoot ?? throw new ArgumentNullException(nameof(hudRoot));
        }

        private LevelCompleteHost()
        {
            m_onPrimaryRequested = OnPrimaryRequested;
        }

        public event Action PrimaryRequested;

        public bool IsShowing => !m_disposed && m_isShowing;

        /// <summary>The bound view, or null before the first show; for tests and captures.</summary>
        public LevelCompleteView View => m_view;

        /// <summary>The confetti stage, or null for a host over a bare tree; for tests and captures.</summary>
        public UiParticleOverlay Confetti => m_confetti;

        /// <summary>Home: creates the confetti stage once the scene container is built, before any attempt.</summary>
        public void Initialize()
        {
            if (!m_disposed)
            {
                m_confetti?.Prepare();
            }
        }

        public bool Show(string rewardText)
        {
            if (m_disposed || !TryBind())
            {
                return false;
            }

            m_view.SetReward(rewardText);
            m_view.SetVisible(true);
            m_hudRoot.AddToClassList(ResultClassName);
            m_isShowing = true;

            // The burst sits behind the coin pile and plays once per opening.
            m_confetti?.Play(m_view.ConfettiSurface, m_view.Coins);
            return true;
        }

        public void SetPrimary(LevelCompleteAction action, bool enabled)
        {
            if (!m_disposed)
            {
                m_view?.SetPrimary(action, enabled);
            }
        }

        public void SetMessage(string text)
        {
            if (!m_disposed)
            {
                m_view?.SetMessage(text);
            }
        }

        public void Hide()
        {
            if (m_disposed)
            {
                return;
            }

            m_isShowing = false;
            m_confetti?.Stop();
            m_view?.SetVisible(false);
            m_hudRoot?.RemoveFromClassList(ResultClassName);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Hide();
            m_disposed = true;
            m_confetti?.Dispose();
            if (m_view != null)
            {
                m_view.PrimaryRequested -= m_onPrimaryRequested;
                m_view.Dispose();
                m_view = null;
            }

            m_safeArea?.Dispose();
            m_safeArea = null;
            PrimaryRequested = null;
        }

        // Runs once, on the first show; later calls return the bound state. A HUD without the popup instance, or a popup
        // missing a named element, is an authoring error: it is logged once and no popup shows.
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

            VisualElement screen = root.Q<VisualElement>(LevelCompleteView.ScreenName);
            if (screen == null)
            {
                m_bindFailed = true;
                Debug.LogError("Level Complete: the HUD shows no LevelCompletePopup.uxml (level-complete-screen); "
                               + "the win popup cannot show.");
                return false;
            }

            LevelCompleteView view;
            try
            {
                view = new LevelCompleteView(screen);
            }
            catch (InvalidOperationException exception)
            {
                m_bindFailed = true;
                Debug.LogError("Level Complete: " + exception.Message + " The win popup cannot show.");
                return false;
            }

            m_view = view;
            m_hudRoot = root;
            m_view.PrimaryRequested += m_onPrimaryRequested;

            // The content centres inside its safe area, padded on every edge from the full-screen root (Hub's pattern).
            m_safeArea = new SafeAreaPadding(root, m_view.SafeArea, SafeAreaEdges.All);
            m_safeArea.Apply();
            return true;
        }

        private void OnPrimaryRequested()
        {
            PrimaryRequested?.Invoke();
        }
    }
}
