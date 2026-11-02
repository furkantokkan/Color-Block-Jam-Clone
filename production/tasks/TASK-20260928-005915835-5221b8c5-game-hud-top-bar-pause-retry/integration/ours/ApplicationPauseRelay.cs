using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Gameplay.Session;
using Onity.DI;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Composition
{
    /// <summary>
    /// Forwards OS backgrounding to the level session (AC-3.12), then to the optional in-game menu listener, which
    /// shows the pause menu on the foreground return (Game HUD design 4). It is a MonoBehaviour only because
    /// Unity reports backgrounding through OnApplicationPause; it has no per-frame code.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationPauseRelay : MonoBehaviour
    {
        private LevelSession m_session;

        // Bound only with the HUD; null in a HUD-less scene.
        private IApplicationPauseListener m_listener;

        [Inject]
        public void Construct(LevelSession session, IResolver resolver)
        {
            m_session = session;
            m_listener = resolver != null && resolver.TryResolve(out IApplicationPauseListener listener) ? listener : null;
        }

        // Unity can report a pause before the scene context injects this component.
        private void OnApplicationPause(bool paused)
        {
            if (m_session != null)
            {
                m_session.NotifyBackground(paused);
            }

            m_listener?.OnApplicationPauseChanged(paused);
        }
    }
}
