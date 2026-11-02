using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Runs one scene change at a time behind the black screen fade (DEC-198): it fades out, runs the change, waits
    /// until the active scene is ready and settled, then fades back in, so the load and the new scene's build frames
    /// never show.
    /// </summary>
    /// <remarks>
    /// Owner: the project container (AsSingle), because a change outlives the scene that asks for it: the load
    /// disposes that scene's objects. Stop points: each change ends with its fade in; <see cref="Dispose"/> cancels
    /// the lifetime token, which stops the fade, the change's wait and the readiness wait. No Update.
    /// </remarks>
    public sealed class SceneFadeTransition : IDisposable
    {
        private readonly IScreenFade m_fade;
        private readonly IActiveSceneReadiness m_readiness;
        private readonly CancellationTokenSource m_lifetime = new CancellationTokenSource();
        private bool m_isRunning;
        private bool m_disposed;

        public SceneFadeTransition(IScreenFade fade, IActiveSceneReadiness readiness)
        {
            m_fade = fade ?? throw new ArgumentNullException(nameof(fade));
            m_readiness = readiness ?? throw new ArgumentNullException(nameof(readiness));
        }

        /// <summary>True from the start of a change until its fade in ends.</summary>
        public bool IsRunning => m_isRunning;

        /// <summary>
        /// Runs <paramref name="sceneChange"/> behind the fade. The change receives this transition's lifetime token,
        /// not <paramref name="cancellationToken"/>, because the scene that asks is usually the one the change unloads.
        /// The task ends after the fade in; it fails with the change's exception once the old scene has faded back in,
        /// and it is cancelled when <paramref name="cancellationToken"/> is, which ends the caller's wait but never a
        /// started change. A request while another change runs, or after disposal, is ignored and completes at once.
        /// </summary>
        public Task RunAsync(Func<CancellationToken, Task> sceneChange, CancellationToken cancellationToken)
        {
            if (sceneChange == null)
            {
                throw new ArgumentNullException(nameof(sceneChange));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled(cancellationToken);
            }

            if (m_disposed || m_isRunning)
            {
                return Task.CompletedTask;
            }

            m_isRunning = true;
            return CallerWait.WaitAsync(RunOwnedAsync(sceneChange, m_lifetime.Token), cancellationToken);
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

        // Owner: this transition, which the returned task reports to the caller. Stops: the fade in, a failed change
        // after its fade back in, or the lifetime token.
        private async Task RunOwnedAsync(Func<CancellationToken, Task> sceneChange, CancellationToken token)
        {
            try
            {
                await m_fade.FadeOutAsync(token);
                try
                {
                    await sceneChange(token);
                }
                catch (Exception) when (!token.IsCancellationRequested)
                {
                    // The old scene still shows: reveal it again so its caller can offer the action once more.
                    await m_fade.FadeInAsync(token);
                    throw;
                }

                await m_readiness.WaitUntilReadyAsync(token);
                await m_fade.FadeInAsync(token);
            }
            finally
            {
                m_isRunning = false;
            }
        }
    }
}
