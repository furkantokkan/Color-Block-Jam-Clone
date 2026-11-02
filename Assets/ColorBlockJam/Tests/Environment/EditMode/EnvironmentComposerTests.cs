using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class EnvironmentComposerTests
    {
        internal const string DatabasePath = "Assets/ColorBlockJam/Data/Environment/EnvironmentDatabase.asset";
        internal const string PrefabPath = "Assets/ColorBlockJam/Prefabs/Environment/Environment.prefab";
        internal const string FloorBasePath = "Assets/ColorBlockJam/Prefabs/Environment/FloorBase.prefab";

        private GameObject m_root;
        private Grid m_grid;
        private EnvironmentDatabase m_database;
        private EnvironmentComposer m_composer;
        private List<EnvironmentPiecePlacement> m_placements;
        private BoardPieceView m_wallPiece;
        private BoardPieceView m_cornerPiece;

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(DatabasePath);
            Assert.That(m_database, Is.Not.Null, DatabasePath);
            // Placements are grid positions; map them with a copy of the prefab's BoardRoot
            // Grid so the golden local positions below stay the reference.
            m_root = new GameObject("EnvironmentComposerTests", typeof(Grid));
            m_grid = m_root.GetComponent<Grid>();
            Grid prefabGrid = GetPrefabGrid();
            m_grid.cellSize = prefabGrid.cellSize;
            m_grid.cellGap = prefabGrid.cellGap;
            m_grid.cellLayout = prefabGrid.cellLayout;
            m_grid.cellSwizzle = prefabGrid.cellSwizzle;
            m_composer = new EnvironmentComposer(m_database);
            m_placements = new List<EnvironmentPiecePlacement>();
            m_wallPiece = m_database.RuleSet.GetRule(1).Output;
            m_cornerPiece = m_database.RuleSet.GetRule(0).Output;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_root);
        }

        [Test]
        public void DatabaseAuthorsThreeRotatedFloorVoidRulesOnTheBoardKitPieces()
        {
            Assert.That(m_database.Validate(out string error), Is.True, error);
            EnvironmentRuleSet rules = m_database.RuleSet;
            Assert.That(AssetDatabase.GetAssetPath(rules), Is.EqualTo(EnvironmentRuleSetTests.RuleSetPath));
            Assert.That(rules.RuleCount, Is.EqualTo(3));
            Assert.That(rules.GetRule(0).Output.name, Is.EqualTo("Corner_1"));
            Assert.That(rules.GetRule(1).Output.name, Is.EqualTo("Wall"));
            Assert.That(rules.GetRule(2).Output.name, Is.EqualTo("Corner_1"));
            Assert.That(AssetDatabase.GetAssetPath(rules.GetRule(1).Output),
                Is.EqualTo("Assets/ColorBlockJam/Prefabs/Board/Pieces/Wall.prefab"));
            Assert.That(m_database.FloorPiece.name, Is.EqualTo("Grid"));
            Assert.That(AssetDatabase.GetAssetPath(m_database.FloorBasePiece), Is.EqualTo(FloorBasePath));
            Assert.That(m_database.DoorPiece.name, Is.EqualTo("Door_1x1"));
            Assert.That(m_database.DoorArrowPiece.name, Is.EqualTo("Door_Arrow"));
            Assert.That(m_database.WallPieceScale, Is.EqualTo(new Vector3(0.5f, 1f, 0.5f)));
            Assert.That(m_database.DoorPieceScale, Is.EqualTo(new Vector3(1f, 1f, 0.5f)));
        }

        // Pieces carry no colour of their own (DEC-123): the composer tints every placement, with the
        // colours the floor and wall prefabs showed before (Ground and Wall).
        [Test]
        public void FloorAndWallPlacementsCarryTheDatabaseFloorAndWallColours()
        {
            Assert.That(m_database.GetFloorColor(), Is.EqualTo(GetPaletteColor(PaletteColor.Ground)));
            Assert.That(m_database.GetWallColor(), Is.EqualTo(GetPaletteColor(PaletteColor.Wall)));
            Compose(BoardLayoutTests.Create(4, 3));

            int floors = 0;
            int walls = 0;
            foreach (EnvironmentPiecePlacement placement in m_placements)
            {
                if (placement.Piece == m_database.FloorPiece)
                {
                    floors++;
                    Assert.That(placement.Tint, Is.EqualTo(m_database.GetFloorColor()));
                }
                else if (placement.Piece == m_wallPiece || placement.Piece == m_cornerPiece)
                {
                    walls++;
                    Assert.That(placement.Tint, Is.EqualTo(m_database.GetWallColor()));
                }
            }

            Assert.That(floors, Is.EqualTo(12));
            Assert.That(walls, Is.EqualTo(32));
        }

        private static Color GetPaletteColor(PaletteColor color)
        {
            return AssetDatabase.LoadAssetAtPath<GamePalette>("Assets/ColorBlockJam/Data/Art/GamePalette.asset").GetColor(color);
        }

        [Test]
        public void RectangleGetsFloorAWallRingAndFourOuterCorners()
        {
            Summary summary = Compose(BoardLayoutTests.Create(4, 3));

            Assert.That(summary.Floors, Is.EqualTo(12));
            Assert.That(summary.FloorBases, Is.EqualTo(12));
            Assert.That(summary.Walls, Is.EqualTo(28));
            Assert.That(summary.OuterCorners, Is.EqualTo(4));
            Assert.That(summary.InnerCorners, Is.EqualTo(0));
            AssertCorner(new Vector3(-0.75f, 0f, -0.75f), 180, false);
            AssertCorner(new Vector3(3.75f, 0f, 2.75f), 0, false);
            AssertWall(new Vector3(3.75f, 0f, 1.25f), alongZ: true);
            AssertWall(new Vector3(0.25f, 0f, -0.75f), alongZ: false);
        }

        [Test]
        public void EveryFloorCellGetsOneTintedBaseSquareAtItsCentreAndVoidCellsGetNone()
        {
            BoardLayout[] layouts =
            {
                BoardLayoutTests.Create(4, 3),
                BoardLayoutTests.CreateHoleAndNotchLayout(),
                // A corner notch.
                BoardLayoutTests.Create(5, 5, BoardLayoutTests.Cells((4, 4))),
            };

            foreach (BoardLayout layout in layouts)
            {
                Summary summary = Compose(layout);
                int floorCells = 0;
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        Vector3 position = layout.CellToLocal(new Vector2Int(x, y)) + new Vector3(0f, -0.01f, 0f);
                        int bases = CountAt(position, m_database.FloorBasePiece);
                        if (layout.IsFloor(x, y))
                        {
                            floorCells++;
                            Assert.That(bases, Is.EqualTo(1), $"floor base under cell ({x}, {y})");
                            EnvironmentPiecePlacement placement = Find(position, m_database.FloorBasePiece);
                            Assert.That(placement.Scale, Is.EqualTo(Vector3.one));
                            Assert.That(placement.Yaw, Is.Zero);
                            Assert.That(placement.Tint, Is.EqualTo(m_database.GetFloorBaseColor()));
                        }
                        else
                        {
                            Assert.That(bases, Is.Zero, $"void cell ({x}, {y}) must show the background");
                        }
                    }
                }

                Assert.That(summary.FloorBases, Is.EqualTo(floorCells));
                Assert.That(summary.Floors, Is.EqualTo(floorCells));
            }
        }

        [Test]
        public void InteriorWallRowFormsTheGroovedBlockOfReference17()
        {
            Summary summary = Compose(BoardLayoutTests.Create(7, 5, BoardLayoutTests.Cells((2, 2), (3, 2), (4, 2))));

            Assert.That(summary.Floors, Is.EqualTo(32));
            Assert.That(summary.InnerCorners, Is.EqualTo(4));
            AssertWall(new Vector3(2.75f, 0f, 1.75f), alongZ: false);
            AssertWall(new Vector3(3.25f, 0f, 1.75f), alongZ: false);
            AssertWall(new Vector3(2.75f, 0f, 2.25f), alongZ: false);
            AssertWall(new Vector3(3.25f, 0f, 2.25f), alongZ: false);
            AssertCorner(new Vector3(1.75f, 0f, 1.75f), 180, true);
            AssertCorner(new Vector3(1.75f, 0f, 2.25f), 270, true);
            AssertCorner(new Vector3(4.25f, 0f, 1.75f), 90, true);
            AssertCorner(new Vector3(4.25f, 0f, 2.25f), 0, true);
        }

        [Test]
        public void HoleIsRingedByWallsAndInnerCornersAndKeepsItsCentreOpen()
        {
            Compose(BoardLayoutTests.CreateHoleAndNotchLayout());

            AssertCorner(new Vector3(1.75f, 0f, 1.75f), 180, true);
            AssertCorner(new Vector3(3.25f, 0f, 1.75f), 90, true);
            AssertCorner(new Vector3(1.75f, 0f, 3.25f), 270, true);
            AssertCorner(new Vector3(3.25f, 0f, 3.25f), 0, true);
            AssertNothingAt(new Vector3(2.25f, 0f, 2.25f));
            AssertNothingAt(new Vector3(2.75f, 0f, 2.25f));
            AssertNothingAt(new Vector3(2.25f, 0f, 2.75f));
            AssertNothingAt(new Vector3(2.75f, 0f, 2.75f));
            AssertWall(new Vector3(0.25f, 0f, 4.75f), alongZ: false);
            // Notch cell (1,5): its lower-right quadrant faces floor at (2,5) and (1,4).
            AssertCorner(new Vector3(1.25f, 0f, 4.75f), 90, true);
        }

        [Test]
        public void CornerNotchTurnsItsInnerCornerTowardTheFloor()
        {
            Summary summary = Compose(BoardLayoutTests.Create(5, 5, BoardLayoutTests.Cells((4, 4))));

            Assert.That(summary.Floors, Is.EqualTo(24));
            Assert.That(summary.InnerCorners, Is.EqualTo(1));
            AssertCorner(new Vector3(3.75f, 0f, 3.75f), 180, true);
            AssertNothingAt(new Vector3(4f, 0f, 4f));
        }

        [Test]
        public void PinchedFloorGetsTwoInnerCornersMeetingAtTheVertex()
        {
            Compose(BoardLayoutTests.Create(3, 3, BoardLayoutTests.Cells((1, 0), (0, 1))));

            AssertCorner(new Vector3(0.75f, 0f, 0.25f), 270, true);
            AssertCorner(new Vector3(0.25f, 0f, 0.75f), 90, true);
        }

        [Test]
        public void CorridorGetsWallsOnBothSidesAndOuterCornersAtItsDeadEnds()
        {
            Summary summary = Compose(BoardLayoutTests.Create(5, 3, BoardLayoutTests.Cells(
                (0, 0), (1, 0), (2, 0), (3, 0), (4, 0), (0, 2), (1, 2), (2, 2), (3, 2), (4, 2))));

            Assert.That(summary.Floors, Is.EqualTo(5));
            Assert.That(summary.Walls, Is.EqualTo(24));
            Assert.That(summary.OuterCorners, Is.EqualTo(4));
            Assert.That(summary.InnerCorners, Is.EqualTo(0));
        }

        [Test]
        public void DoorsReplaceTheWallsBehindTheirSpanAndKeepTheirNeighbourCorners()
        {
            Summary summary = Compose(BoardLayoutTests.Create(5, 4, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 2, "Red"),
                BoardLayoutTests.Door("right", BoardSide.Right, 0, 1, "Blue"),
                BoardLayoutTests.Door("bottom", BoardSide.Bottom, 4, 1, "Yellow")));

            Assert.That(summary.Walls, Is.EqualTo(2 * (5 + 4) * 2 - 2 * 4));
            Assert.That(summary.Doors, Is.EqualTo(4));
            Assert.That(summary.OuterCorners, Is.EqualTo(4));
            AssertNothingAt(new Vector3(0.75f, 0f, 3.75f), m_wallPiece);
            AssertNothingAt(new Vector3(2.25f, 0f, 3.75f), m_wallPiece);
            AssertNothingAt(new Vector3(4.75f, 0f, -0.25f), m_wallPiece);
            AssertNothingAt(new Vector3(4.25f, 0f, -0.75f), m_wallPiece);
            AssertCorner(new Vector3(4.75f, 0f, -0.75f), 90, false);
            AssertDoor(new Vector3(1f, 0f, 3.75f), 0, PaletteColor.Red);
            AssertDoor(new Vector3(2f, 0f, 3.75f), 0, PaletteColor.Red);
            AssertDoor(new Vector3(4.75f, 0f, 0f), 90, PaletteColor.Blue);
            AssertDoor(new Vector3(4f, 0f, -0.75f), 0, PaletteColor.Yellow);
            Assert.That(summary.Arrows, Is.EqualTo(3));
            AssertArrow(new Vector3(1.5f, 0f, 3.75f), 180, PaletteColor.Red);
            AssertArrow(new Vector3(4.75f, 0f, 0f), 270, PaletteColor.Blue);
            AssertArrow(new Vector3(4f, 0f, -0.75f), 0, PaletteColor.Yellow);
        }

        [Test]
        public void TransposedBoardGivesTheTransposedPieces()
        {
            Compose(BoardLayoutTests.Create(7, 5, BoardLayoutTests.Cells((2, 2), (3, 2), (4, 2))));
            HashSet<string> original = CreateSignatures(transpose: true);
            Compose(BoardLayoutTests.Create(5, 7, BoardLayoutTests.Cells((2, 2), (2, 3), (2, 4))));
            HashSet<string> transposed = CreateSignatures(transpose: false);

            Assert.That(transposed.SetEquals(original), Is.True);
        }

        [Test]
        public void EveryPieceHasItsOwnPosition()
        {
            Compose(BoardLayoutTests.CreateHoleAndNotchLayout(
                BoardLayoutTests.Door("left", BoardSide.Left, 0, 2, "Pink")));
            HashSet<string> seen = new HashSet<string>();
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                Assert.That(seen.Add(placement.Piece.name + ToLocal(placement).ToString("F3")), Is.True,
                    $"{placement.Piece.name} at {ToLocal(placement)}");
            }
        }

        [Test]
        public void RecomposingAPreparedBoardAllocatesZeroBytes()
        {
            BoardLayout layout = BoardLayoutTests.Create(7, 5, BoardLayoutTests.Cells((2, 2), (3, 2), (4, 2)),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 2, "Red"));
            m_composer.Compose(layout, m_placements);

            // GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor; the Recorder-based
            // constraint below is the one that actually counts Mono allocations (AD-27).
            Assert.That(() => { }, Is.Not.AllocatingGCMemory(), "warms the recorder");
            Assert.That(() => m_composer.Compose(layout, m_placements), Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void PlayAreaGateSpansGroupEachDoorsSlotsThenItsArrowInAuthoredDoorOrder()
        {
            BoardLayout layout = BoardLayoutTests.Create(5, 4, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 2, "Red"),
                BoardLayoutTests.Door("right", BoardSide.Right, 0, 1, "Blue"),
                BoardLayoutTests.Door("bottom", BoardSide.Bottom, 4, 1, "Yellow"));
            List<EnvironmentGateSpan> gates = new List<EnvironmentGateSpan>();
            m_composer.ComposePlayArea(layout, m_placements, gates);

            int[] lengths = { 2, 1, 1 };
            Assert.That(gates.Count, Is.EqualTo(lengths.Length));
            int next = gates[0].FirstPlacement;
            for (int door = 0; door < gates.Count; door++)
            {
                EnvironmentGateSpan span = gates[door];
                Assert.That(span.DoorIndex, Is.EqualTo(door));
                Assert.That(span.FirstPlacement, Is.EqualTo(next), "spans are contiguous");
                // A one-slot gate is closed by two half-length ends; a longer gate by an end on each end slot.
                int pieces = Math.Max(lengths[door], 2);
                Assert.That(span.PlacementCount, Is.EqualTo(pieces + 1), "its slot pieces and one arrow");
                for (int piece = 0; piece < pieces; piece++)
                {
                    bool isEnd = piece == 0 || piece == pieces - 1;
                    Assert.That(m_placements[span.FirstPlacement + piece].Piece,
                        Is.EqualTo(isEnd ? m_database.DoorEndPiece : m_database.DoorPiece));
                }

                Assert.That(m_placements[span.FirstPlacement + pieces].Piece, Is.EqualTo(m_database.DoorArrowPiece));
                next = span.FirstPlacement + span.PlacementCount;
            }

            Assert.That(next, Is.EqualTo(m_placements.Count), "doors are composed last");

            // Recomposing into the prepared lists allocates nothing. The Recorder-based constraint counts
            // Mono allocations; GC.GetAllocatedBytesForCurrentThread reads 0 in the 2022.3 Editor.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory(), "warms the recorder");
            Assert.That(() => m_composer.ComposePlayArea(layout, m_placements, gates), Is.Not.AllocatingGCMemory());
            Assert.That(gates.Count, Is.EqualTo(lengths.Length));
        }

        [Test]
        public void ComposeNamesARuleSetEditMadeAfterTheComposerWasCreated()
        {
            EnvironmentRuleSet rules = Object.Instantiate(m_database.RuleSet);
            EnvironmentDatabase database = Object.Instantiate(m_database);
            try
            {
                SerializedObject serializedDatabase = new SerializedObject(database);
                serializedDatabase.FindProperty("m_ruleSet").objectReferenceValue = rules;
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                EnvironmentComposer composer = new EnvironmentComposer(database);
                BoardLayout layout = BoardLayoutTests.Create(3, 3);
                composer.Compose(layout, m_placements);
                int count = m_placements.Count;

                // The inspector's Add rule leaves a new rule without an output.
                SerializedObject serializedRules = new SerializedObject(rules);
                serializedRules.FindProperty("m_rules").GetArrayElementAtIndex(1)
                    .FindPropertyRelative("m_output").objectReferenceValue = null;
                serializedRules.ApplyModifiedPropertiesWithoutUndo();

                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(() => composer.Compose(layout, m_placements));
                StringAssert.Contains("Environment rule 1: it needs an output piece prefab.", exception.Message);
                Assert.That(m_placements.Count, Is.EqualTo(count), "A rejected compose leaves the output untouched.");
            }
            finally
            {
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(rules);
            }
        }

        [Test]
        public void ReferenceLevelsLoadWithTheCurrentLoaderAndGetACompleteRing()
        {
            string json = File.ReadAllText(GetReferencePath("reference-levels.v1.json"));
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] levels, out string error), Is.True, error);
            Assert.That(levels.Length, Is.EqualTo(9));

            foreach (LevelData level in levels)
            {
                Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out error), Is.True, error);
                int doorSegments = 0;
                for (int index = 0; index < level.DoorCount; index++)
                {
                    doorSegments += level.GetDoorAt(index).Length;
                }

                Summary summary = Compose(layout);
                // A rectangle's ring has 4 (W + H) wall quadrants; each door segment replaces two.
                Assert.That(summary.Floors, Is.EqualTo(level.Width * level.Height), level.Id);
                Assert.That(summary.FloorBases, Is.EqualTo(level.Width * level.Height), level.Id);
                Assert.That(summary.Walls, Is.EqualTo(4 * (level.Width + level.Height) - 2 * doorSegments), level.Id);
                Assert.That(summary.Doors, Is.EqualTo(doorSegments), level.Id);
                Assert.That(summary.Arrows, Is.EqualTo(level.DoorCount), level.Id);
                Assert.That(summary.OuterCorners, Is.EqualTo(4), level.Id);
                Assert.That(summary.InnerCorners, Is.Zero, level.Id);
            }
        }

        [Test]
        public void ReferenceLevelNineTurnsItsVoidNotchesIntoInnerCorners()
        {
            LevelData[] levels = ReadPlannedLevels();
            Assert.That(levels.Length, Is.EqualTo(10));
            foreach (LevelData level in levels)
            {
                Compose(CreateLayout(level));
            }

            // BoardLayout.TryCreate(LevelData) passes the level's void cells (environment AC-2).
            Assert.That(levels[8].VoidCellCount, Is.EqualTo(2));
            Summary summary = Compose(CreateLayout(levels[8]));

            Assert.That(summary.Floors, Is.EqualTo(34));
            Assert.That(summary.FloorBases, Is.EqualTo(34));
            Assert.That(summary.InnerCorners, Is.EqualTo(2));
            Assert.That(summary.OuterCorners, Is.EqualTo(6));
            AssertCorner(new Vector3(0.25f, 0f, 0.25f), 0, true);
            AssertCorner(new Vector3(4.75f, 0f, 0.25f), 270, true);
            // The notch turns the left wall east and the bottom wall north around void (0,0).
            AssertCorner(new Vector3(-0.75f, 0f, 0.25f), 180, false);
            AssertCorner(new Vector3(0.25f, 0f, -0.75f), 180, false);
            AssertCorner(new Vector3(5.75f, 0f, 0.25f), 90, false);
            AssertNothingAt(new Vector3(0f, 0f, 0f));
            AssertDoor(new Vector3(1f, 0f, -0.75f), 0, PaletteColor.Blue);
        }

        [Test]
        public void PlannedAndCurrentReferenceFilesDescribeTheSameBoards()
        {
            LevelDatabaseJson.TryRead(File.ReadAllText(GetReferencePath("reference-levels.v1.json")),
                out LevelData[] current, out _);
            LevelData[] planned = ReadPlannedLevels();

            foreach (LevelData level in current)
            {
                LevelData match = null;
                foreach (LevelData candidate in planned)
                {
                    if (candidate.Id == level.Id) match = candidate;
                }

                Assert.That(match, Is.Not.Null, level.Id);
                Assert.That(match.VoidCellCount, Is.Zero, level.Id);
                Assert.That(match.Width, Is.EqualTo(level.Width), level.Id);
                Assert.That(match.Height, Is.EqualTo(level.Height), level.Id);
                Assert.That(match.DoorCount, Is.EqualTo(level.DoorCount), level.Id);
                Assert.That(match.BlockCount, Is.EqualTo(level.BlockCount), level.Id);
            }
        }

        [Test]
        public void PrefabHasOneGridOnBoardRootAndNoTilemap()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Grid[] grids = prefab.GetComponentsInChildren<Grid>(true);
            Assert.That(grids.Length, Is.EqualTo(1));
            Assert.That(grids[0].transform, Is.SameAs(prefab.GetComponent<EnvironmentView>().BoardRoot));
            Assert.That(grids[0].cellSize.x, Is.EqualTo(0.5f));
            Assert.That(grids[0].cellSize.y, Is.EqualTo(0.5f));
            Assert.That(grids[0].cellSwizzle, Is.EqualTo(GridLayout.CellSwizzle.XZY));
            Assert.That(grids[0].cellLayout, Is.EqualTo(GridLayout.CellLayout.Rectangle));
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component.GetType().Name, Is.Not.EqualTo("Tilemap"), component.name);
            }
        }

        [Test]
        public void GridMapsCellsAndQuadrantsToTheBoardLayoutPositions()
        {
            BoardLayout layout = BoardLayoutTests.Create(4, 3);
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    // A cell centre is the quadrant grid vertex (2x, 2y).
                    Assert.That(m_grid.CellToLocalInterpolated(new Vector3(2 * x, 2 * y, 0f)),
                        Is.EqualTo(layout.CellToLocal(new Vector2Int(x, y))));
                    // Its lower-left quadrant is grid cell (2x - 1, 2y - 1).
                    Assert.That(m_grid.CellToLocalInterpolated(new Vector3(2 * x - 0.5f, 2 * y - 0.5f, 0f)),
                        Is.EqualTo(layout.CellToLocal(new Vector2Int(x, y)) + new Vector3(-0.25f, 0f, -0.25f)));
                }
            }
        }

        private static Grid GetPrefabGrid()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            return prefab.GetComponent<EnvironmentView>().BoardRoot.GetComponent<Grid>();
        }

        private Vector3 ToLocal(EnvironmentPiecePlacement placement)
        {
            return m_grid.CellToLocalInterpolated(placement.GridPosition);
        }

        private static string GetReferencePath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "levels", fileName));
        }

        // Authoring is the reader's default validation mode for anything but the actual game load
        // (LevelReadMode.Runtime is LevelCatalogLoader.LoadPackAsync only), so tests read it the same way.
        private static LevelData[] ReadPlannedLevels()
        {
            bool read = LevelDatabaseJson.TryRead(File.ReadAllText(GetReferencePath("reference-levels.json")),
                LevelReadMode.Authoring, out LevelData[] levels, out string error);
            Assert.That(read, Is.True, error);
            return levels;
        }

        private static BoardLayout CreateLayout(LevelData level)
        {
            bool created = BoardLayout.TryCreate(level, out BoardLayout layout, out string error);
            Assert.That(created, Is.True, $"{level.Id}: {error}");
            return layout;
        }

        private Summary Compose(BoardLayout layout)
        {
            m_composer.Compose(layout, m_placements);
            Summary summary = default;
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                if (placement.Piece == m_database.FloorPiece) summary.Floors++;
                else if (placement.Piece == m_database.FloorBasePiece) summary.FloorBases++;
                else if (placement.Piece == m_database.DoorPiece) summary.Doors++;
                else if (placement.Piece == m_database.DoorArrowPiece) summary.Arrows++;
                else if (placement.Piece == m_database.BlockFillPiece) continue;
                else if (placement.Piece == m_wallPiece) summary.Walls++;
                else if (IsInnerCorner(placement)) summary.InnerCorners++;
                else summary.OuterCorners++;
            }

            return summary;
        }

        // A corner is inner when its rounded side faces the quadrant's own outward
        // direction (toward floor) and outer when it faces away from the floor.
        private bool IsInnerCorner(EnvironmentPiecePlacement placement)
        {
            Vector2Int quadrant = GetQuadrantSign(ToLocal(placement));
            return GetRoundedDirection(placement.Yaw) == quadrant;
        }

        private static Vector2Int GetQuadrantSign(Vector3 position)
        {
            float fractionX = position.x - Mathf.Floor(position.x);
            float fractionZ = position.z - Mathf.Floor(position.z);
            return new Vector2Int(fractionX < 0.5f ? 1 : -1, fractionZ < 0.5f ? 1 : -1);
        }

        // Corner_1 at yaw 0 has its rounded vertical edge at local (+X, +Z).
        private static Vector2Int GetRoundedDirection(int yaw)
        {
            switch (yaw)
            {
                case 0: return new Vector2Int(1, 1);
                case 90: return new Vector2Int(1, -1);
                case 180: return new Vector2Int(-1, -1);
                default: return new Vector2Int(-1, 1);
            }
        }

        private HashSet<string> CreateSignatures(bool transpose)
        {
            HashSet<string> signatures = new HashSet<string>();
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                Vector3 position = ToLocal(placement);
                float x = transpose ? position.z : position.x;
                float z = transpose ? position.x : position.z;
                string shape;
                if (placement.Piece == m_wallPiece)
                {
                    bool alongZ = placement.Yaw % 180 == 0;
                    shape = (alongZ != transpose) ? "wall-z" : "wall-x";
                }
                else if (placement.Piece == m_cornerPiece)
                {
                    Vector2Int direction = GetRoundedDirection(placement.Yaw);
                    shape = transpose ? $"corner{direction.y},{direction.x}" : $"corner{direction.x},{direction.y}";
                }
                else
                {
                    shape = placement.Piece.name;
                }

                signatures.Add($"{shape}@{x:F2},{z:F2}");
            }

            return signatures;
        }

        private void AssertCorner(Vector3 position, int yaw, bool inner)
        {
            EnvironmentPiecePlacement placement = Find(position, m_cornerPiece);
            Assert.That(placement.Yaw, Is.EqualTo(yaw), $"corner yaw at {position}");
            Assert.That(IsInnerCorner(placement), Is.EqualTo(inner), $"inner corner at {position}");
            Assert.That(placement.Scale, Is.EqualTo(m_database.WallPieceScale));
        }

        private void AssertWall(Vector3 position, bool alongZ)
        {
            EnvironmentPiecePlacement placement = Find(position, m_wallPiece);
            Assert.That(placement.Yaw % 180 == 0, Is.EqualTo(alongZ), $"wall direction at {position}");
            Assert.That(placement.Scale, Is.EqualTo(m_database.WallPieceScale));
        }

        private void AssertDoor(Vector3 position, int yaw, PaletteColor color)
        {
            EnvironmentPiecePlacement placement = Find(position, m_database.DoorPiece);
            Assert.That(placement.Yaw, Is.EqualTo(yaw));
            Assert.That(placement.Scale, Is.EqualTo(m_database.DoorPieceScale));
            Assert.That(placement.Tint, Is.EqualTo(m_database.GetDoorColor(color)));
        }

        private void AssertArrow(Vector3 position, int yaw, PaletteColor color)
        {
            EnvironmentPiecePlacement placement = Find(position, m_database.DoorArrowPiece);
            Assert.That(placement.Yaw, Is.EqualTo(yaw), $"arrow yaw at {position}");
            Assert.That(placement.Scale, Is.EqualTo(m_database.DoorArrowScale));
            Assert.That(placement.Tint, Is.EqualTo(m_database.GetDoorArrowColor(color)));
        }

        private void AssertNothingAt(Vector3 position, BoardPieceView piece = null)
        {
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                if ((piece == null || placement.Piece == piece) && ToLocal(placement) == position)
                {
                    Assert.Fail($"Unexpected {placement.Piece.name} at {position}.");
                }
            }
        }

        private int CountAt(Vector3 position, BoardPieceView piece)
        {
            int count = 0;
            for (int index = 0; index < m_placements.Count; index++)
            {
                if (m_placements[index].Piece == piece && ToLocal(m_placements[index]) == position)
                {
                    count++;
                }
            }

            return count;
        }

        private EnvironmentPiecePlacement Find(Vector3 position, BoardPieceView piece)
        {
            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                if (placement.Piece == piece && ToLocal(placement) == position)
                {
                    return placement;
                }
            }

            Assert.Fail($"No {piece.name} at {position}.");
            return default;
        }

        private struct Summary
        {
            public int Floors;
            public int FloorBases;
            public int Walls;
            public int InnerCorners;
            public int OuterCorners;
            public int Doors;
            public int Arrows;
        }
    }
}
