using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.Unity.SceneFlow;

namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// Starts the next level with a <see cref="LevelLaunchRequest"/> and no starting boosters: the level prepared behind
    /// the win popup starts at once when it matches (DEC-199); otherwise the Game scene loads again through the project's
    /// scene-flow profile behind the black screen fade, with no loading screen (DEC-140, DEC-198). Either way a fresh scene
    /// scope rebuilds everything, so nothing from the won attempt survives (PROJECT.md Rule 8). The launch request
    /// outranks the Level Editor's Test Level request, so Continue also advances during test play (DEC-127, DEC-138).
    /// </summary>
    public sealed class NextLevelTransition : INextLevelTransition
    {
        private readonly OnitySceneFlowProfile m_profile;
        private readonly SceneFadeTransition m_sceneFade;
        private readonly LevelPreloader m_preloader;

        public NextLevelTransition(OnitySceneFlowProfile profile, SceneFadeTransition sceneFade, LevelPreloader preloader)
        {
            m_profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            m_sceneFade = sceneFade ?? throw new ArgumentNullException(nameof(sceneFade));
            m_preloader = preloader ?? throw new ArgumentNullException(nameof(preloader));
        }

        public Task PrepareAsync(int levelIndex, CancellationToken cancellationToken)
        {
            return m_preloader.PrepareAsync(levelIndex, cancellationToken);
        }

        public async Task TransitionAsync(int levelIndex, CancellationToken cancellationToken)
        {
            var request = new LevelLaunchRequest(levelIndex, null);
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
