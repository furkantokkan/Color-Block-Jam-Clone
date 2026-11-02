using System.Collections.Generic;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Boosters
{
    /// <summary>
    /// Inspector for the booster restriction matrix (DEC-171): one row per target kind that a targeted booster
    /// definition uses, labelled with those boosters, one column per block effect, and a toggle per cell where checked
    /// means "allowed" and unchecked closes the cell. Rows are read from the project's booster definitions and redrawn
    /// on every project change, so a new booster appears without a code change. Each change is one Undo step.
    /// </summary>
    [CustomEditor(typeof(BoosterRestrictionMatrix))]
    public sealed class BoosterRestrictionMatrixEditor : UnityEditor.Editor
    {
        public const string GridName = "booster-restriction-grid";
        public const string StatusName = "booster-restriction-status";

        private const float k_rowLabelWidth = 190f;
        private const float k_columnWidth = 70f;

        private static readonly string[] s_helpLines =
        {
            "Checked: the booster can target blocks with this effect.",
            "Unchecked: the booster skips those blocks.",
            "Rows follow the booster definitions in the project."
        };

        private VisualElement m_grid;
        private Label m_status;

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = new VisualElement();
            foreach (string line in s_helpLines)
            {
                root.Add(new Label(line));
            }

            root[root.childCount - 1].style.marginBottom = 6;

            m_grid = new VisualElement { name = GridName };
            root.Add(m_grid);

            m_status = new Label { name = StatusName };
            m_status.style.whiteSpace = WhiteSpace.Normal;
            m_status.style.marginTop = 6;
            root.Add(m_status);

            // Undo and redo change the serialized list; a new, deleted or edited booster definition changes the rows.
            // The project callback lives only while the Inspector is shown.
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.RegisterCallback<AttachToPanelEvent>(_ => EditorApplication.projectChanged += Refresh);
            root.RegisterCallback<DetachFromPanelEvent>(_ => EditorApplication.projectChanged -= Refresh);
            Refresh();
            return root;
        }

        /// <summary>The effect columns: every runtime block effect, then any block effect the matrix already names.</summary>
        public static List<string> GetColumns(BoosterRestrictionMatrix matrix)
        {
            List<string> columns = new List<string>();
            for (int i = 0; i < LevelEffectRegistry.DefinitionCount; i++)
            {
                LevelEffectDefinition definition = LevelEffectRegistry.GetDefinitionAt(i);
                if (definition.Target == LevelEffectTarget.Block && definition.IsRuntimeImplemented)
                {
                    columns.Add(definition.TypeId);
                }
            }

            for (int i = 0; i < matrix.Count; i++)
            {
                string typeId = matrix.GetEffectTypeIdAt(i);
                if (!columns.Contains(typeId) && BoosterRestrictionMatrix.IsRestrictableEffect(typeId))
                {
                    columns.Add(typeId);
                }
            }

            return columns;
        }

        /// <summary>
        /// The rows in target kind order: every kind a targeted booster definition uses, plus any kind the matrix still
        /// names, so a restriction never hides after its booster is removed. Each row lists its boosters' names.
        /// </summary>
        public static SortedDictionary<BoosterTargetKind, List<string>> GetRows(BoosterRestrictionMatrix matrix)
        {
            SortedDictionary<BoosterTargetKind, List<string>> rows = new SortedDictionary<BoosterTargetKind, List<string>>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TargetedBoosterDefinition)))
            {
                TargetedBoosterDefinition definition =
                    AssetDatabase.LoadAssetAtPath<TargetedBoosterDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (definition == null || definition.TargetKind == BoosterTargetKind.None)
                {
                    continue;
                }

                GetRow(rows, definition.TargetKind).Add(ObjectNames.NicifyVariableName(definition.Id.ToString()));
            }

            for (int i = 0; i < matrix.Count; i++)
            {
                GetRow(rows, matrix.GetTargetAt(i));
            }

            foreach (List<string> names in rows.Values)
            {
                names.Sort();
            }

            return rows;
        }

        /// <summary>The row label, such as "Rocket, Starting Rocket (Cell)".</summary>
        public static string GetRowLabel(BoosterTargetKind target, List<string> boosters)
        {
            string names = boosters.Count > 0 ? string.Join(", ", boosters) : "No booster";
            return names + " (" + ObjectNames.NicifyVariableName(target.ToString()) + ")";
        }

        private void Refresh()
        {
            BoosterRestrictionMatrix matrix = (BoosterRestrictionMatrix)target;
            if (matrix == null || m_grid == null)
            {
                return;
            }

            m_grid.Clear();
            List<string> columns = GetColumns(matrix);

            VisualElement header = CreateRow();
            header.Add(CreateCell(string.Empty, k_rowLabelWidth));
            foreach (string column in columns)
            {
                Label label = CreateCell(ObjectNames.NicifyVariableName(column), k_columnWidth);
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.unityTextAlign = TextAnchor.MiddleCenter;
                header.Add(label);
            }

            m_grid.Add(header);

            foreach (KeyValuePair<BoosterTargetKind, List<string>> pair in GetRows(matrix))
            {
                BoosterTargetKind row = pair.Key;
                bool restrictable = BoosterRestrictionMatrix.IsRestrictableTarget(row);
                VisualElement line = CreateRow();
                Label rowLabel = CreateCell(GetRowLabel(row, pair.Value), k_rowLabelWidth);
                if (!restrictable)
                {
                    rowLabel.tooltip = "This target kind has no board command yet, so it cannot be restricted.";
                }

                line.Add(rowLabel);
                foreach (string column in columns)
                {
                    Toggle toggle = new Toggle { name = GridName + "-" + row + "-" + column };
                    toggle.style.width = k_columnWidth;
                    toggle.style.justifyContent = Justify.Center;
                    toggle.SetValueWithoutNotify(!matrix.IsRestricted(row, column));
                    toggle.SetEnabled(restrictable);
                    toggle.tooltip = ObjectNames.NicifyVariableName(row.ToString()) + " on " + column;
                    BoosterTargetKind rowKind = row;
                    string effect = column;
                    toggle.RegisterValueChangedCallback(evt => SetAllowed(matrix, rowKind, effect, evt.newValue));
                    line.Add(toggle);
                }

                m_grid.Add(line);
            }

            m_status.text = matrix.Validate(out string error) ? string.Empty : error;
        }

        private static void SetAllowed(BoosterRestrictionMatrix matrix, BoosterTargetKind row, string effect, bool allowed)
        {
            Undo.RecordObject(matrix, allowed ? "Allow Booster Target" : "Restrict Booster Target");
            matrix.SetRestricted(row, effect, !allowed);
            EditorUtility.SetDirty(matrix);
        }

        private static List<string> GetRow(SortedDictionary<BoosterTargetKind, List<string>> rows, BoosterTargetKind target)
        {
            if (!rows.TryGetValue(target, out List<string> names))
            {
                names = new List<string>();
                rows.Add(target, names);
            }

            return names;
        }

        private static VisualElement CreateRow()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 20;
            return row;
        }

        private static Label CreateCell(string text, float width)
        {
            Label label = new Label(text);
            label.style.width = width;
            label.style.whiteSpace = WhiteSpace.Normal;
            return label;
        }
    }
}
