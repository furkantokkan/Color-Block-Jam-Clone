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
    /// Verifies the Level Editor's colour order in the window: the action row under the Colors swatches, the badges, and
    /// Color Shuffle and Color Switch as one undo step each that recolour the board one to one and leave the order, without
    /// writing the level JSON (TASK-20260926-145804549-da16f02f).
    /// </summary>
    public sealed class LevelColorOrderWindowTests
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
        public void ActionRowSitsUnderTheSwatchesWithIconsNamesAndTooltips()
        {
            VisualElement root = OpenWindow();
            Foldout colors = root.Q<Foldout>("colors-group");
            LevelColorSwatches swatches = colors.Q<LevelColorSwatches>();
            VisualElement row = root.Q("color-order-actions");

            Assert.That(row, Is.Not.Null);
            Assert.That(row.parent, Is.SameAs(swatches.parent), "In the Colors group.");
            Assert.That(row.parent.IndexOf(row), Is.EqualTo(row.parent.IndexOf(swatches) + 1), "Directly under the swatches.");
            Assert.That(root.Q("canvas-slot").parent.Q("color-order-actions"), Is.Null, "Nothing is left under the canvas.");
            // Reset Order was removed on the user's order of 2026-09-27.
            Assert.That(row.Query<Button>().ToList().ConvertAll(button => button.name),
                Is.EqualTo(new[] { "color-switch", "color-shuffle" }));
            string[] names = { "Color Switch", "Color Shuffle" };
            for (int index = 0; index < names.Length; index++)
            {
                Button button = row.Query<Button>().AtIndex(index);
                Assert.That(button.Q<Label>().text, Is.EqualTo(names[index]));
                Assert.That(button.Q<Image>()?.image, Is.Not.Null, $"{names[index]} shows its icon.");
                Assert.That(button.tooltip, Does.StartWith(names[index] + ":").And.Contain("One undo step"));
            }
        }

        // The Colors group's content width in a 330 px tools pane, with and without its vertical scrollbar.
        [TestCase(289f)]
        [TestCase(302f)]
        public void TheButtonsShareOneLineOfTheToolsPane(float rowWidth)
        {
            VisualElement root = OpenWindow();
            VisualElement row = root.Q("color-order-actions");
            VisualElement host = new VisualElement();
            host.style.width = rowWidth;
            host.Add(row);
            root.Add(host);
            ValidateLayout(root);

            System.Collections.Generic.List<Button> buttons = row.Query<Button>().ToList();
            for (int index = 0; index < buttons.Count; index++)
            {
                Rect button = buttons[index].layout;
                Assert.That(button.y, Is.EqualTo(buttons[0].layout.y).Within(0.5f), $"{buttons[index].name} stays on line one.");
                Assert.That(button.xMax, Is.LessThanOrEqualTo(rowWidth + 0.5f), $"{buttons[index].name} fits the row.");
            }
        }

        [Test]
        public void SwatchesShowTheirOrderNumbers()
        {
            VisualElement root = OpenWindow();
            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();

            for (int value = 0; value < LevelColorOrder.k_count; value++)
            {
                Label badge = swatches.GetBadge((PaletteColor)value);
                Assert.That(badge.text, Is.EqualTo((value + 1).ToString()), ((PaletteColor)value).ToString());
                Assert.That(badge.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(badge.pickingMode, Is.EqualTo(PickingMode.Ignore), "The swatch keeps its click and hover.");
            }

            Assert.That(root.Q<Button>("color-shuffle").enabledSelf, Is.EqualTo(IsLevelLoaded(root)));
        }

        // User order of 2026-09-27: Shuffle recolours only the board; the swatches, badges and order stay.
        [UnityTest]
        public IEnumerator ShuffleRecoloursOnlyTheBoardOneToOneInOneUndoStep()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            LevelData before = GetDraft().Snapshot();
            LevelColorSwatches swatches = root.Q<LevelColorSwatches>();

            Press(root.Q<Button>("color-shuffle"));

            LevelData after = GetDraft().Snapshot();
            Assert.That(GetDraft().ColorOrder.IsDefault, Is.True, "The order, and with it the Colors panel, stays.");
            for (int value = 0; value < LevelColorOrder.k_count; value++)
            {
                Assert.That(swatches.GetBadge((PaletteColor)value).text, Is.EqualTo((value + 1).ToString()));
                Assert.That(swatches[value], Is.SameAs(swatches.GetSwatch((PaletteColor)value)), "No swatch moves.");
            }

            AssertOneToOneRecolour(before, after);
            if (System.Array.IndexOf(LevelColorRemap.GetUsedColors(before), true) >= 0)
            {
                Assert.That(JsonUtility.ToJson(after), Is.Not.EqualTo(JsonUtility.ToJson(before)),
                    "A colour the level uses moves on the board.");
                Assert.That(m_window.hasUnsavedChanges, Is.True);
            }

            Assert.That(root.Q<Label>("status").text, Does.StartWith("Colours: "));

            Undo.PerformUndo();

            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(JsonUtility.ToJson(before)));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        // User order of 2026-09-26: Switch takes the clicked block's colour, not the tool colour, and changes every
        // reference of it; with nothing clicked it changes nothing and says what to do.
        [UnityTest]
        public IEnumerator SwitchTradesTheClickedBlocksColourWithAnotherEverywhere()
        {
            yield return ShowWindowBuiltByUnity();
            VisualElement root = m_window.rootVisualElement;
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root) && GetDraft().Snapshot().BlockCount > 0, Is.True,
                "The fixture database has a loaded level with a block.");

            LevelData before = GetDraft().Snapshot();
            Press(root.Q<Button>("color-switch"));
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(JsonUtility.ToJson(before)),
                "Nothing is clicked, so nothing changes.");
            Assert.That(GetDraft().ColorOrder.IsDefault, Is.True);
            Assert.That(root.Q<HelpBox>(LevelDatabaseWindow.k_hintBoxName).text, Is.EqualTo(LevelDatabaseWindow.k_switchHint));

            LevelBlockData block = before.GetBlockAt(0);
            Assume.That(System.Enum.TryParse(block.ColorId, out PaletteColor current)
                        && (int)current < LevelColorOrder.k_count, "The first block has a gate colour.");
            // A tool colour other than the block's, so the test shows Switch ignores it.
            GetToolState().SetColor(current == PaletteColor.Red ? PaletteColor.Blue : PaletteColor.Red);
            LevelEditorTestAccess.Invoke(m_window, "SelectOnCanvas", new object[] { LevelCanvasSelection.ForBlock(0, block.Id) });
            bool[] usedBefore = LevelColorRemap.GetUsedColors(before);

            Press(root.Q<Button>("color-switch"));

            PaletteColor partner = GetToolState().Color;
            Assert.That(partner, Is.Not.EqualTo(current), "The tool colour follows the switched colour.");
            bool anyUnused = false;
            for (int value = 0; value < usedBefore.Length; value++)
            {
                anyUnused |= value != (int)current && !usedBefore[value];
            }

            if (anyUnused)
            {
                Assert.That(usedBefore[(int)partner], Is.False, "An unused colour is picked while one exists.");
            }

            Assert.That(GetDraft().Snapshot().GetBlockAt(0).ColorId, Is.EqualTo(partner.ToString()));
            // User order of 2026-09-27: the board changes, the order and the Colors panel stay.
            Assert.That(GetDraft().ColorOrder.IsDefault, Is.True);
            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(JsonUtility.ToJson(
                LevelColorRemap.Apply(before, LevelColorOrder.Default, LevelColorOrder.Default.Swap(current, partner)))));
            Assert.That(m_window.hasUnsavedChanges, Is.True, "The clicked block's colour always changes.");

            Undo.PerformUndo();

            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(JsonUtility.ToJson(before)));
        }

        [Test]
        public void AShuffledBoardSurvivesAKeptDraftReload()
        {
            // A named exception to the in-memory fixture database (Tests-Editor-1-F3), like LevelDraftReloadTests:
            // CreateGUI always reloads the real project database, and the kept draft is restored only onto that same
            // level and JSON, so a fixture level would be discarded by the reload this test performs.
            VisualElement root = OpenWindow();
            Assume.That(IsLevelLoaded(root), "The active database has no loaded level.");
            Press(root.Q<Button>("color-shuffle"));
            string colors = JsonUtility.ToJson(GetDraft().Snapshot());

            // A domain reload disables the window, enables it again and rebuilds its GUI.
            Invoke("OnDisable");
            Invoke("OnEnable");
            Invoke("CreateGUI");

            Assert.That(JsonUtility.ToJson(GetDraft().Snapshot()), Is.EqualTo(colors), "The kept draft keeps its colours.");
            Assert.That(GetDraft().ColorOrder.IsDefault, Is.True);
            Assert.That(m_window.rootVisualElement.Q<LevelColorSwatches>().GetBadge(PaletteColor.Red).text, Is.EqualTo("1"));
        }

        // Every block and gate colour maps through one one-to-one colour map, so each block keeps matching its gates.
        private static void AssertOneToOneRecolour(LevelData before, LevelData after)
        {
            string[] map = new string[LevelColorOrder.k_count];
            System.Collections.Generic.HashSet<string> targets = new System.Collections.Generic.HashSet<string>();
            void Check(string from, string to)
            {
                if (!ColorBlockJam.Runtime.Layout.BoardLayout.TryParseDoorColor(from, out PaletteColor color)
                    || (int)color >= LevelColorOrder.k_count)
                {
                    Assert.That(to, Is.EqualTo(from), "A non-gate colour stays.");
                    return;
                }

                if (map[(int)color] == null)
                {
                    Assert.That(targets.Add(to), Is.True, $"{to} is the target of two colours.");
                    map[(int)color] = to;
                }

                Assert.That(to, Is.EqualTo(map[(int)color]), $"Every {from} becomes the same colour.");
            }

            for (int index = 0; index < before.BlockCount; index++)
            {
                Check(before.GetBlockAt(index).ColorId, after.GetBlockAt(index).ColorId);
            }

            for (int index = 0; index < before.DoorCount; index++)
            {
                Check(before.GetDoorAt(index).ColorId, after.GetDoorAt(index).ColorId);
            }
        }

        private VisualElement OpenWindow()
        {
            m_fixture = new LevelEditorWindowFixture();
            // A window action never drives a Piece Editor the designer has open in this Editor.
            VisualElement root = m_fixture.Open(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
            return root;
        }

        // Unity builds this window's GUI itself here: a manual CreateGUI could be followed by Unity's own
        // call, which rebuilds the draft and clears its undo records between an edit and its undo.
        private IEnumerator ShowWindowBuiltByUnity()
        {
            m_fixture = new LevelEditorWindowFixture();
            yield return m_fixture.ShowBuiltByUnity(isPieceEditorOpen: () => false);
            m_window = m_fixture.Window;
        }

        private void Invoke(string method)
        {
            LevelEditorTestAccess.Invoke(m_window, method, null);
        }

        private LevelDraft GetDraft()
        {
            return (LevelDraft)LevelEditorTestAccess.Get(m_window, "m_draft");
        }

        private LevelEditorToolState GetToolState()
        {
            return (LevelEditorToolState)LevelEditorTestAccess.Get(m_window, "m_toolState");
        }

        // Submit presses the button the way a click does.
        private static void Press(Button button)
        {
            UiTestInput.Press(button);
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        private static void ValidateLayout(VisualElement root)
        {
            UiTestInput.ValidateLayout(root);
        }

        // A 4 x 4 level with two gate-coloured blocks and one door, never the real project's active database
        // (Tests-Editor-1-F3).
        private static LevelData CreateDefaultLevel()
        {
            return new LevelData("fixture", "Color order fixture", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Pink") },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(0, 0), new[] { Vector2Int.zero }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), new[] { Vector2Int.zero })
                });
        }
    }
}
