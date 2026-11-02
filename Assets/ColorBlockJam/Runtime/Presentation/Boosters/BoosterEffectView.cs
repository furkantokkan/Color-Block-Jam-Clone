using PrimeTween;
using UnityEngine;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>
    /// Owns one bounded visual tween and an optional Shuriken show; the session calls Play, Suspend, Resume and Stop.
    /// </summary>
    public abstract class BoosterEffectView : MonoBehaviour
    {
        /// <summary>The fraction of the motion by which the visual has fully shown (Visibility).</summary>
        protected const float k_shownFromFraction = 0.13f;

        /// <summary>The fraction of the motion from which the visual starts hiding again (Visibility).</summary>
        protected const float k_hiddenFromFraction = 0.86f;

        private const float k_minDurationSeconds = 0.1f;

        // The Won Finish bound (DEC-93): a cast's motion never outlasts the 0.8 s the session may hold for it.
        private const float k_maxDurationSeconds = 0.8f;

        [SerializeField] private Transform m_visual;
        [SerializeField, Range(k_minDurationSeconds, k_maxDurationSeconds)] private float m_duration = 0.75f;
        [Tooltip("Optional: the CBJ Template particle this cast plays (DEC-152). It starts at the view's particle " +
                 "fraction of the motion and is cleared when its show ends or the view stops.")]
        [SerializeField] private ParticleSystem m_particles;
        [Tooltip("Seconds the particle show lasts after it starts; its tween owns that lifetime (DEC-152).")]
        [SerializeField, Range(0.1f, 2f)] private float m_particleSeconds = 0.8f;

        private Tween m_tween;
        private Tween m_particleTween;
        private Transform m_particleTransform;
        private bool m_particlesStarted;
        private bool m_particlesEmitting;
        private bool m_particlesPaused;
        private bool m_hasRestPose;
        private Vector3 m_restPosition;
        private Quaternion m_restRotation;
        private Vector3 m_restScale;

        /// <summary>True while the motion runs; the particle show may outlast it (IsBusy).</summary>
        public bool IsPlaying => m_tween.isAlive;
        public bool IsPaused => (m_tween.isAlive && m_tween.isPaused) || (m_particleTween.isAlive && m_particleTween.isPaused);

        /// <summary>True while the motion or its particle show runs; the cast pool returns the view only after both.</summary>
        public bool IsBusy => m_tween.isAlive || m_particleTween.isAlive;
        public float Duration => Mathf.Clamp(m_duration, k_minDurationSeconds, k_maxDurationSeconds);
        public bool HasParticles => m_particles != null;
        public bool AreParticlesPlaying => m_particleTween.isAlive;
        public float ParticleSeconds => Mathf.Clamp(m_particleSeconds, 0.1f, 2f);

        /// <summary>Seconds into the motion at which the particle show starts.</summary>
        public float ParticleStartSeconds => Duration * ParticleStartFraction;

        /// <summary>
        /// The seconds the running motion still needs; 0 when idle. Its owner adds it to the Won Finish (DEC-93). A
        /// particle show that outlasts the motion is not part of the Finish: Teardown clears it.
        /// </summary>
        public float RemainingSeconds => m_tween.isAlive ? Mathf.Max(0f, m_tween.durationTotal - m_tween.elapsedTimeTotal) : 0f;

        protected Transform Visual => m_visual;
        protected Vector3 RestPosition => m_restPosition;
        protected Quaternion RestRotation => m_restRotation;
        protected Vector3 RestScale => m_restScale;

        /// <summary>The fraction of the motion at which the particle show starts.</summary>
        protected virtual float ParticleStartFraction => 1f;

        /// <summary>The fraction at which a looping particle stops emitting; 1 leaves a burst to end on its own.</summary>
        protected virtual float ParticleStopFraction => 1f;

        private void Awake()
        {
            CaptureRestPose();
            // Read once here: the first managed access to a component creates its C# wrapper (AC-16).
            m_particleTransform = m_particles != null ? m_particles.transform : null;
            Stop();
        }

        private void OnDisable()
        {
            Stop();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                Suspend();
            }
        }

        public void Suspend()
        {
            if (m_tween.isAlive)
            {
                m_tween.isPaused = true;
            }

            if (m_particleTween.isAlive)
            {
                m_particleTween.isPaused = true;
                m_particles.Pause(true);
                m_particlesPaused = true;
            }
        }

        public void Resume()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (m_tween.isAlive)
            {
                m_tween.isPaused = false;
            }

            if (m_particleTween.isAlive && m_particlesPaused)
            {
                m_particleTween.isPaused = false;
                m_particlesPaused = false;
                m_particles.Play(true);
                if (!m_particlesEmitting)
                {
                    // Play resumes a paused system with emission on; a show past its stop fraction stays off.
                    m_particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }

        public void Stop()
        {
            EndMotion();
            StopParticles();
        }

        /// <summary>Sets the particle's start colour, for example the hit block's tint; no material instance.</summary>
        public void SetParticleColor(Color color)
        {
            if (m_particles == null)
            {
                return;
            }

            ParticleSystem.MainModule main = m_particles.main;
            main.startColor = color;
        }

        /// <summary>Places the particle in world space before Play, for example at a Rocket's target cell.</summary>
        protected void SetParticlePosition(Vector3 worldPosition)
        {
            if (m_particleTransform != null)
            {
                m_particleTransform.position = worldPosition;
            }
        }

        /// <summary>Places and turns the particle in world space before Play, for example around a Vacuum's intake.</summary>
        protected void SetParticlePose(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (m_particleTransform != null)
            {
                m_particleTransform.SetPositionAndRotation(worldPosition, worldRotation);
            }
        }

        protected bool StartMotion()
        {
            if (!isActiveAndEnabled || m_visual == null || !CanPlay())
            {
                return false;
            }
            CaptureRestPose();
            EndMotion();
            StopParticles();
            m_visual.gameObject.SetActive(true);
            ApplyProgress(0f);
            // Targeted, non-capturing callbacks allocate no closure per activation.
            m_tween = Tween.Custom(this, 0f, 1f, Duration,
                    static (view, progress) => view.Advance(progress), Ease.Linear)
                .OnComplete(this, static view => view.EndMotion());
            return true;
        }

        protected virtual bool CanPlay() => true;
        protected virtual void ResetVisual() { }
        protected abstract void ApplyProgress(float progress);

        protected static float Segment(float progress, float start, float end)
        {
            return Mathf.Clamp01((progress - start) / (end - start));
        }

        protected static float Smooth(float progress)
        {
            return progress * progress * (3f - 2f * progress);
        }

        protected static float Visibility(float progress)
        {
            return Mathf.Min(Smooth(Segment(progress, 0f, k_shownFromFraction)),
                1f - Smooth(Segment(progress, k_hiddenFromFraction, 1f)));
        }

        private void Advance(float progress)
        {
            ApplyProgress(progress);
            if (m_particles == null)
            {
                return;
            }

            if (!m_particlesStarted && progress >= ParticleStartFraction)
            {
                StartParticles();
            }

            if (m_particlesEmitting && ParticleStopFraction < 1f && progress >= ParticleStopFraction)
            {
                m_particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                m_particlesEmitting = false;
            }
        }

        // Owner: this view; target: the particle system, so the view's own tween count stays its motion. Stops: its
        // tween's end, Stop, OnDisable.
        private void StartParticles()
        {
            m_particlesStarted = true;
            m_particlesEmitting = true;
            m_particles.gameObject.SetActive(true);
            m_particles.Play(true);
            m_particleTween = Tween.Delay(m_particles, ParticleSeconds)
                .OnComplete(this, static view => view.StopParticles());
        }

        private void StopParticles()
        {
            if (m_particleTween.isAlive)
            {
                m_particleTween.Stop();
            }
            m_particleTween = default;
            m_particlesStarted = false;
            m_particlesEmitting = false;
            m_particlesPaused = false;
            if (m_particles != null)
            {
                m_particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                m_particles.gameObject.SetActive(false);
            }
        }

        // Ends the motion only: a particle show that already started keeps running until its own tween ends.
        private void EndMotion()
        {
            if (m_tween.isAlive)
            {
                m_tween.Stop();
            }
            m_tween = default;
            if (m_hasRestPose && m_visual != null)
            {
                m_visual.SetLocalPositionAndRotation(m_restPosition, m_restRotation);
                m_visual.localScale = m_restScale;
                ResetVisual();
                m_visual.gameObject.SetActive(false);
            }
        }

        private void CaptureRestPose()
        {
            if (m_hasRestPose || m_visual == null)
            {
                return;
            }
            m_restPosition = m_visual.localPosition;
            m_restRotation = m_visual.localRotation;
            m_restScale = m_visual.localScale;
            m_hasRestPose = true;
        }

#if UNITY_EDITOR
        // Preview sampling changes only the visual pose and starts no runtime work.
        public void SampleForPreview(float progress)
        {
            CaptureRestPose();
            if (m_visual == null || !CanPlay())
            {
                return;
            }
            m_visual.gameObject.SetActive(true);
            ApplyProgress(Mathf.Clamp01(progress));
        }
#endif
    }
}
