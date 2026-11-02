using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using Onity.Unity.Async;
using Onity.Unity.Contexts;
using Onity.Unity.SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Keeps the next level ready (DEC-199): it loads the Game scene additively behind the current scene, lets it build
    /// that level hidden on Home, and starts it in one frame on Play or Continue, so the level opens with no fade and no
    /// freeze while the scene that asked hides and unloads behind it. One level is prepared at a time.
    /// </summary>
    /// <remarks>
    /// Owner: the project container (AsSingle), because a prepared scene outlives the scene that asked for it. Stop
    /// points: a preparation ends when its scene is ready or has failed, a start when the replaced scene has unloaded,
    /// and <see cref="Dispose"/> cancels the lifetime token every wait observes. A Single scene load (a fade transition)
    /// unloads a prepared scene with every other scene, which the next request notices. No Update.
    /// </remarks>
    public sealed class LevelPreloader : IDisposable
    {
        private readonly string m_gameSceneName;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();

        // The last preparation requested, chained after the ones before it, and its level; the index means something
        // only while that task runs.
        private Task m_preparation = Task.CompletedTask;
        private int m_preparingIndex = -1;
        private Scene m_scene;
        private IPreparedLevel m_level;
        private bool m_disposed;

        public LevelPreloader(OnitySceneFlowProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            if (!profile.TryGetSceneName(OnitySceneFlowStateId.Gameplay, out m_gameSceneName))
            {
                throw new InvalidOperationException("Level preloader: the scene-flow profile names no Gameplay scene.");
            }
        }

        /// <summary>True while a preparation runs.</summary>
        public bool IsPreparing => !m_preparation.IsCompleted;

        /// <summary>True when a loaded, hidden Game scene waits with <paramref name="levelIndex"/> built.</summary>
        public bool IsPrepared(int levelIndex)
        {
            return m_level != null && m_scene.IsValid() && m_scene.isLoaded && m_level.IsPrepared
                   && m_level.LevelIndex == levelIndex;
        }

        /// <summary>
        /// Prepares <paramref name="levelIndex"/> behind the current scene, after any running preparation, and releases a
        /// scene prepared for another level. The task completes when the level waits hidden or the preparation failed,
        /// which leaves nothing prepared, so a start then falls back to the fade. Cancelling
        /// <paramref name="cancellationToken"/> ends only the caller's wait.
        /// </summary>
        public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
        {
            if (levelIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(levelIndex), levelIndex, "The level index cannot be negative.");
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (m_disposed || IsPrepared(levelIndex))
            {
                return Task.CompletedTask;
            }

            // Always chained, even behind a running preparation of the same level: a Single scene load (Back to Home
            // from the win popup) can unload that one's scene mid-build. The chained run prepares only what is still
            // missing when it starts.
            m_preparingIndex = levelIndex;
            m_preparation = RunPrepareAsync(levelIndex, m_preparation, m_lifetime.Token);
            return CallerWait.WaitAsync(m_preparation, cancellationToken);
        }

        /// <summary>
        /// Starts the level prepared for <paramref name="request"/>: its starting boosters commit while it is hidden, then
        /// in one frame it becomes the active scene and shows while the active scene hides, and that scene unloads. False
        /// when no level is prepared for the request's index, or its start failed before it showed; the caller then
        /// loads the level behind the fade. A start for the level still preparing waits for it. Cancelling
        /// <paramref name="cancellationToken"/> ends only the caller's wait: the replaced scene is usually the caller's.
        /// </summary>
        public async Task<bool> TryStartAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (m_disposed)
            {
                return false;
            }

            if (IsPreparing && m_preparingIndex == request.LevelIndex)
            {
                await CallerWait.WaitAsync(m_preparation, cancellationToken);
            }

            if (m_disposed || !IsPrepared(request.LevelIndex))
            {
                return false;
            }

            IPreparedLevel level = m_level;
            Scene scene = m_scene;
            m_level = null;
            m_scene = default;
            return await CallerWait.WaitAsync(StartAsync(level, scene, request, m_lifetime.Token), cancellationToken);
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_level = null;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
        }

        // Owner: this preloader, which stores the task. Stops: the level waits hidden, the preparation failed, or the
        // lifetime token. It never faults, so the next preparation can await it.
        private async Task RunPrepareAsync(int levelIndex, Task previous, CancellationToken token)
        {
            await previous;
            if (token.IsCancellationRequested || IsPrepared(levelIndex))
            {
                return;
            }

            Scene scene = default;
            try
            {
                await ReleaseAsync(token);
                scene = await LoadHiddenAsync(levelIndex, token);
                IPreparedLevel level = await WaitForPreparedLevelAsync(scene, levelIndex, token);
                if (level != null)
                {
                    m_scene = scene;
                    m_level = level;
                    return;
                }

                // A Single scene load unloaded the scene mid-build: whoever asks next prepares again, so no warning.
                if (scene.IsValid() && scene.isLoaded)
                {
                    Debug.LogWarning($"Level preload: the Game scene did not prepare level {levelIndex + 1}; Play and "
                                     + "Continue load it behind the fade instead.");
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                // The project scope ended.
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }

            // A scene that prepared nothing only costs memory, so it goes.
            try
            {
                await UnloadAsync(scene, token);
            }
            catch (OperationCanceledException)
            {
                // The project scope ended during the unload, which Unity still completes.
            }
        }

        // Loads the Game scene additively with a prepare request, which its installer consumes while the scene wakes,
        // and returns that scene; the scene name alone cannot tell it from a Game scene that already shows.
        private async Task<Scene> LoadHiddenAsync(int levelIndex, CancellationToken token)
        {
            Scene loaded = default;

            void OnSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                if (!loaded.IsValid() && mode == LoadSceneMode.Additive && scene.name == m_gameSceneName)
                {
                    loaded = scene;
                }
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            try
            {
                OnitySceneTransitionStore.SetActiveEnterData(new LevelPrepareRequest(levelIndex));
                await OnitySceneLoader.LoadAdditiveAsync(m_gameSceneName, null, token);
            }
            finally
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }

            return loaded;
        }

        // Owner: this preloader. Stops: the replaced scene has unloaded, or the lifetime token.
        private async Task<bool> StartAsync(IPreparedLevel level, Scene scene, LevelLaunchRequest request,
            CancellationToken token)
        {
            Scene replaced = SceneManager.GetActiveScene();
            try
            {
                await level.PrepareStartAsync(request, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The level never showed: the caller loads it behind the fade instead.
                Debug.LogException(exception);
                await UnloadAsync(scene, token);
                return false;
            }

            if (!scene.isLoaded)
            {
                return false;
            }

            // One frame: the prepared scene becomes active and shows while the replaced one hides; it unloads after.
            SceneManager.SetActiveScene(scene);
            SceneVisuals.Hide(replaced);
            level.Play();
            if (replaced != scene)
            {
                await UnloadAsync(replaced, token);
            }

            return true;
        }

        // A prepared scene for another level is released before a new one loads.
        private async Task ReleaseAsync(CancellationToken token)
        {
            Scene scene = m_scene;
            m_level = null;
            m_scene = default;
            await UnloadAsync(scene, token);
        }

        // The scene's prepared level once its scope has built, or null when the scene did not prepare this level.
        private static async Task<IPreparedLevel> WaitForPreparedLevelAsync(Scene scene, int levelIndex,
            CancellationToken token)
        {
            SceneContext scope = SceneScopes.Find(scene);
            if (scope == null)
            {
                return null;
            }

            // A failed or destroyed scope completes its task too, so this wait always ends.
            while (!scope.ReadyTask.IsCompleted)
            {
                await OnityAsync.NextFrameAsync(token);
            }

            return scope.Container != null && scope.Container.TryResolve(out IPreparedLevel level) && level.IsPrepared
                   && level.LevelIndex == levelIndex
                ? level
                : null;
        }

        // The unload completes under Unity; the token ends only this wait.
        private static async Task UnloadAsync(Scene scene, CancellationToken token)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
            while (operation != null && !operation.isDone)
            {
                await OnityAsync.NextFrameAsync(token);
            }
        }
    }
}
