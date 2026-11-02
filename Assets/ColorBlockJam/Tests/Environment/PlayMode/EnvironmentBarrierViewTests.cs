#if UNITY_EDITOR
using ColorBlockJam.Runtime.Art;
using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using PrimeTween;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Verifies the Barrier view (catalog OBJ-05): barriers build at their start height, and
    /// SetBarrierOpen snaps or runs one owned tween per piece that Suspend pauses, Teardown stops
    /// and Clear returns, allocating nothing, and pieces at rest under the floor draw nothing
    /// (contract AC-1 and AC-2).
    /// </summary>
    public sealed class EnvironmentBarrierViewTests
    {
        private const float k_closedHeight = 0f;
        private const float k_openHeight = -0.8f;
        // AC-2: an animated flip reaches its height within 0.3 seconds.
        private const float k_flipSeconds = 0.3f;

        private static readonly Vector2Int[] s_single = { new Vector2Int(1, 1) };
        private static readonly Vector2Int[] s_straight = { new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1) };
        private static readonly Vector2Int[] s_ell = { new Vector2Int(1, 4), new Vector2Int(1, 5), new Vector2Int(2, 5) };

        private GameObject m_root;
        private EnvironmentView m_view;
        private EnvironmentDatabase m_database;
        private LevelData m_level;
        private BoardLayout m_layout;

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentViewTestSupport.DatabasePath);
            m_root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentViewTestSupport.PrefabPath));
            m_view = m_root.GetComponent<EnvironmentView>();
            m_view.Construct(m_database);
            m_level = CreateLevel();
            Assert.That(m_level.Validate(out string error), Is.True, error);
            Assert.That(BoardLayout.TryCreate(m_level, out m_layout, out error), Is.True, error);
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
        public IEnumerator ClosedBarriersStandOnTheirCellsAndOpenOnesSinkUnderTheFloor()
        {
            m_view.Build(m_layout);
            int environmentPieces = m_view.ActivePieceCount;
            m_view.Build(m_layout, m_level);
            yield return null;

            Assert.That(m_view.BarrierCount, Is.EqualTo(3));
            Assert.That(m_view.ActivePieceCount, Is.EqualTo(environmentPieces + 4 + 12 + 12),
                "Barriers add their pieces to an unchanged play area.");
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.EqualTo(m_view.ActivePieceCount));
            AssertHeights(FindBarrierPieces(s_single), 4, k_closedHeight);
            AssertHeights(FindBarrierPieces(s_straight), 12, k_closedHeight);
            AssertHeights(FindBarrierPieces(s_ell), 12, k_openHeight);
            AssertRenderersEnabled(FindBarrierPieces(s_straight), true);
            AssertRenderersEnabled(FindBarrierPieces(s_ell), false, "An open barrier at rest draws nothing.");
            List<Transform> ell = FindBarrierPieces(s_ell);
            AtlasColor barrierColor = m_database.GetBarrierColor();
            foreach (Transform piece in FindBarrierPieces(s_straight))
            {
                Assert.That(EnvironmentViewTestSupport.ColorDistance(EnvironmentViewTestSupport.ReadColor(piece.GetComponent<BoardPieceView>()), barrierColor),
                    Is.LessThan(1e-3f), "Tinted with the database barrier colour.");
            }

            foreach (Vector2Int cell in s_straight)
            {
                // Floor tiles and floor-base squares stay under the barrier cells.
                Assert.That(CountActive("Grid", new Vector3(cell.x, 0f, cell.y)), Is.EqualTo(1), $"floor at {cell}");
                Assert.That(CountActive("FloorBase", new Vector3(cell.x, -0.01f, cell.y)), Is.EqualTo(1), $"base at {cell}");
            }

            m_view.Clear();
            Assert.That(m_view.ActivePieceCount, Is.Zero);
            Assert.That(m_view.BarrierCount, Is.Zero);
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.Zero, "Clear returns the barrier pieces too.");
            AssertRenderersEnabled(ell, true, "Pieces return to the shared wall pools visible.");
        }

        [UnityTest]
        public IEnumerator ClosedBarrierFlushWithTheBoardEdgeCoversEveryQuadrantOfItsCells()
        {
            Vector2Int[] edge = { new Vector2Int(0, 0), new Vector2Int(1, 0), new Vector2Int(2, 0) };
            LevelBlockData block = new LevelBlockData("b1", "Pink", new Vector2Int(3, 3),
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) });
            LevelData level = new LevelData("edge", "Edge", 5, 5, 60, Array.Empty<Vector2Int>(),
                Array.Empty<LevelDoorData>(), new[] { block }, new[] { Barrier("edge", false, edge) });
            Assert.That(level.Validate(out string error), Is.True, error);
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out error), Is.True, error);

            m_view.Build(layout, level);
            yield return null;

            List<Transform> pieces = FindBarrierPieces(edge);
            AssertHeights(pieces, 12, k_closedHeight);
            AssertRenderersEnabled(pieces, true);
            foreach (Vector2Int cell in edge)
            {
                for (int quadrant = 0; quadrant < 4; quadrant++)
                {
                    Vector2 center = new Vector2(cell.x - 0.25f + 0.5f * (quadrant & 1), cell.y - 0.25f + 0.5f * (quadrant >> 1));
                    int count = 0;
                    foreach (Transform piece in pieces)
                    {
                        if (Mathf.Approximately(piece.localPosition.x, center.x)
                            && Mathf.Approximately(piece.localPosition.z, center.y))
                        {
                            count++;
                        }
                    }

                    Assert.That(count, Is.EqualTo(1), $"cell {cell} quadrant {quadrant}");
                }
            }
        }

        [UnityTest]
        public IEnumerator AnimatedFlipReachesItsHeightWithinThreeTenthsOfASecond()
        {
            m_view.Build(m_layout, m_level);
            List<Transform> straight = FindBarrierPieces(s_straight);

            m_view.SetBarrierOpen("straight", true, true);
            AssertTweenCount(straight, 1);
            yield return new WaitForSeconds(k_flipSeconds);
            yield return null;
            AssertHeights(straight, 12, k_openHeight);
            AssertTweenCount(straight, 0);
            AssertRenderersEnabled(straight, false, "A sunk piece hides once its tween completes.");

            m_view.SetBarrierOpen("straight", false, true);
            AssertRenderersEnabled(straight, true, "A rising piece shows from its first frame.");
            yield return new WaitForSeconds(k_flipSeconds);
            yield return null;
            AssertHeights(straight, 12, k_closedHeight);
            AssertTweenCount(straight, 0);

            List<Transform> ell = FindBarrierPieces(s_ell);
            m_view.SetBarrierOpen("ell", true, true);
            AssertTweenCount(ell, 0, "A barrier already at its height starts no tween.");
        }

        [UnityTest]
        public IEnumerator SuspendPausesTheTweenAndTeardownStopsItAndReturnsThePieces()
        {
            m_view.Build(m_layout, m_level);
            List<Transform> straight = FindBarrierPieces(s_straight);
            m_view.SetBarrierOpen("straight", true, true);
            // Suspend in the flip's own command phase, so no frame length can finish the tween first.
            m_view.SetBarrierTweensPaused(true);
            float[] paused = ReadHeights(straight);
            yield return new WaitForSeconds(0.4f);
            Assert.That(ReadHeights(straight), Is.EqualTo(paused), "Suspend holds every piece where it is.");
            AssertTweenCount(straight, 1);
            AssertRenderersEnabled(straight, true, "A paused sink is still visible.");

            m_view.SetBarrierTweensPaused(false);
            yield return new WaitForSeconds(k_flipSeconds);
            yield return null;
            AssertHeights(straight, 12, k_openHeight);

            m_view.SetBarrierOpen("straight", false, true);
            yield return new WaitForSeconds(0.1f);
            m_view.StopBarrierTweens();
            AssertTweenCount(straight, 0);
            float[] stopped = ReadHeights(straight);
            yield return null;
            Assert.That(ReadHeights(straight), Is.EqualTo(stopped), "A stopped piece stays where it was.");

            m_view.SetBarrierOpen("straight", true, true);
            AssertTweenCount(straight, 1);
            m_view.Clear();
            AssertTweenCount(straight, 0);
            Assert.That(EnvironmentViewTestSupport.CountActivePoolItems(), Is.Zero);
            foreach (Transform piece in straight)
            {
                Assert.That(piece.gameObject.activeSelf, Is.False, "returned to its pool");
            }
        }

        [UnityTest]
        public IEnumerator SnapAndRestartReturnBarriersToTheirStartStateWithoutRebuilding()
        {
            m_view.Build(m_layout, m_level);
            List<Transform> straight = FindBarrierPieces(s_straight);
            List<Transform> ell = FindBarrierPieces(s_ell);
            int active = m_view.ActivePieceCount;

            m_view.SetBarrierOpen("straight", true, false);
            m_view.SetBarrierOpen("ell", false, false);
            AssertHeights(straight, 12, k_openHeight);
            AssertHeights(ell, 12, k_closedHeight);
            AssertTweenCount(straight, 0, "A snap starts no tween.");

            m_view.Build(m_layout, m_level);
            yield return null;
            Assert.That(m_view.ActivePieceCount, Is.EqualTo(active));
            Assert.That(FindBarrierPieces(s_straight), Is.EqualTo(straight), "Restart keeps the same pieces.");
            AssertHeights(straight, 12, k_closedHeight);
            AssertHeights(ell, 12, k_openHeight);

            m_view.Build(m_layout);
            Assert.That(m_view.BarrierCount, Is.Zero, "A level without board objects shows no barrier.");
            Assert.Throws<ArgumentException>(() => m_view.SetBarrierOpen("straight", true, false));
        }

        [Test]
        public void BarrierFlipsPauseAndStopAllocateNothing()
        {
            // The first measurement in a domain also measures the recorder's own setup.
            Assert.That(() => { }, Is.Not.AllocatingGCMemory());
            m_view.Build(m_layout, m_level);
            Flip();

            Assert.That(() => Flip(), Is.Not.AllocatingGCMemory());
        }

        private void Flip()
        {
            m_view.SetBarrierOpen("straight", true, true);
            m_view.SetBarrierTweensPaused(true);
            m_view.SetBarrierTweensPaused(false);
            m_view.SetBarrierOpen("straight", false, true);
            m_view.SetBarrierOpen("single", true, false);
            m_view.SetBarrierOpen("single", false, false);
            m_view.StopBarrierTweens();
        }

        private static LevelData CreateLevel()
        {
            LevelBlockData block = new LevelBlockData("b1", "Pink", new Vector2Int(4, 4),
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) });
            return new LevelData("barriers", "Barriers", 7, 8, 60, Array.Empty<Vector2Int>(),
                Array.Empty<LevelDoorData>(), new[] { block },
                new[] { Barrier("single", false, s_single), Barrier("straight", false, s_straight), Barrier("ell", true, s_ell) });
        }

        private static LevelCellObjectData Barrier(string id, bool startsOpen, Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, new LevelEffectData("barrier",
                new[] { new LevelEffectParamData("startsOpen", startsOpen ? "true" : "false") }));
        }

        // Barrier pieces are the wall and corner pieces standing on floor cells; fixed walls stand only
        // on void cells and the ring, so the cell under a piece names its barrier.
        private List<Transform> FindBarrierPieces(Vector2Int[] cells)
        {
            List<Transform> pieces = new List<Transform>();
            foreach (BoardPieceView piece in m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (!piece.name.StartsWith("Wall", StringComparison.Ordinal)
                    && !piece.name.StartsWith("Corner_1", StringComparison.Ordinal))
                {
                    continue;
                }

                Vector3 position = piece.transform.localPosition;
                Vector2Int cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
                if (Array.IndexOf(cells, cell) >= 0)
                {
                    pieces.Add(piece.transform);
                }
            }

            return pieces;
        }

        private int CountActive(string prefabName, Vector3 localPosition)
        {
            int count = 0;
            foreach (BoardPieceView piece in m_view.BoardRoot.GetComponentsInChildren<BoardPieceView>(false))
            {
                if (piece.name.StartsWith(prefabName, StringComparison.Ordinal)
                    && piece.transform.localPosition == localPosition)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertHeights(List<Transform> pieces, int count, float height)
        {
            Assert.That(pieces.Count, Is.EqualTo(count));
            foreach (Transform piece in pieces)
            {
                Assert.That(piece.localPosition.y, Is.EqualTo(height).Within(1e-4f), piece.name);
            }
        }

        private static void AssertRenderersEnabled(List<Transform> pieces, bool enabled, string message = null)
        {
            foreach (Transform piece in pieces)
            {
                Assert.That(piece.GetComponent<BoardPieceView>().Renderer.enabled, Is.EqualTo(enabled), message ?? piece.name);
            }
        }

        private static void AssertTweenCount(List<Transform> pieces, int count, string message = null)
        {
            foreach (Transform piece in pieces)
            {
                Assert.That(Tween.GetTweensCount(piece), Is.EqualTo(count), message ?? piece.name);
            }
        }

        private static float[] ReadHeights(List<Transform> pieces)
        {
            float[] heights = new float[pieces.Count];
            for (int index = 0; index < heights.Length; index++)
            {
                heights[index] = pieces[index].localPosition.y;
            }

            return heights;
        }
    }
}
#endif
