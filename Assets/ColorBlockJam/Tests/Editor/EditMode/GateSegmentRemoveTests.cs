using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that a Pen right click on a gate takes only the clicked segment off while the Eraser, and a right click
    /// with any other tool, removes the whole gate: the pure <see cref="LevelEditCommands.TryRemoveDoorSegment"/> table
    /// (ends, middle split, one-cell gate, effects, new id, refusals) and the canvas-to-window routing through real
    /// pointer events with one undo step (TASK-20260926-212320311-949d4415-level-editor-pen-gate-segment-remove).
    /// </summary>
    public sealed class GateSegmentRemoveTests
    {
        private const string k_undoName = "Remove Gate Part";

        // No gate effect type is registered (DEC-177); the pure command copies a door's effect list without
        // validating it, so a placeholder type shows that the list travels with the gate's id.
        private const string k_gateEffectType = "sample-gate-effect";
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
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
        public void EndSegmentsShrinkAThreeCellGateAndKeepItsIdColourAndEffects()
        {
            LevelData level = CreateLevel();
            string before = JsonUtility.ToJson(level);

            LevelData first = Remove(level, 0, BoardSide.Top, 1);
            Assert.That(first.DoorCount, Is.EqualTo(2));
            AssertDoor(first.GetDoorAt(0), "d1", BoardSide.Top, 2, 2, "Red", 1);
            AssertDoor(first.GetDoorAt(1), "d2", BoardSide.Left, 1, 2, "Blue", 0);

            LevelData last = Remove(level, 0, BoardSide.Top, 3);
            Assert.That(last.DoorCount, Is.EqualTo(2));
            AssertDoor(last.GetDoorAt(0), "d1", BoardSide.Top, 1, 2, "Red", 1);
            AssertDoor(last.GetDoorAt(1), "d2", BoardSide.Left, 1, 2, "Blue", 0);
            Assert.That(last.GetDoorAt(0).GetEffectAt(0).Type, Is.EqualTo(k_gateEffectType), "The effect travels with the id.");

            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "The source level keeps its gates.");
        }

        [Test]
        public void AMiddleSegmentSplitsTheGateAndOnlyThePartBeforeItKeepsTheIdAndEffects()
        {
            LevelData level = CreateLevel();
            string nextId = LevelEditCommands.GetNextDoorId(level);

            LevelData split = Remove(level, 0, BoardSide.Top, 2);

            Assert.That(nextId, Is.EqualTo("d3"));
            Assert.That(split.DoorCount, Is.EqualTo(3), "Two gates of the same colour replace the one.");
            AssertDoor(split.GetDoorAt(0), "d1", BoardSide.Top, 1, 1, "Red", 1);
            AssertDoor(split.GetDoorAt(1), "d2", BoardSide.Left, 1, 2, "Blue", 0);
            AssertDoor(split.GetDoorAt(2), nextId, BoardSide.Top, 3, 1, "Red", 0);

            // The new part takes the lowest free id, not the count plus one.
            LevelData gap = new LevelData("gap", "Gap", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 3, "Red"),
                new LevelDoorData("d3", BoardSide.Left, 1, 1, "Blue")
            }, new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });
            LevelData gapSplit = Remove(gap, 0, BoardSide.Top, 2);
            AssertDoor(gapSplit.GetDoorAt(0), "d1", BoardSide.Top, 1, 1, "Red", 0);
            AssertDoor(gapSplit.GetDoorAt(1), "d3", BoardSide.Left, 1, 1, "Blue", 0);
            AssertDoor(gapSplit.GetDoorAt(2), "d2", BoardSide.Top, 3, 1, "Red", 0);
        }

        [Test]
        public void EitherEndOfATwoCellGateLeavesTheOtherCell()
        {
            LevelData level = CreateLevel();

            AssertDoor(Remove(level, 1, BoardSide.Left, 1).GetDoorAt(1), "d2", BoardSide.Left, 2, 1, "Blue", 0);
            AssertDoor(Remove(level, 1, BoardSide.Left, 2).GetDoorAt(1), "d2", BoardSide.Left, 1, 1, "Blue", 0);
            Assert.That(Remove(level, 1, BoardSide.Left, 2).GetDoorAt(0).Length, Is.EqualTo(3), "The other gate stays.");
        }

        [Test]
        public void TheOnlyCellOfAOneCellGateRemovesTheGateAndTheOthersKeepTheirOrder()
        {
            LevelData level = new LevelData("single", "Single", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red"),
                new LevelDoorData("d2", BoardSide.Right, 3, 1, "Blue", new[] { GateEffect() }),
                new LevelDoorData("d3", BoardSide.Bottom, 0, 1, "Red")
            }, new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });

            LevelData removed = Remove(level, 1, BoardSide.Right, 3);

            Assert.That(removed.DoorCount, Is.EqualTo(2));
            AssertDoor(removed.GetDoorAt(0), "d1", BoardSide.Top, 1, 2, "Red", 0);
            AssertDoor(removed.GetDoorAt(1), "d3", BoardSide.Bottom, 0, 1, "Red", 0);
            Assert.That(removed.BlockCount, Is.EqualTo(1), "Blocks stay.");
        }

        [Test]
        public void AStaleIndexAnotherSideOrASegmentOutsideTheGateIsRefusedAndChangesNothing()
        {
            LevelData level = CreateLevel();
            string before = JsonUtility.ToJson(level);

            AssertRefused(level, 2, BoardSide.Top, 2, "Select a door first.");
            AssertRefused(level, -1, BoardSide.Top, 2, "Select a door first.");
            AssertRefused(level, 0, BoardSide.Left, 2, "Door d1 does not hold the Left edge at 2.");
            AssertRefused(level, 0, BoardSide.Top, 0, "Door d1 does not hold the Top edge at 0.");
            AssertRefused(level, 0, BoardSide.Top, 4, "Door d1 does not hold the Top edge at 4.");
            AssertRefused(level, 1, BoardSide.Left, 3, "Door d2 does not hold the Left edge at 3.");
            AssertRefused(null, 0, BoardSide.Top, 1, "Select a level first.");
            Assert.That(JsonUtility.ToJson(level), Is.EqualTo(before), "A refusal leaves the level unchanged.");
        }

        [UnityTest]
        public IEnumerator PenRightClickTakesOnlyTheClickedGateCellOffAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            m_fixture.LoadDatabase();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, new LevelData("gates", "Gates", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 3, "Red"),
                new LevelDoorData("d2", BoardSide.Left, 2, 1, "Blue")
            }, new[]
            {
                new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single)
            }));
            string original = JsonUtility.ToJson(GetDraft());
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));

            // The middle cell splits the gate: the part before it keeps d1, the part after it is a new d3.
            int records = CountUndoRecords(k_undoName);
            Click(root, DoorCentre(canvas, BoardSide.Top, 2), 1);
            LevelData split = GetDraft();
            Assert.That(split.DoorCount, Is.EqualTo(3), "A Pen right click on the middle cell splits the gate.");
            AssertDoor(split.GetDoorAt(0), "d1", BoardSide.Top, 1, 1, "Red", 0);
            AssertDoor(split.GetDoorAt(1), "d2", BoardSide.Left, 2, 1, "Blue", 0);
            AssertDoor(split.GetDoorAt(2), "d3", BoardSide.Top, 3, 1, "Red", 0);
            // Undo.GetRecords lists each record of a group, so only the name is counted; the single undo below proves
            // that the removal is one step.
            Assert.That(CountUndoRecords(k_undoName), Is.GreaterThan(records), "The undo step is \"Remove Gate Part\".");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen), "The Pen stays active.");

            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original), "One undo restores the gate exactly.");

            Click(root, DoorCentre(canvas, BoardSide.Top, 1), 1);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(2));
            AssertDoor(GetDraft().GetDoorAt(0), "d1", BoardSide.Top, 2, 2, "Red", 0);
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original));

            Click(root, DoorCentre(canvas, BoardSide.Top, 3), 1);
            AssertDoor(GetDraft().GetDoorAt(0), "d1", BoardSide.Top, 1, 2, "Red", 0);
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original));

            Click(root, DoorCentre(canvas, BoardSide.Left, 2), 1);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(1), "A one-cell gate goes whole.");
            AssertDoor(GetDraft().GetDoorAt(0), "d1", BoardSide.Top, 1, 3, "Red", 0);
            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original));
        }

        [Test]
        public void EraserClicksAndOtherToolsRightClicksRemoveTheWholeGate()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            m_fixture.LoadDatabase();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            LoadFixture(root, new LevelData("gates", "Gates", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 3, "Red"),
                new LevelDoorData("d2", BoardSide.Left, 1, 3, "Blue"),
                new LevelDoorData("d3", BoardSide.Right, 1, 3, "Blue"),
                new LevelDoorData("d4", BoardSide.Bottom, 1, 3, "Red")
            }, new[]
            {
                new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single)
            }));

            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
            Click(root, DoorCentre(canvas, BoardSide.Top, 2), 1);
            AssertDoorIds("A Select right click removes the whole gate.", "d2", "d3", "d4");

            Press(root.Q<Button>("tool-eraser"));
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Eraser));
            Click(root, DoorCentre(canvas, BoardSide.Left, 2), 0);
            AssertDoorIds("An Eraser left click removes the whole gate.", "d3", "d4");
            Click(root, DoorCentre(canvas, BoardSide.Right, 2), 1);
            AssertDoorIds("An Eraser right click removes the whole gate.", "d4");

            Press(root.Q<Button>("rotate-block-cw"));
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.RotateClockwise));
            Click(root, DoorCentre(canvas, BoardSide.Bottom, 2), 1);
            Assert.That(GetDraft().DoorCount, Is.Zero, "A transform tool's right click removes the whole gate.");
        }

        private static LevelData Remove(LevelData level, int doorIndex, BoardSide side, int segment)
        {
            bool removed = LevelEditCommands.TryRemoveDoorSegment(level, doorIndex, side, segment, out LevelData result,
                out string issue);
            Assert.That(removed, Is.True, issue);
            Assert.That(issue, Is.Null);
            return result;
        }

        private static void AssertRefused(LevelData level, int doorIndex, BoardSide side, int segment, string expected)
        {
            bool removed = LevelEditCommands.TryRemoveDoorSegment(level, doorIndex, side, segment, out LevelData result,
                out string issue);
            Assert.That(removed, Is.False, expected);
            Assert.That(result, Is.Null, expected);
            Assert.That(issue, Is.EqualTo(expected));
        }

        private static void AssertDoor(LevelDoorData door, string id, BoardSide side, int start, int length,
            string colorId, int effectCount)
        {
            Assert.That(door.Id, Is.EqualTo(id));
            Assert.That(door.Side, Is.EqualTo(side), id);
            Assert.That(door.Start, Is.EqualTo(start), id);
            Assert.That(door.Length, Is.EqualTo(length), id);
            Assert.That(door.ColorId, Is.EqualTo(colorId), id);
            Assert.That(door.EffectCount, Is.EqualTo(effectCount), id);
        }

        private void AssertDoorIds(string message, params string[] ids)
        {
            LevelData level = GetDraft();
            Assert.That(level.DoorCount, Is.EqualTo(ids.Length), message);
            for (int index = 0; index < ids.Length; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                Assert.That(door.Id, Is.EqualTo(ids[index]), message);
                Assert.That(door.Length, Is.EqualTo(3), message);
            }
        }

        // A 5 x 5 board: d1 spans Top 1-3 in Red with one placeholder effect, d2 spans Left 1-2 in Blue.
        private static LevelData CreateLevel()
        {
            return new LevelData("segments", "Segments", 5, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 3, "Red", new[] { GateEffect() }),
                new LevelDoorData("d2", BoardSide.Left, 1, 2, "Blue")
            }, new[] { new LevelBlockData("b1", "Red", new Vector2Int(2, 2), s_single) });
        }

        private static LevelEffectData GateEffect()
        {
            return LevelEffectTests.Effect(k_gateEffectType, ("clearsToOpen", "3"));
        }

        // Unity 2022.3 lists the undo stack only through the internal Undo.GetRecords.
        private static int CountUndoRecords(string name)
        {
            MethodInfo getRecords = typeof(Undo).GetMethod("GetRecords", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(List<string>), typeof(List<string>) }, null);
            Assume.That(getRecords, Is.Not.Null, "Undo.GetRecords lists the undo stack.");
            List<string> undo = new List<string>();
            List<string> redo = new List<string>();
            getRecords.Invoke(null, new object[] { undo, redo });
            int count = 0;
            for (int index = 0; index < undo.Count; index++)
            {
                count += undo[index] == name ? 1 : 0;
            }

            return count;
        }

        private static Vector2 DoorCentre(LevelCanvasElement canvas, BoardSide side, int segment)
        {
            return canvas.LocalToWorld(canvas.Geometry.GetEdgeRect(side, segment, LevelCanvasGeometry.k_doorDepth).center);
        }

        // Replaces the window's draft with a known level, as a Reload would, and refreshes the canvas.
        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private LevelData GetDraft()
        {
            return m_fixture.GetDraft();
        }

        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // Button 0 is a left click and 1 a right click.
        private static void Click(VisualElement root, Vector2 position, int button)
        {
            UiTestInput.Click(root, position, button);
        }

        private static void SendPointer(VisualElement root, EventType type, Vector2 position, int button)
        {
            UiTestInput.SendPointer(root, type, position, button);
        }

        // UI Toolkit routes keyboard events to the focused element, so the target takes focus first.
        private static void SendKey(VisualElement target, KeyCode key)
        {
            UiTestInput.SendKey(target, key);
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open();
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds the GUI itself, so a later automatic CreateGUI cannot clear the undo records under a test.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity();
            m_window = m_fixture.Window;
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }
    }
}
