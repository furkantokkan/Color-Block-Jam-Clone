using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Controls
{
    /// <summary>
    /// Row that shows a DropdownField with Up and Down buttons that move it to the previous or next choice,
    /// followed by optional small action buttons such as New and Delete.
    /// </summary>
    internal sealed class ChoiceStepper : FieldActionsRow
    {
        private readonly DropdownField m_field;
        private readonly Button m_previousButton;
        private readonly Button m_nextButton;

        public ChoiceStepper(DropdownField field, string previousTooltip, string nextTooltip) : base(field)
        {
            m_field = field;
            m_previousButton = CreateButton("▲", previousTooltip, () => Step(-1));
            Add(m_previousButton);
            m_nextButton = CreateButton("▼", nextTooltip, () => Step(1));
            Add(m_nextButton);
        }

        /// <summary>
        /// Enables the dropdown and its Up and Down buttons. Action buttons keep their own state, so
        /// New stays usable while an empty list is locked.
        /// </summary>
        public void SetChoicesEnabled(bool enabled)
        {
            m_field.SetEnabled(enabled);
            m_previousButton.SetEnabled(enabled);
            m_nextButton.SetEnabled(enabled);
        }

        /// <summary>
        /// Moves the field by <paramref name="offset"/> choices. Setting the value runs the field's change
        /// callbacks, so unsaved-draft prompts still decide whether the move happens.
        /// </summary>
        internal void Step(int offset)
        {
            int next = m_field.index + offset;
            if (next < 0 || next >= m_field.choices.Count)
            {
                return;
            }

            m_field.value = m_field.choices[next];
        }
    }
}
