using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Controls
{
    /// <summary>
    /// Popup under a database row's + button that asks for a new database name and hands it to the owner on Create,
    /// so the Level and Piece editors need no permanent name row.
    /// </summary>
    internal sealed class DatabaseNamePopup : PopupWindowContent
    {
        private const float k_width = 300f;
        // Title, name field, two lines of error text and the Create button.
        private const float k_height = 112f;
        private const float k_padding = 6f;
        private const float k_labelWidth = 44f;
        private static readonly Color s_errorColor = new Color(0.9f, 0.35f, 0.3f);

        private readonly string m_title;
        private readonly string m_suggestedName;
        private readonly Func<string, string> m_getNameError;
        private readonly Action<string> m_create;
        private Action m_close;
        private TextField m_nameField;
        private Label m_errorLabel;
        private Button m_createButton;

        /// <param name="title">Heading of the popup, such as "New Level Database".</param>
        /// <param name="suggestedName">Name the field starts with, selected so typing replaces it.</param>
        /// <param name="getNameError">Returns why a name cannot be used, or null when it can.</param>
        /// <param name="create">Receives the accepted name after the popup has closed.</param>
        internal DatabaseNamePopup(string title, string suggestedName, Func<string, string> getNameError,
            Action<string> create)
        {
            m_title = title;
            m_suggestedName = suggestedName;
            m_getNameError = getNameError;
            m_create = create;
        }

        /// <summary>
        /// Opens the popup below <paramref name="anchor"/>, the button that asked for it.
        /// </summary>
        public void Show(VisualElement anchor)
        {
            UnityEditor.PopupWindow.Show(anchor.worldBound, this);
        }

        public override Vector2 GetWindowSize()
        {
            return new Vector2(k_width, k_height);
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
        /// Builds the title, the name field, the error line and the Create button into <paramref name="root"/>;
        /// <paramref name="close"/> hides the popup.
        /// </summary>
        internal void Build(VisualElement root, Action close)
        {
            m_close = close;
            root.style.paddingLeft = k_padding;
            root.style.paddingRight = k_padding;
            root.style.paddingTop = k_padding;
            root.style.paddingBottom = k_padding;
            root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            Label title = new Label(m_title) { name = "database-popup-title" };
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            root.Add(title);

            m_nameField = new TextField("Name") { value = m_suggestedName, name = "database-name" };
            m_nameField.labelElement.style.minWidth = k_labelWidth;
            m_nameField.labelElement.style.width = k_labelWidth;
            m_nameField.RegisterValueChangedCallback(change => ShowNameError(change.newValue));
            // The popup receives its panel when it is shown, so focus the field once it is attached. Selecting the
            // suggestion lets the first key press replace it.
            m_nameField.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                m_nameField.Focus();
                m_nameField.SelectAll();
            });
            root.Add(m_nameField);

            // Takes the free height, so the Create button stays at the bottom whether or not an error shows.
            m_errorLabel = new Label { name = "database-name-error" };
            m_errorLabel.style.flexGrow = 1;
            m_errorLabel.style.whiteSpace = WhiteSpace.Normal;
            m_errorLabel.style.color = s_errorColor;
            root.Add(m_errorLabel);

            m_createButton = new Button(Submit) { text = "Create", name = "create" };
            m_createButton.style.alignSelf = Align.FlexEnd;
            m_createButton.style.minWidth = 70;
            root.Add(m_createButton);

            ShowNameError(m_suggestedName);
        }

        /// <summary>
        /// Applies one key: Enter creates when the name is valid, Escape closes. Returns false for keys that belong
        /// to the name field.
        /// </summary>
        internal bool HandleKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    Submit();
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

            // Keep Enter and Escape away from the text field's own handling.
            evt.StopPropagation();
            evt.PreventDefault();
        }

        private void ShowNameError(string databaseName)
        {
            string error = m_getNameError(databaseName);
            m_errorLabel.text = error ?? string.Empty;
            m_createButton.SetEnabled(error == null);
        }

        private void Submit()
        {
            string databaseName = m_nameField.value;
            if (m_getNameError(databaseName) != null)
            {
                return;
            }

            // Close first so a dialog opened by the owner, such as the unsaved-changes prompt, cannot outlive the popup.
            m_close();
            m_create(databaseName);
        }
    }
}
