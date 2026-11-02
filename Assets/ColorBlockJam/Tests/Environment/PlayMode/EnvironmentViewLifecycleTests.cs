#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class EnvironmentViewLifecycleTests
    {
        private const string k_floorBaseName = "FloorBase";
        // A 4 x 3 rectangle: 12 floor tiles, 12 floor-base squares, 28 walls and 4 corners, and one
        // floor-base square under each of those 32 wall quadrants (DEC-139, D-002).
        private const int k_rectanglePieceCount = 12 + 12 + 28 + 4 + 32;
        // A kit piece no rectangle wall uses, so a rule set that places it is easy to tell apart.
        private const string k_otherPiecePath = "Assets/ColorBlockJam/Prefabs/Board/Pieces/Door_1x1.prefab";

        private GameObject m_root;
        private EnvironmentView m_view;
        private EnvironmentDatabase m_database;

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentViewTestSupport.DatabasePath);
            m_root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentViewTestSupport.PrefabPath));
            m_view = m_root.GetComponent<EnvironmentView>();
            m_view.Construct(m_database);
        }

        [TearDown]
        public void TearDown()
        {
            if (m_root != null)
            {
                Object.DestroyImmediate(m_root);
            }
        }

        [UnityTest]
        public IEnumerator BuildRentsEveryPieceWithItsTransformAndClearReturnsThemAll()
        {
            m_view.Build(Rectangle(4, 3));
            yield return null;

            Assert.That(m_view.ActivePieceCount, Is.EqualTo(k_rectanglePieceCount));
            Assert.That(CountActivePieces(), Is.EqualTo(k_rectanglePieceCount));
            Assert.That(m_view.BoardRoot.localPosition, Is.EqualTo(new Vector3(-1.5f, 0f, -1f)));
            AssertPiece("Grid", new Vector3(0f, 0f, 0f), Vector3.one);
            AssertPiece(k_floorBaseName, new Vector3(0f, -0.01f, 0f), Vector3.one);
            AssertPiece("Wall", new Vector3(3.75f, 0f, 1.25f), m_database.WallPieceScale);
            // The play area's corners are Corner_End units, each on a base toward the floor (DEC-139).
            AssertPiece("Corner_End", new Vector3(-0.75f, 0f, -0.75f), m_database.WallPieceScale);
            AssertPiece(k_floorBaseName, new Vector3(-0.6875f, -0.01f, -0.6875f), new Vector3(0.375f, 1f, 0.375f));
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.EqualTo(k_rectanglePieceCount));

            m_view.Clear();

            Assert.That(m_view.ActivePieceCount, Is.Zero);
            Assert.That(m_view.IsBuilt, Is.False);
            Assert.That(CountActivePieces(), Is.Zero);
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.Zero);
        }

        [UnityTest]
        public IEnumerator RebuildingReusesPiecesAndOnlyALargerLevelAddsInstances()
        {
            BoardLayout small = Rectangle(4, 3);
            m_view.Build(small);
            int instances = CountAllPieces();
            m_view.Build(small);
            Assert.That(CountAllPieces(), Is.EqualTo(instances), "Restart of the same layout must not rebuild.");

            m_view.Clear();
            m_view.Build(Rectangle(4, 3));
            Assert.That(CountAllPieces(), Is.EqualTo(instances), "A same-size rebuild must reuse pooled pieces.");

            m_view.Build(Rectangle(8, 10));
            int larger = CountAllPieces();
            Assert.That(larger, Is.GreaterThan(instances));

            m_view.Build(Rectangle(4, 3));
            Assert.That(CountAllPieces(), Is.EqualTo(larger), "Shrinking keeps released pieces in the pools.");
            Assert.That(CountActivePieces(), Is.EqualTo(k_rectanglePieceCount));
            yield return null;
        }

        [UnityTest]
        public IEnumerator DoorsCarryTheirPaletteColourAndPiecesResetWhenReleased()
        {
            m_view.Build(Create(5, 4, new LevelDoorData("top", BoardSide.Top, 1, 2, "Red")));
            yield return null;

            // The gate's first slot is its rounded Door_End (DEC-139); it is tinted like every door piece.
            BoardPieceView door = FindActive("Door_End", new Vector3(1f, 0f, 3.75f));
            AtlasColor red = m_database.GetDoorColor(PaletteColor.Red);
            Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(door), red), Is.LessThan(1e-3f));
            Assert.That(door.transform.localScale, Is.EqualTo(m_database.DoorPieceScale));

            // Release makes no GPU write (DEC-123); the next build tints the reused door again.
            m_view.Clear();
            m_view.Build(Create(5, 4, new LevelDoorData("top", BoardSide.Top, 1, 2, "Blue")));
            yield return null;
            BoardPieceView reused = FindActive("Door_End", new Vector3(1f, 0f, 3.75f));
            Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(reused), m_database.GetDoorColor(PaletteColor.Blue)), Is.LessThan(1e-3f));

            // Every piece is tinted by its placement, including the floor and the walls.
            BoardPieceView floor = FindActive("Grid", new Vector3(0f, 0f, 0f));
            Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(floor), m_database.GetFloorColor()), Is.LessThan(1e-3f));
            BoardPieceView wall = FindActive("Wall", new Vector3(0.25f, 0f, -0.75f));
            Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(wall), m_database.GetWallColor()), Is.LessThan(1e-3f));
        }

        [UnityTest]
        public IEnumerator EachFloorCellGetsAPooledGreyBaseSquareAndNoPlaneRemains()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentViewTestSupport.PrefabPath);
            Assert.That(prefab.GetComponentsInChildren<BoardPieceView>(true), Is.Empty,
                "The prefab holds no permanent floor plane; every piece is pooled.");

            // A database copy whose floor-base colour differs from the prefab's own
            // reset colour (Wall), so the check proves the view applies the tint.
            EnvironmentDatabase database = Object.Instantiate(m_database);
            try
            {
                SerializedObject serializedDatabase = new SerializedObject(database);
                serializedDatabase.FindProperty("m_floorBaseColor").intValue = (int)PaletteColor.Ground;
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                m_view.Construct(database);
                AtlasColor floorBaseColor = database.GetFloorBaseColor();

                // A 4 x 3 board with a corner notch at (3, 2): 11 floor cells.
                m_view.Build(Create(4, 3, new[] { new Vector2Int(3, 2) }));
                yield return null;

                List<BoardPieceView> bases = FindActiveFloorBases();
                int wallBases = 0;
                foreach (BoardPieceView floorBase in bases)
                {
                    if (floorBase.transform.localScale != Vector3.one)
                    {
                        // A wall base (D-002): flat under a wall quadrant, never on a floor cell.
                        wallBases++;
                        Vector3 wallBase = floorBase.transform.localPosition;
                        Assert.That(wallBase.y, Is.EqualTo(-0.01f).Within(1e-5f), "under the floor, like the cell bases");
                        Assert.That(m_view.Layout.IsFloor(Mathf.RoundToInt(wallBase.x), Mathf.RoundToInt(wallBase.z)),
                            Is.False, "a wall base stands outside the floor");
                        Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(floorBase), floorBaseColor), Is.LessThan(1e-3f),
                            "Tinted with the database floor-base colour.");
                        continue;
                    }

                    Vector3 position = floorBase.transform.localPosition;
                    Vector2Int cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
                    Assert.That(position, Is.EqualTo(new Vector3(cell.x, -0.01f, cell.y)), "cell centre, under the tile");
                    Assert.That(m_view.Layout.IsFloor(cell.x, cell.y), Is.True, $"no base on void cell {cell}");
                    Assert.That(floorBase.transform.localScale, Is.EqualTo(Vector3.one), "one cell, not a stretched plane");
                    Assert.That(floorBase.Renderer.transform.up.y, Is.EqualTo(0f).Within(1e-4f), "The quad lies flat.");
                    Assert.That(floorBase.Renderer.transform.forward.y, Is.EqualTo(-1f).Within(1e-4f), "It faces up.");
                    Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(floorBase), floorBaseColor), Is.LessThan(1e-3f),
                        "Tinted with the database floor-base colour.");
                }

                Assert.That(bases.Count - wallBases, Is.EqualTo(11), "one base square per floor cell");
                Assert.That(wallBases, Is.EqualTo(CountWallBases(database, m_view.Layout)), "one base per wall quadrant and door slot, as composed");

                m_view.Clear();
                Assert.That(FindActiveFloorBases(), Is.Empty);
                Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.Zero);
                foreach (BoardPieceView floorBase in bases)
                {
                    // Release makes no GPU write (DEC-123): the hidden square keeps its last tint
                    // until its next placement colours it again.
                    Assert.That(floorBase.gameObject.activeSelf, Is.False, "Clear hides the square.");
                    Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(floorBase), floorBaseColor), Is.LessThan(1e-3f),
                        "Clear writes no property block.");
                }

                m_view.Release();
                yield return null;
                Assert.That(m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(true), Is.Empty,
                    "Release destroys the pooled base squares.");
            }
            finally
            {
                m_view.Release();
                Object.DestroyImmediate(database);
            }
        }

        [UnityTest]
        public IEnumerator DisablingClearsAndReleaseDestroysThePooledPieces()
        {
            m_view.Build(Rectangle(4, 3));
            m_root.SetActive(false);
            Assert.That(m_view.ActivePieceCount, Is.Zero);
            Assert.That(m_view.IsBuilt, Is.False);

            m_root.SetActive(true);
            Assert.That(m_view.ActivePieceCount, Is.Zero, "Enabling must not rebuild on its own.");

            m_view.Build(Rectangle(4, 3));
            m_view.Release();
            yield return null;

            Assert.That(CountAllPieces(), Is.Zero);
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.Zero);
        }

        // A designer can edit the rule set while a view keeps its composer and pools
        // (LevelEditor preview, Editor Play Mode).
        [UnityTest]
        public IEnumerator RuleSetEditsAfterTheFirstBuildGetNewPoolsOrAClearError()
        {
            EnvironmentRuleSet rules = Object.Instantiate(m_database.RuleSet);
            EnvironmentDatabase database = Object.Instantiate(m_database);
            try
            {
                SerializedObject serializedDatabase = new SerializedObject(database);
                serializedDatabase.FindProperty("m_ruleSet").objectReferenceValue = rules;
                serializedDatabase.ApplyModifiedPropertiesWithoutUndo();
                m_view.Construct(database);
                m_view.Build(Rectangle(4, 3));

                SetWallRuleOutput(rules, AssetDatabase.LoadAssetAtPath<BoardPieceView>(k_otherPiecePath));
                m_view.Build(Rectangle(4, 3));
                yield return null;

                Assert.That(m_view.ActivePieceCount, Is.EqualTo(k_rectanglePieceCount));
                AssertPiece("Door_1x1", new Vector3(3.75f, 0f, 1.25f), m_database.WallPieceScale);
                Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.EqualTo(k_rectanglePieceCount), "The old pools were disposed with nothing active.");

                SetWallRuleOutput(rules, null);
                InvalidOperationException exception =
                    Assert.Throws<InvalidOperationException>(() => m_view.Build(Rectangle(4, 3)));
                StringAssert.Contains("Environment rule 1: it needs an output piece prefab.", exception.Message);
                Assert.That(m_view.ActivePieceCount, Is.EqualTo(k_rectanglePieceCount), "A rejected build keeps the current play area.");
            }
            finally
            {
                m_view.Release();
                Object.DestroyImmediate(database);
                Object.DestroyImmediate(rules);
            }
        }

        private static void SetWallRuleOutput(EnvironmentRuleSet rules, BoardPieceView output)
        {
            SerializedObject serialized = new SerializedObject(rules);
            serialized.FindProperty("m_rules").GetArrayElementAtIndex(1).FindPropertyRelative("m_output")
                .objectReferenceValue = output;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void EnvironmentComponentsNeverTick()
        {
            Type[] types = { typeof(EnvironmentView), typeof(EnvironmentSceneInstaller) };
            string[] loops = { "Update", "LateUpdate", "FixedUpdate" };
            foreach (Type type in types)
            {
                foreach (string loop in loops)
                {
                    MethodInfo method = type.GetMethod(loop, BindingFlags.Instance | BindingFlags.Public
                        | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    Assert.That(method, Is.Null, $"{type.Name}.{loop}");
                }

                Assert.That(type.GetInterface("IOnityTickable"), Is.Null, type.Name);
            }
        }

        private static BoardLayout Rectangle(int width, int height)
        {
            return Create(width, height);
        }

        private static BoardLayout Create(int width, int height, params LevelDoorData[] doors)
        {
            return Create(width, height, Array.Empty<Vector2Int>(), doors);
        }

        private static BoardLayout Create(int width, int height, Vector2Int[] voidCells, params LevelDoorData[] doors)
        {
            Assert.That(BoardLayout.TryCreate(width, height, voidCells, doors,
                out BoardLayout layout, out string error), Is.True, error);
            return layout;
        }

        private int CountActivePieces()
        {
            return CountPieces(false);
        }

        private int CountAllPieces()
        {
            return CountPieces(true);
        }

        private int CountPieces(bool includeInactive)
        {
            int count = 0;
            foreach (BoardPieceView piece in m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(includeInactive))
            {
                count++;
            }

            return count;
        }

        private static int CountWallBases(EnvironmentDatabase database, IBoardLayout layout)
        {
            List<EnvironmentPiecePlacement> placements = new List<EnvironmentPiecePlacement>();
            new EnvironmentComposer(database).ComposePlayArea(layout, placements, new List<EnvironmentGateSpan>());
            int count = 0;
            foreach (EnvironmentPiecePlacement placement in placements)
            {
                if (placement.Piece == database.FloorBasePiece && placement.Scale != Vector3.one) count++;
            }

            return count;
        }

        private List<BoardPieceView> FindActiveFloorBases()
        {
            List<BoardPieceView> bases = new List<BoardPieceView>();
            foreach (BoardPieceView piece in m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (piece.name.StartsWith(k_floorBaseName, StringComparison.Ordinal))
                {
                    bases.Add(piece);
                }
            }

            return bases;
        }

        private void AssertPiece(string prefabName, Vector3 localPosition, Vector3 scale)
        {
            BoardPieceView piece = FindActive(prefabName, localPosition);
            Assert.That(piece.transform.localScale, Is.EqualTo(scale), $"{prefabName} scale");
        }

        private BoardPieceView FindActive(string prefabName, Vector3 localPosition)
        {
            foreach (BoardPieceView piece in m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (piece.name.StartsWith(prefabName, StringComparison.Ordinal)
                    && piece.transform.localPosition == localPosition)
                {
                    return piece;
                }
            }

            Assert.Fail($"No active {prefabName} at {localPosition}.");
            return null;
        }
    }
}
#endif
