using System.Threading;
using System.Threading.Tasks;

namespace ColorBlockJam.Runtime.SceneFlow
{
    /// <summary>
    /// Waits until the scene a change left active can be shown (DEC-198); a seam so the scene transition can be tested
    /// without loading scenes.
    /// </summary>
    public interface IActiveSceneReadiness
    {
        /// <summary>
        /// Completes once the active scene has finished building and its first frames have settled. Owner: the scene
        /// transition, which awaits it. Stops: readiness, the wait limit, or <paramref name="cancellationToken"/>.
        /// </summary>
        Task WaitUntilReadyAsync(CancellationToken cancellationToken);
    }
}
