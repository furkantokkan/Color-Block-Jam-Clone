using System;
using System.Collections.Generic;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Environment
{
    public sealed class EnvironmentSolidTests
    {
        private EnvironmentDatabase m_database;

        private static IEnumerable<TestCaseData> Footprints()
        {
            yield return new TestCaseData(BoardLayoutTests.Cells((2, 2))).SetName("BarrierSingle");
            yield return new TestCaseData(BoardLayoutTests.Cells((2, 2), (3, 2), (4, 2))).SetName("BarrierRun");
            yield return new TestCaseData(BoardLayoutTests.Cells((2, 2), (3, 2), (2, 3), (3, 3))).SetName("BarrierSquare");
            yield return new TestCaseData(BoardLayoutTests.Cells((2, 2), (2, 3), (3, 2))).SetName("BarrierL");
            yield return new TestCaseData(BoardLayoutTests.Cells(
                (2, 2), (3, 2), (4, 2), (2, 3), (3, 3), (4, 3), (2, 4), (3, 4), (4, 4))).SetName("BarrierWide");
            yield return new TestCaseData(BoardLayoutTests.Cells(
                (2, 2), (3, 2), (4, 2), (2, 3), (4, 3), (2, 4), (3, 4), (4, 4))).SetName("BarrierWithFloorHole");
        }

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentComposerTests.DatabasePath);
        }

        [TestCaseSource(nameof(Footprints))]
        public void BarrierCoversExactlyItsOccupiedQuadrants(Vector2Int[] cells)
        {
            var barriers = new List<EnvironmentPiecePlacement>();
            var groups = new List<EnvironmentBarrier>();
            var level = new LevelData("barrier", "Barrier", 7, 7, 60, Array.Empty<Vector2Int>(),
                Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>(),
                new[] { EnvironmentBarrierComposerTests.Barrier("barrier", false, cells) });
            new EnvironmentBarrierComposer(m_database).Compose(BoardLayoutTests.Create(7, 7), level, barriers, groups);

            Assert.That(barriers.Count, Is.EqualTo(4 * cells.Length));
            var occupied = new HashSet<Vector3>();
            foreach (Vector2Int cell in cells)
                for (int quadrant = 0; quadrant < 4; quadrant++) occupied.Add(QuadrantCenter(cell, quadrant));
            foreach (Vector3 position in occupied)
            {
                EnvironmentPiecePlacement barrier = FindOne(barriers, position);
                Assert.That(barrier.Tint, Is.EqualTo(m_database.GetBarrierColor()));
                if (IsInterior(occupied, position))
                    Assert.That(barrier.Piece, Is.SameAs(m_database.BlockFillPiece), "An interior quadrant gets the flat cap.");
            }

            foreach (EnvironmentPiecePlacement placement in barriers)
            {
                if (placement.Piece != m_database.BlockFillPiece) continue;
                var cell = new Vector2Int(Mathf.RoundToInt(placement.GridPosition.x * 0.5f),
                    Mathf.RoundToInt(placement.GridPosition.y * 0.5f));
                Assert.That(cells, Does.Contain(cell), "Fill must not cover floor or extend beyond the board.");
            }
        }

        [Test]
        public void BoundaryAndGatesKeepTheSuppliedWallAndDoorMeshes()
        {
            const string source = "Assets/ColorBlockJam/Art/Models/Supplied/WallAndDoor.fbx";
            string[] names = { "Wall", "Corner_1", "Door_1x1", "Door_Arrow" };
            foreach (string name in names)
            {
                BoardPieceView piece = AssetDatabase.LoadAssetAtPath<BoardPieceView>(
                    "Assets/ColorBlockJam/Prefabs/Board/Pieces/" + name + ".prefab");
                Assert.That(piece, Is.Not.Null, name);
                Assert.That(AssetDatabase.GetAssetPath(piece.Filter.sharedMesh), Is.EqualTo(source), name);
                Assert.That(piece.Renderer.sharedMaterial, Is.SameAs(m_database.BlockFillPiece.Renderer.sharedMaterial));
            }
        }

        [Test]
        public void FillReusesTheExistingQuadMaterialAndPoolRegistration()
        {
            BoardPieceView fill = m_database.BlockFillPiece;
            Assert.That(fill, Is.Not.Null);
            Assert.That(fill.Filter.sharedMesh, Is.SameAs(m_database.FloorBasePiece.Filter.sharedMesh));
            Assert.That(fill.Renderer.sharedMaterial, Is.SameAs(m_database.FloorBasePiece.Renderer.sharedMaterial));
            Assert.That(fill.Filter.sharedMesh.triangles.Length, Is.EqualTo(6));
            Assert.That(fill.Renderer.bounds.max.y, Is.EqualTo(0.66764f).Within(0.0001f),
                "The cap meets the Wall's bevelled joining edge, below its 0.72 crest.");
            Vector3 normal = fill.Filter.transform.TransformDirection(fill.Filter.sharedMesh.normals[0]);
            Assert.That(Vector3.Dot(normal, Vector3.up), Is.GreaterThan(0.999f));
            var pieces = new List<BoardPieceView>();
            m_database.GetPieces(pieces);
            Assert.That(pieces.FindAll(piece => piece == fill).Count, Is.EqualTo(1));
        }

        [Test]
        public void RebuildingTheBoardAndBarriersAllocatesNothing()
        {
            Vector2Int[] cells = BoardLayoutTests.Cells((2, 2), (3, 2), (2, 3), (3, 3));
            BoardLayout floor = BoardLayoutTests.Create(7, 7);
            var level = new LevelData("barrier", "Barrier", 7, 7, 60, Array.Empty<Vector2Int>(),
                Array.Empty<LevelDoorData>(), Array.Empty<LevelBlockData>(),
                new[] { EnvironmentBarrierComposerTests.Barrier("barrier", false, cells) });
            var composer = new EnvironmentComposer(m_database);
            var barrierComposer = new EnvironmentBarrierComposer(m_database);
            var pieces = new List<EnvironmentPiecePlacement>();
            var barrierPieces = new List<EnvironmentPiecePlacement>();
            var groups = new List<EnvironmentBarrier>();
            composer.Compose(floor, pieces);
            barrierComposer.Compose(floor, level, barrierPieces, groups);
            long before = GC.GetAllocatedBytesForCurrentThread();
            composer.Compose(floor, pieces);
            barrierComposer.Compose(floor, level, barrierPieces, groups);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private static Vector3 QuadrantCenter(Vector2Int cell, int quadrant)
        {
            return new Vector3(2 * cell.x - 0.5f + (quadrant & 1), 2 * cell.y - 0.5f + (quadrant >> 1), 0f);
        }

        // A quadrant whose eight neighbouring quadrants all belong to the barrier has no outline.
        private static bool IsInterior(HashSet<Vector3> occupied, Vector3 position)
        {
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if (!occupied.Contains(position + new Vector3(dx, dy, 0f))) return false;
            return true;
        }

        private static EnvironmentPiecePlacement FindOne(List<EnvironmentPiecePlacement> pieces, Vector3 position)
        {
            int count = 0;
            EnvironmentPiecePlacement found = default;
            foreach (EnvironmentPiecePlacement piece in pieces)
            {
                if (piece.GridPosition != position) continue;
                count++;
                found = piece;
            }
            Assert.That(count, Is.EqualTo(1), position.ToString());
            return found;
        }
    }
}
