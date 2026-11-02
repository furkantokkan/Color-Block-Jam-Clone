using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor canvas inside the window: it draws the selected draft with one label per block,
    /// keeps the board centred when the window changes size, and syncs a clicked block or door with the Selected
    /// Block dropdown without changing the draft or writing the level JSON.
    /// </summary>
    public sealed class LevelCanvasElementTests
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
        public void CanvasDrawsTheSelectedLevelWithOneLabelPerBlock()
        {
            VisualElement root = OpenWindow(960f, 640f);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>(LevelCanvasElement.k_name);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.parent.name, Is.EqualTo("canvas-slot"), "The canvas fills the slot under the selection.");
            // A fixture database load guarantees a loaded level, whatever the real project's active database
            // holds (Tests-Editor-1-F3).
            LevelData level = CreateDefaultLevel();
            m_fixture.LoadDatabase(level);
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");

            Assert.That(canvas.Model.Width, Is.EqualTo(level.Width));
            Assert.That(canvas.Model.Height, Is.EqualTo(level.Height));
            Assert.That(canvas.Model.DoorCount, Is.EqualTo(level.DoorCount));
            var labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
            Assert.That(labels, Has.Count.EqualTo(level.BlockCount), "One label per block.");
            for (int index = 0; index < level.BlockCount; index++)
            {
                Assert.That(labels[index].text, Is.EqualTo(level.GetBlockAt(index).Id.ToUpperInvariant()));
                Assert.That(labels[index].pickingMode, Is.EqualTo(PickingMode.Ignore));
            }

            AssertCentred(canvas);
        }

        [Test]
        public void ResizedWindowKeepsTheBoardCentredWithCellsOfAtLeastEighteenPixels()
        {
            VisualElement root = OpenWindow(960f, 640f);
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            float smallCell = AssertCentred(root.Q<LevelCanvasElement>());
            CloseWindow();

            root = OpenWindow(1280f, 800f);
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            float largeCell = AssertCentred(root.Q<LevelCanvasElement>());

            Assert.That(largeCell, Is.GreaterThan(smallCell), "A larger window gets larger cells.");
        }

        [Test]
        public void ClickedBlockAndDropdownSelectionStayInSync()
        {
            VisualElement root = OpenWindow(960f, 640f);
            LevelData level = CreateDefaultLevel();
            m_fixture.LoadDatabase(level);
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            Assert.That(level.BlockCount, Is.GreaterThanOrEqualTo(2), "The fixture level has two blocks.");
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            DropdownField blockChoice = root.Q<Foldout>("blocks-group").Query<DropdownField>().First();

            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForBlock(0, level.GetBlockAt(0).Id)),
                "The first block starts selected, as the dropdown shows.");

            Click(root, canvas, canvas.Model.GetBlock(1).LabelCell);

            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForBlock(1, level.GetBlockAt(1).Id)));
            Assert.That(blockChoice.index, Is.EqualTo(1), "The click selects the block in the dropdown.");

            blockChoice.value = blockChoice.choices[0];

            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForBlock(0, level.GetBlockAt(0).Id)),
                "The dropdown selects the block on the canvas.");
            Assert.That(m_window.hasUnsavedChanges, Is.False, "Selecting never edits the draft.");
        }

        [Test]
        public void ClickedDoorIsSelectedAndKeepsTheTransformToolsShown()
        {
            VisualElement root = OpenWindow(960f, 640f);
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Assert.That(canvas.Model.DoorCount, Is.GreaterThan(0), "The fixture level has a door.");
            LevelCanvasDoor door = canvas.Model.GetDoor(0);
            Foldout blocks = root.Q<Foldout>("blocks-group");
            DropdownField blockChoice = blocks.Query<DropdownField>().First();

            Rect bar = canvas.Geometry.GetEdgeRect(door.Side, door.Start, LevelCanvasGeometry.k_doorDepth);
            Click(root, canvas.LocalToWorld(bar.center));

            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForDoor(0, door.Id)));
            Assert.That(blockChoice.value, Is.EqualTo($"Door {door.Id}"));
            Assert.That(root.Q("block-actions").style.display.value, Is.EqualTo(DisplayStyle.Flex),
                "The transform tools need no selected block, so they stay shown.");

            // A click on a ring corner selects nothing new.
            Rect board = canvas.Geometry.BoardRect;
            float quarter = 0.25f * canvas.Geometry.CellSize;
            Click(root, canvas.LocalToWorld(new Vector2(board.xMin - quarter, board.yMin - quarter)));
            Assert.That(canvas.Selection, Is.EqualTo(LevelCanvasSelection.ForDoor(0, door.Id)));

            blockChoice.value = blockChoice.choices[0];
            Assert.That(canvas.Selection.Kind, Is.EqualTo(LevelCanvasSelectionKind.Block));
            Assert.That(root.Q("block-actions").style.display.value, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void PointerMovesInsideOneCellKeepTheSameHover()
        {
            VisualElement root = OpenWindow(960f, 640f);
            m_fixture.LoadDatabase(CreateDefaultLevel());
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            Rect cell = canvas.Geometry.GetCellRect(Vector2Int.zero);

            UiTestInput.SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(cell.min + Vector2.one * 2f));
            LevelCanvasHit first = canvas.Hover;
            UiTestInput.SendPointer(root, EventType.MouseMove, canvas.LocalToWorld(cell.max - Vector2.one * 2f));

            Assert.That(first.Kind, Is.Not.EqualTo(LevelCanvasHitKind.Outside));
            Assert.That(canvas.Hover, Is.EqualTo(first));
            Assert.That(m_window.hasUnsavedChanges, Is.False);
        }

        [Test]
        public void ModelWithFewerBlocksAndAnotherBoardSizeKeepsOneLabelPerBlock()
        {
            LevelCanvasElement canvas = new LevelCanvasElement();
            LevelData[] levels =
            {
                CreateLevel(5, 6, 3),
                CreateLevel(5, 5, 2),
                CreateLevel(3, 3, 1),
                null
            };

            for (int index = 0; index < levels.Length; index++)
            {
                LevelCanvasModel model = LevelCanvasModel.Create(levels[index]);
                Assert.DoesNotThrow(() => canvas.SetModel(model), $"Model {index} replaces the previous one.");
                var labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
                Assert.That(labels, Has.Count.EqualTo(model.BlockCount), "One label per block.");
                for (int block = 0; block < labels.Count; block++)
                {
                    Assert.That(labels[block].text, Is.EqualTo(model.GetBlock(block).Id.ToUpperInvariant()));
                }
            }
        }

        [Test]
        public void AnOverlappingBlocksLabelIsStruckThroughUntilTheBlocksAreApart()
        {
            // Contract D3-B: only a re-stamp leaves blocks sharing a cell; b2 sits on b1 here.
            LevelCanvasElement canvas = new LevelCanvasElement();
            LevelData overlapping = CreateLevel(4, 4, 3);
            LevelBlockData[] blocks = LevelRecordCopy.GetBlocks(overlapping);
            blocks[1] = LevelRecordCopy.WithOrigin(blocks[1], Vector2Int.zero);

            canvas.SetModel(LevelCanvasModel.Create(LevelRecordCopy.WithBlocks(overlapping, blocks)));

            var labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
            Assert.That(labels.ConvertAll(label => label.text), Is.EqualTo(new[] { "<s>B1</s>", "<s>B2</s>", "B3" }));
            Assert.That(labels.TrueForAll(label => label.enableRichText), Is.True, "<s> draws a line, not its text.");

            // The next model, after a move apart, an undo or a delete, sets every label again.
            canvas.SetModel(LevelCanvasModel.Create(overlapping));
            labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
            Assert.That(labels.ConvertAll(label => label.text), Is.EqualTo(new[] { "B1", "B2", "B3" }));
        }

        [Test]
        public void SwitchingToALevelWithFewerBlocksRefreshesTheCanvasAndTheBlockChoices()
        {
            VisualElement root = OpenWindow(960f, 640f);
            // A fixture database of exactly two levels, the second strictly larger with more blocks, makes the
            // larger-then-smaller switch deterministic instead of scanning the real project's active database
            // (Tests-Editor-1-F3).
            // A database refuses a repeated level ID, so the two levels get their own.
            LevelData smallerLevel = CreateLevel(3, 3, 1, "smaller");
            LevelData largerLevel = CreateLevel(5, 5, 3, "larger");
            m_fixture.LoadDatabase(smallerLevel, largerLevel);
            Assert.That(IsLevelLoaded(root), Is.True, "The fixture database has a loaded level.");
            const int larger = 1;
            const int smaller = 0;
            DropdownField target = null;
            root.Query<DropdownField>().ForEach(field =>
            {
                if (field.label == "Target Level")
                {
                    target = field;
                }
            });
            Assert.That(target, Is.Not.Null);
            LevelCanvasElement canvas = root.Q<LevelCanvasElement>();
            DropdownField blockChoice = root.Q<Foldout>("blocks-group").Query<DropdownField>().First();

            target.value = target.choices[larger];
            target.value = target.choices[smaller];

            LevelData level = smallerLevel;
            var labels = canvas.Query<Label>(className: LevelCanvasElement.k_labelClass).ToList();
            Assert.That(canvas.Model.BlockCount, Is.EqualTo(level.BlockCount));
            Assert.That(labels, Has.Count.EqualTo(level.BlockCount), "One label per block of the new level.");
            Assert.That(blockChoice.choices, Has.Count.EqualTo(level.BlockCount), "The block choices follow the new level.");
            for (int index = 0; index < level.BlockCount; index++)
            {
                Assert.That(labels[index].text, Is.EqualTo(level.GetBlockAt(index).Id.ToUpperInvariant()));
                Assert.That(blockChoice.choices[index], Is.EqualTo($"{index + 1}. {level.GetBlockAt(index).Id}"));
            }

            Assert.That(m_window.hasUnsavedChanges, Is.False, "Switching levels never edits the draft.");
        }

        // A width x height level with blockCount one-cell blocks b1.. along the bottom row.
        private static LevelData CreateLevel(int width, int height, int blockCount, string id = "l")
        {
            LevelBlockData[] blocks = new LevelBlockData[blockCount];
            for (int index = 0; index < blockCount; index++)
            {
                blocks[index] = new LevelBlockData("b" + (index + 1), "Red", new Vector2Int(index, 0),
                    new[] { Vector2Int.zero });
            }

            return new LevelData(id, "L", width, height, 60, new LevelDoorData[0], blocks);
        }

        private static float AssertCentred(LevelCanvasElement canvas)
        {
            LevelCanvasGeometry geometry = canvas.Geometry;
            Rect content = canvas.contentRect;
            Assert.That(content.width, Is.GreaterThan(0f), "The canvas has a laid-out size.");
            Assert.That(geometry.CellSize, Is.GreaterThanOrEqualTo(LevelCanvasGeometry.k_minCellSize));
            Assert.That(geometry.BoardRect.center.x, Is.EqualTo(content.width * 0.5f).Within(1f));
            Assert.That(geometry.BoardRect.center.y, Is.EqualTo(content.height * 0.5f).Within(1f));
            return geometry.CellSize;
        }

        // Renamed from SendPointerDown (Tests-Editor-1-F2): it already sends a whole click, down then up.
        private static void Click(VisualElement root, LevelCanvasElement canvas, Vector2Int cell)
        {
            UiTestInput.Click(root, canvas.LocalToWorld(canvas.Geometry.GetCellRect(cell).center));
        }

        private static void Click(VisualElement root, Vector2 position)
        {
            UiTestInput.Click(root, position);
        }

        private VisualElement OpenWindow(float width, float height)
        {
            m_fixture = new LevelEditorWindowFixture();
            VisualElement root = m_fixture.Open(width, height);
            m_window = m_fixture.Window;
            return root;
        }

        private static bool IsLevelLoaded(VisualElement root)
        {
            return LevelEditorWindowFixture.IsLevelLoaded(root);
        }

        // A 4 x 4 level with two blocks and one door, never the real project's active database (Tests-Editor-1-F3).
        private static LevelData CreateDefaultLevel()
        {
            return new LevelData("fixture", "Canvas fixture", 4, 4, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 1, "Pink") },
                new[]
                {
                    new LevelBlockData("b1", "Red", new Vector2Int(0, 0), new[] { Vector2Int.zero }),
                    new LevelBlockData("b2", "Blue", new Vector2Int(2, 2), new[] { Vector2Int.zero })
                });
        }
    }
}
