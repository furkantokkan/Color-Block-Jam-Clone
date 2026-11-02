using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.Unity.SceneFlow;

namespace ColorBlockJam.Hub.Launch
{
    /// <summary>
    /// Opens the requested level: the level prepared behind Hub starts at once when it matches (DEC-199); otherwise the
    /// Gameplay scene group of the project's scene-flow profile loads behind the black screen fade with the request as
    /// its entry data, which the Game scene consumes on load (DEC-98, DEC-140, DEC-198).
    /// </summary>
    public sealed class OnityLevelSceneTransition : ILevelSceneTransition
    {
        private readonly OnitySceneFlowProfile m_profile;
        private readonly SceneFadeTransition m_sceneFade;
        private readonly LevelPreloader m_preloader;

        public OnityLevelSceneTransition(OnitySceneFlowProfile profile, SceneFadeTransition sceneFade,
            LevelPreloader preloader)
        {
            m_profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            m_sceneFade = sceneFade ?? throw new ArgumentNullException(nameof(sceneFade));
            m_preloader = preloader ?? throw new ArgumentNullException(nameof(preloader));
        }

        public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
        {
            return m_preloader.PrepareAsync(levelIndex, cancellationToken);
        }

        public Task TransitionAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            return OpenAsync(request, cancellationToken);
        }

        private async Task OpenAsync(LevelLaunchRequest request, CancellationToken cancellationToken)
        {
            if (await m_preloader.TryStartAsync(request, cancellationToken))
            {
                return;
            }

            await m_sceneFade.RunAsync(
                token => OnitySceneFlow.TransitionAsync(m_profile, OnitySceneFlowStateId.Gameplay, request, token),
                cancellationToken);
        }
    }
}
