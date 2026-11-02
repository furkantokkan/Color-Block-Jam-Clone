using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the lean tools column of the Level Editor (user requests of 2026-09-25): the transform tools and
    /// their divider stay shown whatever is selected, and the Pen tooltip carries the gate help the Doors group held.
    /// </summary>
    public sealed class LevelEditorToolsCleanupTests
    {
        private LevelEditorWindowFixture m_fixture;
        private LevelDatabaseWindow m_window;

        [TearDown]
        public void CloseWindow()
        {
            m_fixture?.Dispose();
            m_fixture = null;
            m_window = null;
        }

        [Test]
        public void TransformRowAndDividerStayShownWithoutASelectedBlockOrAnArmedPiece()
        {
            // Supersedes this task's AC-3 (TASK-20260925-142546927-22e06638-level-editor-transform-tools): the
            // transforms are tools that act on the clicked block, so they no longer wait for a target.
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(canvas.Model.DoorCount, Is.GreaterThan(0), "The fixture level needs a door.");
            LevelCanvasSelection door = LevelCanvasSelection.ForDoor(0, canvas.Model.GetDoor(0).Id);
            VisualElement actions = root.Q("block-actions");
            VisualElement divider = root.Q("tools-divider");

            Invoke("SelectOnCanvas", door);
            AssertShown(actions, divider, "A selected gate leaves the transform tools shown.");

            DropdownField blockChoice = root.Q<Foldout>("blocks-group").Q<DropdownField>();
            blockChoice.value = blockChoice.choices[0];
            AssertShown(actions, divider, "A selected block keeps them shown.");

            LevelShapeChoice piece = FindTemplate();
            Assume.That(piece, Is.Not.Null, "The piece library has a template.");
            Invoke("ArmShape", piece);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "A piece is placed with Select (2026-09-26).");
            AssertShown(actions, divider, "An armed piece keeps them shown.");

            Invoke("SetTool", LevelEditorTool.Select);
            Invoke("SelectOnCanvas", door);
            AssertShown(actions, divider, "Nothing armed and no block selected still shows them.");
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void PenTooltipCarriesTheGateHelp()
        {
            VisualElement root = OpenWindow();
            string tooltip = root.Q<Button>("tool-pen").tooltip;

            Assert.That(tooltip, Does.Contain("wall ring to paint a gate (1 to 3 cells)"));
            // A Pen right click takes one gate cell off (TASK-20260926-212320311-949d4415).
            Assert.That(tooltip, Does.Contain("A right click on a gate takes one gate cell off; the Eraser removes the whole gate"));
            Assert.That(tooltip, Does.Contain("a swatch recolours it"));
        }

        private static void AssertShown(VisualElement actions, VisualElement divider, string message)
        {
            Assert.That(actions.style.display.value, Is.EqualTo(DisplayStyle.Flex), message);
            Assert.That(divider.style.display.value, Is.EqualTo(DisplayStyle.Flex), message);
        }

        // Any template can be armed (DEC-193: templates carry no prefab).
        private LevelShapeChoice FindTemplate()
        {
            var catalog = (LevelShapeCatalog)LevelEditorTestAccess.Get(m_window, "m_shapeCatalog");
            return catalog.Count > 0 ? catalog.Get(0) : null;
        }

        private void Invoke(string method, object argument)
        {
            LevelEditorTestAccess.Invoke(m_window, method, new[] { argument });
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open();
            m_window = m_fixture.Window;
            // A fixture level with a door and one block, deterministic whatever the real project's active database
            // holds (Tests-Editor-1-F3); the block lets the "selected block" case pick a real block entry.
            m_fixture.LoadDatabase(new LevelData("cleanup", "Cleanup", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Red") },
                new[] { new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), new[] { Vector2Int.zero }) }));
            return root;
        }
    }
}
