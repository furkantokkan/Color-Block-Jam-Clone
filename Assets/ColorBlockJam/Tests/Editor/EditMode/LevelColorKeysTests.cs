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
    /// Verifies the Pen's colour keys (TASK-20260926-180414067-b923c8a9): LevelColorKeys maps 1-9, 0 and - on the top
    /// row and the keypad to colour-order positions 1-11, and in the Level Editor window each key picks the colour whose
    /// badge shows its number while the Pen is active, as a swatch click does, while 1, 2 and 3 still pick tools with
    /// every other tool, text fields keep their keys and the tooltips name the keys for the current order.
    /// </summary>
    public sealed class LevelColorKeysTests
    {
        private const string k_penKeysText = "With the Pen, keys 1-9, 0 and - pick the colour whose";
        // The key for each 1-based position, top row and keypad; index 0 is position 1.
        private static readonly KeyCode[] s_topRowKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0, KeyCode.Minus
        };
        private static readonly KeyCode[] s_keypadKeys =
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
            KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9, KeyCode.Keypad0, KeyCode.KeypadMinus
        };
        private static readonly string[] s_keyLabels = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-" };
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
        public void EveryTopRowAndKeypadKeyNamesItsBadgePosition()
        {
            Assert.That(s_topRowKeys, Has.Length.EqualTo(LevelColorOrder.k_count), "One key per gate colour.");
            for (int index = 0; index < s_topRowKeys.Length; index++)
            {
                foreach (KeyCode key in new[] { s_topRowKeys[index], s_keypadKeys[index] })
                {
                    Assert.That(LevelColorKeys.TryGetPosition(key, out int position), Is.True, key.ToString());
                    Assert.That(position, Is.EqualTo(index + 1), key.ToString());
                }
            }
        }

        // The neighbours of each key range, the other bracket and sign keys, and keys other shortcuts use.
        [TestCase(KeyCode.None)]
        [TestCase(KeyCode.Slash)]
        [TestCase(KeyCode.Colon)]
        [TestCase(KeyCode.KeypadPeriod)]
        [TestCase(KeyCode.KeypadDivide)]
        [TestCase(KeyCode.KeypadPlus)]
        [TestCase(KeyCode.Equals)]
        [TestCase(KeyCode.Underscore)]
        [TestCase(KeyCode.LeftBracket)]
        [TestCase(KeyCode.R)]
        [TestCase(KeyCode.Escape)]
        [TestCase(KeyCode.F1)]
        public void AnyOtherKeyNamesNoPosition(KeyCode key)
        {
            Assert.That(LevelColorKeys.TryGetPosition(key, out int position), Is.False);
            Assert.That(position, Is.Zero);
        }

        [Test]
        public void KeyLabelsAreThePrebuiltKeyNames()
        {
            for (int position = 1; position <= LevelColorOrder.k_count; position++)
            {
                Assert.That(LevelColorKeys.GetKeyLabel(position), Is.EqualTo(s_keyLabels[position - 1]));
                Assert.That(LevelColorKeys.GetKeyLabel(position), Is.SameAs(LevelColorKeys.GetKeyLabel(position)),
                    "A label is built once, not per call.");
            }
        }

        // AC-1: in palette order, each key picks palette position 1-11 as its swatch click would, in every Pen use.
        [Test]
        public void WithThePenEachKeyPicksThePaletteColourAtItsPositionAndOutlinesItsSwatch()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            LevelEditorToolState state = GetToolState();
            Assert.That(GetOrder().IsDefault, Is.True, "A freshly loaded draft is in palette order.");
            int keysPastTheWindow = 0;
            EventCallback<KeyDownEvent> probe = _ => keysPastTheWindow++;
            canvas.RegisterCallback(probe);
            Press(root.Q<Button>("tool-pen"));
            // Pink first, so every key below changes the colour.
            Invoke("PickSwatch", PaletteColor.Pink);

            foreach (KeyCode[] keys in new[] { s_topRowKeys, s_keypadKeys })
            {
                for (int index = 0; index < keys.Length; index++)
                {
                    PaletteColor expected = (PaletteColor)index;
                    SendKey(canvas, keys[index]);
                    Assert.That(state.Color, Is.EqualTo(expected), keys[index].ToString());
                    Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen), $"{keys[index]} keeps the Pen in hand.");
                    AssertOutlined(swatches, expected);
                    Assert.That(canvas.ToolBadgeIcon.tintColor, Is.EqualTo(canvas.GetPaletteColor(expected)),
                        "The Pen badge takes the colour, as after a swatch click.");
                }
            }

            canvas.UnregisterCallback(probe);
            Assert.That(keysPastTheWindow, Is.Zero, "The window consumes every colour key.");

            // The Barrier brush and a "+" New piece drawing are Pen uses too; the key keeps them armed.
            Press(root.Q<Button>("obstacle-barrier"));
            SendKey(canvas, KeyCode.Alpha6);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.LightTurquoise));
            Assert.That(state.IsBarrierArmed, Is.True, "The Barrier brush stays armed.");
            state.ArmFreehand();
            Invoke("SyncTool");
            SendKey(canvas, KeyCode.Keypad7);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.DarkTurquoise));
            Assert.That(state.IsFreehandArmed, Is.True, "The freehand Pen stays armed.");
        }

        // AC-2: with a non-palette order, key k picks the colour whose badge shows k, not palette colour k.
        [Test]
        public void WithAnOrderEachKeyPicksTheColourWhoseBadgeShowsItsNumber()
        {
            VisualElement root = OpenWindow();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            LevelColorOrder order = SetNonDefaultOrder();
            Press(root.Q<Button>("tool-pen"));

            foreach (KeyCode[] keys in new[] { s_topRowKeys, s_keypadKeys })
            {
                for (int index = 0; index < keys.Length; index++)
                {
                    SendKey(canvas, keys[index]);
                    PaletteColor picked = GetToolState().Color;
                    Assert.That(picked, Is.EqualTo(order.GetColorAt(index)), keys[index].ToString());
                    Assert.That(swatches.GetBadge(picked).text, Is.EqualTo((index + 1).ToString()),
                        $"{keys[index]} picks the swatch whose badge shows its number.");
                    AssertOutlined(swatches, picked);
                }
            }
        }

        // User request of 2026-09-26: with a non-palette order the swatches sit in their order positions, so the grid's
        // badges still read 1 to 11, and ] / [ step through that grid order. Since 2026-09-27 Shuffle leaves the order.
        [Test]
        public void WithAnOrderTheSwatchesSitInOrderAndTheBracketsFollowIt()
        {
            VisualElement root = OpenWindow();
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            Press(root.Q<Button>("color-shuffle"));
            Assert.That(GetOrder().IsDefault, Is.True, "Shuffle recolours the board and leaves the Colors panel.");
            LevelColorOrder order = SetNonDefaultOrder();

            for (int position = 0; position < LevelColorOrder.k_count; position++)
            {
                VisualElement swatch = swatches[position];
                Assert.That(swatch, Is.SameAs(swatches.GetSwatch(order.GetColorAt(position))), $"Grid slot {position + 1}");
                Assert.That(swatches.GetBadge(order.GetColorAt(position)).text, Is.EqualTo((position + 1).ToString()));
            }

            Invoke("PickSwatch", order.GetColorAt(LevelColorOrder.k_count - 1));
            SendKey(canvas, KeyCode.RightBracket);
            Assert.That(GetToolState().Color, Is.EqualTo(order.GetColorAt(0)), "] wraps from the last slot to the first.");
            SendKey(canvas, KeyCode.RightBracket);
            Assert.That(GetToolState().Color, Is.EqualTo(order.GetColorAt(1)));
            SendKey(canvas, KeyCode.LeftBracket);
            SendKey(canvas, KeyCode.LeftBracket);
            Assert.That(GetToolState().Color, Is.EqualTo(order.GetColorAt(LevelColorOrder.k_count - 1)),
                "[ wraps from the first slot to the last.");
            AssertOutlined(swatches, order.GetColorAt(LevelColorOrder.k_count - 1));
        }

        // AC-3: a key recolours the block the Pen edits as one "Recolour Block" step; the same colour records nothing,
        // and the plain Pen without an edit changes only the tool colour.
        [UnityTest]
        public IEnumerator AKeyRecoloursTheBlockThePenEditsAsOneUndoStepAndTheSameColourRecordsNothing()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");

            LoadFixture(root, CreateFixture());
            string fixture = JsonUtility.ToJson(GetLevel());
            Press(root.Q<Button>("tool-pen"));

            SendKey(canvas, KeyCode.Alpha5);
            Assert.That(GetToolState().Color, Is.EqualTo(PaletteColor.DarkGreen));
            Assert.That(JsonUtility.ToJson(GetLevel()), Is.EqualTo(fixture), "The plain Pen recolours no block.");

            ClickCell(root, canvas, new Vector2Int(2, 2));
            Assume.That(canvas.FreehandBlockId, Is.EqualTo("b2"), "A Pen click on b2 starts editing it.");
            SendKey(canvas, KeyCode.Alpha0);
            LevelData recoloured = GetLevel();
            Assert.That(recoloured.GetBlockAt(1).ColorId, Is.EqualTo(PaletteColor.Purple.ToString()),
                "Key 0 (badge 10) recolours the edited block.");
            Assert.That(recoloured.GetBlockAt(0).ColorId, Is.EqualTo("Red"), "Only the edited block.");
            Assert.That(canvas.FreehandBlockId, Is.EqualTo("b2"), "The Pen keeps editing it.");
            string purple = JsonUtility.ToJson(recoloured);

            SendKey(canvas, KeyCode.Keypad0);
            Assert.That(JsonUtility.ToJson(GetLevel()), Is.EqualTo(purple), "The same colour changes nothing.");
            SendKey(canvas, KeyCode.Minus);
            Assert.That(GetLevel().GetBlockAt(1).ColorId, Is.EqualTo(PaletteColor.Pink.ToString()));

            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetLevel()), Is.EqualTo(purple), "One undo step per recolour.");
            Undo.PerformUndo();
            yield return null;
            Assert.That(JsonUtility.ToJson(GetLevel()), Is.EqualTo(fixture),
                "The repeated key recorded no step, so the next undo is key 0's.");
        }

        // AC-4: with every other tool, 1, 2 and 3 pick Select, Pen and Eraser, and 4-9, 0 and - do nothing.
        [Test]
        public void OutsideThePenOneTwoAndThreePickToolsAndTheOtherColourKeysDoNothing()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelEditorToolState state = GetToolState();
            string[] others = { "tool-select", "tool-eraser", "rotate-block-ccw", "rotate-block-cw", "flip-block-h", "flip-block-v" };
            KeyCode[] toolKeys =
            {
                KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3
            };
            LevelEditorTool[] tools =
            {
                LevelEditorTool.Select, LevelEditorTool.Pen, LevelEditorTool.Eraser, LevelEditorTool.Select,
                LevelEditorTool.Pen, LevelEditorTool.Eraser
            };

            foreach (string button in others)
            {
                for (int index = 0; index < toolKeys.Length; index++)
                {
                    Press(root.Q<Button>(button));
                    PaletteColor color = state.Color;
                    SendKey(canvas, toolKeys[index]);
                    Assert.That(canvas.Tool, Is.EqualTo(tools[index]), $"{toolKeys[index]} with {button}");
                    Assert.That(state.Color, Is.EqualTo(color), $"{toolKeys[index]} with {button} picks no colour.");
                }

                for (int index = 3; index < s_topRowKeys.Length; index++)
                {
                    foreach (KeyCode key in new[] { s_topRowKeys[index], s_keypadKeys[index] })
                    {
                        Press(root.Q<Button>(button));
                        LevelEditorTool tool = canvas.Tool;
                        PaletteColor color = state.Color;
                        SendKey(canvas, key);
                        Assert.That(canvas.Tool, Is.EqualTo(tool), $"{key} with {button} picks no tool.");
                        Assert.That(state.Color, Is.EqualTo(color), $"{key} with {button} picks no colour.");
                    }
                }
            }
        }

        // AC-4: with the Pen, Shift is ignored, Ctrl, Cmd and Alt combinations are no shortcuts, [ and ] still cycle and
        // Esc returns to Select.
        [Test]
        public void WithThePenShiftIsIgnoredModifiedKeysDoNothingBracketsCycleAndEscReturnsToSelect()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            LevelEditorToolState state = GetToolState();
            Press(root.Q<Button>("tool-pen"));

            SendKey(canvas, KeyCode.Alpha3, EventModifiers.Shift);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Yellow), "Shift+3 is 3.");
            foreach (EventModifiers modifier in new[] { EventModifiers.Control, EventModifiers.Command, EventModifiers.Alt })
            {
                SendKey(canvas, KeyCode.Alpha7, modifier);
                Assert.That(state.Color, Is.EqualTo(PaletteColor.Yellow), $"{modifier}+7 is no shortcut.");
                Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));
            }

            SendKey(canvas, KeyCode.RightBracket);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.LightGreen), "] still cycles forward.");
            SendKey(canvas, KeyCode.LeftBracket);
            SendKey(canvas, KeyCode.LeftBracket);
            Assert.That(state.Color, Is.EqualTo(PaletteColor.Orange), "[ still cycles back.");
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Pen));

            SendKey(canvas, KeyCode.Escape);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select), "Esc returns to Select.");
        }

        // AC-4: Esc still ends a Pen edit, so the next key picks a tool again.
        [Test]
        public void EscEndsAPenEditAndTheNextThreePicksTheEraser()
        {
            VisualElement root = OpenWindow();
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(IsLevelLoaded(root) && canvas.Geometry.IsValid, Is.True, "The fixture database has a loaded level.");
            LoadFixture(root, CreateFixture());
            Press(root.Q<Button>("tool-pen"));
            ClickCell(root, canvas, new Vector2Int(2, 2));
            Assume.That(canvas.IsPenEditing, Is.True, "A Pen click on b2 starts editing it.");

            SendKey(canvas, KeyCode.Escape);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Select));
            Assert.That(canvas.IsFreehand, Is.False, "Esc ends the edit.");
            PaletteColor color = GetToolState().Color;
            SendKey(canvas, KeyCode.Alpha3);
            Assert.That(canvas.Tool, Is.EqualTo(LevelEditorTool.Eraser), "Outside the Pen, 3 picks the Eraser again.");
            Assert.That(GetToolState().Color, Is.EqualTo(color), "It picks no colour.");
        }

        // AC-4: digits and - typed into Level Name or Timer Seconds belong to the field.
        [Test]
        public void KeysTypedIntoLevelNameOrTimerSecondsPickNeitherAColourNorATool()
        {
            VisualElement root = OpenWindow();
            Assert.That(IsLevelLoaded(root), Is.True, "Level Name and Timer Seconds take keys only while a level is loaded.");
            LevelEditorToolState state = GetToolState();
            VisualElement[] fields = { root.Q<TextField>("level-name"), root.Q<IntegerField>("timer-seconds") };
            KeyCode[] keys = { KeyCode.Alpha1, KeyCode.Alpha3, KeyCode.Alpha5, KeyCode.Alpha0, KeyCode.Minus, KeyCode.Keypad2 };

            foreach (string toolButton in new[] { "tool-pen", "tool-select" })
            {
                Press(root.Q<Button>(toolButton));
                LevelEditorTool tool = GetToolState().Tool;
                PaletteColor color = state.Color;
                foreach (VisualElement field in fields)
                {
                    foreach (KeyCode key in keys)
                    {
                        SendKey(field, key);
                        Assert.That(state.Tool, Is.EqualTo(tool), $"{key} in {field.name} with {toolButton}");
                        Assert.That(state.Color, Is.EqualTo(color), $"{key} in {field.name} with {toolButton}");
                    }
                }
            }
        }

        // AC-5: the Pen tool and Colors tooltips name the keys, and each swatch names its own key in palette order.
        [Test]
        public void ThePenColorsAndSwatchTooltipsNameTheColourKeys()
        {
            VisualElement root = OpenWindow();
            Assert.That(root.Q<Button>("tool-pen").tooltip, Does.Contain(k_penKeysText).And.Contain("(0 is 10, - is 11)"));
            Assert.That(root.Q<Foldout>("colors-group").tooltip,
                Does.Contain(k_penKeysText).And.Contain("(0 is 10, - is 11)"));

            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            Assume.That(GetOrder().IsDefault, Is.True, "A freshly loaded draft is in palette order.");
            Assert.That(swatches.GetSwatch(PaletteColor.Red).tooltip, Is.EqualTo("Red: key 1 with the Pen ([ / ] cycles)"));
            Assert.That(swatches.GetSwatch(PaletteColor.Purple).tooltip,
                Is.EqualTo("Purple: key 0 with the Pen ([ / ] cycles)"));
            Assert.That(swatches.GetSwatch(PaletteColor.Pink).tooltip, Is.EqualTo("Pink: key - with the Pen ([ / ] cycles)"));
            AssertTooltipsFollow(swatches, LevelColorOrder.Default);
        }

        // AC-5: swatch tooltips follow the order; Color Shuffle, Color Switch and undo recolour only the board, so they
        // keep the palette-order keys (user order of 2026-09-27).
        [UnityTest]
        public IEnumerator SwatchTooltipsFollowTheOrderAndStayThroughShuffleSwitchAndUndo()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();
            Press(root.Q<Button>("color-shuffle"));
            AssertTooltipsFollow(swatches, LevelColorOrder.Default);

            // Color Switch trades the clicked block's colour, so a block is clicked first.
            LevelData level = GetLevel();
            Assert.That(level.BlockCount, Is.GreaterThan(0), "The fixture database has at least one block.");
            Invoke("SelectOnCanvas", LevelCanvasSelection.ForBlock(0, level.GetBlockAt(0).Id));
            Press(root.Q<Button>("color-switch"));
            AssertTooltipsFollow(swatches, LevelColorOrder.Default);

            Undo.PerformUndo();
            yield return null;
            AssertTooltipsFollow(swatches, LevelColorOrder.Default);

            AssertTooltipsFollow(swatches, SetNonDefaultOrder());
        }

        // Every swatch names the key for its position in the order: "Red: key 3 with the Pen ([ / ] cycles)".
        private static void AssertTooltipsFollow(LevelColorSwatches swatches, LevelColorOrder order)
        {
            for (int value = 0; value < LevelColorOrder.k_count; value++)
            {
                PaletteColor color = (PaletteColor)value;
                string expected = color + ": key " + s_keyLabels[order.IndexOf(color)] + " with the Pen ([ / ] cycles)";
                Assert.That(swatches.GetSwatch(color).tooltip, Is.EqualTo(expected), color.ToString());
            }
        }

        // Only the picked colour's swatch has the white selection outline.
        private static void AssertOutlined(LevelColorSwatches swatches, PaletteColor picked)
        {
            for (int value = 0; value < LevelColorOrder.k_count; value++)
            {
                PaletteColor color = (PaletteColor)value;
                Color border = swatches.GetSwatch(color).style.borderTopColor.value;
                Assert.That(border == LevelColorSwatches.s_selectedBorder, Is.EqualTo(color == picked),
                    $"Only {picked} is outlined; {color} is checked.");
            }
        }

        // A 4 x 4 board: b1 (Red) at (0,0) is the automatic default selection, and b2 (Blue) at (2,2) is free to edit.
        private static LevelData CreateFixture()
        {
            return new LevelData("fixture", "Colour keys fixture", 4, 4, 60, new LevelDoorData[0], new[]
            {
                new LevelBlockData("b1", "Red", Vector2Int.zero, s_single),
                new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), s_single)
            });
        }

        private void LoadFixture(VisualElement root, LevelData level)
        {
            m_fixture.LoadFixture(root, level);
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            // A window action never drives a Piece Editor the designer has open in this Editor.
            VisualElement root = m_fixture.Open(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
            // A fixture level, deterministic whatever the real project's active database holds (Tests-Editor-1-F3).
            m_fixture.LoadDatabase();
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
            // A fixture level with blocks (Tests-Editor-1-F3), so a caller that reads BlockCount sees a real one.
            m_fixture.LoadDatabase(CreateFixture());
        }

        private void Invoke(string method, params object[] arguments)
        {
            LevelEditorTestAccess.Invoke(m_window, method, arguments);
        }

        private LevelDraft GetDraft()
        {
            return (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
        }

        private LevelData GetLevel()
        {
            return GetDraft().Snapshot();
        }

        // The window's colour order: the draft's, else palette order.
        private LevelColorOrder GetOrder()
        {
            LevelDraft draft = GetDraft();
            return draft != null ? draft.ColorOrder : LevelColorOrder.Default;
        }

        // A fixed non-palette order written to the draft, as a kept draft can bring one back: since 2026-09-27 no
        // Level Editor button changes the order (Shuffle and Switch recolour only the board).
        private LevelColorOrder SetNonDefaultOrder()
        {
            LevelColorOrder order = LevelColorOrder.Default.Swap(PaletteColor.Red, PaletteColor.Pink)
                .Swap(PaletteColor.Blue, PaletteColor.Yellow);
            GetDraft().SetColorOrder(order);
            Invoke("RefreshColorOrder");
            return order;
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        // Submit presses a button the way a click does, wherever the tools view is scrolled.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        // The tool info box and the template banner above the canvas change with the tool, so the layout is brought up
        // to date before a cell's position is read.
        private static void ClickCell(VisualElement root, LevelCanvasElement canvas, Vector2Int cell)
        {
            ValidateLayout(root);
            Vector2 position = canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center);
            UiTestInput.Click(root, position);
        }

        // Sends a key press to one element after focusing it: UI Toolkit routes keyboard events to the focused
        // element, so a key with nothing focused never reaches the window's shortcut handler.
        private static void SendKey(VisualElement target, KeyCode key, EventModifiers modifiers = EventModifiers.None)
        {
            UiTestInput.SendKey(target, key, modifiers);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }
    }
}
