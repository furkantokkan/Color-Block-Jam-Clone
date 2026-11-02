using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Editor.Controls;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// One Effect Editor edit: a command run on the Level Editor's current draft level that returns the new level, or
    /// refuses with a designer message. Only the owning draft editor runs it.
    /// </summary>
    internal delegate bool LevelEditRequest(LevelData level, out LevelData result, out string issue);

    /// <summary>
    /// Effect Editor window: shows the block, gate or board object selected on the Level Editor canvas as a title and its
    /// effects as collapsible cards, like Inspector components, and turns each change into one edit request that the
    /// Level Editor applies to its draft; it never writes the draft or the level JSON itself
    /// (docs/Level-Editor-Design.md, Workspace and Effect Editor; TASK-20260926-170647550-7683988f).
    /// </summary>
    public sealed class LevelEffectEditorWindow : EditorWindow
    {
        internal const string k_title = "Effect Editor";
        internal const string k_selectHint = "Click a block or a gate in the Level Editor.";
        internal const string k_noLevelEditorText = "No Level Editor is open. Open it with Color Block Jam/Level Editor.";
        internal const string k_noLevelText = "The Level Editor has no level open.";
        internal const string k_gateEffectsText = "Gate effects are outside the case scope.";
        internal const string k_objectEffectText =
            "A board object has exactly one effect. Its optional settings can be added, changed and removed here.";
        internal const string k_readOnlyTooltip = "Outside the case scope: shown read-only. Remove deletes it.";
        internal const string k_addEffectText = "Add Effect";
        private const float k_labelWidth = 96f;
        // The Inspector's Add Component button size, which the user asked Add Effect to copy.
        private const float k_addEffectWidth = 230f;
        private const float k_addEffectHeight = 24f;
        private const float k_removeIconSize = 16f;
        private static readonly Color s_issueColor = new Color(1f, 0.55f, 0.45f);

        // The Level Editor this window follows; not serialized, OnEnable finds an open one again after a domain reload.
        private ILevelDraftEditor m_source;
        private ILevelDraftEditorSource m_sources;
        private LevelCanvasSelection m_shownSelection;
        // True while this window's own request runs, so the selection event it raises waits for one refresh after it.
        private bool m_applying;
        // Collapsed cards by entity kind and effect type ("Block:ice"), so a rebuild keeps them collapsed while the window
        // is open. Like the Inspector, which folds a component type on every object, folding Ice folds it on every
        // block. Folding is view state only: it never touches the draft.
        private readonly HashSet<string> m_collapsedCards = new HashSet<string>();
        // What Add Effect offers for the shown block, rebuilt with its cards.
        private readonly List<LevelEffectChoice> m_addChoices = new List<LevelEffectChoice>();

        private Label m_message;
        private Label m_issue;
        private Label m_title;
        private VisualElement m_effectsSection;
        private VisualElement m_effectsList;
        private Button m_addEffectButton;
        private Label m_effectsNote;

        /// <summary>The Level Editor this window follows, or null.</summary>
        internal ILevelDraftEditor Source => m_source;

        /// <summary>
        /// What Add Effect offers for the shown block; a type the block already has is disabled, and a type that cannot
        /// stack with its effects is left out.
        /// </summary>
        internal IReadOnlyList<LevelEffectChoice> AddEffectChoices => m_addChoices;

        /// <summary>Tests set rules here; null reads the project's <see cref="EffectStackingRules"/>.</summary>
        internal EffectStackingRules StackingRules { get; set; }

        /// <summary>
        /// A designer's canvas click: opens a closed Effect Editor docked beside the Level Editor and gives the focus
        /// back to that Level Editor; an open one only follows the click through the selection event. It is the Level
        /// Editor's default canvas-selection opener.
        /// </summary>
        internal static void ShowBeside(EditorWindow source)
        {
            if (source == null || HasOpenInstances<LevelEffectEditorWindow>())
            {
                return;
            }

            // Deferred, so the dock change never runs inside the click's own event dispatch. Owner: this call; stop
            // point: delayCall runs it once.
            EditorApplication.delayCall += () =>
            {
                if (source == null || HasOpenInstances<LevelEffectEditorWindow>())
                {
                    return;
                }

                GetWindow<LevelEffectEditorWindow>(k_title, false, source.GetType());
                source.Focus();
            };
        }

        /// <summary>Follows this Level Editor and shows its selection; the selection event calls it too.</summary>
        internal void Follow(ILevelDraftEditor source)
        {
            m_source = source;
            Refresh();
        }

        internal void SetSources(ILevelDraftEditorSource sources)
        {
            if (m_sources != null)
            {
                m_sources.Changed -= OnLevelEditorChanged;
                m_sources.Dispose();
            }

            m_sources = sources;
            m_sources.Changed += OnLevelEditorChanged;
            m_source = null;
            Refresh();
        }

        /// <summary>
        /// Adds an effect of this type to the shown block as one request (one undo step); the Add Effect popup calls it.
        /// Its card starts expanded.
        /// </summary>
        internal void AddEffect(string type)
        {
            if (m_shownSelection.Kind != LevelCanvasSelectionKind.Block)
            {
                return;
            }

            int index = m_shownSelection.Index;
            m_collapsedCards.Remove(GetCardKey(LevelEffectTarget.Block, type));
            Request($"Add {ToLabel(type)} Effect", (LevelData level, out LevelData result, out string issue) =>
                LevelEffectCommands.TryAddEffect(level, LevelEffectTarget.Block, index, type, out result, out issue));
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(k_title);
            minSize = new Vector2(260f, 240f);
            // Owner: this window instance. Stop point: OnDisable.
            SetSources(new LevelDraftEditorSource());
        }

        private void OnDisable()
        {
            if (m_sources != null)
            {
                m_sources.Changed -= OnLevelEditorChanged;
                m_sources.Dispose();
                m_sources = null;
            }
        }

        // Back from the Level Editor: the cards show the draft as it is now.
        private void OnFocus()
        {
            Refresh();
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            ScrollView scroll = new ScrollView { name = "effect-editor" };
            scroll.style.flexGrow = 1;
            scroll.contentContainer.style.paddingLeft = 6;
            scroll.contentContainer.style.paddingRight = 6;
            scroll.contentContainer.style.paddingTop = 4;
            root.Add(scroll);

            m_message = new Label { name = "effect-editor-message" };
            m_message.style.whiteSpace = WhiteSpace.Normal;
            m_message.style.marginTop = 6;
            scroll.Add(m_message);
            m_issue = new Label { name = "effect-editor-issue" };
            m_issue.style.whiteSpace = WhiteSpace.Normal;
            m_issue.style.color = s_issueColor;
            m_issue.style.marginTop = 4;
            scroll.Add(m_issue);

            // Only a title names the selection (user request of 2026-09-26): the Level Editor canvas already moves,
            // turns, recolours and re-types blocks and paints gates, so this window shows effects alone.
            m_title = new Label { name = "effect-editor-title" };
            m_title.style.unityFontStyleAndWeight = FontStyle.Bold;
            m_title.style.fontSize = 13;
            m_title.style.marginTop = 6;
            scroll.Add(m_title);
            scroll.Add(CreateEffectsSection());
            SetIssue(null);
            Refresh();
        }

        // The Level Editor's selection event. Owner: this window instance; stop point: OnDisable.
        private void OnLevelEditorChanged(ILevelDraftEditor sender)
        {
            if (sender == null || !sender.IsAvailable)
            {
                return;
            }

            // A Level Editor that closes or has no level hands over to another open one with a level, for example
            // when a test window closes; otherwise this window follows the Level Editor that changed.
            m_source = sender.GetDraftLevel() != null ? sender : m_sources.Find(sender) ?? sender;
            if (!m_applying)
            {
                Refresh();
            }
        }

        // Single refresh point: the title and effect cards of the selected entity, or the hint.
        private void Refresh()
        {
            if (m_message == null)
            {
                return;
            }

            if (m_source == null || !m_source.IsAvailable)
            {
                m_source = m_sources?.Find(null);
            }

            LevelData level = m_source != null ? m_source.GetDraftLevel() : null;
            LevelCanvasSelection selection = level != null ? m_source.CanvasSelection : LevelCanvasSelection.None;
            if (!selection.Equals(m_shownSelection))
            {
                // A refusal belongs to the entity it was about.
                m_shownSelection = selection;
                SetIssue(null);
            }

            string title = null;
            LevelEffectTarget target = default;
            LevelEffectData[] effects = null;
            bool resolved = level != null && TryResolveSelection(level, selection, out title, out target, out effects);
            m_title.text = resolved ? title : string.Empty;
            SetVisible(m_title, resolved);
            SetVisible(m_effectsSection, resolved);
            m_message.text = m_source == null ? k_noLevelEditorText
                : level == null ? k_noLevelText
                : resolved ? string.Empty
                : k_selectHint;
            SetVisible(m_message, m_message.text.Length > 0);

            if (resolved)
            {
                ShowEffects(target, selection.Index, effects);
            }
            else
            {
                // Nothing is shown, so Add Effect offers nothing either.
                m_addChoices.Clear();
            }
        }

        // The selected entity's title, effect target and current effects in one lookup, so Refresh fetches the
        // record once instead of branching on its kind three times.
        private static bool TryResolveSelection(LevelData level, LevelCanvasSelection selection, out string title,
            out LevelEffectTarget target, out LevelEffectData[] effects)
        {
            int index = selection.Index;
            switch (selection.Kind)
            {
                case LevelCanvasSelectionKind.Block:
                    if (index >= 0 && index < level.BlockCount && level.GetBlockAt(index) != null)
                    {
                        LevelBlockData block = level.GetBlockAt(index);
                        title = GetBlockTitle(block);
                        target = LevelEffectTarget.Block;
                        effects = LevelRecordCopy.GetEffects(block);
                        return true;
                    }

                    break;
                case LevelCanvasSelectionKind.Door:
                    if (index >= 0 && index < level.DoorCount && level.GetDoorAt(index) != null)
                    {
                        LevelDoorData door = level.GetDoorAt(index);
                        title = GetGateTitle(door);
                        target = LevelEffectTarget.Door;
                        effects = LevelRecordCopy.GetEffects(door);
                        return true;
                    }

                    break;
                case LevelCanvasSelectionKind.Obstacle:
                    if (index >= 0 && index < level.CellObjectCount && level.GetCellObjectAt(index) != null)
                    {
                        LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                        title = GetObjectTitle(cellObject);
                        target = LevelEffectTarget.CellObject;
                        effects = cellObject.Effect != null
                            ? new[] { cellObject.Effect }
                            : Array.Empty<LevelEffectData>();
                        return true;
                    }

                    break;
            }

            title = null;
            target = default;
            effects = null;
            return false;
        }

        // Sends one edit to the Level Editor, the single writer of the draft, then shows the result or the refusal
        // the Level Editor also shows in its status line.
        private void Request(string undoName, LevelEditRequest request)
        {
            ILevelDraftEditor source = m_source;
            if (source == null || !source.IsAvailable)
            {
                Refresh();
                return;
            }

            bool applied;
            string issue;
            m_applying = true;
            try
            {
                applied = source.TryApplyEditRequest(undoName, request, out issue);
            }
            finally
            {
                m_applying = false;
            }

            // A refused value snaps back to the draft's.
            Refresh();
            SetIssue(applied ? null : issue);
        }

        private void SetIssue(string issue)
        {
            m_issue.text = issue ?? string.Empty;
            SetVisible(m_issue, !string.IsNullOrEmpty(issue));
        }

        private VisualElement CreateEffectsSection()
        {
            m_effectsSection = new VisualElement { name = "effects-section" };
            m_effectsSection.style.marginTop = 4;
            m_effectsNote = new Label { name = "effects-note" };
            m_effectsNote.style.whiteSpace = WhiteSpace.Normal;
            m_effectsNote.style.opacity = 0.75f;
            m_effectsSection.Add(m_effectsNote);
            m_effectsList = new VisualElement { name = "effects-list" };
            m_effectsSection.Add(m_effectsList);

            // Like the Inspector's Add Component: a wide, centred button under the cards that opens a searchable popup
            // (user request of 2026-09-26).
            m_addEffectButton = new Button(OpenAddEffect) { name = "effect-add", text = k_addEffectText };
            m_addEffectButton.style.alignSelf = Align.Center;
            m_addEffectButton.style.width = k_addEffectWidth;
            m_addEffectButton.style.height = k_addEffectHeight;
            m_addEffectButton.style.marginTop = 10;
            m_effectsSection.Add(m_addEffectButton);
            return m_effectsSection;
        }

        // Effect cards are rebuilt on every refresh: they are few, and their keys come from the registry.
        private void ShowEffects(LevelEffectTarget target, int index, LevelEffectData[] effects)
        {
            m_effectsList.Clear();
            for (int effectIndex = 0; effectIndex < effects.Length; effectIndex++)
            {
                if (effects[effectIndex] != null)
                {
                    m_effectsList.Add(CreateEffectCard(target, index, effectIndex, effects[effectIndex]));
                }
            }

            // A gate without effects shows only the scope note: there is nothing to add or remove.
            if (effects.Length == 0 && target != LevelEffectTarget.Door)
            {
                m_effectsList.Add(new Label("No effects.") { name = "effects-empty" });
            }

            m_effectsNote.text = target == LevelEffectTarget.Door ? k_gateEffectsText
                : target == LevelEffectTarget.CellObject ? k_objectEffectText
                : string.Empty;
            SetVisible(m_effectsNote, m_effectsNote.text.Length > 0);

            // Only blocks take new effects: gate effects are outside the case scope, and a board object has exactly one.
            // A type that cannot stack with the block's effects is left out (Effect Constraints, DEC-177).
            m_addChoices.Clear();
            bool addable = false;
            if (target == LevelEffectTarget.Block)
            {
                EffectStackingRules rules = StackingRules != null ? StackingRules : EffectStackingRulesStore.Load();
                List<string> existing = GetTypes(effects);
                for (int typeIndex = 0; typeIndex < LevelEffectCommands.BlockEffectTypeCount; typeIndex++)
                {
                    string type = LevelEffectCommands.GetBlockEffectType(typeIndex);
                    bool enabled = !HasEffect(effects, type);
                    if (enabled && !EffectStackingRules.CanJoin(rules, target, existing, type))
                    {
                        continue;
                    }

                    m_addChoices.Add(new LevelEffectChoice(type, ToLabel(type), enabled));
                    addable |= enabled;
                }
            }

            SetVisible(m_addEffectButton, target == LevelEffectTarget.Block);
            m_addEffectButton.SetEnabled(addable);
            m_addEffectButton.tooltip = addable
                ? "Add an effect to this block (one undo step)."
                : "This block already has every effect it can take.";
        }

        private void OpenAddEffect()
        {
            // The popup copies the choices, so a refresh while it is open cannot change what it lists.
            new LevelEffectDropdown(m_addChoices, AddEffect).Show(m_addEffectButton.worldBound);
        }

        private LevelEffectCard CreateEffectCard(LevelEffectTarget target, int index, int effectIndex,
            LevelEffectData effect)
        {
            string cardName = "effect-" + effectIndex;
            string label = ToLabel(effect.Type);
            bool known = LevelEffectRegistry.TryFind(effect.Type, out LevelEffectDefinition definition);
            bool editable = known && LevelEffectCommands.IsEditable(target, effect.Type);
            string cardKey = GetCardKey(target, effect.Type);
            string tooltip = editable ? DescribeType(effect.Type) : k_readOnlyTooltip;
            LevelEffectCard card = new LevelEffectCard(cardName, label, tooltip, !m_collapsedCards.Contains(cardKey),
                expanded => SetCardExpanded(cardKey, expanded));

            // A board object keeps its one effect, so its card has no Remove.
            if (target != LevelEffectTarget.CellObject)
            {
                Button remove = new Button(() => Request($"Remove {label} Effect",
                    (LevelData level, out LevelData result, out string issue) =>
                        LevelEffectCommands.TryRemoveEffect(level, target, index, effectIndex, out result, out issue)))
                {
                    name = cardName + "-remove",
                    tooltip = $"Remove the {label} effect (one undo step)."
                };
                ActionIcons.ShowDelete(remove, k_removeIconSize);
                card.SetHeaderAction(remove);
            }

            if (editable)
            {
                for (int key = 0; key < definition.KeyCount; key++)
                {
                    card.Add(CreateParamRow(target, index, effectIndex, effect, definition.GetKeyAt(key)));
                }
            }
            else
            {
                for (int param = 0; param < effect.ParamCount; param++)
                {
                    LevelEffectParamData pair = effect.GetParamAt(param);
                    TextField field = CreateReadOnlyField($"{cardName}-param-{param}", ToLabel(pair?.Key));
                    field.SetValueWithoutNotify(pair?.Value ?? string.Empty);
                    card.Add(field);
                }
            }

            return card;
        }

        private void SetCardExpanded(string cardKey, bool expanded)
        {
            if (expanded)
            {
                m_collapsedCards.Remove(cardKey);
            }
            else
            {
                m_collapsedCards.Add(cardKey);
            }
        }

        private static string GetCardKey(LevelEffectTarget target, string type)
        {
            return target + ":" + type;
        }

        // One key of an editable effect; each change is one request. An absent optional key shows its default, marked
        // "default", and a change adds it (a Barrier's Starts open, for example); a present optional key has Remove,
        // which returns it to that default.
        private VisualElement CreateParamRow(LevelEffectTarget target, int index, int effectIndex, LevelEffectData effect,
            LevelEffectKey key)
        {
            string keyName = key.Name;
            string label = ToLabel(keyName);
            string value = effect.FindValue(keyName);
            string defaultValue = LevelEffectCommands.GetDefaultValue(key);
            bool present = value != null;
            string rowName = $"effect-{effectIndex}-{keyName}";
            VisualElement row = CreateRow(rowName + "-row");
            row.style.alignItems = Align.Center;

            VisualElement field = CreateValueField(key, present ? value : defaultValue,
                newValue => Request((present ? "Set " : "Add ") + label,
                    (LevelData level, out LevelData result, out string issue) =>
                        LevelEffectCommands.TrySetParam(level, target, index, effectIndex, keyName, newValue,
                            out result, out issue)));
            field.name = rowName;
            field.tooltip = key.IsRequired
                ? $"{label}: {key.DescribeRule()}."
                : $"{label}: {key.DescribeRule()}. Optional; without it the default {defaultValue} applies.";
            field.style.flexGrow = 1;
            field.style.flexShrink = 1;
            row.Add(field);

            if (key.IsRequired)
            {
                return row;
            }

            if (!present)
            {
                Label marker = new Label("default") { name = rowName + "-default", tooltip = "Not set: a change adds it." };
                marker.style.opacity = 0.6f;
                row.Add(marker);
                return row;
            }

            Button remove = new Button(() => Request($"Remove {label}",
                (LevelData level, out LevelData result, out string issue) =>
                    LevelEffectCommands.TryRemoveParam(level, target, index, effectIndex, keyName, out result,
                        out issue)))
            {
                name = rowName + "-remove",
                text = "Remove",
                tooltip = $"Remove {label}, so its default {defaultValue} applies (one undo step)."
            };
            remove.style.flexShrink = 0;
            row.Add(remove);
            return row;
        }

        // A typed control for one key: a dropdown for an enum, a delayed integer field, a toggle, or delayed text.
        private static VisualElement CreateValueField(LevelEffectKey key, string value, Action<string> changed)
        {
            string label = ToLabel(key.Name);
            if (key.Kind == LevelEffectValueKind.Enum && !key.IsList)
            {
                List<string> choices = new List<string>(key.EnumValueCount);
                for (int index = 0; index < key.EnumValueCount; index++)
                {
                    choices.Add(key.GetEnumValueAt(index));
                }

                DropdownField dropdown = new DropdownField(label, choices, Mathf.Max(0, choices.IndexOf(value)));
                SetLabelWidth(dropdown);
                dropdown.RegisterValueChangedCallback(change => changed(change.newValue));
                return dropdown;
            }

            if (key.Kind == LevelEffectValueKind.Integer && !key.IsList)
            {
                IntegerField number = CreateIntegerField(null, label);
                int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int parsed);
                number.SetValueWithoutNotify(parsed);
                number.RegisterValueChangedCallback(change =>
                    changed(change.newValue.ToString(CultureInfo.InvariantCulture)));
                return number;
            }

            if (key.Kind == LevelEffectValueKind.Bool && !key.IsList)
            {
                Toggle toggle = new Toggle(label);
                SetLabelWidth(toggle);
                toggle.SetValueWithoutNotify(string.Equals(value, "true", StringComparison.Ordinal));
                toggle.RegisterValueChangedCallback(change => changed(change.newValue ? "true" : "false"));
                return toggle;
            }

            TextField text = new TextField(label) { isDelayed = true };
            SetLabelWidth(text);
            text.SetValueWithoutNotify(value ?? string.Empty);
            text.RegisterValueChangedCallback(change => changed(change.newValue));
            return text;
        }

        private static List<string> GetTypes(LevelEffectData[] effects)
        {
            List<string> types = new List<string>(effects.Length);
            for (int index = 0; index < effects.Length; index++)
            {
                if (effects[index] != null)
                {
                    types.Add(effects[index].Type);
                }
            }

            return types;
        }

        private static bool HasEffect(LevelEffectData[] effects, string type)
        {
            for (int index = 0; index < effects.Length; index++)
            {
                if (string.Equals(effects[index]?.Type, type, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        // Titles name the colour and the id the canvas labels (user request of 2026-09-27: "Editing Green B2"). A
        // board object has no colour; its id ("barrier-1") reads as a label ("Barrier 1").
        internal static string GetBlockTitle(LevelBlockData block)
        {
            return "Editing " + GetColorLabel(block.ColorId) + " " + GetCanvasLabel(block.Id);
        }

        internal static string GetGateTitle(LevelDoorData door)
        {
            return "Editing " + GetColorLabel(door.ColorId) + " Gate " + GetCanvasLabel(door.Id);
        }

        internal static string GetObjectTitle(LevelCellObjectData cellObject)
        {
            string type = cellObject.Effect != null ? cellObject.Effect.Type : null;
            string id = cellObject.Id ?? string.Empty;
            // An id named after its type ("barrier-1") already says what it is; any other id keeps the canvas form.
            return !string.IsNullOrEmpty(type) && id.StartsWith(type + "-", StringComparison.Ordinal)
                ? "Editing " + ToLabel(id)
                : "Editing " + (string.IsNullOrEmpty(type) ? "Board object" : ToLabel(type)) + " " + GetCanvasLabel(id);
        }

        // The id as the canvas draws it on the block or gate: upper case.
        private static string GetCanvasLabel(string id)
        {
            return id != null ? id.ToUpperInvariant() : string.Empty;
        }

        // A palette colour id as words ("DarkGreen" reads "Dark Green"); an unknown id is shown as it is.
        private static string GetColorLabel(string colorId)
        {
            return string.IsNullOrEmpty(colorId) ? "Uncoloured" : ObjectNames.NicifyVariableName(colorId);
        }

        private static string DescribeType(string type)
        {
            return LevelEffectRegistry.TryFind(type, out LevelEffectDefinition definition)
                ? $"{ToLabel(type)} ({definition.CatalogId})"
                : $"{type} is not a known effect type.";
        }

        /// <summary>A registry name as a label: "clearsToThaw" reads "Clears to thaw", "moveable-crate" "Moveable crate".</summary>
        internal static string ToLabel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            char[] letters = new char[name.Length * 2];
            int length = 0;
            for (int index = 0; index < name.Length; index++)
            {
                char letter = name[index];
                if (letter == '-')
                {
                    letters[length++] = ' ';
                }
                else if (index == 0)
                {
                    letters[length++] = char.ToUpperInvariant(letter);
                }
                else if (char.IsUpper(letter))
                {
                    letters[length++] = ' ';
                    letters[length++] = char.ToLowerInvariant(letter);
                }
                else
                {
                    letters[length++] = letter;
                }
            }

            return new string(letters, 0, length);
        }

        private static VisualElement CreateRow(string name)
        {
            VisualElement row = new VisualElement { name = name };
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2;
            return row;
        }

        private static TextField CreateReadOnlyField(string name, string label)
        {
            TextField field = new TextField(label) { name = name, isReadOnly = true };
            SetLabelWidth(field);
            return field;
        }

        // Delayed: one commit per edit, so one request and one undo step.
        private static IntegerField CreateIntegerField(string name, string label)
        {
            IntegerField field = new IntegerField(label) { name = name, isDelayed = true };
            SetLabelWidth(field);
            return field;
        }

        private static void SetLabelWidth(VisualElement field)
        {
            Label label = field.Q<Label>(className: BaseField<int>.labelUssClassName);
            if (label != null)
            {
                label.style.minWidth = k_labelWidth;
                label.style.width = k_labelWidth;
            }
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
