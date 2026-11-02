using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One effect in the Effect Editor, shown like an Inspector component: a foldout header with the effect's name that
    /// collapses and expands the card, an optional action (Remove) at the header's right, and the effect's rows as its
    /// content. It holds no draft state; the window rebuilds it after every change (TASK-20260926-170647550-7683988f).
    /// </summary>
    internal sealed class LevelEffectCard : VisualElement
    {
        internal const string k_foldoutSuffix = "-foldout";
        // Room the header keeps free at its right for the action, so a long name never runs under it.
        private const float k_headerActionSpace = 22f;
        private static readonly Color s_fill = new Color(0f, 0f, 0f, 0.12f);
        private static readonly Color s_frame = new Color(0f, 0f, 0f, 0.35f);

        private readonly Foldout m_foldout;

        /// <param name="expandedChanged">Called when the designer folds or unfolds the card, with the new state.</param>
        public LevelEffectCard(string name, string title, string tooltip, bool expanded, Action<bool> expandedChanged)
        {
            this.name = name;
            style.marginTop = 4;
            style.paddingBottom = 4;
            style.paddingRight = 4;
            style.backgroundColor = s_fill;
            style.borderTopWidth = 1;
            style.borderBottomWidth = 1;
            style.borderLeftWidth = 1;
            style.borderRightWidth = 1;
            style.borderTopColor = s_frame;
            style.borderBottomColor = s_frame;
            style.borderLeftColor = s_frame;
            style.borderRightColor = s_frame;
            style.borderTopLeftRadius = 3;
            style.borderTopRightRadius = 3;
            style.borderBottomLeftRadius = 3;
            style.borderBottomRightRadius = 3;

            m_foldout = new Foldout { name = name + k_foldoutSuffix, text = title, value = expanded, tooltip = tooltip };
            Toggle header = m_foldout.Q<Toggle>();
            if (header != null)
            {
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            // Field changes inside the content bubble up through the foldout as the same event type, so only the
            // foldout's own change folds the card. Owner: this card; stop point: the window drops the card on its
            // next rebuild.
            m_foldout.RegisterValueChangedCallback(change =>
            {
                if (change.target == m_foldout)
                {
                    expandedChanged?.Invoke(change.newValue);
                }
            });
            hierarchy.Add(m_foldout);
        }

        /// <summary>Rows added to the card go into its foldout, so folding hides them.</summary>
        public override VisualElement contentContainer => m_foldout.contentContainer;

        public string Title => m_foldout.text;

        public bool IsExpanded
        {
            get => m_foldout.value;
            set => m_foldout.value = value;
        }

        /// <summary>
        /// Puts an action, such as Remove, at the header's right. It sits beside the foldout rather than inside its
        /// header, so a click on it never folds the card.
        /// </summary>
        public void SetHeaderAction(VisualElement action)
        {
            action.style.position = Position.Absolute;
            action.style.top = 2;
            action.style.right = 4;
            Toggle header = m_foldout.Q<Toggle>();
            if (header != null)
            {
                header.style.marginRight = k_headerActionSpace;
            }

            hierarchy.Add(action);
        }
    }
}
