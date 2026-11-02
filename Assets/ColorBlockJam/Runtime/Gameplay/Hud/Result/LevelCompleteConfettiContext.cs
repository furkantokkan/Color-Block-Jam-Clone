using UnityEngine;

namespace ColorBlockJam.Gameplay.Hud.Result
{
    /// <summary>
    /// The confetti the Level Complete popup plays when it opens (DEC-173): a particle prefab, by default Epic Toon FX's
    /// ConfettiBlastRainbow, or null for none. It holds no state.
    /// </summary>
    public sealed class LevelCompleteConfettiContext
    {
        /// <param name="prefab">The effect's root particle system, or null: the popup then opens without confetti.</param>
        public LevelCompleteConfettiContext(ParticleSystem prefab)
        {
            Prefab = prefab;
        }

        public ParticleSystem Prefab { get; }
    }
}
