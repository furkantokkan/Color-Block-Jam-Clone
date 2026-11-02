using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Environment.Editor
{
    /// <summary>
    /// UI Toolkit inspector for EnvironmentRuleSet. Each rule shows its output piece,
    /// a 3x3 neighbour grid whose cells cycle Any, Floor and Void, and its Rotated
    /// toggle, with add, remove and reorder. Every edit goes through the
    /// SerializedObject, so it records Undo and marks the asset dirty.
    /// </summary>
    [CustomEditor(typeof(EnvironmentRuleSet))]
    public sealed class EnvironmentRuleSetEditor : UnityEditor.Editor
    {
        internal const string StyleSheetPath = "Assets/ColorBlockJam/Editor/Environment/EnvironmentRuleSetEditor.uss";
        internal const string RuleClass = "rule-set__rule";
        internal const string CellClass = "rule-set__cell";
        internal const string AnyClass = "rule-set__cell--any";
        internal const string FloorClass = "rule-set__cell--floor";
        internal const string VoidClass = "rule-set__cell--void";
        internal const string CentreClass = "rule-set__cell--centre";
        internal const string StatusClass = "rule-set__status";

        private const string k_rulesProperty = "m_rules";
        private const string k_outputProperty = "m_output";
        private const string k_neighborsProperty = "m_neighbors";
        private const string k_rotatedProperty = "m_isRotated";

        private VisualElement m_rulesContainer;
        private HelpBox m_status;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            StyleSheet styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }

            root.Add(new HelpBox(
                "Rules run in order and the first match places its piece in a void quadrant. Rotated rules are "
                + "also tried turned 90, 180 and 270 degrees clockwise. Click a neighbour to cycle Any, Floor "
                + "and Void. The upper row is the board's +Z side.", HelpBoxMessageType.Info));
            m_status = new HelpBox(string.Empty, HelpBoxMessageType.Error);
            m_status.AddToClassList(StatusClass);
            root.Add(m_status);
            m_rulesContainer = new VisualElement();
            root.Add(m_rulesContainer);
            Button add = new Button(AddRule) { text = "Add rule", tooltip = "Adds an empty rule at the end." };
            add.AddToClassList("rule-set__add");
            root.Add(add);
            RebuildRules();
            // Edits through the bound Output field and Rotated toggle do not rebuild the
            // rules, so the status follows every change of the asset instead.
            // Owner: the root element. Stop point: the tracking binding ends with the
            // root when the inspector's panel releases it.
            root.TrackSerializedObjectValue(serializedObject, _ => RefreshStatus());
            return root;
        }

        // Owner: this inspector. Stop point: OnDisable, when the inspector closes.
        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        internal void CycleNeighbor(int ruleIndex, int neighborIndex)
        {
            serializedObject.Update();
            SerializedProperty neighbor = GetRule(ruleIndex).FindPropertyRelative(k_neighborsProperty)
                .GetArrayElementAtIndex(neighborIndex);
            neighbor.intValue = (int)GetNext((EnvironmentNeighbor)neighbor.intValue);
            serializedObject.ApplyModifiedProperties();
            RebuildRules();
        }

        internal void AddRule()
        {
            serializedObject.Update();
            SerializedProperty rules = serializedObject.FindProperty(k_rulesProperty);
            int index = rules.arraySize;
            rules.InsertArrayElementAtIndex(index);
            SerializedProperty rule = rules.GetArrayElementAtIndex(index);
            rule.FindPropertyRelative(k_outputProperty).objectReferenceValue = null;
            rule.FindPropertyRelative(k_rotatedProperty).boolValue = true;
            SerializedProperty neighbors = rule.FindPropertyRelative(k_neighborsProperty);
            neighbors.arraySize = EnvironmentRuleSet.NeighborCount;
            for (int neighbor = 0; neighbor < EnvironmentRuleSet.NeighborCount; neighbor++)
            {
                neighbors.GetArrayElementAtIndex(neighbor).intValue = (int)EnvironmentNeighbor.Any;
            }

            serializedObject.ApplyModifiedProperties();
            RebuildRules();
        }

        internal void RemoveRule(int ruleIndex)
        {
            serializedObject.Update();
            serializedObject.FindProperty(k_rulesProperty).DeleteArrayElementAtIndex(ruleIndex);
            serializedObject.ApplyModifiedProperties();
            RebuildRules();
        }

        internal void MoveRule(int fromIndex, int toIndex)
        {
            serializedObject.Update();
            serializedObject.FindProperty(k_rulesProperty).MoveArrayElement(fromIndex, toIndex);
            serializedObject.ApplyModifiedProperties();
            RebuildRules();
        }

        internal static EnvironmentNeighbor GetNext(EnvironmentNeighbor neighbor)
        {
            switch (neighbor)
            {
                case EnvironmentNeighbor.Any: return EnvironmentNeighbor.Floor;
                case EnvironmentNeighbor.Floor: return EnvironmentNeighbor.Void;
                default: return EnvironmentNeighbor.Any;
            }
        }

        internal static string GetCellClass(EnvironmentNeighbor neighbor)
        {
            switch (neighbor)
            {
                case EnvironmentNeighbor.Floor: return FloorClass;
                case EnvironmentNeighbor.Void: return VoidClass;
                default: return AnyClass;
            }
        }

        private void OnUndoRedo()
        {
            if (m_rulesContainer == null)
            {
                return;
            }

            serializedObject.Update();
            RebuildRules();
        }

        private SerializedProperty GetRule(int ruleIndex)
        {
            return serializedObject.FindProperty(k_rulesProperty).GetArrayElementAtIndex(ruleIndex);
        }

        private void RebuildRules()
        {
            if (m_rulesContainer == null)
            {
                return;
            }

            m_rulesContainer.Clear();
            SerializedProperty rules = serializedObject.FindProperty(k_rulesProperty);
            for (int index = 0; index < rules.arraySize; index++)
            {
                m_rulesContainer.Add(CreateRule(index, rules.arraySize, rules.GetArrayElementAtIndex(index)));
            }

            m_rulesContainer.Bind(serializedObject);
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (m_status == null || target == null)
            {
                return;
            }

            bool isValid = ((EnvironmentRuleSet)target).Validate(out string error);
            m_status.text = error;
            m_status.style.display = isValid ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private VisualElement CreateRule(int index, int count, SerializedProperty rule)
        {
            VisualElement element = new VisualElement();
            element.AddToClassList(RuleClass);

            VisualElement header = new VisualElement();
            header.AddToClassList("rule-set__header");
            Label title = new Label($"Rule {index + 1}");
            title.AddToClassList("rule-set__title");
            header.Add(title);
            header.Add(CreateHeaderButton("▲", "Move this rule up (it is tried earlier).",
                () => MoveRule(index, index - 1), index > 0));
            header.Add(CreateHeaderButton("▼", "Move this rule down (it is tried later).",
                () => MoveRule(index, index + 1), index < count - 1));
            header.Add(CreateHeaderButton("✕", "Remove this rule.", () => RemoveRule(index), true));
            element.Add(header);

            VisualElement body = new VisualElement();
            body.AddToClassList("rule-set__body");
            body.Add(CreateGrid(index, rule));
            VisualElement fields = new VisualElement();
            fields.AddToClassList("rule-set__fields");
            fields.Add(new PropertyField(rule.FindPropertyRelative(k_outputProperty), "Output piece")
            {
                tooltip = "The piece prefab placed in a matching void quadrant."
            });
            Toggle rotated = new Toggle("Rotated")
            {
                tooltip = "Also try this rule turned 90, 180 and 270 degrees clockwise; the angle becomes the piece yaw."
            };
            rotated.BindProperty(rule.FindPropertyRelative(k_rotatedProperty));
            fields.Add(rotated);
            body.Add(fields);
            element.Add(body);
            return element;
        }

        private VisualElement CreateGrid(int ruleIndex, SerializedProperty rule)
        {
            SerializedProperty neighbors = rule.FindPropertyRelative(k_neighborsProperty);
            VisualElement grid = new VisualElement();
            grid.AddToClassList("rule-set__grid");
            for (int row = 0; row < 3; row++)
            {
                VisualElement line = new VisualElement();
                line.AddToClassList("rule-set__row");
                for (int column = 0; column < 3; column++)
                {
                    int neighborIndex = GetNeighborIndex(row, column);
                    if (neighborIndex < 0)
                    {
                        Label centre = new Label("Piece") { tooltip = "The void quadrant that receives the output piece." };
                        centre.AddToClassList(CellClass);
                        centre.AddToClassList(CentreClass);
                        line.Add(centre);
                        continue;
                    }

                    EnvironmentNeighbor value = neighborIndex < neighbors.arraySize
                        ? (EnvironmentNeighbor)neighbors.GetArrayElementAtIndex(neighborIndex).intValue
                        : EnvironmentNeighbor.Any;
                    Button cell = new Button(() => CycleNeighbor(ruleIndex, neighborIndex))
                    {
                        text = value.ToString(),
                        tooltip = $"{GetDirectionName(neighborIndex)} neighbour: {value}. "
                            + "Click to cycle Any → Floor → Void."
                    };
                    cell.AddToClassList(CellClass);
                    cell.AddToClassList(GetCellClass(value));
                    line.Add(cell);
                }

                grid.Add(line);
            }

            return grid;
        }

        private static Button CreateHeaderButton(string text, string tooltip, System.Action action, bool isEnabled)
        {
            Button button = new Button(action) { text = text, tooltip = tooltip };
            button.AddToClassList("rule-set__header-button");
            button.SetEnabled(isEnabled);
            return button;
        }

        // Grid row 0 is the upper row (+Z); the centre (1, 1) has no neighbour index.
        // Built from EnvironmentRuleSet.GetNeighborOffset (the serialized m_neighbors
        // order) so the grid layout cannot drift from the runtime's neighbour order.
        private static readonly int[,] s_neighborIndexByCell = BuildNeighborIndexByCell();

        private static int GetNeighborIndex(int row, int column) => s_neighborIndexByCell[row, column];

        private static int[,] BuildNeighborIndexByCell()
        {
            int[,] table = { { -1, -1, -1 }, { -1, -1, -1 }, { -1, -1, -1 } };
            for (int index = 0; index < EnvironmentRuleSet.NeighborCount; index++)
            {
                Vector2Int offset = EnvironmentRuleSet.GetNeighborOffset(index);
                table[1 - offset.y, offset.x + 1] = index;
            }

            return table;
        }

        private static string GetDirectionName(int neighborIndex)
        {
            Vector2Int offset = EnvironmentRuleSet.GetNeighborOffset(neighborIndex);
            string vertical = offset.y > 0 ? "Upper" : offset.y < 0 ? "Lower" : string.Empty;
            string horizontal = offset.x < 0 ? "left" : offset.x > 0 ? "right" : string.Empty;
            if (vertical.Length == 0)
            {
                return char.ToUpperInvariant(horizontal[0]) + horizontal.Substring(1);
            }

            return horizontal.Length == 0 ? vertical : vertical + "-" + horizontal;
        }
    }
}
