using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor's keyboard map (docs/Level-Editor-Design.md, section 2): turns one key press into one window
    /// action through <see cref="ILevelEditorShortcutTarget"/>. Text and number fields keep their own keys. While a
    /// solution shows, ← and → step through its frames; any other key outside the validation panel stops the playback
    /// first and then does its normal work on the restored draft.
    /// </summary>
    internal sealed class LevelEditorShortcuts
    {
        private readonly ILevelEditorShortcutTarget m_target;

        internal LevelEditorShortcuts(ILevelEditorShortcutTarget target)
        {
            m_target = target;
        }

        /// <summary>Runs the shortcut of one key press; true when it ran one, so the caller consumes the key.</summary>
        internal bool Handle(KeyDownEvent evt)
        {
            VisualElement element = evt.target as VisualElement;
            if (m_target.IsShowingSolution)
            {
                if (StepSolutionByKey(evt, element))
                {
                    return true;
                }

                if (m_target.IsInValidationPanel(element))
                {
                    // A focused solution row keeps the playback and its own keys. Esc stops the playback; the other
                    // shortcuts would act on the draft's selection, which the playback hides.
                    if (evt.keyCode != KeyCode.Escape)
                    {
                        return false;
                    }

                    m_target.StopSolutionPlayback();
                    return true;
                }

                m_target.StopSolutionPlayback();
            }

            if (IsInTextField(element))
            {
                return false;
            }

            bool command = evt.ctrlKey || evt.commandKey;
            if (command && !evt.altKey && (evt.keyCode == KeyCode.Z || evt.keyCode == KeyCode.Y))
            {
                if (evt.keyCode == KeyCode.Y || evt.shiftKey)
                    m_target.RedoEdit();
                else
                    m_target.UndoEdit();
                return true;
            }

            if (RunAltGrShortcut(evt))
            {
                return true;
            }

            return evt.keyCode != KeyCode.None && !command && !evt.altKey && RunShortcut(evt.keyCode, evt.shiftKey);
        }

        /// <summary>True when the element is a text or number field, or inside one.</summary>
        internal static bool IsInTextField(VisualElement element)
        {
            for (; element != null; element = element.parent)
            {
                if (element is TextField || element is IntegerField)
                    return true;
            }

            return false;
        }

        // ← and → while a solution shows, outside a text field and without Ctrl, Cmd or Alt, the keys that would nudge a
        // block otherwise: → shows the next frame and ← the previous one. True consumes the key, so it neither nudges a
        // block nor runs another shortcut, and it never stops or ends the playback: unlike the Step control, → does
        // nothing on the cleared board, so a held → can never run on into nudging a block.
        private bool StepSolutionByKey(KeyDownEvent evt, VisualElement element)
        {
            if (evt.ctrlKey || evt.commandKey || evt.altKey || IsInTextField(element))
            {
                return false;
            }

            switch (evt.keyCode)
            {
                case KeyCode.RightArrow:
                    m_target.StepSolution(true);
                    return true;
                case KeyCode.LeftArrow:
                    m_target.StepSolution(false);
                    return true;
                default:
                    return false;
            }
        }

        // Layouts such as Turkish Q type [ and ] with AltGr (AltGr+8 and AltGr+9), which Windows reports as Ctrl+Alt.
        // That key event is no shortcut, so the typed character, which arrives as its own event, cycles the colour. A
        // plain [ or ] arrives as LeftBracket or RightBracket with no modifier and runs through RunShortcut.
        private bool RunAltGrShortcut(KeyDownEvent evt)
        {
            if (!evt.ctrlKey || !evt.altKey || evt.commandKey)
            {
                return false;
            }

            switch (evt.character)
            {
                case '[':
                    m_target.CycleColor(-1);
                    return true;
                case ']':
                    m_target.CycleColor(1);
                    return true;
                default:
                    return false;
            }
        }

        private bool RunShortcut(KeyCode key, bool shift)
        {
            // With the Pen, 1-9, 0 and - pick colours before 1, 2 and 3 can pick tools (user answer of 2026-09-26).
            if (m_target.IsPenActive && LevelColorKeys.TryGetPosition(key, out int colorPosition))
            {
                m_target.PickColorAtPosition(colorPosition);
                return true;
            }

            switch (key)
            {
                case KeyCode.LeftArrow:
                    m_target.NudgeSelection(Vector2Int.left);
                    return true;
                case KeyCode.RightArrow:
                    m_target.NudgeSelection(Vector2Int.right);
                    return true;
                case KeyCode.UpArrow:
                    m_target.NudgeSelection(Vector2Int.up);
                    return true;
                case KeyCode.DownArrow:
                    m_target.NudgeSelection(Vector2Int.down);
                    return true;
                case KeyCode.Delete:
                    m_target.DeleteSelection();
                    return true;
                case KeyCode.R:
                    m_target.RotateSelection(!shift);
                    return true;
                case KeyCode.F:
                    m_target.FlipSelection(shift);
                    return true;
                case KeyCode.Alpha1:
                case KeyCode.Keypad1:
                    m_target.SetTool(LevelEditorTool.Select);
                    return true;
                case KeyCode.Alpha2:
                case KeyCode.Keypad2:
                    m_target.SetTool(LevelEditorTool.Pen);
                    return true;
                case KeyCode.Alpha3:
                case KeyCode.Keypad3:
                    m_target.SetTool(LevelEditorTool.Eraser);
                    return true;
                case KeyCode.Escape:
                    m_target.CancelCurrentAction();
                    return true;
                case KeyCode.Home:
                    m_target.ResetView();
                    return true;
                case KeyCode.LeftBracket:
                    m_target.CycleColor(-1);
                    return true;
                case KeyCode.RightBracket:
                    m_target.CycleColor(1);
                    return true;
                default:
                    return false;
            }
        }
    }
}
