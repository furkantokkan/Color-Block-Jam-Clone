using System;
using System.Threading;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using ColorBlockJam.Runtime.SceneFlow;
using Onity.Unity.SceneFlow;

namespace ColorBlockJam.Gameplay.Hud.Navigation
{
    /// <summary>
    /// Returns from the Game scene to Hub through the project's scene-flow profile, with no entry data, behind the
    /// black screen fade and with no loading screen (DEC-140, DEC-198); during the Level Editor's Test Level it ends
    /// Play Mode instead, because test play starts no runtime scene flow (DEC-127).
    /// </summary>
    public sealed class HubSceneTransition : IHubSceneTransition
    {
        private readonly OnitySceneFlowProfile m_profile;
        private readonly SceneFadeTransition m_sceneFade;

        public HubSceneTransition(OnitySceneFlowProfile profile, SceneFadeTransition sceneFade)
        {
            m_profile = profile != null ? profile : throw new ArgumentNullException(nameof(profile));
            m_sceneFade = sceneFade ?? throw new ArgumentNullException(nameof(sceneFade));
        }

        public Task TransitionAsync(CancellationToken cancellationToken)
        {
#if UNITY_EDITOR
            if (LevelTestPlayRequest.TryRead(out LevelTestPlayRequest _))
            {
                // The Level Editor restores its scene and level when Play Mode ends (DEC-127).
                UnityEditor.EditorApplication.isPlaying = false;
                return Task.CompletedTask;
            }
#endif
            return m_sceneFade.RunAsync(
                token => OnitySceneFlow.TransitionAsync(m_profile, OnitySceneFlowStateId.Hub, null, token),
                cancellationToken);
        }
    }
}
