using System;
using ColorBlockJam.Domain.Session;
using Onity.DI;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Session
{
    /// <summary>
    /// The project's single IOnityTickable (Performance Rule 4): it forwards one frame to the level
    /// session only while Playing. Onity ticks it from the Game scene context's Update, which starts
    /// before the async attempt build ends, so every other state returns at once.
    /// </summary>
    public sealed class GameplayTickOwner : IOnityTickable
    {
        private readonly LevelSession m_session;

        // Owner: the Game scene container (AsSingle). Stops: the container stops ticking when the scene unloads.
        public GameplayTickOwner(LevelSession session)
        {
            m_session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public void Tick()
        {
            if (m_session.State.Value != SessionState.Playing)
            {
                return;
            }

            m_session.RunTick(Time.deltaTime);
        }
    }
}
