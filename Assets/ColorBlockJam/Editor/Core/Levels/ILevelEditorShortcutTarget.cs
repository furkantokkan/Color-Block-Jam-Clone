using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// The Level Editor actions its keyboard shortcuts run (<see cref="LevelEditorShortcuts"/>). The window implements
    /// them; tests drive the key map with a fake.
    /// </summary>
    internal interface ILevelEditorShortcutTarget
    {
        /// <summary>True while a validation solution plays on the canvas instead of the draft.</summary>
        bool IsShowingSolution { get; }

        /// <summary>True while the Pen is the tool, so the number keys pick colours instead of tools.</summary>
        bool IsPenActive { get; }

        /// <summary>True when the element is the validation panel or inside it.</summary>
        bool IsInValidationPanel(VisualElement element);

        /// <summary>Shows the solution's next frame, or the previous one, and pauses there.</summary>
        void StepSolution(bool forward);

        /// <summary>Stops a shown solution and shows the draft again.</summary>
        void StopSolutionPlayback();

        void UndoEdit();

        void RedoEdit();

        /// <summary>Picks the colour whose Colors badge shows this 0-based position.</summary>
        void PickColorAtPosition(int position);

        /// <summary>Moves the selected block one cell.</summary>
        void NudgeSelection(Vector2Int step);

        /// <summary>Removes the selected gate or block.</summary>
        void DeleteSelection();

        void RotateSelection(bool clockwise);

        void FlipSelection(bool vertical);

        void SetTool(LevelEditorTool tool);

        /// <summary>Esc: cancels a pending resize, else a drag in progress, else returns to Select.</summary>
        void CancelCurrentAction();

        /// <summary>Frames the whole board on the canvas again.</summary>
        void ResetView();

        /// <summary>Picks the previous (-1) or the next (1) colour in the Colors order.</summary>
        void CycleColor(int step);
    }
}
