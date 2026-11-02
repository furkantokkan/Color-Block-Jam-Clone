using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor's issues list under the canvas: a collapsible "Issues (n)" foldout with one row per draft
    /// issue, errors first. Clicking a row raises <see cref="IssueSelected"/> so the canvas outlines that issue.
    /// </summary>
    internal sealed class LevelEditorIssuesList : VisualElement
    {
        internal const string k_name = "issues-list";
        private const float k_rowHeight = 18f;
        private const float k_maxListHeight = 120f;
        private static readonly Color s_errorColor = new Color(0.95f, 0.4f, 0.35f);
        private static readonly Color s_warningColor = new Color(0.95f, 0.75f, 0.3f);

        private readonly List<LevelIssue> m_issues = new List<LevelIssue>();
        private readonly Foldout m_foldout;
        private readonly ListView m_list;

        public LevelEditorIssuesList()
        {
            name = k_name;
            style.flexShrink = 0;
            m_foldout = new Foldout { name = "issues-foldout", value = true };
            Add(m_foldout);

            // Owner: this list; stop point: it is dropped with the window GUI.
            m_list = new ListView(m_issues, k_rowHeight, CreateRow, BindRow)
            {
                name = "issues-rows",
                selectionType = SelectionType.Single
            };
            m_list.style.maxHeight = k_maxListHeight;
            m_list.selectionChanged += OnSelectionChanged;
            m_foldout.Add(m_list);
            SetIssues(Array.Empty<LevelIssue>());
        }

        /// <summary>Raised when a row is picked, with its issue, or null when the selection clears. Subscriber: the window.</summary>
        public event Action<LevelIssue> IssueSelected;

        internal int IssueCount => m_issues.Count;

        internal LevelIssue GetIssue(int index)
        {
            return m_issues[index];
        }

        /// <summary>Shows a new issue list; the previous selection and highlight clear.</summary>
        public void SetIssues(IReadOnlyList<LevelIssue> issues)
        {
            m_issues.Clear();
            int errors = 0;
            for (int index = 0; issues != null && index < issues.Count; index++)
            {
                m_issues.Add(issues[index]);
                errors += issues[index].Severity == LevelIssueSeverity.Error ? 1 : 0;
            }

            m_foldout.text = m_issues.Count == 0
                ? "Issues (none)"
                : errors > 0 ? $"Issues ({m_issues.Count}, {errors} blocking Save)" : $"Issues ({m_issues.Count})";
            m_list.style.display = m_issues.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            m_list.ClearSelection();
            m_list.Rebuild();
        }

        /// <summary>Picks one row as a click would; tests and keyboard users share this path.</summary>
        internal void Select(int index)
        {
            m_list.SetSelection(index);
        }

        private static VisualElement CreateRow()
        {
            Label row = new Label { pickingMode = PickingMode.Position };
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.whiteSpace = WhiteSpace.NoWrap;
            row.style.overflow = Overflow.Hidden;
            row.style.textOverflow = TextOverflow.Ellipsis;
            row.style.paddingLeft = 4;
            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            LevelIssue issue = m_issues[index];
            Label row = (Label)element;
            bool error = issue.Severity == LevelIssueSeverity.Error;
            row.text = (error ? "✖ " : "⚠ ") + issue.Message;
            row.tooltip = issue.Message;
            row.style.color = error ? s_errorColor : s_warningColor;
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            LevelIssue picked = null;
            foreach (object item in selection)
            {
                picked = item as LevelIssue;
                break;
            }

            IssueSelected?.Invoke(picked);
        }
    }
}
