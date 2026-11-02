using System;
using ColorBlockJam.Presentation;
using PrimeTween;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// Plays one particle effect prefab inside a UI Toolkit panel: a small offscreen stage renders the effect with its
    /// own orthographic camera into a render texture, and a full-screen element of the panel shows that texture. In
    /// Unity 2022.3 every UI Toolkit panel draws after all cameras, so an effect placed in the world would stay hidden
    /// behind a full-screen popup (DEC-173).
    /// </summary>
    /// <remarks>
    /// Owner: the view host that creates it (the Level Complete host), which calls <see cref="Prepare"/> on Home,
    /// <see cref="Play"/> when its screen opens, <see cref="Stop"/> when it closes and <see cref="Dispose"/> with its
    /// scene. Stop points: a show ends by itself after <see cref="ShowSeconds"/> through one PrimeTween delay that
    /// targets this object, and Stop and Dispose end it at once. Between shows the camera is off and no render texture
    /// exists, so nothing renders. The camera sees only the stage's layer, and the stage sits far below the board, out of
    /// the gameplay camera's view. The effect keeps its own particle settings; only Play On Awake and Looping are turned
    /// off, so one show is one burst.
    /// </remarks>
    public sealed class UiParticleOverlay : IDisposable
    {
        // The built-in UI layer holds no renderer in these scenes, so the stage camera draws only the effect.
        private const string k_layerName = "UI";

        private const float k_cameraDistance = 10f;

        // Far below the board: the gameplay camera, whose far plane is 50 units (DEC-118), never sees the stage.
        private static readonly Vector3 s_stagePosition = new Vector3(0f, -1000f, 0f);

        // Cached once: the show's end callback, which captures nothing.
        private static readonly Action<UiParticleOverlay> s_endShow = overlay => overlay.Stop();

        private readonly ParticleSystem m_prefab;
        private readonly Transform m_parent;
        private readonly float m_halfHeight;
        private readonly EventCallback<GeometryChangedEvent> m_onSurfaceLaidOut;
        private GameObject m_stage;
        private Camera m_camera;
        private ParticleSystem m_effect;
        private RenderTexture m_target;
        private VisualElement m_surface;
        private VisualElement m_anchor;
        private Tween m_show;
        private bool m_waitingForLayout;
        private bool m_disposed;

        /// <param name="prefab">The effect to play, or null: the overlay then stays inert and creates nothing.</param>
        /// <param name="parent">The scene object the stage is created under, so it unloads with that scene.</param>
        /// <param name="viewHeight">
        /// How many world units of the effect fill the surface's height: a smaller view shows the effect larger.
        /// </param>
        public UiParticleOverlay(ParticleSystem prefab, Transform parent, float viewHeight)
        {
            if (viewHeight <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(viewHeight), viewHeight, "The view height must be positive.");
            }

            m_prefab = prefab;
            m_parent = parent;
            m_halfHeight = 0.5f * viewHeight;
            m_onSurfaceLaidOut = OnSurfaceLaidOut;
        }

        /// <summary>True once the stage exists.</summary>
        public bool IsPrepared => m_stage != null;

        /// <summary>True from Play until the show ends, Stop or Dispose.</summary>
        public bool IsPlaying => m_surface != null;

        /// <summary>
        /// How long one show lasts: the latest moment any particle of the effect can still live, from each system's
        /// start delay, last emission and longest particle life. Known after <see cref="Prepare"/>.
        /// </summary>
        public float ShowSeconds { get; private set; }

        /// <summary>The stage camera once prepared, for tests and captures; null before.</summary>
        public Camera Camera => m_camera;

        /// <summary>The render texture of the running show, for tests and captures; null between shows.</summary>
        public RenderTexture Target => m_target;

        /// <summary>
        /// Creates the stage once, on Home: the camera, switched off, and one instance of the effect in front of it.
        /// A later call does nothing. False, creating nothing, without a prefab or after Dispose.
        /// </summary>
        public bool Prepare()
        {
            if (m_disposed || m_prefab == null)
            {
                return false;
            }

            if (m_stage != null)
            {
                return true;
            }

            int layer = LayerMask.NameToLayer(k_layerName);

            // Created inactive, so the effect's Play On Awake cannot start it before it is switched off below.
            m_stage = new GameObject("UI particle overlay");
            m_stage.SetActive(false);
            if (m_parent != null)
            {
                m_stage.transform.SetParent(m_parent, false);
            }

            m_stage.transform.position = s_stagePosition;
            m_stage.layer = layer;
            m_camera = CreateCamera(m_stage.transform, layer, m_halfHeight);

            m_effect = Object.Instantiate(m_prefab, m_stage.transform, false);
            Transform[] parts = m_effect.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < parts.Length; index++)
            {
                parts[index].gameObject.layer = layer;
            }

            ParticleSystem[] systems = m_effect.GetComponentsInChildren<ParticleSystem>(true);
            float showSeconds = 0f;
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem.MainModule main = systems[index].main;
                main.playOnAwake = false;
                main.loop = false;
                showSeconds = Mathf.Max(showSeconds, GetEndSeconds(systems[index]));
            }

            ShowSeconds = showSeconds;
            m_stage.SetActive(true);
            return true;
        }

        /// <summary>
        /// Shows the effect in <paramref name="surface"/>, a full-screen element, bursting at the centre of
        /// <paramref name="anchor"/> (the surface's centre without one). A running show restarts. The effect starts
        /// once the surface has its layout, because only then is the anchor's position known. False without a
        /// prepared stage.
        /// </summary>
        public bool Play(VisualElement surface, VisualElement anchor)
        {
            if (m_disposed || m_effect == null || surface == null)
            {
                return false;
            }

            Stop();
            m_surface = surface;
            m_anchor = anchor;

            // One texture for this show only, the size of the screen, so its pixels map one to one onto the panel.
            m_target = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 0,
                RenderTextureFormat.ARGB32)
            {
                name = "UI particle overlay",
                useMipMap = false,
                antiAliasing = 1
            };
            m_target.Create();
            Clear(m_target);
            m_camera.targetTexture = m_target;
            m_camera.aspect = (float)m_target.width / m_target.height;

            surface.style.backgroundImage = Background.FromRenderTexture(m_target);
            surface.style.display = DisplayStyle.Flex;
            m_waitingForLayout = true;
            surface.RegisterCallback(m_onSurfaceLaidOut);
            return true;
        }

        /// <summary>Ends a show at once: the particles clear, the camera turns off and its texture is released.</summary>
        public void Stop()
        {
            if (m_show.isAlive)
            {
                m_show.Stop();
            }

            if (m_surface != null)
            {
                if (m_waitingForLayout)
                {
                    m_surface.UnregisterCallback(m_onSurfaceLaidOut);
                }

                m_surface.style.display = DisplayStyle.None;
                m_surface.style.backgroundImage = StyleKeyword.Null;
                m_surface = null;
                m_anchor = null;
            }

            m_waitingForLayout = false;

            // A scene unload may already have destroyed the stage.
            if (m_effect != null)
            {
                m_effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (m_camera != null)
            {
                m_camera.enabled = false;
                m_camera.targetTexture = null;
            }

            if (m_target != null)
            {
                m_target.Release();
                UnityObjects.Destroy(m_target);
                m_target = null;
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            Stop();
            m_disposed = true;
            UnityObjects.Destroy(m_stage);
            m_stage = null;
            m_camera = null;
            m_effect = null;
        }

        // The latest moment a particle of one system can still live: its start delay, its last emission and its longest
        // particle life. With Looping off, emission ends with the system's duration at the latest.
        private static float GetEndSeconds(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.EmissionModule emission = system.emission;
            float lastEmission = 0f;
            if (emission.enabled)
            {
                if (emission.rateOverTimeMultiplier > 0f || emission.rateOverDistanceMultiplier > 0f)
                {
                    lastEmission = main.duration;
                }

                // Runs once per system on Home.
                ParticleSystem.Burst[] bursts = new ParticleSystem.Burst[emission.burstCount];
                emission.GetBursts(bursts);
                for (int index = 0; index < bursts.Length; index++)
                {
                    ParticleSystem.Burst burst = bursts[index];
                    float last = burst.time + burst.repeatInterval * Mathf.Max(0, burst.cycleCount - 1);
                    lastEmission = Mathf.Max(lastEmission, Mathf.Min(last, main.duration));
                }
            }

            return GetMax(main.startDelay) + lastEmission + GetMax(main.startLifetime);
        }

        // The largest value a MinMaxCurve can give: its upper constant, or its curve multiplier for curve modes.
        private static float GetMax(ParticleSystem.MinMaxCurve curve)
        {
            return curve.mode == ParticleSystemCurveMode.Curve || curve.mode == ParticleSystemCurveMode.TwoCurves
                ? curve.curveMultiplier
                : curve.constantMax;
        }

        private static Camera CreateCamera(Transform stage, int layer, float halfHeight)
        {
            GameObject cameraObject = new GameObject("Camera");
            cameraObject.layer = layer;
            cameraObject.transform.SetParent(stage, false);
            cameraObject.transform.localPosition = new Vector3(0f, 0f, -k_cameraDistance);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = halfHeight;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.cullingMask = 1 << layer;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 2f * k_cameraDistance;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.useOcclusionCulling = false;
            return camera;
        }

        // A new render texture holds undefined pixels until something draws into it; the panel must never show them.
        private static void Clear(RenderTexture target)
        {
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            GL.Clear(false, true, Color.clear);
            RenderTexture.active = active;
        }

        // The surface now has its layout: match the camera to the surface's shape, place the burst at the anchor, then
        // start the camera, the effect and the show's end.
        private void OnSurfaceLaidOut(GeometryChangedEvent evt)
        {
            m_surface.UnregisterCallback(m_onSurfaceLaidOut);
            m_waitingForLayout = false;
            PlaceEffect(m_surface.worldBound);
            m_camera.enabled = true;
            m_effect.Play(true);

            // Owner: this overlay (target). Stops: it ends by itself, or Stop and Dispose end it.
            m_show = Tween.Delay(this, ShowSeconds, s_endShow);
        }

        // The texture is stretched over the surface, so the camera takes the surface's aspect: then the effect keeps its
        // proportions even when the panel draws at another size than the screen (a panel with its own target texture).
        // The anchor's centre maps from panel space onto the stage, whose view the surface fills.
        private void PlaceEffect(Rect surface)
        {
            float x = 0.5f;
            float y = 0.5f;
            if (surface.width > 0f && surface.height > 0f)
            {
                m_camera.aspect = surface.width / surface.height;
                Vector2 point = m_anchor != null ? m_anchor.worldBound.center : surface.center;
                if (!float.IsNaN(point.x) && !float.IsNaN(point.y))
                {
                    x = (point.x - surface.xMin) / surface.width;
                    y = (point.y - surface.yMin) / surface.height;
                }
            }

            float halfWidth = m_halfHeight * m_camera.aspect;
            m_effect.transform.localPosition = new Vector3((x - 0.5f) * 2f * halfWidth, (0.5f - y) * 2f * m_halfHeight, 0f);
        }

    }
}
