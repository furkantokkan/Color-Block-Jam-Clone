using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// Pads one UI Toolkit element on the chosen edges so its content stays inside the screen's safe area,
    /// recalculating only when the panel root's geometry changes (resolution or orientation), never per frame
    /// (Hub-And-Player-Data-Design 6.5, AC-1.4a, DEC-42).
    /// </summary>
    /// <remarks>
    /// Owner: the screen that creates it (for Hub, <c>HubScreen</c>). Stops: <see cref="Dispose"/>, which the
    /// owner calls from <c>OnDisable</c> and which unregisters the geometry callback. The safe area comes from
    /// <see cref="Screen.safeArea"/> unless the owner pins one with <see cref="SetSource"/>, which a panel that
    /// renders into a texture (or a simulated notch) needs because the screen then describes another surface.
    /// </remarks>
    public sealed class SafeAreaPadding : IDisposable
    {
        private readonly VisualElement m_root;
        private readonly VisualElement m_target;
        private readonly SafeAreaEdges m_edges;
        private readonly EventCallback<GeometryChangedEvent> m_onGeometryChanged;
        private Rect m_pinnedSafeArea;
        private Vector2 m_pinnedScreenSize;
        private bool m_hasPinnedSource;
        private SafeAreaInsets m_applied;
        private bool m_hasApplied;
        private bool m_disposed;

        /// <param name="root">An element whose layout size equals the panel size (a full-screen element).</param>
        /// <param name="target">The element that receives the padding.</param>
        /// <param name="edges">The edges this target pads; the other edges keep padding 0.</param>
        public SafeAreaPadding(VisualElement root, VisualElement target, SafeAreaEdges edges)
        {
            m_root = root ?? throw new ArgumentNullException(nameof(root));
            m_target = target ?? throw new ArgumentNullException(nameof(target));
            m_edges = edges;
            m_onGeometryChanged = OnGeometryChanged;
            m_root.RegisterCallback(m_onGeometryChanged);
        }

        /// <summary>
        /// Converts a screen-pixel safe area (origin bottom left, as <see cref="Screen.safeArea"/>) into
        /// panel-unit insets. Returns zero insets when either size is not positive or not laid out yet (NaN).
        /// </summary>
        public static SafeAreaInsets Calculate(Rect safeArea, Vector2 screenSize, Vector2 panelSize)
        {
            if (!(screenSize.x > 0f) || !(screenSize.y > 0f) || !(panelSize.x > 0f) || !(panelSize.y > 0f))
            {
                return default;
            }

            float scaleX = panelSize.x / screenSize.x;
            float scaleY = panelSize.y / screenSize.y;
            return new SafeAreaInsets(
                Mathf.Max(0f, safeArea.xMin) * scaleX,
                Mathf.Max(0f, screenSize.y - safeArea.yMax) * scaleY,
                Mathf.Max(0f, screenSize.x - safeArea.xMax) * scaleX,
                Mathf.Max(0f, safeArea.yMin) * scaleY);
        }

        /// <summary>
        /// Pins the safe area to an explicit surface instead of <see cref="Screen"/> and applies it now.
        /// </summary>
        /// <param name="safeArea">The safe rectangle in surface pixels, origin bottom left.</param>
        /// <param name="screenSize">The surface size in pixels.</param>
        public void SetSource(Rect safeArea, Vector2 screenSize)
        {
            m_pinnedSafeArea = safeArea;
            m_pinnedScreenSize = screenSize;
            m_hasPinnedSource = true;
            Apply();
        }

        /// <summary>Applies the current safe area now; the owner calls it once after binding.</summary>
        public void Apply()
        {
            if (m_disposed)
            {
                return;
            }

            Rect safeArea = m_hasPinnedSource ? m_pinnedSafeArea : Screen.safeArea;
            Vector2 screenSize = m_hasPinnedSource ? m_pinnedScreenSize : new Vector2(Screen.width, Screen.height);
            SafeAreaInsets insets = Mask(Calculate(safeArea, screenSize, m_root.layout.size), m_edges);
            if (m_hasApplied && insets.Equals(m_applied))
            {
                return;
            }

            m_applied = insets;
            m_hasApplied = true;
            m_target.style.paddingLeft = insets.Left;
            m_target.style.paddingTop = insets.Top;
            m_target.style.paddingRight = insets.Right;
            m_target.style.paddingBottom = insets.Bottom;
        }

        public void Dispose()
        {
            if (m_disposed)
            {
                return;
            }

            m_disposed = true;
            m_root.UnregisterCallback(m_onGeometryChanged);
        }

        /// <summary>Keeps only the insets of the chosen edges.</summary>
        public static SafeAreaInsets Mask(SafeAreaInsets insets, SafeAreaEdges edges)
        {
            return new SafeAreaInsets(
                (edges & SafeAreaEdges.Left) != 0 ? insets.Left : 0f,
                (edges & SafeAreaEdges.Top) != 0 ? insets.Top : 0f,
                (edges & SafeAreaEdges.Right) != 0 ? insets.Right : 0f,
                (edges & SafeAreaEdges.Bottom) != 0 ? insets.Bottom : 0f);
        }

        private void OnGeometryChanged(GeometryChangedEvent geometryChanged)
        {
            Apply();
        }
    }
}
