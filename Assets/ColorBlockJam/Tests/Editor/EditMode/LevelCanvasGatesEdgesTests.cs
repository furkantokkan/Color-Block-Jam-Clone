using System.Collections;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor's gate, edge and wall tools through real pointer, button and key events: the Pen
    /// paints and extends gates on the ring, gates are removed and recoloured, the edge buttons resize the board with
    /// a hatched confirmation for removals, the canvas draws the rule-set walls and the issues list highlights.
    /// </summary>
    public sealed class LevelCanvasGatesEdgesTests
    {
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

        [UnityTest]
        public IEnumerator PenPaintsAGateOnTheRingAndADragExtendsItAsOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, new LevelData("gates", "Gates", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Blue).worldBound.center, 0);
            SendKey(canvas, KeyCode.Alpha2);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));

            Click(root, RingCentre(canvas, BoardSide.Top, 0), 0);
            LevelData painted = GetDraft();
            Assert.That(painted.DoorCount, Is.EqualTo(1), "A Pen click on the ring adds a gate.");
            AssertDoor(painted.GetDoorAt(0), BoardSide.Top, 0, 1, "Blue");
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Door), "The painted gate is selected.");
            string oneGate = JsonUtility.ToJson(painted);

            SendPointer(root, EventType.MouseDown, RingCentre(canvas, BoardSide.Top, 1), 0);
            SendPointer(root, EventType.MouseMove, RingCentre(canvas, BoardSide.Top, 3), 0);
            Assert.That(canvas.IsPaintingGate, Is.True);
            SendPointer(root, EventType.MouseUp, RingCentre(canvas, BoardSide.Top, 3), 0);
            LevelData extended = GetDraft();
            Assert.That(extended.DoorCount, Is.EqualTo(1), "The stroke joins the touching gate of the same colour.");
            AssertDoor(extended.GetDoorAt(0), BoardSide.Top, 0, 3, "Blue");
            Assert.That(root.Q<Label>("status").text, Is.EqualTo("Gate d1 would span 4 cells; gates span 1 to 3."),
                "The stroke stops where the gate would pass three cells.");

            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(oneGate), "The whole stroke is one undo step.");

            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Red).worldBound.center, 0);
            // With the Pen, 2 picks a colour (TASK-20260926-180414067-b923c8a9), so the Pen button picks it up again.
            Press(root.Q<Button>("tool-pen"));
            Click(root, RingCentre(canvas, BoardSide.Top, 0), 0);
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(oneGate), "Another colour's gate is not painted over.");
            Assert.That(root.Q<Label>("status").text, Does.Contain("already holds door"));
        }

        [Test]
        public void EraserAndRightClickRemoveGatesAndASelectedGateIsRecolouredAndDeleted()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            LoadFixture(root, new LevelData("gates", "Gates", 4, 4, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 0, 2, "Red"),
                new LevelDoorData("d2", BoardSide.Left, 1, 1, "Blue"),
                new LevelDoorData("d3", BoardSide.Right, 2, 1, "Blue")
            }, new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));

            Click(root, DoorCentre(canvas, BoardSide.Top, 1), 1);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(2), "Right click removes the whole gate.");
            Assert.That(GetDraft().GetDoorAt(0).Id, Is.EqualTo("d2"));

            SendKey(canvas, KeyCode.Alpha3);
            Click(root, DoorCentre(canvas, BoardSide.Right, 2), 0);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(1), "The Eraser removes a gate.");

            SendKey(canvas, KeyCode.Alpha1);
            Click(root, DoorCentre(canvas, BoardSide.Left, 1), 0);
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Door));
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.DarkGreen).worldBound.center, 0);
            Assert.That(GetDraft().GetDoorAt(0).ColorId, Is.EqualTo("DarkGreen"), "A swatch recolours the selected gate.");

            SendKey(canvas, KeyCode.Delete);
            Assert.That(GetDraft().DoorCount, Is.Zero, "Delete removes the selected gate.");
        }

        [UnityTest]
        public IEnumerator EdgeButtonsResizeAndAShrinkThatRemovesContentWaitsForRemove()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, new LevelData("edges", "Edges", 4, 4, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 0, 2, "Blue")
            }, new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(0, 1), s_single),
                new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), s_single)
            }));
            string original = JsonUtility.ToJson(GetDraft());

            Press(canvas.GetEdgeButton(BoardSide.Left, true));
            LevelData grown = GetDraft();
            Assert.That(grown.Width, Is.EqualTo(5));
            Assert.That(grown.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)), "Content keeps its place.");
            Assert.That(grown.GetDoorAt(0).Start, Is.EqualTo(1));

            Press(canvas.GetEdgeButton(BoardSide.Left, false));
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original), "Removing the empty column applies at once.");

            VisualElement confirm = root.Q("resize-confirm");
            Press(canvas.GetEdgeButton(BoardSide.Left, false));
            Assert.That(confirm.style.display.value, Is.EqualTo(DisplayStyle.Flex), "A removal waits for an answer.");
            Assert.That(root.Q<Label>("resize-confirm-text").text, Is.EqualTo("Remove 1 block and shorten 1 door?"));
            Assert.That(canvas.HatchCells.Count, Is.EqualTo(4), "The removed column is hatched.");
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original), "Nothing changes before Remove.");

            SendKey(canvas, KeyCode.Escape);
            Assert.That(confirm.style.display.value, Is.EqualTo(DisplayStyle.None), "Esc cancels.");
            Assert.That(canvas.HatchCells.Count, Is.Zero);

            Press(canvas.GetEdgeButton(BoardSide.Left, false));
            Press(root.Q<Button>("resize-confirm-apply"));
            LevelData shrunk = GetDraft();
            Assert.That(shrunk.Width, Is.EqualTo(3));
            Assert.That(shrunk.BlockCount, Is.EqualTo(1), "The block in the removed column is gone.");
            Assert.That(shrunk.GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 2)));
            Assert.That(shrunk.GetDoorAt(0).Length, Is.EqualTo(1), "The gate over the removed column is shortened.");

            Undo.PerformUndo();
            Assert.That(JsonUtility.ToJson(GetDraft()), Is.EqualTo(original), "Remove is one undo step.");

            Assert.That(canvas.GetEdgeButton(BoardSide.Right, true).enabledSelf, Is.True);
            LoadFixture(root, new LevelData("wide", "Wide", 8, 3, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));
            Assert.That(canvas.GetEdgeButton(BoardSide.Right, true).enabledSelf, Is.False, "+ stops at the widest board.");
            Assert.That(canvas.GetEdgeButton(BoardSide.Top, false).enabledSelf, Is.False, "- stops at the lowest board.");
        }

        [Test]
        public void CanvasDrawsTheRuleSetWallsAndTheIssuesListHighlightsAnIssue()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            Assume.That(LevelCanvasWalls.LoadDatabase(), Is.Not.Null, "The environment database asset exists.");

            LoadFixture(root, new LevelData("walls", "Walls", 4, 5, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single),
                new LevelBlockData("b2", "Blue", new Vector2Int(1, 1), s_single)
            }));
            int corners = 0;
            for (int index = 0; index < canvas.Walls.Count; index++)
            {
                corners += canvas.Walls[index].IsCorner ? 1 : 0;
            }

            Assert.That(canvas.Walls.Count - corners, Is.EqualTo(36), "A 4 x 5 ring has 4 x (4 + 5) straight wall quadrants.");
            Assert.That(corners, Is.EqualTo(4));

            LevelEditorIssuesList issues = root.Q<LevelEditorIssuesList>();
            Assert.That(issues.IssueCount, Is.GreaterThan(0));
            Assert.That(issues.GetIssue(0).Severity, Is.EqualTo(LevelIssueSeverity.Error), "Errors come first.");
            Assert.That(issues.GetIssue(0).Cells, Does.Contain(new Vector2Int(1, 1)), "The overlap names its cell.");
            issues.Select(0);
            Assert.That(canvas.Highlight, Is.SameAs(issues.GetIssue(0)), "A picked issue is outlined on the canvas.");

            LoadFixture(root, new LevelData("walls", "Walls", 4, 5, 60, new[]
            {
                new LevelDoorData("d1", BoardSide.Top, 1, 2, "Blue")
            }, new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));
            Assert.That(canvas.Highlight, Is.Null, "A new draft clears the highlight.");
            int straight = 0;
            for (int index = 0; index < canvas.Walls.Count; index++)
            {
                straight += canvas.Walls[index].IsCorner ? 0 : 1;
            }

            Assert.That(straight, Is.EqualTo(32), "A two-segment gate holds four ring quadrants instead of walls.");
        }

        [Test]
        public void APaintedGateIsNotRecolouredAndLeftStrokesAndCornersBehave()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            LoadFixture(root, new LevelData("gates", "Gates", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Blue).worldBound.center, 0);
            SendKey(canvas, KeyCode.Alpha2);

            SendPointer(root, EventType.MouseDown, RingCentre(canvas, BoardSide.Left, 1), 0);
            SendPointer(root, EventType.MouseMove, RingCentre(canvas, BoardSide.Left, 2), 0);
            SendPointer(root, EventType.MouseUp, RingCentre(canvas, BoardSide.Left, 2), 0);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(1));
            AssertDoor(GetDraft().GetDoorAt(0), BoardSide.Left, 1, 2, "Blue");

            // A swatch picked for the next piece leaves the painted gate alone, as for a just-placed block. With the Pen,
            // 1 picks a colour (TASK-20260926-180414067-b923c8a9), so the Select button switches.
            Press(root.Q<Button>("tool-select"));
            Click(root, root.Q<LevelColorSwatches>().GetSwatch(PaletteColor.Red).worldBound.center, 0);
            Assert.That(GetDraft().GetDoorAt(0).ColorId, Is.EqualTo("Blue"), "Only a clicked gate is recoloured.");

            SendKey(canvas, KeyCode.Alpha2);
            Rect board = canvas.Geometry.BoardRect;
            float quarter = 0.25f * canvas.Geometry.CellSize;
            Click(root, canvas.LocalToWorld(new Vector2(board.xMin - quarter, board.yMin - quarter)), 0);
            Assert.That(GetDraft().DoorCount, Is.EqualTo(1), "A corner holds no gate.");
            Assert.That(root.Q<Label>("status").text, Is.EqualTo("A gate cannot sit on a corner; paint it along one side."));
        }

        [Test]
        public void TheWheelZoomsAboutTheViewCentreAndHomeResetsTheView()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            LoadFixture(root, new LevelData("zoom", "Zoom", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Blue", new Vector2Int(1, 1), s_single)
            }));
            Assert.That(canvas.Zoom, Is.EqualTo(LevelCanvasGeometry.k_defaultZoom), "The first view is the default zoom.");

            float before = canvas.Geometry.CellSize;
            Vector2 centre = canvas.contentRect.center;
            Vector2 middle = (centre - canvas.Geometry.Origin) / canvas.Geometry.CellSize;
            Vector2 local = canvas.Geometry.GetCellRect(new Vector2Int(0, 0)).center;
            using (WheelEvent wheel = WheelEvent.GetPooled(new Event
                   {
                       type = EventType.ScrollWheel, delta = new Vector2(0f, -1f), mousePosition = canvas.LocalToWorld(local)
                   }))
            {
                wheel.target = canvas;
                canvas.SendEvent(wheel);
            }

            Assert.That(canvas.Zoom, Is.GreaterThan(LevelCanvasGeometry.k_defaultZoom), "The wheel up zooms in.");
            Assert.That(canvas.Geometry.CellSize, Is.GreaterThan(before));
            Vector2 after = (centre - canvas.Geometry.Origin) / canvas.Geometry.CellSize;
            Assert.That(Vector2.Distance(after, middle), Is.LessThan(0.01f),
                "The board point at the view centre stays there, not the one under the pointer.");

            canvas.ZoomAt(centre, 0.01f);
            Assert.That(canvas.Zoom, Is.EqualTo(LevelCanvasGeometry.k_minZoom), "Zoom stops at half the fit.");
            canvas.ZoomAt(centre, 1000f);
            Assert.That(canvas.Zoom, Is.EqualTo(LevelCanvasGeometry.GetMaxZoom(canvas.contentRect.size, 4, 4)),
                "Zoom stops at about three cells across.");

            SendKey(canvas, KeyCode.Home);
            Assert.That(canvas.Zoom, Is.EqualTo(LevelCanvasGeometry.k_defaultZoom));
            Assert.That(canvas.Pan, Is.EqualTo(Vector2.zero));
            Assert.That(canvas.Geometry.CellSize, Is.EqualTo(before), "Home restores the first view.");
        }

        [Test]
        public void EachSideHasAnAddAndARemoveBarAndEachCornerACutAndAFillButton()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            Rect board = canvas.Geometry.BoardRect;
            float ring = LevelCanvasGeometry.k_doorDepth * canvas.Geometry.CellSize;
            foreach (BoardSide side in new[] { BoardSide.Top, BoardSide.Bottom, BoardSide.Left, BoardSide.Right })
            {
                Button add = canvas.GetEdgeButton(side, true);
                Button remove = canvas.GetEdgeButton(side, false);
                bool horizontal = side == BoardSide.Top || side == BoardSide.Bottom;
                Rect addRect = GetRect(add);
                Rect removeRect = GetRect(remove);
                Assert.That(horizontal ? removeRect.width : removeRect.height,
                    Is.EqualTo(horizontal ? addRect.width : addRect.height), $"{side}: both bars run the same length.");
                Assert.That(horizontal ? addRect.xMin : addRect.yMin,
                    Is.LessThanOrEqualTo((horizontal ? board.xMin : board.yMin) - ring),
                    $"{side}: the bar runs past the board over the wall ring (reference frame).");
                Assert.That(horizontal ? addRect.xMax : addRect.yMax,
                    Is.GreaterThanOrEqualTo((horizontal ? board.xMax : board.yMax) + ring));
                float addDistance = DistanceFromBoard(side, board, add);
                float removeDistance = DistanceFromBoard(side, board, remove);
                Assert.That(addDistance, Is.GreaterThan(removeDistance), $"{side}: the add bar is the outer one.");
                Assert.That(removeDistance, Is.GreaterThan(LevelCanvasGeometry.k_doorDepth * canvas.Geometry.CellSize),
                    $"{side}: the bars sit outside the gate band.");
            }

            // The add bar points out, the remove bar in.
            Assert.That(GetIconName(canvas.GetEdgeButton(BoardSide.Top, true)), Is.EqualTo("arrow-up"));
            Assert.That(GetIconName(canvas.GetEdgeButton(BoardSide.Top, false)), Is.EqualTo("arrow-down"));
            Assert.That(GetIconName(canvas.GetEdgeButton(BoardSide.Bottom, true)), Is.EqualTo("arrow-down"));
            Assert.That(GetIconName(canvas.GetEdgeButton(BoardSide.Left, true)), Is.EqualTo("arrow-left"));
            Assert.That(GetIconName(canvas.GetEdgeButton(BoardSide.Right, false)), Is.EqualTo("arrow-left"));
            // Each corner is a 2 x 2 block of the frame: the fill button where the outer bar row and column cross, the
            // cut button where the inner ones cross, both as large as a bar is thick and a gap clear of the bars.
            Rect topOuter = GetRect(canvas.GetEdgeButton(BoardSide.Top, true));
            Rect topInner = GetRect(canvas.GetEdgeButton(BoardSide.Top, false));
            Rect leftOuter = GetRect(canvas.GetEdgeButton(BoardSide.Left, true));
            Rect leftInner = GetRect(canvas.GetEdgeButton(BoardSide.Left, false));
            Rect fill = GetRect(canvas.Q<Button>("corner-top-left-fill"));
            Rect cut = GetRect(canvas.Q<Button>("corner-top-left-cut"));
            Assert.That(fill.width, Is.EqualTo(topOuter.height), "A corner button is as large as a bar is thick.");
            Assert.That(fill.height, Is.EqualTo(fill.width));
            Assert.That(fill.x, Is.EqualTo(leftOuter.x).Within(0.01f));
            Assert.That(fill.y, Is.EqualTo(topOuter.y).Within(0.01f));
            Assert.That(cut.x, Is.EqualTo(leftInner.x).Within(0.01f));
            Assert.That(cut.y, Is.EqualTo(topInner.y).Within(0.01f));
            Assert.That(cut.xMax, Is.LessThan(topInner.xMin), "The bars stop a gap short of the corner block.");
            Assert.That(cut.yMax, Is.LessThan(leftInner.yMin));
            Assert.That(cut.xMin - fill.xMax, Is.EqualTo(LevelCanvasGeometry.k_edgeBarGap).Within(0.01f),
                "The corner buttons keep the bars' gap on each axis.");
            Assert.That(cut.yMin - fill.yMax, Is.EqualTo(LevelCanvasGeometry.k_edgeBarGap).Within(0.01f));
            // The user's sizes (D-001, D-002): the old 16 px controls with a 4 px gap read as too small and too close.
            Assert.That(fill.width, Is.GreaterThanOrEqualTo(22f), "Corner buttons are at least 22 px.");
            Assert.That(LevelCanvasGeometry.k_edgeBarGap, Is.GreaterThanOrEqualTo(6f), "Corner buttons stand 6 px apart.");
            Assert.That(Vector2.Distance(fill.center, cut.center), Is.GreaterThanOrEqualTo(28f * Mathf.Sqrt(2f) - 0.01f));
            Image cornerIcon = canvas.Q<Button>("corner-top-left-fill").Q<Image>("edge-icon");
            Assert.That(cornerIcon.style.width.value.value, Is.EqualTo(fill.width), "The bracket fills its button.");
            // The frame stays inside the band the geometry reserves outside the gate bars.
            Assert.That(board.xMin - leftOuter.xMin, Is.LessThanOrEqualTo(ring + LevelCanvasGeometry.k_edgeButtonBand));
            Assert.That(board.yMin - topOuter.yMin, Is.LessThanOrEqualTo(ring + LevelCanvasGeometry.k_edgeButtonBand));

            // The fill button shows its own corner's bracket, the cut button the opposite one.
            Assert.That(GetIconName(canvas.Q<Button>("corner-top-left-fill")), Is.EqualTo("corner-top-left"));
            Assert.That(GetIconName(canvas.Q<Button>("corner-top-left-cut")), Is.EqualTo("corner-bottom-right"));
            Assert.That(GetIconName(canvas.Q<Button>("corner-bottom-right-fill")), Is.EqualTo("corner-bottom-right"));
            Assert.That(GetIconName(canvas.Q<Button>("corner-top-right-cut")), Is.EqualTo("corner-bottom-left"));

            foreach (string corner in new[] { "top-left", "top-right", "bottom-left", "bottom-right" })
            {
                foreach (string action in new[] { "cut", "fill" })
                {
                    Button button = canvas.Q<Button>("corner-" + corner + "-" + action);
                    Assert.That(button, Is.Not.Null, corner + action);
                    // Enabled from the model since level JSON v2 (LevelCanvasCornerVoidsTests covers each state).
                    Assert.That(button.tooltip, Is.Not.Empty, corner + action);
                }
            }
        }

        private static Rect GetRect(VisualElement element)
        {
            return new Rect(element.style.left.value.value, element.style.top.value.value, element.style.width.value.value,
                element.style.height.value.value);
        }

        private static string GetIconName(Button button)
        {
            Image icon = button.Q<Image>("edge-icon");
            Assert.That(icon, Is.Not.Null, $"{button.name} shows an icon.");
            Color tint = LevelEditorIcons.NeutralTint;
            Assert.That(new Color(icon.tintColor.r, icon.tintColor.g, icon.tintColor.b), Is.EqualTo(new Color(tint.r, tint.g, tint.b)),
                $"{button.name} uses the skin's tint.");
            // A disabled control fades its icon, since the disabled style alone barely dims a white icon.
            Assert.That(icon.tintColor.a, Is.EqualTo(button.enabledSelf ? tint.a : 0.3f).Within(0.001f), button.name);
            return icon.image.name;
        }

        // How far a bar's near edge sits outside the board edge it belongs to.
        private static float DistanceFromBoard(BoardSide side, Rect board, Button bar)
        {
            float left = bar.style.left.value.value;
            float top = bar.style.top.value.value;
            switch (side)
            {
                case BoardSide.Top:
                    return board.yMin - (top + bar.style.height.value.value);
                case BoardSide.Bottom:
                    return top - board.yMax;
                case BoardSide.Left:
                    return board.xMin - (left + bar.style.width.value.value);
                default:
                    return left - board.xMax;
            }
        }

        private static void AssertDoor(LevelDoorData door, BoardSide side, int start, int length, string colorId)
        {
            Assert.That(door.Side, Is.EqualTo(side));
            Assert.That(door.Start, Is.EqualTo(start));
            Assert.That(door.Length, Is.EqualTo(length));
            Assert.That(door.ColorId, Is.EqualTo(colorId));
        }

        private static Vector2 RingCentre(LevelCanvasElement canvas, BoardSide side, int segment)
        {
            return canvas.LocalToWorld(canvas.Geometry.GetEdgeRect(side, segment, LevelCanvasGeometry.k_ringDepth).center);
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
            // A fixture level, deterministic whatever the real project's active database holds (Tests-Editor-1-F3).
            m_fixture.LoadDatabase();
            return root;
        }

        // Unity builds the GUI itself, so a later automatic CreateGUI cannot clear the undo records under a test.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity();
            m_window = m_fixture.Window;
            m_fixture.LoadDatabase();
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
