using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// Draws soft light rays from its centre as one mesh without a texture, like the rays behind the coin pile of
    /// reference 05: each ray is a wedge whose middle is evenly bright, whose sides fade out and whose brightness falls
    /// off towards its tip, and a round glow can brighten the centre.
    /// </summary>
    /// <remarks>
    /// UI Toolkit calls <see cref="OnGenerateVisualContent"/> only when the element repaints, that is when its size or an
    /// attribute changes; nothing runs per frame. The rays are measured from the element's content box: their length is
    /// half its smaller side (a ray ends at 92 % of it), so the element's width and height set the size in USS.
    /// </remarks>
    public sealed class SunburstElement : VisualElement
    {
        public new sealed class UxmlFactory : UxmlFactory<SunburstElement, UxmlTraits>
        {
        }

        public new sealed class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlIntAttributeDescription m_rayCount = new UxmlIntAttributeDescription
                { name = "ray-count", defaultValue = k_defaultRayCount };

            private readonly UxmlFloatAttributeDescription m_rayWidth = new UxmlFloatAttributeDescription
                { name = "ray-width", defaultValue = k_defaultRayWidth };

            private readonly UxmlFloatAttributeDescription m_startAngle = new UxmlFloatAttributeDescription
                { name = "start-angle", defaultValue = 0f };

            private readonly UxmlColorAttributeDescription m_rayColor = new UxmlColorAttributeDescription
                { name = "ray-color", defaultValue = s_defaultRayColor };

            private readonly UxmlColorAttributeDescription m_glowColor = new UxmlColorAttributeDescription
                { name = "glow-color", defaultValue = Color.clear };

            private readonly UxmlFloatAttributeDescription m_glowRadius = new UxmlFloatAttributeDescription
                { name = "glow-radius", defaultValue = k_defaultGlowRadius };

            public override void Init(VisualElement element, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(element, bag, context);
                SunburstElement sunburst = (SunburstElement)element;
                sunburst.RayCount = m_rayCount.GetValueFromBag(bag, context);
                sunburst.RayWidth = m_rayWidth.GetValueFromBag(bag, context);
                sunburst.StartAngle = m_startAngle.GetValueFromBag(bag, context);
                sunburst.RayColor = m_rayColor.GetValueFromBag(bag, context);
                sunburst.GlowColor = m_glowColor.GetValueFromBag(bag, context);
                sunburst.GlowRadius = m_glowRadius.GetValueFromBag(bag, context);
            }
        }

        public const string UssClassName = "sunburst";

        private const int k_defaultRayCount = 12;
        private const float k_defaultRayWidth = 14f;
        private const float k_defaultGlowRadius = 0.6f;
        private const int k_maxRayCount = 64;

        // Across a ray, this share of its width in the middle is evenly bright; the rest fades to nothing at the sides.
        // Reference 05's rays are flat beams with soft edges, not peaks.
        private const float k_plateau = 0.5f;

        // Segments of the round glow's edge.
        private const int k_glowSegments = 48;

        // Five rings from the ray's start to its tip (GetRingRadius), each of four vertices across the ray: side,
        // plateau edge, plateau edge, side.
        private const int k_ringCount = 5;
        private const int k_ringVertices = 4;
        private const int k_rayVertices = k_ringCount * k_ringVertices;
        private const int k_rayIndices = (k_ringCount - 1) * (k_ringVertices - 1) * 6;

        private static readonly Color s_defaultRayColor = new Color(1f, 1f, 1f, 0.3f);

        private int m_rayCount = k_defaultRayCount;
        private float m_rayWidth = k_defaultRayWidth;
        private float m_startAngle;
        private Color m_rayColor = s_defaultRayColor;
        private Color m_glowColor = Color.clear;
        private float m_glowRadius = k_defaultGlowRadius;

        public SunburstElement()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>How many rays go round the centre, evenly spaced (0 to 64).</summary>
        public int RayCount
        {
            get => m_rayCount;
            set => SetAndRepaint(ref m_rayCount, Mathf.Clamp(value, 0, k_maxRayCount));
        }

        /// <summary>The angle each ray covers, in degrees.</summary>
        public float RayWidth
        {
            get => m_rayWidth;
            set => SetAndRepaint(ref m_rayWidth, Mathf.Clamp(value, 0f, 180f));
        }

        /// <summary>The first ray's direction, in degrees clockwise from straight up.</summary>
        public float StartAngle
        {
            get => m_startAngle;
            set => SetAndRepaint(ref m_startAngle, value);
        }

        /// <summary>The rays' colour; its alpha is the brightness next to the centre.</summary>
        public Color RayColor
        {
            get => m_rayColor;
            set => SetAndRepaint(ref m_rayColor, value);
        }

        /// <summary>The round glow's colour at the centre; a transparent colour draws no glow.</summary>
        public Color GlowColor
        {
            get => m_glowColor;
            set => SetAndRepaint(ref m_glowColor, value);
        }

        /// <summary>The glow's radius as a fraction of the rays' length.</summary>
        public float GlowRadius
        {
            get => m_glowRadius;
            set => SetAndRepaint(ref m_glowRadius, Mathf.Max(0f, value));
        }

        private void SetAndRepaint<T>(ref T field, T value)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext context)
        {
            Rect box = contentRect;
            float length = 0.5f * Mathf.Min(box.width, box.height);
            if (float.IsNaN(length) || length <= 0f)
            {
                return;
            }

            Vector2 centre = box.center;
            if (m_glowColor.a > 0f && m_glowRadius > 0f)
            {
                DrawGlow(context, centre, length * m_glowRadius);
            }

            if (m_rayCount > 0 && m_rayColor.a > 0f && m_rayWidth > 0f)
            {
                DrawRays(context, centre, length);
            }
        }

        // One fan: the centre vertex carries the glow colour and the edge fades to transparent, a radial gradient.
        private void DrawGlow(MeshGenerationContext context, Vector2 centre, float radius)
        {
            MeshWriteData mesh = context.Allocate(k_glowSegments + 1, k_glowSegments * 3);
            Color32 inner = m_glowColor;
            Color32 outer = Transparent(m_glowColor);
            mesh.SetNextVertex(CreateVertex(centre, inner));
            for (int segment = 0; segment < k_glowSegments; segment++)
            {
                float angle = segment * (2f * Mathf.PI / k_glowSegments);
                mesh.SetNextVertex(CreateVertex(Point(centre, angle, radius), outer));
            }

            // Angles grow clockwise on screen, so (centre, edge k, edge k + 1) runs clockwise.
            for (int segment = 0; segment < k_glowSegments; segment++)
            {
                int next = segment + 1 < k_glowSegments ? segment + 1 : 0;
                mesh.SetNextIndex(0);
                mesh.SetNextIndex((ushort)(1 + segment));
                mesh.SetNextIndex((ushort)(1 + next));
            }
        }

        private void DrawRays(MeshGenerationContext context, Vector2 centre, float length)
        {
            MeshWriteData mesh = context.Allocate(m_rayCount * k_rayVertices, m_rayCount * k_rayIndices);
            float halfWidth = 0.5f * m_rayWidth * Mathf.Deg2Rad;

            for (int ray = 0; ray < m_rayCount; ray++)
            {
                float angle = (m_startAngle + ray * 360f / m_rayCount) * Mathf.Deg2Rad;

                // Each ring from the left side to the right: side, plateau edge, plateau edge, side. The plateau carries
                // the ring's brightness and the sides nothing.
                for (int ring = 0; ring < k_ringCount; ring++)
                {
                    float radius = GetRingRadius(ring) * length;
                    float alpha = m_rayColor.a * GetRingBrightness(ring);
                    for (int step = 0; step < k_ringVertices; step++)
                    {
                        bool side = step == 0 || step == k_ringVertices - 1;
                        float across = side ? 1f : k_plateau;
                        if (step < k_ringVertices / 2)
                        {
                            across = -across;
                        }

                        mesh.SetNextVertex(CreateVertex(Point(centre, angle + across * halfWidth, radius),
                            WithAlpha(m_rayColor, side ? 0f : alpha)));
                    }
                }

                // Between two rings the ray is three quads (left edge, plateau, right edge) of two triangles each. Each
                // edge quad is split along the diagonal from its inner plateau vertex, mirrored on the two sides, so both
                // sides of a ray fade alike; the plateau quad is even across, so its split does not matter. Angles grow
                // clockwise and radii outwards, so every triangle below runs clockwise.
                int first = ray * k_rayVertices;
                for (int ring = 0; ring < k_ringCount - 1; ring++)
                {
                    int innerRing = first + ring * k_ringVertices;
                    int outerRing = innerRing + k_ringVertices;
                    for (int step = 0; step < k_ringVertices - 1; step++)
                    {
                        int innerA = innerRing + step;
                        int innerB = innerA + 1;
                        int outerA = outerRing + step;
                        int outerB = outerA + 1;
                        if (step == 0)
                        {
                            WriteTriangle(mesh, innerA, outerA, innerB);
                            WriteTriangle(mesh, innerB, outerA, outerB);
                        }
                        else
                        {
                            WriteTriangle(mesh, innerA, outerA, outerB);
                            WriteTriangle(mesh, innerA, outerB, innerB);
                        }
                    }
                }
            }
        }

        // A ring's distance from the centre as a fraction of the length. A ray starts a little way out, so the rays do
        // not pile up in one bright point, and ends at 92 % of the length, where reference 05's rays have faded out.
        private static float GetRingRadius(int ring)
        {
            switch (ring)
            {
                case 0:
                    return 0.08f;
                case 1:
                    return 0.35f;
                case 2:
                    return 0.6f;
                case 3:
                    return 0.8f;
                default:
                    return 0.92f;
            }
        }

        // A ring's share of the ray colour's alpha, fitted to reference 05's measured rays: it falls off about with the
        // square of the distance to the tip. The tail from the fourth ring is short on purpose: UI Toolkit's output
        // here steps in 1/255 linear units, so a long faint tail shows as visible bands.
        private static float GetRingBrightness(int ring)
        {
            switch (ring)
            {
                case 0:
                    return 1f;
                case 1:
                    return 0.5f;
                case 2:
                    return 0.19f;
                case 3:
                    return 0.04f;
                default:
                    return 0f;
            }
        }

        private static void WriteTriangle(MeshWriteData mesh, int a, int b, int c)
        {
            mesh.SetNextIndex((ushort)a);
            mesh.SetNextIndex((ushort)b);
            mesh.SetNextIndex((ushort)c);
        }

        // Clockwise from straight up on screen, where y grows downwards.
        private static Vector2 Point(Vector2 centre, float angle, float radius)
        {
            return new Vector2(centre.x + radius * Mathf.Sin(angle), centre.y - radius * Mathf.Cos(angle));
        }

        private static Vertex CreateVertex(Vector2 position, Color32 tint)
        {
            return new Vertex { position = new Vector3(position.x, position.y, Vertex.nearZ), tint = tint };
        }

        private static Color32 Transparent(Color colour)
        {
            return WithAlpha(colour, 0f);
        }

        private static Color32 WithAlpha(Color colour, float alpha)
        {
            colour.a = alpha;
            return colour;
        }
    }
}
