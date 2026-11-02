using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's question before it leaves a level with unsaved changes: Cancel (also what Esc and the
    /// close box answer) keeps the draft and the Target Level choice, Discard opens the chosen level, and the Target
    /// Level field shows the level that opened even when the choices were rebuilt inside the question, as a Save on the
    /// way out does. No dialog opens: the question is replaced, and nothing is written.
    /// </summary>
    public sealed class LevelLeaveDraftTests
    {
        private LevelEditorWindowFixture m_fixture;

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
        }

        [Test]
        public void CancelKeepsTheDraftAndTheTargetLevel()
        {
            VisualElement root = OpenWithThreeLevels();
            int asked = 0;
            m_fixture.Window.ChooseLeaveDraft = () =>
            {
                asked++;
                return LevelLeaveChoice.Cancel;
            };
            int edited = EditTimer(root);
            DropdownField target = GetTargetField();

            target.value = target.choices[2];

            Assert.That(asked, Is.EqualTo(1));
            Assert.That(target.index, Is.Zero, "The field goes back to the open level.");
            Assert.That(m_fixture.GetDraft().Name, Is.EqualTo("One"));
            Assert.That(m_fixture.GetDraft().TimerSeconds, Is.EqualTo(edited), "The unsaved edit stays.");
        }

        [Test]
        public void DiscardOpensTheChosenLevel()
        {
            VisualElement root = OpenWithThreeLevels();
            m_fixture.Window.ChooseLeaveDraft = () => LevelLeaveChoice.Discard;
            EditTimer(root);
            DropdownField target = GetTargetField();

            target.value = target.choices[2];

            Assert.That(target.index, Is.EqualTo(2));
            Assert.That(m_fixture.GetDraft().Name, Is.EqualTo("Three"));
            Assert.That(m_fixture.Window.hasUnsavedChanges, Is.False, "The edit of the level it left is dropped.");
        }

        [Test]
        public void TheFieldShowsTheOpenedLevelAfterTheChoicesWereRebuiltOnTheWayOut()
        {
            VisualElement root = OpenWithThreeLevels();
            LevelDatabaseWindow window = m_fixture.Window;
            window.ChooseLeaveDraft = () =>
            {
                // What a Save answer does after its write (OnSaveProgress): the choices are rebuilt while the level it
                // leaves is still selected, inside the Target Level change.
                LevelEditorTestAccess.Set(window, "m_refreshing", true);
                LevelEditorTestAccess.Invoke(window, "UpdateLevelChoices", null);
                LevelEditorTestAccess.Set(window, "m_refreshing", false);
                return LevelLeaveChoice.Discard;
            };
            EditTimer(root);
            DropdownField target = GetTargetField();

            target.value = target.choices[2];

            Assert.That(m_fixture.GetDraft().Name, Is.EqualTo("Three"));
            Assert.That(target.value, Is.EqualTo(target.choices[2]), "The field names the level that opened.");
        }

        [Test]
        public void ALevelWithoutChangesIsLeftWithoutAsking()
        {
            OpenWithThreeLevels();
            int asked = 0;
            m_fixture.Window.ChooseLeaveDraft = () =>
            {
                asked++;
                return LevelLeaveChoice.Cancel;
            };
            DropdownField target = GetTargetField();

            target.value = target.choices[1];

            Assert.That(asked, Is.Zero);
            Assert.That(m_fixture.GetDraft().Name, Is.EqualTo("Two"));
        }

        private VisualElement OpenWithThreeLevels()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open();
            m_fixture.LoadDatabase(Level("level-01", "One"), Level("level-02", "Two"), Level("level-03", "Three"));
            Assume.That(m_fixture.GetDraft().Name, Is.EqualTo("One"), "The fixture database opens its first level.");
            return root;
        }

        // An unsaved change: the timer of the open level, one second off.
        private static int EditTimer(VisualElement root)
        {
            IntegerField timer = root.Q<IntegerField>("timer-seconds");
            int edited = timer.value == 60 ? 61 : 60;
            timer.value = edited;
            return edited;
        }

        private DropdownField GetTargetField()
        {
            return LevelEditorTestAccess.Get<DropdownField>(m_fixture.Window, "m_targetField");
        }

        private static LevelData Level(string id, string name)
        {
            return new LevelData(id, name, 4, 4, 60, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }
    }
}
