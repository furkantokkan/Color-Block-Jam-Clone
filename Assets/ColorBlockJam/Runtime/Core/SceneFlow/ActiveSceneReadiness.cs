using System.Threading;
using System.Threading.Tasks;
using Onity.Unity.Async;
using Onity.Unity.Contexts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Waits, once per frame, until <see cref="SceneRevealPolicy"/> lets the active scene show: its SceneContext's
    /// ReadyTask covers the scene's whole build (a Game scene's level pack, board and pools, Hub's Home bind), and the
    /// policy adds the frames after it (DEC-198).
    /// </summary>
    /// <remarks>
    /// Owner: the scene transition, which awaits it. Stops: the policy's answer, or the token. A scope whose build
    /// fails or is destroyed completes its ReadyTask too, so a failed scene still shows instead of staying black.
    /// </remarks>
    public sealed class ActiveSceneReadiness : IActiveSceneReadiness
    {
        public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
        {
            // A scene without a context has nothing to build.
            SceneContext scope = SceneScopes.Find(SceneManager.GetActiveScene());
            Task ready = scope != null ? scope.ReadyTask : Task.CompletedTask;
            float start = Time.realtimeSinceStartup;
            int framesSinceReady = 0;

            while (true)
            {
                bool isSceneReady = ready.IsCompleted;
                if (SceneRevealPolicy.ShouldReveal(isSceneReady, framesSinceReady, Time.realtimeSinceStartup - start))
                {
                    return;
                }

                await OnityAsync.NextFrameAsync(cancellationToken);
                if (isSceneReady)
                {
                    framesSinceReady++;
                }
            }
        }
    }
}
