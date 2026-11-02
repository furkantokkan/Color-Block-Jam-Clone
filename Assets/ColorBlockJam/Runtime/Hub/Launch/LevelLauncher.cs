using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Hub.Launch
{
    /// <summary>
    /// Runs the single Hub-to-Game transition with a <see cref="LevelLaunchRequest"/>: while one is in flight, or after
    /// one completed, every further launch is ignored (design 9.3, AC-1.1).
    /// </summary>
    /// <remarks>
    /// Owner: the Hub scene container (AsSingle). Stops: <see cref="Dispose"/> at Hub unload cancels its token, which
    /// ends the wait; a scene load that Unity already started still finishes. A failed load logs once and releases
    /// the guard so Play can be pressed again. No Update, coroutine or static state.
    /// </remarks>
    public sealed class LevelLauncher : ILevelLauncher, IDisposable
    {
        private readonly ILevelSceneTransition m_transition;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private bool m_isLaunching;
        private bool m_disposed;

        public LevelLauncher(ILevelSceneTransition transition)
        {
            m_transition = transition ?? throw new ArgumentNullException(nameof(transition));
        }

        public bool IsLaunching => m_isLaunching;

        /// <summary>
        /// Starts the transition, or returns <see cref="LevelLaunchResult.Ignored"/> at once when one is in flight or
        /// completed, the launcher is disposed or <paramref name="cancellationToken"/> is already cancelled.
        /// Owner: the caller, which awaits the result. Stops: <paramref name="cancellationToken"/> or disposal.
        /// </summary>
        public Task<LevelLaunchResult> LaunchAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (m_disposed || m_isLaunching || cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(LevelLaunchResult.Ignored);
            }

            m_isLaunching = true;
            return TransitionAsync(request, cancellationToken);
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

        private async Task<LevelLaunchResult> TransitionAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            using (CancellationTokenSource linked =
                   CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, m_lifetime.Token))
            {
                try
                {
                    await m_transition.TransitionAsync(request, linked.Token);
                    return LevelLaunchResult.Completed;
                }
                catch (OperationCanceledException)
                {
                    m_isLaunching = false;
                    return LevelLaunchResult.Cancelled;
                }
                catch (Exception exception)
                {
                    m_isLaunching = false;
                    Debug.LogException(exception);
                    return LevelLaunchResult.Failed;
                }
            }
        }
    }
}
