using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.DI;
using Onity.Unity.Async;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Runtime.Startup
{
    /// <summary>
    /// Runs the app's only loading screen: Init shows it while the project scope loads, builds Hub additively behind
    /// it and, once Home is bound, unloads itself behind the black screen fade, so no later transition shows a loading
    /// screen (DEC-140, DEC-198).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StartupLoader : OnitySceneInitiator
    {
        [Tooltip("Scene-flow profile that names the Hub scene.")]
        [SerializeField] private OnitySceneFlowProfile m_profile;

        [Tooltip("The loading bar in this scene's UI document.")]
        [SerializeField] private StartupLoadingView m_view;

        private SceneFadeTransition m_sceneFade;
        private float m_shownProgress;
        private float m_hubLoadStartProgress;

        /// <summary>
        /// Init's SceneContext injects the project scope's scene fade. A project scope that failed to bind it leaves the
        /// fade null, and Init then unloads without it rather than keeping the player on the loading screen.
        /// </summary>
        [Inject]
        public void Construct(IResolver resolver)
        {
            m_sceneFade = resolver != null && resolver.TryResolve(out SceneFadeTransition sceneFade) ? sceneFade : null;
        }

        /// <summary>
        /// Owner: this Init component; the base class passes its destroy token. Stops: when Init unloads itself at
        /// the end, or on that token (another scene replaced Init first).
        /// </summary>
        protected override async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (m_profile == null || m_view == null)
            {
                throw new InvalidOperationException("Startup loader: the scene-flow profile or the loading view is missing.");
            }

            if (!m_profile.TryGetSceneName(OnitySceneFlowStateId.Hub, out string hubSceneName))
            {
                throw new InvalidOperationException("Startup loader: the scene-flow profile names no Hub scene.");
            }

            string startupSceneName = gameObject.scene.name;
            await WaitForProjectScopeAsync(cancellationToken);

            m_hubLoadStartProgress = m_shownProgress;
            await OnitySceneLoader.LoadAdditiveAsync(hubSceneName, ShowHubLoad, cancellationToken);
            Scene hubScene = SceneManager.GetSceneByName(hubSceneName);
            SceneManager.SetActiveScene(hubScene);
            await WaitForSceneScopeAsync(hubScene, cancellationToken);

            // One more frame lets Home's first layout and safe-area pass run under the loading screen.
            await OnityAsync.NextFrameAsync(cancellationToken);

            // Unloading Init destroys this component, so the unload takes no token and nothing here runs after it.
            // With the fade, the project scope owns the step: black covers the loading screen, Init unloads behind it
            // and Home fades in once it has settled (DEC-198).
            if (m_sceneFade == null)
            {
                await OnitySceneLoader.UnloadAsync(startupSceneName);
                return;
            }

            // The fade ignores a request while another change runs, and Init must unload. Nothing runs before Init in
            // a player; a test can open Init while an earlier change still fades in, so let that one finish first.
            while (m_sceneFade.IsRunning)
            {
                await OnityAsync.NextFrameAsync(cancellationToken);
            }

            await m_sceneFade.RunAsync(_ => OnitySceneLoader.UnloadAsync(startupSceneName), cancellationToken);
        }

        // A failed project scope is not rethrown: Onity logs it and Hub shows Home with the level button disabled
        // (AC-10), so the player never stays on the loading screen.
        private async Task WaitForProjectScopeAsync(CancellationToken cancellationToken)
        {
            Task projectReady = ProjectContext.Instance != null ? ProjectContext.Instance.ReadyTask : Task.CompletedTask;
            float startTime = Time.realtimeSinceStartup;
            while (!projectReady.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Show(StartupProgress.ForProjectWait(Time.realtimeSinceStartup - startTime));
                await Task.Yield();
            }
        }

        // Hub's SceneContext completes ReadyTask after its build callbacks, which bind Home and prepare the offered level
        // behind it (HubSceneInstaller, DEC-199); the bar eases meanwhile and fills once Hub is ready. A destroyed or
        // failed scope completes it too, so this wait always ends.
        private async Task WaitForSceneScopeAsync(Scene scene, CancellationToken cancellationToken)
        {
            SceneContext scope = SceneScopes.Find(scene);
            if (scope == null)
            {
                Debug.LogWarning($"Startup loader: scene '{scene.name}' has no SceneContext; Init unloads without waiting.");
                return;
            }

            Task ready = scope.ReadyTask;
            float buildStartProgress = m_shownProgress;
            float startTime = Time.realtimeSinceStartup;
            while (!ready.IsCompleted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Show(StartupProgress.ForSceneBuild(buildStartProgress, Time.realtimeSinceStartup - startTime));
                await Task.Yield();
            }

            Show(1f);
        }

        private void ShowHubLoad(float hubLoadProgress)
        {
            Show(StartupProgress.ForHubLoad(m_hubLoadStartProgress, hubLoadProgress));
        }

        // The bar only moves forward, whatever either phase reports.
        private void Show(float progress)
        {
            if (progress <= m_shownProgress)
            {
                return;
            }

            m_shownProgress = progress;
            m_view.SetProgress(m_shownProgress);
        }
    }
}
