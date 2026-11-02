using UnityEngine;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>Flies a rigid rocket along a short arc toward a previously committed visual destination.</summary>
    public sealed class RocketEffectView : BoosterEffectView
    {
        /// <summary>The fraction of the motion at which the rocket's nose reaches its destination (B-20).</summary>
        public const float k_noseFraction = 0.90f;

        private const float k_launchFraction = 0.12f;

        [SerializeField] private Vector3 m_localDestination = new Vector3(0f, 0f, 3f);
        [SerializeField, Min(0f)] private float m_arcHeight = 0.55f;
        [SerializeField] private Vector3 m_tipPoint = new Vector3(0f, 2.29f, 0f);

        public bool Play(Vector3 worldDestination)
        {
            Vector3 destination = transform.InverseTransformPoint(worldDestination);
            if (!IsValidDestination(destination))
            {
                return false;
            }
            m_localDestination = destination;
            SetParticlePosition(worldDestination);
            return StartMotion();
        }

        /// <summary>Flies to worldDestination and shatters there in the removed cell's colour (template Cut Shatter, DEC-152).</summary>
        public bool Play(Vector3 worldDestination, Color cellColor)
        {
            SetParticleColor(cellColor);
            return Play(worldDestination);
        }

        // The shatter starts when the nose reaches the removed cell.
        protected override float ParticleStartFraction => k_noseFraction;

        protected override bool CanPlay() => IsValidDestination(m_localDestination);

        private static bool IsValidDestination(Vector3 destination)
        {
            float distance = destination.sqrMagnitude;
            return distance > 0.0001f && !float.IsInfinity(distance);
        }

        protected override void ApplyProgress(float progress)
        {
            float flight = Segment(progress, k_launchFraction, k_noseFraction);
            flight *= flight;
            Vector3 startTip = RestPosition + RestRotation * Vector3.Scale(m_tipPoint, RestScale);
            Vector3 offset = m_localDestination - startTip;
            Vector3 tip = startTip + offset * flight
                + Vector3.up * (4f * m_arcHeight * flight * (1f - flight));
            Vector3 tangent = offset + Vector3.up * (4f * m_arcHeight * (1f - 2f * flight));
            if (tangent.sqrMagnitude > 0.0001f)
            {
                Visual.localRotation = Quaternion.FromToRotation(RestRotation * Vector3.up, tangent.normalized) * RestRotation;
            }
            Visual.localScale = RestScale * Visibility(progress);
            Visual.localPosition = tip - Visual.localRotation * Vector3.Scale(m_tipPoint, Visual.localScale);
        }
    }
}
