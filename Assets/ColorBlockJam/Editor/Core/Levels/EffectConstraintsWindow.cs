using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Effect Constraints window (DEC-177): a Blocks / Gates tab, a searchable list of that target's effects with the
    /// number of effects each may stack with, and a detail pane with one checkbox per other effect for the selected one.
    /// The screen shows at most one checkbox per effect, so it stays readable however many effects the level format
    /// registers, and a new effect appears without a code change. A checked effect may share one block or gate with the
    /// selected one; the Effect Editor's Add Effect hides an effect that cannot join the ones already there. Each change
    /// is one Undo step on <see cref="EffectStackingRules"/>.
    /// </summary>
    public sealed class EffectConstraintsWindow : EditorWindow
    {
        internal const string k_title = "Effect Constraints";
        internal const string k_effectListName = "effect-constraints-effects";
        internal const string k_partnerListName = "effect-constraints-partners";
        internal const string k_searchName = "effect-constraints-search";

        // A compact layout (user request of 2026-09-28): a narrow list, a wrapping header and buttons that share the row.
        private const float k_listWidth = 120f;
        private const float k_listMinWidth = 90f;
        private const float k_detailMinWidth = 150f;
        private static readonly Color s_selectedColor = new Color(0.17f, 0.36f, 0.53f);

        private const string k_help = "Ticked effects can share a block or gate; unticked ones are hidden in Add Effect.";

        // View state kept across domain reloads; it never touches the rules.
        [SerializeField] private LevelEffectTarget m_target = LevelEffectTarget.Block;
        [SerializeField] private string m_selected;
        [SerializeField] private string m_search = string.Empty;

        private VisualElement m_effectList;
        private VisualElement m_detail;
        private ToolbarToggle m_blockTab;
        private ToolbarToggle m_gateTab;

        [MenuItem("Color Block Jam/Levels/Effect Constraints")]
        public static void Open()
        {
            GetWindow<EffectConstraintsWindow>(k_title, true, typeof(LevelEffectEditorWindow), typeof(LevelDatabaseWindow));
        }

        /// <summary>Every effect type the level format registers for the target, in registry order.</summary>
        internal static List<string> GetTypes(LevelEffectTarget target)
        {
            List<string> types = new List<string>();
            for (int i = 0; i < LevelEffectRegistry.DefinitionCount; i++)
            {
                LevelEffectDefinition definition = LevelEffectRegistry.GetDefinitionAt(i);
                if (definition.Target == target)
                {
                    types.Add(definition.TypeId);
                }
            }

            return types;
        }

        /// <summary>Selects a tab and an effect, as a click would; tests use it.</summary>
        internal void Select(LevelEffectTarget target, string type)
        {
            m_target = target;
            m_selected = type;
            Refresh();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(k_title);
            minSize = new Vector2(k_listMinWidth + k_detailMinWidth + 16f, 180f);
            // Owner: this window instance. Stop point: OnDisable.
            Undo.undoRedoPerformed += Refresh;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Refresh;
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;

            // The tab already names the window, so the title is gone; the help is one small wrapping line.
            Label help = new Label(k_help);
            help.style.whiteSpace = WhiteSpace.Normal;
            help.style.fontSize = 11;
            help.style.opacity = 0.8f;
            root.Add(help);

            // One tab group: two joined toolbar toggles, exactly one of them on (Unity 2022.3 has no TabView).
            Toolbar tabs = new Toolbar();
            tabs.style.marginTop = 4;
            tabs.style.marginBottom = 2;
            m_blockTab = CreateTab("Blocks", LevelEffectTarget.Block);
            m_gateTab = CreateTab("Gates", LevelEffectTarget.Door);
            tabs.Add(m_blockTab);
            tabs.Add(m_gateTab);
            root.Add(tabs);

            TwoPaneSplitView split = new TwoPaneSplitView(0, k_listWidth, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            root.Add(split);

            VisualElement left = new VisualElement();
            left.style.minWidth = k_listMinWidth;
            ToolbarSearchField search = new ToolbarSearchField { name = k_searchName };
            search.style.width = StyleKeyword.Auto;
            search.SetValueWithoutNotify(m_search);
            search.RegisterValueChangedCallback(evt =>
            {
                m_search = evt.newValue ?? string.Empty;
                Refresh();
            });
            left.Add(search);
            ScrollView effects = new ScrollView(ScrollViewMode.Vertical);
            effects.style.flexGrow = 1;
            m_effectList = effects.contentContainer;
            m_effectList.name = k_effectListName;
            left.Add(effects);
            split.Add(left);

            ScrollView detail = new ScrollView(ScrollViewMode.Vertical);
            detail.style.minWidth = k_detailMinWidth;
            detail.contentContainer.style.paddingLeft = 6;
            detail.contentContainer.style.paddingRight = 2;
            m_detail = detail.contentContainer;
            split.Add(detail);

            Refresh();
        }

        private ToolbarToggle CreateTab(string text, LevelEffectTarget target)
        {
            ToolbarToggle tab = new ToolbarToggle { text = text, name = "effect-constraints-tab-" + text.ToLowerInvariant() };
            tab.style.flexGrow = 1;
            tab.style.unityTextAlign = TextAnchor.MiddleCenter;
            // A click on the open tab keeps it open; a click on the other one switches the target.
            tab.RegisterValueChangedCallback(_ => Select(target, m_target == target ? m_selected : null));
            return tab;
        }

        private void Refresh()
        {
            if (m_effectList == null)
            {
                return;
            }

            SetActive(m_blockTab, m_target == LevelEffectTarget.Block);
            SetActive(m_gateTab, m_target == LevelEffectTarget.Door);

            EffectStackingRules rules = EffectStackingRulesStore.Load();
            List<string> types = GetTypes(m_target);
            if (m_selected != null && !types.Contains(m_selected))
            {
                m_selected = null;
            }

            if (m_selected == null && types.Count > 0)
            {
                m_selected = types[0];
            }

            m_effectList.Clear();
            foreach (string type in types)
            {
                if (m_search.Length == 0 || ToLabel(type).IndexOf(m_search, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    m_effectList.Add(CreateEffectRow(type, types, rules));
                }
            }

            m_detail.Clear();
            string owner = m_target == LevelEffectTarget.Door ? "gate" : "block";
            if (types.Count == 0)
            {
                m_detail.Add(new Label("No " + owner + " effect yet."));
                return;
            }

            if (types.Count == 1)
            {
                m_detail.Add(new Label("Only one " + owner + " effect exists, so nothing can stack yet."));
                return;
            }

            Label header = new Label(ToLabel(m_selected) + " can share a " + owner + " with:");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.whiteSpace = WhiteSpace.Normal;
            header.style.marginBottom = 2;
            m_detail.Add(header);

            VisualElement partners = new VisualElement { name = k_partnerListName };
            foreach (string type in types)
            {
                if (!EffectStackingRules.IsFixed(m_selected, type))
                {
                    partners.Add(CreatePartner(m_selected, type, rules));
                }
            }

            m_detail.Add(partners);

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.flexWrap = Wrap.Wrap;
            buttons.style.marginTop = 4;
            string selected = m_selected;
            buttons.Add(CreateCompactButton("Allow All", () => SetAllFor(selected, types, true)));
            buttons.Add(CreateCompactButton("Allow None", () => SetAllFor(selected, types, false)));
            m_detail.Add(buttons);
        }

        // Buttons share the row and wrap onto a second one when the pane is too narrow for both.
        private static Button CreateCompactButton(string text, Action onClick)
        {
            Button button = new Button(onClick) { text = text };
            button.style.flexGrow = 1;
            button.style.flexBasis = 70;
            button.style.marginLeft = 0;
            button.style.marginRight = 2;
            return button;
        }

        private VisualElement CreateEffectRow(string type, List<string> types, EffectStackingRules rules)
        {
            VisualElement row = new VisualElement { name = "effect-row-" + type };
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.paddingLeft = 4;
            row.style.paddingRight = 6;
            row.style.minHeight = 18;
            row.style.alignItems = Align.Center;
            if (type == m_selected)
            {
                row.style.backgroundColor = s_selectedColor;
            }

            Label name = new Label(ToLabel(type));
            Label count = new Label((rules == null ? 0 : rules.CountPartners(m_target, type, types)).ToString())
            {
                tooltip = "Effects it can share a block or gate with."
            };
            count.style.opacity = 0.7f;
            if (!IsImplemented(type))
            {
                name.style.opacity = 0.55f;
                row.tooltip = "Not implemented in the game yet.";
            }

            row.Add(name);
            row.Add(count);
            row.AddManipulator(new Clickable(() => Select(m_target, type)));
            return row;
        }

        private Toggle CreatePartner(string selected, string other, EffectStackingRules rules)
        {
            Toggle toggle = new Toggle(ToLabel(other)) { name = selected + "-" + other };
            toggle.SetValueWithoutNotify(rules != null && rules.CanStack(m_target, selected, other));
            // The label takes only its text's width, so the box sits right after the name in a narrow pane.
            toggle.labelElement.style.minWidth = 0;
            toggle.labelElement.style.flexGrow = 1;
            toggle.style.marginLeft = 0;
            if (!IsImplemented(other))
            {
                toggle.labelElement.style.opacity = 0.55f;
                toggle.tooltip = "Not implemented in the game yet.";
            }

            LevelEffectTarget target = m_target;
            toggle.RegisterValueChangedCallback(evt =>
            {
                EffectStackingRules edited = EffectStackingRulesStore.LoadOrCreate();
                Undo.RecordObject(edited, evt.newValue ? "Allow Effect Stacking" : "Forbid Effect Stacking");
                edited.SetCanStack(target, selected, other, evt.newValue);
                EffectStackingRulesStore.Save(edited);
                Refresh();
            });
            return toggle;
        }

        private void SetAllFor(string selected, List<string> types, bool canStack)
        {
            EffectStackingRules rules = EffectStackingRulesStore.LoadOrCreate();
            Undo.RecordObject(rules, canStack ? "Allow All Effect Stacking" : "Forbid All Effect Stacking");
            rules.SetAllFor(m_target, selected, types, canStack);
            EffectStackingRulesStore.Save(rules);
            Refresh();
        }

        private static void SetActive(ToolbarToggle tab, bool active)
        {
            tab.SetValueWithoutNotify(active);
        }

        private static bool IsImplemented(string type)
            => LevelEffectRegistry.TryFind(type, out LevelEffectDefinition definition) && definition.IsRuntimeImplemented;

        private static string ToLabel(string type) => LevelEffectEditorWindow.ToLabel(type);
    }
}
