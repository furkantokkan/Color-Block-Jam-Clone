using System;
using ColorBlockJam.Domain.Session;
using ColorBlockJam.Environment;
using ColorBlockJam.Gameplay.Board;
using ColorBlockJam.Gameplay.Boosters;
using ColorBlockJam.Gameplay.Effects;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Hud.Menu;
using ColorBlockJam.Gameplay.Hud.Navigation;
using ColorBlockJam.Gameplay.Hud.Result;
using ColorBlockJam.Gameplay.Hud.TopBar;
using ColorBlockJam.Gameplay.Input;
using ColorBlockJam.Gameplay.Session;
using ColorBlockJam.Gameplay.UI;
using ColorBlockJam.Haptics;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Presentation.Boosters;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.DI;
using Onity.Unity.Installers;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Gameplay.Composition
{
    /// <summary>
    /// Binds the Game scene's level-attempt services into its SceneContext and registers the async
    /// attempt build. Session, tick owner, presenter, pointer input and the booster flow (targeting, use
    /// coordinator, frost, HUD and its purchase popup host) are container singletons, so the scene container
    /// disposes them when the scene unloads (PROJECT.md Rule 9, DEC-98, Booster-Gameplay B-12 to B-25, B-32).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameSceneInstaller : MonoInstaller
    {
        [SerializeField] private EnvironmentView m_environment = null;
        [Tooltip("With the piece catalog and its block root, the one BoardArt this scene binds (DEC-123, DEC-193).")]
        [SerializeField] private GamePalette m_palette = null;
        [SerializeField] private BoardPieceCatalog m_catalog = null;
        [SerializeField] private Camera m_camera = null;
        [Tooltip("Optional: frames the board once per build. Without it, one warning is logged and play continues.")]
        [SerializeField] private BoardCamera m_boardCamera = null;
        [Tooltip("Runtime UI documents whose pickable elements block board presses. The HUD document is added automatically.")]
        [SerializeField] private UIDocument[] m_uiDocuments = Array.Empty<UIDocument>();
        [SerializeField] private Material m_iceMaterial = null;
        [Tooltip("Data/Boosters/BoosterRestrictionMatrix.asset: which board boosters a block effect keeps off its block, " +
                 "such as Rocket on Ice (DEC-171). Without it no booster restriction applies.")]
        [SerializeField] private BoosterRestrictionMatrix m_boosterRestrictions = null;
        [SerializeField] private UIDocument m_counterDocument = null;
        [Tooltip("The gameplay HUD (GameplayHud.uxml on BoardOverlayPanelSettings, B-21). Without it, play continues " +
                 "without the booster bar, timer, targeting banner and zero-stock purchase popup.")]
        [SerializeField] private UIDocument m_hudDocument = null;
        [Tooltip("The Time Freeze screen-edge frost material (TimeFreezeOverlay.mat, B-25). Without it the frost stays off.")]
        [SerializeField] private Material m_frostMaterial = null;
        [Tooltip("Art/UI/Supplied/Icons/Gameplay/ic_target.png with Read/Write on: the Rocket reticles' white copy is made from it (B-24).")]
        [SerializeField] private Texture2D m_reticleTexture = null;
        [Tooltip("The confetti the Level Complete popup bursts when it opens (Prefabs/Effects/Confetti/ConfettiBlastRainbow, " +
                 "DEC-173). Any particle prefab works; without one the popup opens without confetti.")]
        [SerializeField] private ParticleSystem m_levelCompleteConfetti = null;
        [Tooltip("Direct Play of Game only: Hub's level launch request and the Level Editor's Test Level choose the level otherwise (design 9.3).")]
        [SerializeField, Min(0)] private int m_levelIndex = 0;
        [Tooltip("Height of the pointer plane above BoardRoot, at the block top (DEC-115).")]
        [SerializeField] private float m_pickPlaneHeight = ExitMotionSettings.k_blockTopHeight;
        [Tooltip("Interim: resume a background pause on foreground until a pause UI exists (DEC-116).")]
        [SerializeField] private bool m_resumeOnForeground = true;

        public override void InstallBindings(OnityContainer container)
        {
            if (m_environment == null)
            {
                throw new InvalidOperationException("Game scene installer: the environment view is missing.");
            }

            if (m_camera == null)
            {
                throw new InvalidOperationException("Game scene installer: the gameplay camera is missing.");
            }

            if (m_palette == null || m_catalog == null)
            {
                throw new InvalidOperationException("Game scene installer: the palette or the board piece catalog is missing.");
            }

            // Validation checks the palette, the tiles and the block root now, at scope build, before any attempt uses them.
            BoardArt art = new BoardArt(m_palette, m_catalog);
            if (!art.Validate(out string artError))
            {
                throw new InvalidOperationException("Game scene installer: " + artError);
            }

            container.BindInstance(art);
            container.BindInstance(m_environment);
            container.BindInstance<IUiPointerFilter>(new UiToolkitPointerFilter(GetPointerVetoDocuments()));
            container.BindInstance<IBoardProjector>(new CameraBoardProjector(m_camera, m_environment.BoardRoot, m_pickPlaneHeight));
            // With the HUD the pause menu shows on the foreground return and only its Close resumes (DEC-135); a
            // HUD-less scene keeps the interim resume, so it never stays paused without a UI (DEC-116).
            container.BindInstance<IPauseOverlay>(m_hudDocument != null
                ? new PopupPauseOverlay()
                : (IPauseOverlay)new NoPauseOverlay(m_resumeOnForeground));

            // The attempt clock with its Time Freeze side (B-8): one instance behind both ports, so Suspend, Resume,
            // Teardown and Won hold or stop both clocks through ICountdown.
            AttemptCountdown countdown = new AttemptCountdown();
            container.BindInstance<ICountdown>(countdown);
            container.BindInstance<IFreezeCountdown>(countdown);

            // Registration order is fixed on Home; hooks and views share this exact source.
            var effects = new BlockEffectContextSource(new IBlockEffectModule[] { new IceEffectModule(m_iceMaterial), new ArrowEffectModule() });
            container.BindInstance(effects);
            container.BindInstance(new BlockEffectViewContext(art, m_environment.BoardRoot, m_camera, m_counterDocument));
            container.BindInstance<IAttemptHookSource>(new EffectAttemptHookSource(effects, m_boosterRestrictions));

            // The container disposes singletons in reverse registration order, so the session is
            // registered after everything its Teardown still calls and is disposed before them.
            container.BindInterfacesAndSelfTo<GameplayPointerInput>().AsSingle();
            container.BindInterfacesAndSelfTo<BoardPresenter>().AsSingle();
            container.BindInterfacesAndSelfTo<DragInputRouter>().AsSingle();
            container.BindInterfacesAndSelfTo<PrimeTweenFinishDeadline>().AsSingle();
            container.BindInterfacesAndSelfTo<LevelSession>().AsSingle();
            container.BindInterfacesAndSelfTo<GameplayTickOwner>().AsSingle();

            // Hot level (DEC-199): a Game scene loaded behind another scene to prepare a level hides now, while it
            // wakes and before its UI documents build their trees, so no frame shows it; it builds the level hidden.
            OnitySceneTransitionStore.TryConsumeActiveEnterData(out LevelPrepareRequest prepare);
            var visibility = new GameSceneVisibility(m_camera, m_environment.transform, GetSceneDocuments());
            if (prepare != null)
            {
                visibility.Hide();
            }

            // The loader maps Hub's launch request onto the starting-booster source (B-17); direct Play, Test Level,
            // Restart and Next Level therefore start with no selection. It is also the level the preloader starts.
            GameAttemptLoader loader = new GameAttemptLoader(m_levelIndex, m_boardCamera, prepare, visibility);
            container.BindInstance<IStartingBoosterSource>(loader);
            container.BindInstance<IPreparedLevel>(loader);
            container.BindInstance<ISeedSource>(new TickCountSeedSource());

            // The zero-stock purchase (B-18, B-32): with the HUD document the HUD branch below binds the shared purchase
            // popup's host; without it a zero-stock tap plays the blocked feedback and nothing pauses.
            if (m_hudDocument == null)
            {
                container.BindInstance<IInGamePurchaseHost>(new UnavailablePurchaseHost());
            }

            // Without a frost material the frost stays inert: Time Freeze still holds the clock and shows its counter.
            container.BindInstance(new TimeFreezeFrostContext(m_camera, m_frostMaterial));

            // The booster flow is registered after the session, so the container disposes it first: the HUD unbinds,
            // the coordinator detaches and cancels its waits, the frost and the targeting mode end, and then the
            // session tears its attempt down, whose calls on them are no-ops by then.
            container.BindInterfacesAndSelfTo<BoosterTargeting>().AsSingle();
            container.BindInterfacesAndSelfTo<TimeFreezeFrostView>().AsSingle();
            container.BindInterfacesAndSelfTo<StartingBoosterPreparer>().AsSingle();
            container.BindInterfacesAndSelfTo<BoosterUseCoordinator>().AsSingle();

            // An unwired HUD binds nothing and logs nothing: the loader resolves IGameplayHud with TryResolve, and the
            // Game scene's composition test reports a missing HUD as ignored rather than passed (B-21).
            if (m_hudDocument != null)
            {
                container.BindInstance(new GameplayHudContext(m_hudDocument, m_reticleTexture));

                // Registered after the coordinator that uses it and before the HUD, so the HUD unbinds first and the
                // host then unbinds the shared presenter and ends its purchase wait (B-32).
                container.BindInterfacesAndSelfTo<InGamePurchaseHost>().AsSingle();
                container.BindInterfacesAndSelfTo<GameplayHud>().AsSingle();

                // The top bar, pause menu and Retry confirmation (Game HUD design 4), registered after the session so
                // the container disposes them first: the top bar, then the menu flow, then its hosts and the exit.
                container.BindInstance(new AttemptLevelNumber());
                container.BindInterfacesAndSelfTo<LevelSessionControl>().AsSingle();
                container.BindInterfacesAndSelfTo<HubSceneTransition>().AsSingle();
                container.BindInterfacesAndSelfTo<GameExitNavigator>().AsSingle();
                container.BindInterfacesAndSelfTo<PauseMenuHost>().AsSingle();
                container.BindInterfacesAndSelfTo<RestartConfirmHost>().AsSingle();
                container.BindInterfacesAndSelfTo<OverlayBackInput>().AsSingle();
                container.BindInterfacesAndSelfTo<GameMenuFlow>().AsSingle();
                container.BindInterfacesAndSelfTo<GameTopBar>().AsSingle();

                // The Level Complete result (reference 05, AC-4.9): after the session, so the flow is disposed first
                // and its save wait and next-level load end with the scene. The host builds the confetti stage on Home.
                container.BindInstance(new LevelCompleteConfettiContext(m_levelCompleteConfetti));
                container.BindInterfacesAndSelfTo<NextLevelTransition>().AsSingle();
                container.BindInterfacesAndSelfTo<LevelCompleteHost>().AsSingle();
                container.BindInterfacesAndSelfTo<LevelCompleteFlow>().AsSingle().NonLazy();

                // Back without targeting opens the pause menu (design 5); set once when the scene container builds.
                container.RegisterBuildCallback(SetBackFallback);
            }

            // Haptics (DEC-156): set once before the loader's Show binds the HUD. Synchronous build callbacks run before
            // the asynchronous one below.
            container.RegisterBuildCallback(SetHaptics);

            // The booster casts enter above the HUD's booster bar; a HUD-less scene keeps the below-board launch.
            container.RegisterBuildCallback(SetCastAnchor);

            // The scene context awaits this callback, so the attempt build is never fire-and-forget.
            container.RegisterBuildCallbackAsync(loader.LoadAsync);
        }

        private static void SetBackFallback(IResolver resolver)
        {
            resolver.Resolve<DragInputRouter>().SetBackFallback(resolver.Resolve<IGameplayBackFallback>());
        }

        // The HUD binds IBoosterCastAnchor through BindInterfacesAndSelfTo; without a HUD nothing is bound.
        private static void SetCastAnchor(IResolver resolver)
        {
            if (resolver.TryResolve(out IBoosterCastAnchor anchor))
            {
                resolver.Resolve<BoardPresenter>().SetCastAnchor(anchor);
            }
        }

        // The project scope binds IHaptics (PlayerDataInstaller); without it, as in a bare test scope, nothing vibrates.
        private static void SetHaptics(IResolver resolver)
        {
            if (!resolver.TryResolve(out IHaptics haptics))
            {
                return;
            }

            resolver.Resolve<DragInputRouter>().SetHaptics(haptics);
            resolver.Resolve<BoardPresenter>().SetHaptics(haptics);
            resolver.Resolve<LevelSession>().SetHaptics(haptics);
            if (resolver.TryResolve(out GameplayHud hud))
            {
                hud.SetHaptics(haptics);
            }
        }

        // Every UI document of the scene the installer knows, once each: the listed ones, the HUD and the counters.
        private UIDocument[] GetSceneDocuments()
        {
            UIDocument[] documents = GetPointerVetoDocuments();
            if (m_counterDocument == null || Array.IndexOf(documents, m_counterDocument) >= 0)
            {
                return documents;
            }

            UIDocument[] all = new UIDocument[documents.Length + 1];
            Array.Copy(documents, all, documents.Length);
            all[documents.Length] = m_counterDocument;
            return all;
        }

        // The listed documents plus the HUD's, once each; they may share a panel, but each is checked while active.
        private UIDocument[] GetPointerVetoDocuments()
        {
            UIDocument[] listed = m_uiDocuments ?? Array.Empty<UIDocument>();
            if (m_hudDocument == null || Array.IndexOf(listed, m_hudDocument) >= 0)
            {
                return listed;
            }

            UIDocument[] documents = new UIDocument[listed.Length + 1];
            Array.Copy(listed, documents, listed.Length);
            documents[listed.Length] = m_hudDocument;
            return documents;
        }
    }
}
