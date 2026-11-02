using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Presentation.UI
{
    /// <summary>
    /// Draws one line of sticker-style title text bent along a circular arc, like the reference Level Complete title
    /// (reference 05): every letter is a stack of four same-text Labels (shadow, rim, stroke, face) and the letters sit
    /// on a circle of <see cref="Radius"/>, lower and turned outwards towards both ends.
    /// </summary>
    /// <remarks>
    /// The layers reuse the project's heavy-text convention (Common.uss): each letter is a <c>.cbj-heavy-text</c> box whose
    /// back layers (<c>.cbj-heavy-text__back</c>) fill it behind the face, so USS styles each layer's colour and outline
    /// through <see cref="ShadowUssClassName"/>, <see cref="RimUssClassName"/>, <see cref="StrokeUssClassName"/> and
    /// <see cref="FaceUssClassName"/>. Letters are built when the text is set (UXML load); the arc is written only when
    /// the line's layout changes, as translate and rotate, which never change layout. Nothing runs per frame.
    /// </remarks>
    public sealed class ArcTextElement : VisualElement
    {
        public new sealed class UxmlFactory : UxmlFactory<ArcTextElement, UxmlTraits>
        {
        }

        public new sealed class UxmlTraits : VisualElement.UxmlTraits
        {
            private readonly UxmlStringAttributeDescription m_text = new UxmlStringAttributeDescription
                { name = "text", defaultValue = string.Empty };

            private readonly UxmlFloatAttributeDescription m_radius = new UxmlFloatAttributeDescription
                { name = "radius", defaultValue = 0f };

            public override void Init(VisualElement element, IUxmlAttributes bag, CreationContext context)
            {
                base.Init(element, bag, context);
                ArcTextElement arcText = (ArcTextElement)element;
                arcText.Radius = m_radius.GetValueFromBag(bag, context);
                arcText.Text = m_text.GetValueFromBag(bag, context);
            }
        }

        public const string UssClassName = "arc-text";
        public const string LetterUssClassName = "arc-text__letter";
        public const string ShadowUssClassName = "arc-text__shadow";
        public const string RimUssClassName = "arc-text__rim";
        public const string StrokeUssClassName = "arc-text__stroke";
        public const string FaceUssClassName = "arc-text__face";

        // Common.uss: the heavy-text box and its absolutely placed back layers.
        private const string k_heavyTextClassName = "cbj-heavy-text";
        private const string k_heavyTextBackClassName = "cbj-heavy-text__back";

        private string m_text = string.Empty;
        private float m_radius;

        public ArcTextElement()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
        }

        /// <summary>The line's text; setting it rebuilds one letter stack per character.</summary>
        public string Text
        {
            get => m_text;
            set
            {
                string text = value ?? string.Empty;
                if (string.Equals(text, m_text, StringComparison.Ordinal))
                {
                    return;
                }

                m_text = text;
                BuildLetters();
            }
        }

        /// <summary>The arc's radius in panel units; 0 keeps the letters on a straight line.</summary>
        public float Radius
        {
            get => m_radius;
            set
            {
                float radius = Mathf.Max(0f, value);
                if (Mathf.Approximately(radius, m_radius))
                {
                    return;
                }

                m_radius = radius;
                ApplyArc();
            }
        }

        /// <summary>
        /// Where a letter whose centre is <paramref name="offset"/> units from the line's centre sits on a circle of
        /// <paramref name="radius"/> that touches the line's centre: how far it moves down and how many degrees it
        /// turns (clockwise on the right side). A radius of 0, or an offset beyond it, keeps the letter straight.
        /// </summary>
        public static void GetArcPlacement(float offset, float radius, out float drop, out float degrees)
        {
            if (radius <= 0f || Mathf.Abs(offset) >= radius)
            {
                drop = 0f;
                degrees = 0f;
                return;
            }

            drop = radius - Mathf.Sqrt(radius * radius - offset * offset);
            degrees = Mathf.Asin(offset / radius) * Mathf.Rad2Deg;
        }

        private void BuildLetters()
        {
            Clear();
            for (int index = 0; index < m_text.Length; index++)
            {
                string letter = m_text.Substring(index, 1);
                VisualElement stack = new VisualElement { pickingMode = PickingMode.Ignore };
                stack.AddToClassList(LetterUssClassName);
                stack.AddToClassList(k_heavyTextClassName);

                // Drawn in child order: the shadow first, the face last and in normal flow, so it sets the letter's size.
                stack.Add(CreateLayer(letter, ShadowUssClassName, true));
                stack.Add(CreateLayer(letter, RimUssClassName, true));
                stack.Add(CreateLayer(letter, StrokeUssClassName, true));
                stack.Add(CreateLayer(letter, FaceUssClassName, false));
                Add(stack);
            }

            ApplyArc();
        }

        private static Label CreateLayer(string letter, string className, bool isBack)
        {
            Label layer = new Label(letter) { pickingMode = PickingMode.Ignore };
            layer.AddToClassList(className);
            if (isBack)
            {
                layer.AddToClassList(k_heavyTextBackClassName);
            }

            return layer;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            ApplyArc();
        }

        // Each letter's offset is measured from the line's centre to the letter's centre, in the line's own space.
        private void ApplyArc()
        {
            float centre = 0.5f * layout.width;
            if (float.IsNaN(centre))
            {
                return;
            }

            for (int index = 0; index < childCount; index++)
            {
                VisualElement letter = this[index];
                Rect box = letter.layout;
                if (float.IsNaN(box.width))
                {
                    continue;
                }

                GetArcPlacement(box.center.x - centre, m_radius, out float drop, out float degrees);
                letter.style.translate = new Translate(0f, drop);
                letter.style.rotate = new Rotate(degrees);
            }
        }
    }
}
