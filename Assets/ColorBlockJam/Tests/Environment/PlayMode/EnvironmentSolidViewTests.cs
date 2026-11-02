#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Verifies the Barrier fill (BlockFill, catalog OBJ-05): a wide barrier's interior quadrant caps
    /// move with the barrier's flip and return to the pool like its wall pieces.
    /// </summary>
    public sealed class EnvironmentSolidViewTests
    {
        private const float k_closedHeight = 0f;
        private const float k_openHeight = -0.8f;
        // AC-2: an animated flip reaches its height within 0.3 seconds.
        private const float k_flipSeconds = 0.3f;
        private const float k_waitMargin = 0.05f;
        // A check made well inside the flip, before it can complete on its own.
        private const float k_pausedCheckSeconds = 0.1f;
        // Fill caps sit at wall height (above 0.5); floor-base squares stay under the floor, below it.
        private const float k_fillCapMinHeight = 0.5f;

        [UnityTest]
        public IEnumerator WideBarrierFillMovesWithItsEdgesAndReturnsToThePool()
        {
            EnvironmentDatabase database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(
                EnvironmentViewTestSupport.DatabasePath);
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                EnvironmentViewTestSupport.PrefabPath));
            try
            {
                EnvironmentView view = root.GetComponent<EnvironmentView>();
                view.Construct(database);
                Vector2Int[] cells =
                {
                    new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2), new Vector2Int(2, 2)
                };
                LevelCellObjectData barrier = new LevelCellObjectData("barrier", cells, new LevelEffectData("barrier",
                    new[] { new LevelEffectParamData("startsOpen", "false") }));
                LevelData level = new LevelData("barrier", "Barrier", 4, 4, 60, Array.Empty<Vector2Int>(),
                    Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>(), new[] { barrier });
                Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
                view.Build(layout, level);

                List<BoardPieceView> fills = FindFillCaps(view, database);
                Assert.That(fills.Count, Is.EqualTo(4));

                view.SetBarrierOpen("barrier", true, true);
                view.SetBarrierTweensPaused(true);
                yield return new WaitForSeconds(k_pausedCheckSeconds);
                foreach (BoardPieceView fill in fills)
                {
                    Assert.That(fill.transform.localPosition.y, Is.EqualTo(k_closedHeight).Within(0.0001f));
                }

                view.SetBarrierTweensPaused(false);
                yield return new WaitForSeconds(k_flipSeconds + k_waitMargin);
                foreach (BoardPieceView fill in fills)
                {
                    Assert.That(fill.transform.localPosition.y, Is.EqualTo(k_openHeight).Within(0.0001f));
                    Assert.That(fill.Renderer.enabled, Is.False);
                }

                view.SetBarrierOpen("barrier", false, false);
                foreach (BoardPieceView fill in fills)
                {
                    Assert.That(fill.Renderer.enabled, Is.True);
                }

                view.Clear();
                foreach (BoardPieceView fill in fills)
                {
                    Assert.That(fill.gameObject.activeSelf, Is.False);
                }

                view.Build(layout, level);
                foreach (BoardPieceView fill in fills)
                {
                    Assert.That(fill.gameObject.activeSelf, Is.True, "The prepared fill instances must be reused.");
                }

                view.Release();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        // Fill caps are the pieces sharing the FloorBase mesh but standing at wall height, above the
        // floor-base squares beneath the floor.
        private static List<BoardPieceView> FindFillCaps(EnvironmentView view, EnvironmentDatabase database)
        {
            List<BoardPieceView> fills = new List<BoardPieceView>();
            foreach (BoardPieceView piece in view.BoardRoot.GetComponentsInChildren<BoardPieceView>())
            {
                if (piece.Filter.sharedMesh == database.BlockFillPiece.Filter.sharedMesh
                    && piece.Filter.transform.localPosition.y > k_fillCapMinHeight)
                {
                    fills.Add(piece);
                }
            }

            return fills;
        }
    }
}
#endif
