using PrimeTween;
using UnityEngine;

namespace ColorBlockJam.Gameplay.Board
{
    /// <summary>
    /// Sequences Hammer contact and Vacuum intake. The presenter owns the returned root-targeted tweens:
    /// Suspend pauses them, Teardown stops them, and its completion poll returns the roots.
    /// </summary>
    internal sealed class BoosterRemovalMotion
    {
        private const float k_removalShrinkSeconds = 0.1f;
        private const int k_none = -1;

        private int m_vacuumSource = k_none;
        private bool m_vacuumPlaying;
        private Vector3 m_vacuumIntake;
        private float m_vacuumStart;
        private float m_vacuumSeconds;

        public void BeginSync()
        {
            m_vacuumSource = k_none;
        }

        public Tween PlayHammer(BoosterCasts casts, Transform root, Vector3 blockTop, Color color,
            in BoosterCastPlacement placement)
        {
            float delay = 0f;
            if (casts != null && casts.TryPlayHammer(blockTop, placement.GetHammerRotation(), color,
                    placement.GetLaunch(), out float contact))
            {
                delay = contact;
            }

            return Shrink(root, delay);
        }

        public Tween PlayVacuum(BoosterCasts casts, Transform root, int source, in BoosterCastPlacement placement)
        {
            if (source != m_vacuumSource)
            {
                m_vacuumSource = source;
                m_vacuumPlaying = casts != null && casts.TryPlayVacuum(placement.GetVacuumLaunch(),
                    placement.GetFacingRotation(), out m_vacuumIntake, out m_vacuumStart, out m_vacuumSeconds);
            }

            if (!m_vacuumPlaying)
            {
                return Shrink(root);
            }

            // Both tweens use the same window and ease, so the root vanishes exactly at the intake.
            Tween.Scale(root, Vector3.zero, m_vacuumSeconds, Ease.InQuad, startDelay: m_vacuumStart);
            return Tween.Position(root, m_vacuumIntake, m_vacuumSeconds, Ease.InQuad, startDelay: m_vacuumStart);
        }

        public static Tween Shrink(Transform root, float delay = 0f)
        {
            return Tween.Scale(root, Vector3.zero, k_removalShrinkSeconds, Ease.InQuad, startDelay: delay);
        }
    }
}
