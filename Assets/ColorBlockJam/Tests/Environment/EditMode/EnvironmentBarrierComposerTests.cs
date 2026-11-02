using System;
using System.Collections.Generic;
using System.Globalization;
using ColorBlockJam.Environment;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Tests.Environment
{
    /// <summary>
    /// Verifies the Barrier composition (catalog OBJ-05, contract AC-1): each barrier's cells get the
    /// wall pieces of an interior wall block, at the closed height, tinted and ranged by barrier.
    /// </summary>
    public sealed class EnvironmentBarrierComposerTests
    {
        private EnvironmentDatabase m_database;
        private EnvironmentBarrierComposer m_composer;
        private List<EnvironmentPiecePlacement> m_placements;
        private List<EnvironmentBarrier> m_barriers;

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentComposerTests.DatabasePath);
            Assert.That(m_database, Is.Not.Null);
            m_composer = new EnvironmentBarrierComposer(m_database);
            m_placements = new List<EnvironmentPiecePlacement>();
            m_barriers = new List<EnvironmentBarrier>();
        }

        [Test]
        public void SingleStraightAndLShapedBarriersGetTheirPieceKindsPositionsAndYaws()
        {
            LevelData level = CreateThreeBarrierLevel();
            Assert.That(level.Validate(out string error), Is.True, error);

            m_composer.Compose(CreateLayout(level), level, m_placements, m_barriers);

            Assert.That(m_barriers.Count, Is.EqualTo(3));
            AssertBarrier(0, "single", false,
                "Corner_1@0.75,0.75/180", "Corner_1@1.25,0.75/90", "Corner_1@0.75,1.25/270", "Corner_1@1.25,1.25/0");
            AssertBarrier(1, "straight", false,
                "Corner_1@2.75,0.75/180", "Corner_1@2.75,1.25/270",
                "Wall@3.25,0.75/90", "Wall@3.75,0.75/90", "Wall@4.25,0.75/90", "Wall@4.75,0.75/90",
                "Wall@3.25,1.25/270", "Wall@3.75,1.25/270", "Wall@4.25,1.25/270", "Wall@4.75,1.25/270",
                "Corner_1@5.25,0.75/90", "Corner_1@5.25,1.25/0");
            AssertBarrier(2, "ell", true,
                "Corner_1@0.75,3.75/180", "Corner_1@1.25,3.75/90",
                "Wall@0.75,4.25/180", "Wall@1.25,4.25/0", "Wall@0.75,4.75/180",
                // The concave corner inside the L.
                "Corner_1@1.25,4.75/270",
                "Corner_1@0.75,5.25/270", "Wall@1.25,5.25/270", "Wall@1.75,4.75/90", "Wall@1.75,5.25/270",
                "Corner_1@2.25,4.75/90", "Corner_1@2.25,5.25/0");
            Assert.That(m_placements.Count, Is.EqualTo(4 + 12 + 12), "Ranges cover every placement once.");

            for (int index = 0; index < m_placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = m_placements[index];
                Assert.That(placement.GridPosition.z, Is.Zero, "Composed at the closed (raised) height.");
                Assert.That(placement.Scale, Is.EqualTo(m_database.WallPieceScale));
                Assert.That(placement.Tint, Is.EqualTo(m_database.GetBarrierColor()));
            }
        }

        [Test]
        public void BarrierPiecesAreThePiecesOfAnInteriorWallBlockOnTheSameCells()
        {
            LevelData level = CreateThreeBarrierLevel();
            m_composer.Compose(CreateLayout(level), level, m_placements, m_barriers);
            EnvironmentComposer environment = new EnvironmentComposer(m_database);
            List<EnvironmentPiecePlacement> walls = new List<EnvironmentPiecePlacement>();

            for (int barrierIndex = 0; barrierIndex < m_barriers.Count; barrierIndex++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(barrierIndex);
                Vector2Int[] cells = new Vector2Int[cellObject.CellCount];
                for (int cell = 0; cell < cells.Length; cell++)
                {
                    cells[cell] = cellObject.GetCellAt(cell);
                }

                // The same cells authored as void: an interior wall block.
                environment.Compose(BoardLayoutTests.Create(level.Width, level.Height, cells), walls);
                HashSet<string> expected = new HashSet<string>();
                foreach (EnvironmentPiecePlacement wall in walls)
                {
                    Vector2Int cell = new Vector2Int(
                        Mathf.RoundToInt(wall.GridPosition.x * 0.5f), Mathf.RoundToInt(wall.GridPosition.y * 0.5f));
                    if (cellObject.ContainsCell(cell) && wall.Piece != m_database.FloorPiece
                        && wall.Piece != m_database.FloorBasePiece)
                    {
                        expected.Add(Describe(wall));
                    }
                }

                Assert.That(Describe(m_barriers[barrierIndex]), Is.EquivalentTo(expected), cellObject.Id);
            }
        }

        [Test]
        public void LevelsWithoutBarriersComposeNothing()
        {
            LevelData plain = CreateLevel(5, 5);
            LevelData crate = CreateLevel(5, 5, new LevelCellObjectData("crate", new[] { new Vector2Int(2, 2) },
                new LevelEffectData("crate", new[] { new LevelEffectParamData("clearsToOpen", "2") })));
            m_placements.Add(default);
            m_barriers.Add(default);

            m_composer.Compose(CreateLayout(plain), null, m_placements, m_barriers);
            Assert.That(m_placements, Is.Empty, "The previous output is replaced.");
            Assert.That(m_barriers, Is.Empty);

            m_composer.Compose(CreateLayout(plain), plain, m_placements, m_barriers);
            Assert.That(m_placements, Is.Empty);
            Assert.That(m_barriers, Is.Empty);

            m_composer.Compose(CreateLayout(crate), crate, m_placements, m_barriers);
            Assert.That(m_placements, Is.Empty, "Other board objects are not barriers.");
            Assert.That(m_barriers, Is.Empty);
        }

        [Test]
        public void BarriersFlushWithTheBoardEdgeOrAVoidCellWallEveryQuadrantOfTheirCells()
        {
            LevelData level = CreateLevel(6, 6, new[] { new Vector2Int(0, 3) },
                Barrier("corner", false, new Vector2Int(0, 0)),
                Barrier("edge", false, new Vector2Int(2, 0), new Vector2Int(3, 0), new Vector2Int(4, 0)),
                Barrier("hole", false, new Vector2Int(1, 3)));
            Assert.That(level.Validate(out string error), Is.True, error);

            m_composer.Compose(CreateLayout(level), level, m_placements, m_barriers);

            // The same pieces as a barrier in the middle of the board: the ring and the hole count as floor.
            AssertBarrier(0, "corner", false,
                "Corner_1@-0.25,-0.25/180", "Corner_1@0.25,-0.25/90", "Corner_1@-0.25,0.25/270", "Corner_1@0.25,0.25/0");
            AssertBarrier(1, "edge", false,
                "Corner_1@1.75,-0.25/180", "Corner_1@1.75,0.25/270",
                "Wall@2.25,-0.25/90", "Wall@2.75,-0.25/90", "Wall@3.25,-0.25/90", "Wall@3.75,-0.25/90",
                "Wall@2.25,0.25/270", "Wall@2.75,0.25/270", "Wall@3.25,0.25/270", "Wall@3.75,0.25/270",
                "Corner_1@4.25,-0.25/90", "Corner_1@4.25,0.25/0");
            AssertBarrier(2, "hole", false,
                "Corner_1@0.75,2.75/180", "Corner_1@1.25,2.75/90", "Corner_1@0.75,3.25/270", "Corner_1@1.25,3.25/0");
            for (int barrierIndex = 0; barrierIndex < m_barriers.Count; barrierIndex++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(barrierIndex);
                for (int cell = 0; cell < cellObject.CellCount; cell++)
                {
                    AssertOnePiecePerQuadrant(m_barriers[barrierIndex], cellObject.GetCellAt(cell));
                }
            }
        }

        [Test]
        public void BarrierOffTheFloorOrWithARepeatedIdIsRefused()
        {
            LevelData offFloor = CreateLevel(4, 4, Barrier("outside", false, new Vector2Int(4, 1)));
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => m_composer.Compose(CreateLayout(offFloor), offFloor, m_placements, m_barriers));
            StringAssert.Contains("Barrier outside has cell (4, 1) off the board floor.", exception.Message);

            LevelData repeated = CreateLevel(4, 4,
                Barrier("twin", false, new Vector2Int(0, 0)), Barrier("twin", true, new Vector2Int(3, 3)));
            exception = Assert.Throws<ArgumentException>(
                () => m_composer.Compose(CreateLayout(repeated), repeated, m_placements, m_barriers));
            StringAssert.Contains("Barrier ID twin is used twice.", exception.Message);
        }

        // A 7 x 8 board with a closed single cell, a closed 3 x 1 run and an open L; the block
        // stays off the closed barriers so the level passes authoring validation.
        internal static LevelData CreateThreeBarrierLevel()
        {
            return CreateLevel(7, 8,
                Barrier("single", false, new Vector2Int(1, 1)),
                Barrier("straight", false, new Vector2Int(3, 1), new Vector2Int(4, 1), new Vector2Int(5, 1)),
                Barrier("ell", true, new Vector2Int(1, 4), new Vector2Int(1, 5), new Vector2Int(2, 5)));
        }

        internal static LevelCellObjectData Barrier(string id, bool startsOpen, params Vector2Int[] cells)
        {
            return new LevelCellObjectData(id, cells, new LevelEffectData("barrier",
                new[] { new LevelEffectParamData("startsOpen", startsOpen ? "true" : "false") }));
        }

        private static LevelData CreateLevel(int width, int height, params LevelCellObjectData[] cellObjects)
        {
            return CreateLevel(width, height, Array.Empty<Vector2Int>(), cellObjects);
        }

        private static LevelData CreateLevel(
            int width, int height, Vector2Int[] voidCells, params LevelCellObjectData[] cellObjects)
        {
            LevelBlockData block = new LevelBlockData("b1", "Pink", new Vector2Int(width - 2, height - 2),
                new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(1, 1) });
            return new LevelData("barriers", "Barriers", width, height, 60, voidCells,
                Array.Empty<LevelDoorData>(), new[] { block }, cellObjects);
        }

        // Each quadrant centre of the cell holds exactly one of the barrier's pieces.
        private void AssertOnePiecePerQuadrant(EnvironmentBarrier barrier, Vector2Int cell)
        {
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                Vector2 center = new Vector2(2 * cell.x - 0.5f + (quadrant & 1), 2 * cell.y - 0.5f + (quadrant >> 1));
                int count = 0;
                for (int index = barrier.FirstPiece; index < barrier.FirstPiece + barrier.PieceCount; index++)
                {
                    Vector3 position = m_placements[index].GridPosition;
                    if (Mathf.Approximately(position.x, center.x) && Mathf.Approximately(position.y, center.y))
                    {
                        count++;
                    }
                }

                Assert.That(count, Is.EqualTo(1), $"{barrier.Id} cell {cell} quadrant {quadrant}");
            }
        }

        private static BoardLayout CreateLayout(LevelData level)
        {
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            return layout;
        }

        private void AssertBarrier(int index, string id, bool startsOpen, params string[] pieces)
        {
            EnvironmentBarrier barrier = m_barriers[index];
            Assert.That(barrier.Id, Is.EqualTo(id));
            Assert.That(barrier.StartsOpen, Is.EqualTo(startsOpen), id);
            Assert.That(barrier.PieceCount, Is.EqualTo(pieces.Length), id);
            Assert.That(Describe(barrier), Is.EquivalentTo(pieces), id);
        }

        private List<string> Describe(EnvironmentBarrier barrier)
        {
            List<string> descriptions = new List<string>();
            for (int index = barrier.FirstPiece; index < barrier.FirstPiece + barrier.PieceCount; index++)
            {
                descriptions.Add(Describe(m_placements[index]));
            }

            return descriptions;
        }

        // Quadrant grid units are half cells, so the board-local position is half the grid position.
        private static string Describe(EnvironmentPiecePlacement placement)
        {
            return placement.Piece.name + "@"
                + (placement.GridPosition.x * 0.5f).ToString("0.##", CultureInfo.InvariantCulture) + ","
                + (placement.GridPosition.y * 0.5f).ToString("0.##", CultureInfo.InvariantCulture) + "/" + placement.Yaw;
        }
    }
}
