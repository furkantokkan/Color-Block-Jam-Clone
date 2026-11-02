using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>One row of the Effect Editor's Add Effect popup: an effect type, its label and whether it can be added.</summary>
    internal readonly struct LevelEffectChoice
    {
        public LevelEffectChoice(string type, string label, bool isEnabled)
        {
            Type = type;
            Label = label;
            IsEnabled = isEnabled;
        }

        public string Type { get; }
        public string Label { get; }
        public bool IsEnabled { get; }
    }

    /// <summary>
    /// The Effect Editor's Add Effect popup, built on Unity's Add Component control: a title, a search field and one row
    /// per effect type, where a type the block already has is disabled. Picking a row reports its type once (user
    /// request of 2026-09-26, TASK-20260926-170647550-7683988f).
    /// </summary>
    internal sealed class LevelEffectDropdown : AdvancedDropdown
    {
        private const string k_title = "Effects";
        private static readonly Vector2 s_minimumSize = new Vector2(230f, 180f);

        private readonly LevelEffectChoice[] m_choices;
        private readonly Action<string> m_picked;

        public LevelEffectDropdown(IReadOnlyList<LevelEffectChoice> choices, Action<string> picked)
            : base(new AdvancedDropdownState())
        {
            m_choices = new LevelEffectChoice[choices?.Count ?? 0];
            for (int index = 0; index < m_choices.Length; index++)
            {
                m_choices[index] = choices[index];
            }

            m_picked = picked;
            minimumSize = s_minimumSize;
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            AdvancedDropdownItem root = new AdvancedDropdownItem(k_title);
            for (int index = 0; index < m_choices.Length; index++)
            {
                LevelEffectChoice choice = m_choices[index];
                root.AddChild(new EffectItem(choice.Label, choice.Type) { enabled = choice.IsEnabled });
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item)
        {
            if (item is EffectItem effect && effect.enabled)
            {
                m_picked?.Invoke(effect.Type);
            }
        }

        private sealed class EffectItem : AdvancedDropdownItem
        {
            public EffectItem(string label, string type)
                : base(label)
            {
                Type = type;
            }

            public string Type { get; }
        }
    }
}
