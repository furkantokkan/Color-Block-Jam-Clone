using UnityEngine;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>Plays a baked hose-deformation sprite strip with no runtime skinning or render camera.</summary>
    public sealed class VacuumEffectView : BoosterEffectView
    {
        /// <summary>The fraction of the motion from which the front-facing intake collects pieces (B-20).</summary>
        public const float k_intakeStartFraction = k_shownFromFraction;

        /// <summary>The fraction of the motion after which the intake turns away again (B-20).</summary>
        public const float k_intakeEndFraction = k_hiddenFromFraction;

        // The suction ring's authored local turn in VacuumEffect.prefab: the template shape lies in its local XZ plane
        // (shape rotation X -90), and this puts it in the root's camera-facing XY plane, so on screen the lines close
        // in on the intake instead of lying edge-on to the camera. The ring's radius and board-side arc are authored
        // on the prefab's Magnet Effect instance.
        private static readonly Quaternion s_ringFacing = Quaternion.Euler(90f, 0f, 0f);

        [SerializeField] private SpriteRenderer m_spriteRenderer;
        [SerializeField] private Sprite[] m_frames;
        [SerializeField] private Transform m_intake;

        // The baked 192-pixel frames (75 pixels per unit, centre pivot) draw the canister's bottom 79 pixels below the
        // centre, 17 pixels above the frame's edge.
        [Tooltip("How far the drawn canister's bottom sits below the root, in visual units; measured on the baked frames.")]
        [SerializeField, Min(0f)] private float m_baseDepth = 1.05f;

        private int m_frameIndex = -1;
        private bool m_isIntakeReady;

        public Vector3 IntakePosition => m_intake.position;
        public bool IsIntakeReady => m_isIntakeReady;

        /// <summary>
        /// The distance from the root down to the drawn canister's bottom at the visual's rest scale. A caster raises
        /// the root by it along the root's up axis, so the canister stands on its launch point.
        /// </summary>
        public float BaseDepth => m_baseDepth * Mathf.Abs(RestScale.y);

        /// <summary>
        /// Where the intake is while the vacuum is fully shown (the intake window): the visual at its rest pose,
        /// which ApplyProgress changes only in scale. A collector reads it when the motion starts, while the visual
        /// is still scaled to zero, so it cannot read IntakePosition then.
        /// </summary>
        public Vector3 ReadyIntakePosition
        {
            get
            {
                Transform visual = Visual;
                if (visual == null || m_intake == null || m_intake.parent != visual || visual.parent == null)
                {
                    return m_intake != null ? m_intake.position : transform.position;
                }

                return visual.parent.TransformPoint(RestPosition + RestRotation * Vector3.Scale(RestScale, m_intake.localPosition));
            }
        }

        // Set the root's camera-facing rotation once at placement; no per-frame camera lookup.
        public bool Play()
        {
            if (!StartMotion())
            {
                return false;
            }

            // The suction lines converge on the intake's shown position (template Magnet Effect, DEC-152). The prefab
            // authors the same pose; placing it here keeps it on the intake if the Intake point is moved.
            SetParticlePose(ReadyIntakePosition, transform.rotation * s_ringFacing);
            return true;
        }

        // The suction particle runs only while the intake faces the board, as the template's magnet does.
        protected override float ParticleStartFraction => k_intakeStartFraction;
        protected override float ParticleStopFraction => k_intakeEndFraction;

        protected override bool CanPlay()
        {
            return m_spriteRenderer != null && m_intake != null && m_frames != null && m_frames.Length > 0;
        }

        protected override void ApplyProgress(float progress)
        {
            m_isIntakeReady = progress >= k_intakeStartFraction && progress <= k_intakeEndFraction;
            int frame = Mathf.Min((int)(progress * m_frames.Length), m_frames.Length - 1);
            if (frame != m_frameIndex)
            {
                m_spriteRenderer.sprite = m_frames[frame];
                m_frameIndex = frame;
            }
            Visual.localScale = RestScale * Visibility(progress);
        }

        protected override void ResetVisual()
        {
            m_isIntakeReady = false;
            m_frameIndex = -1;
            if (m_spriteRenderer != null && m_frames != null && m_frames.Length > 0)
            {
                m_spriteRenderer.sprite = m_frames[0];
            }
        }
    }
}
