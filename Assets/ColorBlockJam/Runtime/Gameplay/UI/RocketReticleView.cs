using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Picking;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Gameplay.UI
{
    /// <summary>
    /// The pooled Rocket crosshair layer in UI Toolkit (B-24): every reticle stacks the supplied black ic_target.png,
    /// slightly larger, under a white copy of the same alpha, so it reads on white blocks too. UI Toolkit can only
    /// multiply a tint, so the white copy is derived once, on Home, from the readable import. All reticles are Ignore,
    /// so they never veto a board press.
    /// </summary>
    /// <remarks>
    /// Owner: the gameplay HUD. Reticles are created only in <see cref="Reserve"/> on Home and hidden with display
    /// none; <see cref="Dispose"/> unregisters the geometry callback, removes them and destroys the white copy.
    /// Without a readable source texture only the black outline (from GameplayHud.uss) shows.
    /// </remarks>
    public sealed class RocketReticleView : IReticleView, IDisposable
    {
        public const string ReticleClass = "reticle";
        public const string OutlineClass = "reticle__outline";
        public const string MarkClass = "reticle__mark";

        private readonly VisualElement m_layer;
        private readonly List<VisualElement> m_reticles = new List<VisualElement>();
        private readonly EventCallback<GeometryChangedEvent> m_onGeometryChanged;
        private Texture2D m_white;
        private int m_shown;
        private bool m_disposed;

        /// <param name="layer">The full-screen target layer the reticles go into.</param>
        /// <param name="source">The supplied ic_target.png with Read/Write on, or null.</param>
        public RocketReticleView(VisualElement layer, Texture2D source)
        {
            m_layer = layer ?? throw new ArgumentNullException(nameof(layer));
            m_layer.pickingMode = PickingMode.Ignore;
            m_white = CreateWhiteCopy(source);
            m_onGeometryChanged = OnGeometryChanged;
            m_layer.RegisterCallback(m_onGeometryChanged);
        }

        public event Action GeometryChanged;

        public int Capacity => m_reticles.Count;

        /// <summary>The number of reticles shown now.</summary>
        public int ShownCount => m_shown;

        /// <summary>True when the white copy exists; false when the source was missing or unreadable.</summary>
        public bool HasWhiteCopy => m_white != null;

        /// <summary>The pooled reticle at index, for tests.</summary>
        public VisualElement GetReticle(int index) => m_reticles[index];

        public void Reserve(int capacity)
        {
            if (m_disposed)
            {
                throw new ObjectDisposedException(nameof(RocketReticleView));
            }

            while (m_reticles.Count < capacity)
            {
                VisualElement reticle = new VisualElement { pickingMode = PickingMode.Ignore };
                reticle.AddToClassList(ReticleClass);
                reticle.style.display = DisplayStyle.None;
                // Every inline property Show writes exists from here on, so the first Rocket targeting during Playing
                // replaces values instead of growing the reticle's inline style list (B-24, as IceCounterLabels does).
                reticle.style.left = 0f;
                reticle.style.top = 0f;
                reticle.style.width = 0f;
                reticle.style.height = 0f;

                VisualElement outline = new VisualElement { pickingMode = PickingMode.Ignore };
                outline.AddToClassList(OutlineClass);
                reticle.Add(outline);

                VisualElement mark = new VisualElement { pickingMode = PickingMode.Ignore };
                mark.AddToClassList(MarkClass);
                if (m_white != null)
                {
                    mark.style.backgroundImage = new StyleBackground(m_white);
                }
                else
                {
                    mark.style.display = DisplayStyle.None;
                }

                reticle.Add(mark);
                m_layer.Add(reticle);
                m_reticles.Add(reticle);
            }
        }

        public void Show(int index, ScreenPoint center, float size)
        {
            if (index < 0 || index >= m_reticles.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Reserve the reticle pool on Home.");
            }

            VisualElement reticle = m_reticles[index];
            IPanel panel = m_layer.panel;
            if (panel == null || center.IsOffscreen || !(size > 0f))
            {
                Hide(reticle);
                if (index >= m_shown)
                {
                    m_shown = index + 1;
                }

                return;
            }

            // Screen y points up and panel y points down, so flip before converting (UiToolkitPointerFilter does too).
            float screenY = Screen.height - center.Y;
            Vector2 panelCenter = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.X, screenY));
            Vector2 panelEdge = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(center.X + size, screenY));
            Vector2 local = m_layer.WorldToLocal(panelCenter);
            float width = Mathf.Abs(panelEdge.x - panelCenter.x);
            reticle.style.left = local.x - width * 0.5f;
            reticle.style.top = local.y - width * 0.5f;
            reticle.style.width = width;
            reticle.style.height = width;
            reticle.style.display = DisplayStyle.Flex;
            if (index >= m_shown)
            {
                m_shown = index + 1;
            }
        }

        public void HideFrom(int index)
        {
            int from = index < 0 ? 0 : index;
            for (int i = from; i < m_shown && i < m_reticles.Count; i++)
            {
                Hide(m_reticles[i]);
            }

            if (from < m_shown)
            {
                m_shown = from;
            }
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_layer.UnregisterCallback(m_onGeometryChanged);
            for (int i = 0; i < m_reticles.Count; i++)
            {
                m_reticles[i].RemoveFromHierarchy();
            }

            m_reticles.Clear();
            m_shown = 0;
            GeometryChanged = null;
            if (m_white != null)
            {
                if (Application.isPlaying)
                {
                    Object.Destroy(m_white);
                }
                else
                {
                    Object.DestroyImmediate(m_white);
                }

                m_white = null;
            }
        }

        private static void Hide(VisualElement reticle)
        {
            reticle.style.display = DisplayStyle.None;
        }

        // The white mark: every pixel white with the source's alpha. Needs the importer's Read/Write; without it, or
        // for a format the CPU cannot read, it logs one warning and the black outline shows alone.
        private static Texture2D CreateWhiteCopy(Texture2D source)
        {
            if (source == null)
            {
                return null;
            }

            if (!source.isReadable)
            {
                Debug.LogWarning($"Rocket reticles: {source.name} is not readable (Read/Write off); only the black "
                                 + "outline shows.");
                return null;
            }

            Color32[] pixels;
            try
            {
                pixels = source.GetPixels32();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Rocket reticles: {source.name} cannot be read ({exception.Message}); only the black "
                                 + "outline shows.");
                return null;
            }

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, pixels[i].a);
            }

            Texture2D white = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false)
            {
                name = source.name + " (white)",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            white.SetPixels32(pixels);
            white.Apply(false, true);
            return white;
        }

        private void OnGeometryChanged(GeometryChangedEvent geometryChanged)
        {
            GeometryChanged?.Invoke();
        }
    }
}
