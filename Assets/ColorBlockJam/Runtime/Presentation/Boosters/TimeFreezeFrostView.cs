using System;
using PrimeTween;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorBlockJam.Presentation.Boosters
{
    /// <summary>
    /// Draws the Time Freeze screen-edge frost (B-25): one quad just in front of the gameplay camera with the shared
    /// frost material, whose amount it writes through a MaterialPropertyBlock and fades in or out with one view-owned
    /// tween when a freeze starts or ends. UI Toolkit panels draw after the camera, so the HUD stays above the frost.
    /// </summary>
    /// <remarks>
    /// Owner: the Game scene container (AsSingle); it creates the quad on Home in <see cref="Prepare"/>, never during
    /// an attempt. Stop points: the board presenter's Suspend step pauses the fade (<see cref="SetPaused"/>), its
    /// Teardown step stops it and clears the frost (<see cref="Stop"/>), and <see cref="Dispose"/> destroys the quad
    /// and its mesh with the scene. Without a material it stays inert and creates nothing. The tween targets this
    /// view, so <c>Tween.GetTweensCount(view)</c> is 0 once it stopped.
    /// </remarks>
    public sealed class TimeFreezeFrostView : IDisposable
    {
        /// <summary>The float property the frost shader reads: 0 shows no frost, 1 full frost.</summary>
        public const string AmountProperty = "_FrostAmount";

        /// <summary>How long a full fade in or out takes, in Playing seconds.</summary>
        public const float k_fadeSeconds = 0.25f;

        // The quad sits this far beyond the near plane so it is never clipped.
        private const float k_nearOffset = 0.02f;

        // A little larger than the view, so no edge pixel is left uncovered.
        private const float k_overscan = 1.02f;

        // PrimeTween rejects shorter durations; a smaller change is written at once.
        private const float k_minFadeSeconds = 0.001f;

        private static readonly int s_amountId = Shader.PropertyToID(AmountProperty);

        // Cached once: the fade's value callback, which captures nothing.
        private static readonly Action<TimeFreezeFrostView, float> s_apply = (view, amount) => view.Apply(amount);

        private readonly Camera m_camera;
        private readonly Material m_material;
        private MaterialPropertyBlock m_block;
        private GameObject m_quad;
        private MeshRenderer m_renderer;
        private Mesh m_mesh;
        private Tween m_fade;
        private float m_amount;
        private bool m_frozen;
        private bool m_disposed;

        public TimeFreezeFrostView(TimeFreezeFrostContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            m_camera = context.Camera;
            m_material = context.Material;
        }

        /// <summary>True once the quad exists.</summary>
        public bool IsPrepared => m_quad != null;

        /// <summary>The frost amount last written, 0 to 1.</summary>
        public float Amount => m_amount;

        /// <summary>True between SetFrozen(true) and the next SetFrozen(false) or Stop.</summary>
        public bool IsFrozen => m_frozen;

        public bool IsFading => m_fade.isAlive;

        public bool IsPaused => m_fade.isAlive && m_fade.isPaused;

        /// <summary>True while the quad draws, that is while the amount is above 0.</summary>
        public bool IsVisible => m_renderer != null && m_renderer.enabled;

        /// <summary>The quad's GameObject once prepared, for tests and captures; null before.</summary>
        public GameObject Quad => m_quad;

        /// <summary>
        /// Creates the quad under the camera once, on Home, and fits it to the camera's view; a later call only refits.
        /// False, creating nothing, without a material or after Dispose.
        /// </summary>
        public bool Prepare()
        {
            if (m_disposed || m_material == null || m_camera == null)
            {
                return false;
            }

            if (m_quad == null)
            {
                CreateQuad();
            }

            Fit();
            return true;
        }

        /// <summary>
        /// Fades the frost in when a freeze starts and out when it ends; a repeated value does nothing. Runs in the view
        /// sync phase of the Playing tick and allocates nothing.
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            if (m_disposed || m_quad == null || frozen == m_frozen)
            {
                return;
            }

            m_frozen = frozen;
            if (frozen)
            {
                Fit();
            }

            StopFade();
            float target = frozen ? 1f : 0f;
            float seconds = k_fadeSeconds * Mathf.Abs(target - m_amount);
            if (seconds < k_minFadeSeconds)
            {
                Apply(target);
                return;
            }

            // Owner: this view (target). Stops: it ends by itself, or Stop (Teardown) and Dispose end it; SetPaused
            // (Suspend) holds it. Nothing else changes the frost while it runs.
            m_fade = Tween.Custom(this, m_amount, target, seconds, s_apply, Ease.Linear);
        }

        /// <summary>Suspend and Resume: holds or continues a running fade.</summary>
        public void SetPaused(bool paused)
        {
            if (m_fade.isAlive)
            {
                m_fade.isPaused = paused;
            }
        }

        /// <summary>Teardown: stops a running fade and clears the frost at once.</summary>
        public void Stop()
        {
            StopFade();
            m_frozen = false;
            if (!m_disposed && m_block != null)
            {
                Apply(0f);
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            StopFade();

            // A scene unload can destroy the camera, and the quad with it, before the container is disposed; the mesh
            // is an object of its own and is always destroyed here.
            UnityObjects.Destroy(m_quad);
            UnityObjects.Destroy(m_mesh);
            m_quad = null;
            m_renderer = null;
            m_mesh = null;
        }

        private void CreateQuad()
        {
            m_mesh = new Mesh { name = "Time Freeze frost quad" };
            m_mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(0.5f, -0.5f, 0f)
            };
            m_mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };

            // Clockwise as the camera sees the quad, so its front face points at the camera.
            m_mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m_mesh.RecalculateBounds();

            m_quad = new GameObject("Time Freeze frost");
            m_quad.layer = m_camera.gameObject.layer;
            m_quad.transform.SetParent(m_camera.transform, false);
            m_quad.AddComponent<MeshFilter>().sharedMesh = m_mesh;
            m_renderer = m_quad.AddComponent<MeshRenderer>();
            m_renderer.sharedMaterial = m_material;
            m_renderer.shadowCastingMode = ShadowCastingMode.Off;
            m_renderer.receiveShadows = false;
            m_renderer.lightProbeUsage = LightProbeUsage.Off;
            m_renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            m_renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            m_renderer.allowOcclusionWhenDynamic = false;
            m_block = new MaterialPropertyBlock();
            Apply(0f);
        }

        // Covers the camera's whole view one near-plane step in front of it; the aspect can change between levels.
        private void Fit()
        {
            float distance = m_camera.nearClipPlane + k_nearOffset;
            float height = m_camera.orthographic
                ? 2f * m_camera.orthographicSize
                : 2f * distance * Mathf.Tan(0.5f * m_camera.fieldOfView * Mathf.Deg2Rad);
            float width = height * m_camera.aspect;
            Transform quad = m_quad.transform;
            quad.localPosition = new Vector3(0f, 0f, distance);
            quad.localRotation = Quaternion.identity;
            quad.localScale = new Vector3(width * k_overscan, height * k_overscan, 1f);
        }

        // The only property-block write: the amount, and the renderer draws only while there is frost to show.
        private void Apply(float amount)
        {
            m_amount = amount;

            // A scene unload may already have destroyed the quad with the camera.
            if (m_renderer == null)
            {
                return;
            }

            m_block.SetFloat(s_amountId, amount);
            m_renderer.SetPropertyBlock(m_block);
            m_renderer.enabled = amount > 0f;
        }

        private void StopFade()
        {
            if (m_fade.isAlive)
            {
                m_fade.Stop();
            }

            m_fade = default;
        }
    }
}
