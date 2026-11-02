using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's keyboard map without a window: <see cref="LevelEditorShortcuts"/> runs each key
    /// against a recording <see cref="ILevelEditorShortcutTarget"/>. Covers the AltGr brackets of layouts such as
    /// Turkish Q, Esc and the other keys while a solution shows, Undo and Redo, text fields and the Pen colour keys.
    /// </summary>
    public sealed class LevelEditorShortcutsTests
    {
        private RecordingTarget m_target;
        private LevelEditorShortcuts m_shortcuts;

        [SetUp]
        public void CreateShortcuts()
        {
            m_target = new RecordingTarget();
            m_shortcuts = new LevelEditorShortcuts(m_target);
        }

        [Test]
        public void AltGrBracketsCycleTheColour()
        {
            // Turkish Q types [ and ] as AltGr+8 and AltGr+9; Windows reports AltGr as Ctrl+Alt. The key event comes
            // first and is no shortcut, then the typed character arrives as its own event.
            Assert.That(Press('\0', KeyCode.Alpha8, EventModifiers.Control | EventModifiers.Alt), Is.False);
            Assert.That(Press('[', KeyCode.None, EventModifiers.Control | EventModifiers.Alt), Is.True);
            Assert.That(Press('\0', KeyCode.Alpha9, EventModifiers.Control | EventModifiers.Alt), Is.False);
            Assert.That(Press(']', KeyCode.None, EventModifiers.Control | EventModifiers.Alt), Is.True);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Cycle -1", "Cycle 1" }));
        }

        [Test]
        public void APlainBracketCyclesOnce()
        {
            // A US layout sends LeftBracket, then the [ character with no modifier: only the key event cycles.
            Assert.That(Press('\0', KeyCode.LeftBracket, EventModifiers.None), Is.True);
            Assert.That(Press('[', KeyCode.None, EventModifiers.None), Is.False);
            Assert.That(Press('\0', KeyCode.RightBracket, EventModifiers.None), Is.True);
            Assert.That(Press(']', KeyCode.None, EventModifiers.None), Is.False);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Cycle -1", "Cycle 1" }));
        }

        [Test]
        public void AltGrDigitsPickNoColourWithThePen()
        {
            m_target.IsPenActive = true;

            Assert.That(Press('\0', KeyCode.Alpha8, EventModifiers.Control | EventModifiers.Alt), Is.False);
            Assert.That(m_target.Calls, Is.Empty);
        }

        [Test]
        public void EscInTheValidationPanelStopsThePlaybackOnly()
        {
            m_target.IsShowingSolution = true;
            m_target.InValidationPanel = true;

            Assert.That(Press('\0', KeyCode.Escape, EventModifiers.None), Is.True);
            Assert.That(m_target.Calls, Is.EqualTo(new[] { "StopPlayback" }),
                "Esc on a focused solution row stops the playback and keeps the tool and an armed piece.");
        }

        [Test]
        public void OtherKeysInTheValidationPanelRunNoShortcut()
        {
            m_target.IsShowingSolution = true;
            m_target.InValidationPanel = true;

            Assert.That(Press('\0', KeyCode.Delete, EventModifiers.None), Is.False);
            Assert.That(Press('\0', KeyCode.R, EventModifiers.None), Is.False);
            Assert.That(m_target.Calls, Is.Empty, "The playback hides the selection these keys would change.");
        }

        [Test]
        public void ArrowsStepThePlaybackAndNeverStopIt()
        {
            m_target.IsShowingSolution = true;
            m_target.InValidationPanel = true;

            Assert.That(Press('\0', KeyCode.RightArrow, EventModifiers.None), Is.True);
            m_target.InValidationPanel = false;
            Assert.That(Press('\0', KeyCode.LeftArrow, EventModifiers.None), Is.True);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Step True", "Step False" }));
        }

        [Test]
        public void AKeyOutsideThePanelStopsThePlaybackThenRuns()
        {
            m_target.IsShowingSolution = true;

            Assert.That(Press('\0', KeyCode.Delete, EventModifiers.None), Is.True);
            Assert.That(m_target.Calls, Is.EqualTo(new[] { "StopPlayback", "Delete" }));
        }

        [Test]
        public void EscOutsideAPlaybackCancelsTheCurrentAction()
        {
            Assert.That(Press('\0', KeyCode.Escape, EventModifiers.None), Is.True);
            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Cancel" }));
        }

        [Test]
        public void TextFieldsKeepTheirKeys()
        {
            TextField field = new TextField();

            Assert.That(Press('\0', KeyCode.Delete, EventModifiers.None, field), Is.False);
            Assert.That(Press('\0', KeyCode.Z, EventModifiers.Control, field), Is.False);
            Assert.That(Press('[', KeyCode.None, EventModifiers.Control | EventModifiers.Alt, field), Is.False);
            Assert.That(m_target.Calls, Is.Empty);
        }

        [Test]
        public void UndoAndRedoTakeCtrlZCtrlYAndCtrlShiftZ()
        {
            Assert.That(Press('\0', KeyCode.Z, EventModifiers.Control), Is.True);
            Assert.That(Press('\0', KeyCode.Y, EventModifiers.Control), Is.True);
            Assert.That(Press('\0', KeyCode.Z, EventModifiers.Control | EventModifiers.Shift), Is.True);
            Assert.That(Press('\0', KeyCode.Z, EventModifiers.Control | EventModifiers.Alt), Is.False);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Undo", "Redo", "Redo" }));
        }

        [Test]
        public void NumberKeysPickColoursWithThePenAndToolsWithoutIt()
        {
            Assert.That(Press('\0', KeyCode.Alpha2, EventModifiers.None), Is.True);
            m_target.IsPenActive = true;
            Assert.That(Press('\0', KeyCode.Alpha2, EventModifiers.None), Is.True);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Tool Pen", "Colour 2" }));
        }

        [Test]
        public void TransformKeysTakeShiftForTheOtherDirection()
        {
            Assert.That(Press('\0', KeyCode.R, EventModifiers.None), Is.True);
            Assert.That(Press('\0', KeyCode.R, EventModifiers.Shift), Is.True);
            Assert.That(Press('\0', KeyCode.F, EventModifiers.None), Is.True);
            Assert.That(Press('\0', KeyCode.F, EventModifiers.Shift), Is.True);

            Assert.That(m_target.Calls, Is.EqualTo(new[] { "Rotate True", "Rotate False", "Flip False", "Flip True" }));
        }

        private bool Press(char character, KeyCode key, EventModifiers modifiers, VisualElement target = null)
        {
            using (KeyDownEvent evt = KeyDownEvent.GetPooled(character, key, modifiers))
            {
                evt.target = target;
                return m_shortcuts.Handle(evt);
            }
        }

        private sealed class RecordingTarget : ILevelEditorShortcutTarget
        {
            public readonly List<string> Calls = new List<string>();

            public bool IsShowingSolution { get; set; }
            public bool IsPenActive { get; set; }
            public bool InValidationPanel { get; set; }

            public bool IsInValidationPanel(VisualElement element) => InValidationPanel;

            public void StepSolution(bool forward) => Calls.Add("Step " + forward);
            public void StopSolutionPlayback() => Calls.Add("StopPlayback");
            public void UndoEdit() => Calls.Add("Undo");
            public void RedoEdit() => Calls.Add("Redo");
            public void PickColorAtPosition(int position) => Calls.Add("Colour " + position);
            public void NudgeSelection(Vector2Int step) => Calls.Add("Nudge " + step);
            public void DeleteSelection() => Calls.Add("Delete");
            public void RotateSelection(bool clockwise) => Calls.Add("Rotate " + clockwise);
            public void FlipSelection(bool vertical) => Calls.Add("Flip " + vertical);
            public void SetTool(LevelEditorTool tool) => Calls.Add("Tool " + tool);
            public void CancelCurrentAction() => Calls.Add("Cancel");
            public void ResetView() => Calls.Add("ResetView");
            public void CycleColor(int step) => Calls.Add("Cycle " + step);
        }
    }
}
