using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Hub.Home;
using ColorBlockJam.Hub.Launch;
using ColorBlockJam.Hub.Lives;
using ColorBlockJam.Hub.Navigation;
using ColorBlockJam.Hub.PreLevel;
using ColorBlockJam.Hub.Purchase;
using ColorBlockJam.Hub.Settings;
using ColorBlockJam.Hub.Tabs;
using ColorBlockJam.Player;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.UI.Purchase;
using ColorBlockJam.UI.Settings;
using Onity.DI;
using Onity.Unity.Contexts;
using Onity.Unity.Installers;
using UnityEngine;

namespace ColorBlockJam.Hub.Composition
{
    /// <summary>
    /// Binds the Hub scene's navigator, presenters, overlays, tab catalog, level launcher and Back input into its
    /// SceneContext, shows Home once the project scope is ready and then prepares the offered level hidden behind it;
    /// the scene container disposes them when Hub unloads (design 6.3, 9, DEC-98, DEC-169, DEC-199).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HubSceneInstaller : MonoInstaller
    {
        [SerializeField] private HubScreen m_screen = null;
        [Tooltip("Data/Hub/HubNavigationCatalog.asset: which Home tabs are open, locked, level-gated or hidden (DEC-169).")]
        [SerializeField] private HubNavigationCatalog m_navigationCatalog = null;

        public override void InstallBindings(OnityContainer container)
        {
            if (m_screen == null)
            {
                throw new InvalidOperationException("Hub scene installer: the Hub screen is missing.");
            }

            if (m_navigationCatalog == null)
            {
                throw new InvalidOperationException("Hub scene installer: the navigation catalog is missing. Assign "
                                                    + "Data/Hub/HubNavigationCatalog.asset on the Hub object.");
            }

            container.BindInterfacesAndSelfTo<HubNavigator>().AsSingle();
            container.BindInterfacesAndSelfTo<HomePresenter>().AsSingle();
            // The Home tab bar: one catalog entry per tab (DEC-169).
            container.BindScriptableObject(m_navigationCatalog);
            container.BindInterfacesAndSelfTo<HubTabPresenter>().AsSingle();
            // Case-only plus-button rewards: life gives +1 life, coin gives +100 coins, in place of a shop.
            container.BindInterfacesAndSelfTo<DummyCurrencyReward>().AsSingle();
            // The shared Settings popup presenter (ColorBlockJam.UI, DEC-136) and its Hub overlay adapter.
            container.BindInterfacesAndSelfTo<SettingsPopupPresenter>().AsSingle();
            container.BindInterfacesAndSelfTo<SettingsOverlay>().AsSingle();

            // Pre-level popup, shared purchase popup (DEC-136) and launch (design 9). The two new overlays bind to
            // themselves only, so IHubOverlay keeps a single container binding; HubScreen registers each overlay
            // with the navigator under its own id.
            container.BindInterfacesAndSelfTo<OnityLevelSceneTransition>().AsSingle();
            container.BindInterfacesAndSelfTo<LevelLauncher>().AsSingle();
            container.BindInterfacesAndSelfTo<BoosterPurchasePresenter>().AsSingle();
            container.Bind<BoosterPurchaseOverlay>().AsSingle();
            container.Bind<PreLevelPresenter>().AsSingle();
            container.BindInterfacesAndSelfTo<HubBackInput>().AsSingle();

            // The life refill popup (DEC-174), bound to itself only like the purchase overlay; HubScreen registers it as
            // the "life-refill" menu. ILives comes from the project scope.
            container.Bind<LifeRefillOverlay>().AsSingle();

            // The scene context awaits this callback, so the startup wait is never fire-and-forget.
            container.RegisterBuildCallbackAsync(ShowWhenReadyAsync);
        }

        /// <summary>
        /// Owner: the Hub scene context, which awaits it. Stops: the scene build token, or after one Show.
        /// Direct Play of Hub starts this while the project scope is still loading (DEC-98).
        /// </summary>
        private Task ShowWhenReadyAsync(IResolver resolver, CancellationToken cancellationToken)
        {
            Task projectReady = ProjectContext.Instance != null ? ProjectContext.Instance.ReadyTask : Task.CompletedTask;
            return ShowWhenReadyAsync(projectReady, resolver, cancellationToken);
        }

        /// <summary>
        /// Waits for <paramref name="projectReady"/>, then shows Home once. Takes the project task as a parameter so
        /// the PlayMode tests can run the failed-project-scope branch with a faulted or cancelled task (AC-10).
        /// </summary>
        internal async Task ShowWhenReadyAsync(Task projectReady, IResolver resolver, CancellationToken cancellationToken)
        {
            while (!projectReady.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                await Task.Yield();
            }

            if (cancellationToken.IsCancellationRequested || m_screen == null)
            {
                return;
            }

            int levelCount = GetLevelCount(projectReady, resolver);
            m_screen.Show(levelCount);

            // Hot level (DEC-199): the offered level builds hidden behind Home before Hub reports ready, so Init's loading
            // screen or the Home fade still covers that build and Play opens the level at once.
            await PrepareOfferedLevelAsync(levelCount, resolver, cancellationToken);
        }

        // The level the Level button offers (Hub design 9.3). A failed preparation leaves Play on the fade transition.
        private static async Task PrepareOfferedLevelAsync(int levelCount, IResolver resolver,
            CancellationToken cancellationToken)
        {
            if (levelCount <= 0 || !resolver.TryResolve(out ILevelSceneTransition transition)
                || !resolver.TryResolve(out IPlayerProgress progress))
            {
                return;
            }

            int levelIndex = PlayerProgress.GetPlayableLevelIndex(progress.SavedLevelIndex.Value, levelCount);
            if (levelIndex < 0)
            {
                return;
            }

            try
            {
                await transition.PrepareAsync(levelIndex, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Hub unloaded first: the preparation goes on under the level preloader.
            }
        }

        private static int GetLevelCount(Task projectReady, IResolver resolver)
        {
            if (!projectReady.IsCompletedSuccessfully || !IsBound<LevelCatalog>(resolver)
                || !resolver.TryResolve(out LevelCatalog levels))
            {
                Debug.LogWarning("Hub: the project scope or its level database is unavailable; the level button is disabled.");
                return 0;
            }

            return levels.Count;
        }

        /// <summary>
        /// True when a scope binds <typeparamref name="T"/> explicitly. Onity builds any unbound concrete type on
        /// demand, so without this check a missing LevelCatalog would resolve to a new, never-loaded instance
        /// whose Count throws (AC-10).
        /// </summary>
        private static bool IsBound<T>(IResolver resolver)
        {
            return resolver is OnityContainer container
                   && container.TryGetBindingSource(typeof(T), out OnityBindingSourceInfo source)
                   && !source.IsImplicitRegistration;
        }
    }
}
