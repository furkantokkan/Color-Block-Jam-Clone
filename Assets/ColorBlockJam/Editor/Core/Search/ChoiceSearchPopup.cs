using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Search
{
    /// <summary>
    /// Popup that lists dropdown choices under a search box and reports the choice the user picks.
    /// </summary>
    internal sealed class ChoiceSearchPopup : PopupWindowContent
    {
        private const float k_rowHeight = 20f;
        private const float k_searchBarHeight = 28f;
        private const float k_minWidth = 240f;
        private const int k_maxVisibleRows = 12;

        private readonly List<string> m_choices;
        private readonly List<int> m_matches = new List<int>();
        private readonly int m_currentIndex;
        private readonly float m_width;
        private readonly Action<string> m_onPicked;
        private Action m_close;
        private ToolbarSearchField m_searchField;
        private ListView m_list;

        internal ChoiceSearchPopup(IReadOnlyList<string> choices, int currentIndex, float width,
            Action<string> onPicked)
        {
            m_choices = new List<string>(choices);
            m_currentIndex = currentIndex;
            m_width = Mathf.Max(width, k_minWidth);
            m_onPicked = onPicked;
        }

        /// <summary>
        /// Opens the popup below <paramref name="anchor"/>, given in the owning window's panel space.
        /// </summary>
        public static void Show(Rect anchor, IReadOnlyList<string> choices, int currentIndex,
            Action<string> onPicked)
        {
            UnityEditor.PopupWindow.Show(anchor, new ChoiceSearchPopup(choices, currentIndex, anchor.width, onPicked));
        }

        public override Vector2 GetWindowSize()
        {
            // The height follows the full list, so the popup does not jump while the user types.
            int rows = Mathf.Clamp(m_choices.Count, 1, k_maxVisibleRows);
            return new Vector2(m_width, k_searchBarHeight + rows * k_rowHeight);
        }

        public override void OnGUI(Rect rect)
        {
            // The content is UI Toolkit; there is nothing to draw with IMGUI.
        }

        public override void OnOpen()
        {
            Build(editorWindow.rootVisualElement, editorWindow.Close);
        }

        /// <summary>
        /// Builds the search box and the list into <paramref name="root"/>; <paramref name="close"/> hides the popup.
        /// </summary>
        internal void Build(VisualElement root, Action close)
        {
            m_close = close;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            m_searchField = new ToolbarSearchField();
            m_searchField.style.width = StyleKeyword.Auto;
            m_searchField.RegisterValueChangedCallback(change => ShowMatches(change.newValue));
            // The window receives its panel when it is shown, so focus the box once it is attached.
            m_searchField.RegisterCallback<AttachToPanelEvent>(_ => m_searchField.Focus());
            root.Add(m_searchField);

            m_list = new ListView(m_matches, k_rowHeight, MakeRow, BindRow);
            m_list.style.flexGrow = 1;
            root.Add(m_list);

            ShowMatches(string.Empty);
        }

        /// <summary>
        /// Applies one navigation key. Returns false for keys that belong to the search box.
        /// </summary>
        internal bool HandleKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.DownArrow:
                    Highlight(m_list.selectedIndex + 1);
                    return true;
                case KeyCode.UpArrow:
                    Highlight(m_list.selectedIndex - 1);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Pick(m_list.selectedIndex);
                    return true;
                case KeyCode.Escape:
                    m_close();
                    return true;
                default:
                    return false;
            }
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (!HandleKey(evt.keyCode))
            {
                return;
            }

            // Keep navigation keys away from the text box's own caret and Escape handling.
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void ShowMatches(string query)
        {
            ChoiceSearch.Filter(m_choices, query, m_matches);
            m_list.RefreshItems();

            // An empty query shows the list around the current choice; a typed query starts at the first match.
            int currentRow = m_matches.IndexOf(m_currentIndex);
            Highlight(string.IsNullOrWhiteSpace(query) && currentRow >= 0 ? currentRow : 0);
        }

        private void Highlight(int row)
        {
            if (m_matches.Count == 0)
            {
                m_list.ClearSelection();
                return;
            }

            int clampedRow = Mathf.Clamp(row, 0, m_matches.Count - 1);
            m_list.selectedIndex = clampedRow;
            // Scrolling needs a laid-out list, so it waits for the next layout pass.
            m_list.schedule.Execute(() => m_list.ScrollToItem(clampedRow));
        }

        private void Pick(int row)
        {
            if (row < 0 || row >= m_matches.Count)
            {
                return;
            }

            string choice = m_choices[m_matches[row]];
            // Close first so a dialog opened by the owner's change callback cannot outlive the popup.
            m_close();
            m_onPicked(choice);
        }

        private VisualElement MakeRow()
        {
            Label row = new Label();
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.paddingLeft = 6;
            row.RegisterCallback<ClickEvent>(_ => Pick((int)row.userData));
            return row;
        }

        private void BindRow(VisualElement element, int row)
        {
            element.userData = row;
            ((Label)element).text = m_choices[m_matches[row]];
        }
    }
}
