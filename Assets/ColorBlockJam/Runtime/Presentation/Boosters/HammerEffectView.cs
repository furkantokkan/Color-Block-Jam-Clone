using UnityEngine;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>
    /// Shows the mallet flying in from its launch point during the anticipation, then a fast strike and recoil, without
    /// applying gameplay damage. The mallet stays upright on screen: it swings in its root's upright plane along the view
    /// (the presenter turns the root to face the camera), head above the handle, and its face lands on the root from
    /// above, as in reference 16.
    /// </summary>
    public sealed class HammerEffectView : BoosterEffectView
    {
        /// <summary>
        /// The fraction of the motion at which the striking face touches the root: the board presenter holds a
        /// removed block until then (B-20).
        /// </summary>
        public const float k_strikeFraction = 0.74f;

        private const float k_strikeStartFraction = 0.48f;
        private const float k_recoilEndFraction = 0.90f;

        // The mallet flies in (0 to 0.36) in its strike pose, then raises its head (0.30 to the strike start). In the
        // strike pose the Game camera (pitch 75, DEC-103) sees the head's cap face-on above a full-length handle, the
        // look of reference 16; a raised head would show its barrel side instead.
        private const float k_travelEndFraction = 0.36f;
        private const float k_raiseStartFraction = 0.30f;

        // How far the mallet bounces back up after the strike.
        private const float k_recoilAngle = 14f;

        [Tooltip("The face centre on the head's +X cap, in the model's space; the grip is the model's origin.")]
        [SerializeField] private Vector3 m_strikePoint = new Vector3(1f, 1.61f, -0.55f);
        [SerializeField] private float m_lift = 0.45f;
        [Tooltip("The handle's tilt above the ground plane at the top of the wind-up, in degrees; the strike lays it flat.")]
        [SerializeField] private float m_raiseAngle = 45f;

        // Where the mallet's origin starts, in its root's space; it reaches its strike pose with the wind-up.
        private Vector3 m_localLaunch;
        private bool m_hasLaunch;

        /// <summary>
        /// Plays the strike with its hit burst in hitColor (template Hammer Hit, DEC-152; BoosterCasts always
        /// passes the struck block's colour). With worldLaunch, the mallet flies in from there (above the booster
        /// bar) before its wind-up, so it rises from the button that cast it; without it, the effect root's own
        /// committed hit point is the start pose. The strike time is unchanged either way.
        /// </summary>
        public bool Play(Color hitColor = default, Vector3? worldLaunch = null)
        {
            m_hasLaunch = worldLaunch.HasValue;
            if (m_hasLaunch) m_localLaunch = transform.InverseTransformPoint(worldLaunch.Value);
            SetParticleColor(hitColor);
            return StartMotion();
        }

        // The hit burst starts when the face touches the block, as the template's PUHammerBehavior does.
        protected override float ParticleStartFraction => k_strikeFraction;

        protected override void ApplyProgress(float progress)
        {
            float travel = Smooth(Segment(progress, 0f, k_travelEndFraction));
            float windup = Smooth(Segment(progress, k_raiseStartFraction, k_strikeStartFraction));
            float strike = Segment(progress, k_strikeStartFraction, k_strikeFraction);
            strike *= strike;
            float recoil = Smooth(Segment(progress, k_strikeFraction, k_recoilEndFraction));
            float angle = Mathf.Lerp(m_raiseAngle * windup, 0f, strike) + recoil * k_recoilAngle;

            // The swing turns about the root's right axis, so the handle never leaves the upright plane along the view.
            Quaternion impact = RestRotation * GetStrikeFacing();
            Quaternion rotation = RestRotation * Quaternion.Euler(-angle, 0f, 0f) * GetStrikeFacing();
            Vector3 contactOffset = impact * Vector3.Scale(m_strikePoint, RestScale);
            Vector3 pose = RestPosition - contactOffset
                + Vector3.up * (m_lift * windup * (1f - strike) + recoil * 0.08f);
            Visual.localRotation = rotation;
            Visual.localPosition = m_hasLaunch ? Vector3.LerpUnclamped(m_localLaunch, pose, travel) : pose;
            Visual.localScale = RestScale * Visibility(progress);
        }

        // The model's pose at the strike: the head's +X cap faces down onto the root and the handle, from the grip to the
        // head's centre, lies flat along the root's forward axis, away from the camera, so on screen the head is above
        // the handle. Quaternion math on structs only, so the tick allocates nothing.
        private Quaternion GetStrikeFacing()
        {
            Vector3 handle = new Vector3(0f, m_strikePoint.y, m_strikePoint.z);
            if (handle.sqrMagnitude < 1e-6f)
            {
                handle = Vector3.up;
            }

            // LookRotation(forward, up) maps Z to forward and Y to up, and X to their cross product: here the model's
            // (cap axis, handle, cap x handle) basis and the target (down, forward, left) basis.
            Quaternion model = Quaternion.LookRotation(Vector3.Cross(Vector3.right, handle), handle);
            Quaternion strike = Quaternion.LookRotation(Vector3.left, Vector3.forward);
            return strike * Quaternion.Inverse(model);
        }
    }
}
