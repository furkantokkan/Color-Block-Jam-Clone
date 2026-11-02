using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Domain.Session;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// Runs the single Game-to-Hub exit (pause HOME, fail popup close, no-life Retry, Level Complete Home; AC-8):
    /// the session tears the attempt down first (a committed but unapplied booster is discarded without refund),
    /// then the Hub transition runs; while it runs, or after it completed, every further HOME is ignored. It
    /// mirrors Hub's LevelLauncher.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle, HUD branch). Stops: <see cref="Dispose"/> cancels the lifetime token
    /// that the transition awaits. The Single load of Hub unloads Game and disposes this navigator mid-await: the
    /// cancellation is caught and nothing is touched afterwards. A failed transition logs once and releases the guard,
    /// so HOME can be pressed again; the session then already is Home, so only the transition repeats. No Update,
    /// coroutine or static state.
    /// </remarks>
    public sealed class GameExitNavigator : IGameExitNavigator, IDisposable
    {
        private readonly IGameSessionControl m_session;
        private readonly IHubSceneTransition m_transition;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private Task m_pendingExit = Task.CompletedTask;
        private bool m_isExiting;
        private bool m_disposed;

        public GameExitNavigator(IGameSessionControl session, IHubSceneTransition transition)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
            m_transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        public bool IsExiting => m_isExiting;

        /// <summary>The exit in flight, or a completed task; it never faults. Tests await it.</summary>
        public Task PendingExit => m_pendingExit;

        public bool TryGoHome()
        {
            if (m_disposed || m_isExiting)
            {
                return false;
            }

            // A retry after a failed transition finds the session already Home.
            if (m_session.State.Value != SessionState.Home && !m_session.GoHome())
            {
                return false;
            }

            m_isExiting = true;
            m_pendingExit = ExitAsync();
            return true;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_lifetime.Cancel();
            m_lifetime.Dispose();
        }

        // Owner: this navigator, which stores the task. Stops: the lifetime token (scene unload) or the transition's end.
        private async Task ExitAsync()
        {
            try
            {
                await m_transition.TransitionAsync(m_lifetime.Token);
            }
            catch (OperationCanceledException)
            {
                // The Game scene unloaded under the load: this navigator is gone, so nothing is touched.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!m_disposed)
                {
                    m_isExiting = false;
                }
            }
        }
    }
}
