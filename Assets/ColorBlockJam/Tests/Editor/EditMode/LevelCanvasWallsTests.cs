using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Environment;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Level Editor canvas walls: the glyphs are exactly EnvironmentComposer's rule-set wall and corner
    /// placements (count, quadrant position, yaw), doors open the ring while invalid doors do not, and a missing or
    /// invalid environment database gives one message and no walls.
    /// </summary>
    public sealed class LevelCanvasWallsTests
    {
        private const float k_tolerance = 0.0001f;

        private List<LevelWallGlyph> m_glyphs;

        [SetUp]
        public void SetUp()
        {
            m_glyphs = new List<LevelWallGlyph>();
        }

        [Test]
        public void PlainBoardGetsAFullStraightRingAndFourOutwardCorners()
        {
            EnvironmentDatabase database = RequireDatabase();
            LevelData level = CreateLevel(4, 5);

            bool composed = new LevelCanvasWalls().TryCompose(level, database, m_glyphs, out string issue);

            Assert.That(composed, Is.True, issue);
            Assert.That(issue, Is.Null);
            // Each side of n cells has 2n half-cell ring quadrants next to the floor, so the ring holds 4 (W + H) = 36
            // straight walls. The four diagonal ring quadrants at the board corners hold the outer corners instead
            // (EnvironmentComposerTests: a 4 x 3 rectangle gives 28 walls and 4 outer corners).
            Assert.That(CountWalls(), Is.EqualTo(4 * (4 + 5)));
            Assert.That(CountCorners(), Is.EqualTo(4));
            Assert.That(CountAt(new Vector2(-0.75f, -0.75f), true), Is.EqualTo(1));
            Assert.That(CountAt(new Vector2(3.75f, -0.75f), true), Is.EqualTo(1));
            Assert.That(CountAt(new Vector2(-0.75f, 4.75f), true), Is.EqualTo(1));
            Assert.That(CountAt(new Vector2(3.75f, 4.75f), true), Is.EqualTo(1));

            Vector2 boardCentre = GetBoardCentre(level);
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                Assert.That(glyph.Size, Is.EqualTo(0.5f));
                AssertInRingOutsideBoard(glyph, level);
                if (!glyph.IsCorner)
                {
                    Assert.That(glyph.RoundedCorner, Is.EqualTo(Vector2Int.zero), $"wall at {glyph.Center}");
                    continue;
                }

                Vector2 offset = glyph.Center - boardCentre;
                Assert.That(glyph.RoundedCorner.x, Is.EqualTo((int)Mathf.Sign(offset.x)), $"corner at {glyph.Center}");
                Assert.That(glyph.RoundedCorner.y, Is.EqualTo((int)Mathf.Sign(offset.y)), $"corner at {glyph.Center}");
            }
        }

        [Test]
        public void ACutCornerWrapsTheWallsExactlyAsTheComposerDoes()
        {
            EnvironmentDatabase database = RequireDatabase();
            LevelData plain = CreateLevel(4, 5);
            Assert.That(LevelEditCommands.TryCutCorner(plain, new Vector2Int(3, 4), out LevelData cut, out string issue),
                Is.True, issue);
            LevelCanvasWalls walls = new LevelCanvasWalls();
            Assert.That(walls.TryCompose(plain, database, m_glyphs, out issue), Is.True, issue);
            Assert.That(CountInsideCell(new Vector2Int(3, 4)), Is.Zero, "A floor corner holds no wall.");

            AssertMatchesComposer(walls, database, cut);
            Assert.That(CountInsideCell(new Vector2Int(3, 4)), Is.GreaterThan(0), "The walls wrap into the void corner.");
        }

        private int CountInsideCell(Vector2Int cell)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                if (Mathf.Abs(glyph.Center.x - cell.x) < 0.5f && Mathf.Abs(glyph.Center.y - cell.y) < 0.5f)
                {
                    count++;
                }
            }

            return count;
        }

        [Test]
        public void TwoSegmentTopDoorHoldsFourTopWallQuadrants()
        {
            EnvironmentDatabase database = RequireDatabase();
            LevelCanvasWalls walls = new LevelCanvasWalls();
            LevelData plain = CreateLevel(4, 5);
            LevelData withDoor = CreateLevel(4, 5, new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red"));

            Assert.That(walls.TryCompose(plain, database, m_glyphs, out string issue), Is.True, issue);
            int plainTopWalls = CountTopWalls(plain);
            Assert.That(walls.TryCompose(withDoor, database, m_glyphs, out issue), Is.True, issue);

            Assert.That(plainTopWalls, Is.EqualTo(2 * 4), "Two quadrants per top cell.");
            Assert.That(CountTopWalls(withDoor), Is.EqualTo(plainTopWalls - 4));
            Assert.That(CountCorners(), Is.EqualTo(4), "The door keeps its neighbour corners.");
            // The door covers cells x = 1 and 2, so the top ring quadrants centred at x = 0.75 to 2.25 hold it.
            for (float x = 0.75f; x < 2.5f; x += 0.5f)
            {
                Assert.That(CountAt(new Vector2(x, 4.75f)), Is.Zero, $"door quadrant at x = {x}");
            }
        }

        [Test]
        public void InvalidDoorsKeepTheirWallsAndValidDoorsStillOpenTheRing()
        {
            EnvironmentDatabase database = RequireDatabase();
            LevelCanvasWalls walls = new LevelCanvasWalls();
            LevelData validOnly = CreateLevel(5, 4, new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red"));
            // d2 overlaps d1, d3 names no gate colour (only BoardLayout rejects it) and d4 runs off its 4-cell edge.
            LevelData withInvalid = CreateLevel(5, 4,
                new LevelDoorData("d1", BoardSide.Top, 1, 2, "Red"),
                new LevelDoorData("d2", BoardSide.Top, 2, 2, "Blue"),
                new LevelDoorData("d3", BoardSide.Left, 0, 1, "NotAColour"),
                new LevelDoorData("d4", BoardSide.Right, 3, 2, "Pink"));
            Assume.That(withInvalid.Validate(out _), Is.False, "The level has invalid doors.");

            Assert.That(walls.TryCompose(validOnly, database, m_glyphs, out string issue), Is.True, issue);
            List<LevelWallGlyph> expected = new List<LevelWallGlyph>(m_glyphs);
            Assert.That(walls.TryCompose(withInvalid, database, m_glyphs, out issue), Is.True, issue);

            AssertSameGlyphs(expected, "invalid doors");
        }

        [Test]
        public void GlyphsMatchTheComposerOnTheFirstFiveActiveLevels()
        {
            EnvironmentDatabase database = RequireDatabase();
            ProjectScopeInstaller projectScope = LevelDatabaseAddressables.LoadProjectScope();
            Assert.That(projectScope, Is.Not.Null, LevelDatabaseAddressables.ProjectContextPath);
            Assert.That(ShippedLevels.TryReadActive(out LevelData[] levels, out string error), Is.True, error);
            Assert.That(levels.Length, Is.GreaterThan(0));

            LevelCanvasWalls walls = new LevelCanvasWalls();
            for (int index = 0; index < Mathf.Min(5, levels.Length); index++)
            {
                AssertMatchesComposer(walls, database, levels[index]);
            }
        }

        [Test]
        public void GlyphsMatchTheComposerWithDoorsAtTheBoardCorners()
        {
            EnvironmentDatabase database = RequireDatabase();
            LevelCanvasWalls walls = new LevelCanvasWalls();
            LevelData[] levels =
            {
                CreateLevel(3, 3,
                    new LevelDoorData("d1", BoardSide.Top, 0, 3, "Red"),
                    new LevelDoorData("d2", BoardSide.Left, 0, 3, "Blue"),
                    new LevelDoorData("d3", BoardSide.Bottom, 1, 1, "Yellow")),
                CreateLevel(8, 10,
                    new LevelDoorData("d1", BoardSide.Right, 7, 3, "Pink"),
                    new LevelDoorData("d2", BoardSide.Bottom, 0, 2, "Purple"),
                    new LevelDoorData("d3", BoardSide.Top, 7, 1, "LightGreen")),
                CreateLevel(6, 4, new LevelDoorData("d1", BoardSide.Left, 1, 2, "Orange"))
            };

            foreach (LevelData level in levels)
            {
                Assume.That(level.Validate(out string error), Is.True, error);
                AssertMatchesComposer(walls, database, level);
            }
        }

        [Test]
        public void SwitchingDatabasesComposesWithTheNewOne()
        {
            EnvironmentDatabase database = RequireDatabase();
            EnvironmentDatabase copy = Object.Instantiate(database);
            EnvironmentDatabase empty = ScriptableObject.CreateInstance<EnvironmentDatabase>();
            try
            {
                LevelCanvasWalls walls = new LevelCanvasWalls();
                LevelData level = CreateLevel(5, 6, new LevelDoorData("d1", BoardSide.Bottom, 2, 2, "Red"));
                Assert.That(walls.TryCompose(level, database, m_glyphs, out string issue), Is.True, issue);
                List<LevelWallGlyph> expected = new List<LevelWallGlyph>(m_glyphs);

                Assert.That(walls.TryCompose(level, empty, m_glyphs, out issue), Is.False);
                Assert.That(m_glyphs, Is.Empty);
                Assert.That(walls.TryCompose(level, copy, m_glyphs, out issue), Is.True, issue);
                AssertSameGlyphs(expected, "copied database");
                Assert.That(walls.TryCompose(level, database, m_glyphs, out issue), Is.True, issue);
                AssertSameGlyphs(expected, "original database again");
            }
            finally
            {
                Object.DestroyImmediate(copy);
                Object.DestroyImmediate(empty);
            }
        }

        [Test]
        public void MissingDatabaseGivesOneMessageAndNoWalls()
        {
            m_glyphs.Add(default);

            bool composed = new LevelCanvasWalls().TryCompose(CreateLevel(4, 5), null, m_glyphs, out string issue);

            Assert.That(composed, Is.False);
            Assert.That(issue, Is.EqualTo("The environment database asset is missing."));
            Assert.That(m_glyphs, Is.Empty);
        }

        [Test]
        public void InvalidDatabaseGivesItsValidationMessageAndNoWalls()
        {
            EnvironmentDatabase database = ScriptableObject.CreateInstance<EnvironmentDatabase>();
            try
            {
                Assume.That(database.Validate(out string expected), Is.False, "An empty database has no rule set.");
                m_glyphs.Add(default);

                bool composed = new LevelCanvasWalls().TryCompose(CreateLevel(4, 5), database, m_glyphs,
                    out string issue);

                Assert.That(composed, Is.False);
                Assert.That(issue, Is.EqualTo(expected));
                Assert.That(m_glyphs, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(database);
            }
        }

        [Test]
        public void NoLevelGivesNoWalls()
        {
            m_glyphs.Add(default);

            bool composed = new LevelCanvasWalls().TryCompose(null, null, m_glyphs, out string issue);

            Assert.That(composed, Is.False);
            Assert.That(issue, Is.EqualTo("Select a level first."));
            Assert.That(m_glyphs, Is.Empty);
        }

        [TestCase(0, 1, 1)]
        [TestCase(90, 1, -1)]
        [TestCase(180, -1, -1)]
        [TestCase(270, -1, 1)]
        public void RoundedCornerTurnsClockwiseWithTheYaw(int yaw, int x, int y)
        {
            Assert.That(LevelCanvasWalls.GetRoundedCorner(yaw), Is.EqualTo(new Vector2Int(x, y)));
        }

        private static EnvironmentDatabase RequireDatabase()
        {
            EnvironmentDatabase database = LevelCanvasWalls.LoadDatabase();
            Assume.That(database, Is.Not.Null, LevelCanvasWalls.k_databasePath);
            return database;
        }

        // Composes the level's layout directly and checks that the glyphs are its rule-output placements, in order.
        private void AssertMatchesComposer(LevelCanvasWalls walls, EnvironmentDatabase database, LevelData level)
        {
            Assert.That(BoardLayout.TryCreate(level, out BoardLayout layout, out string error), Is.True, error);
            List<EnvironmentPiecePlacement> placements = new List<EnvironmentPiecePlacement>();
            new EnvironmentComposer(database).Compose(layout, placements);
            Assert.That(walls.TryCompose(level, database, m_glyphs, out string issue), Is.True, issue);

            EnvironmentRuleSet rules = database.RuleSet;
            // Rule 1 is the straight Wall rule; rules 0 and 2 place Corner_1 (docs/Environment-Layout.md).
            BoardPieceView wallPiece = rules.GetRule(1).Output;
            int next = 0;
            for (int index = 0; index < placements.Count; index++)
            {
                EnvironmentPiecePlacement placement = placements[index];
                if (!IsRuleOutput(rules, placement.Piece))
                {
                    continue;
                }

                string label = $"{level.Id} glyph {next} ({placement.Piece.name} at {placement.GridPosition})";
                Assert.That(next, Is.LessThan(m_glyphs.Count), label);
                LevelWallGlyph glyph = m_glyphs[next++];
                Assert.That(glyph.Center.x * 2f, Is.EqualTo(placement.GridPosition.x).Within(k_tolerance), label);
                Assert.That(glyph.Center.y * 2f, Is.EqualTo(placement.GridPosition.y).Within(k_tolerance), label);
                Assert.That(glyph.Yaw, Is.EqualTo(placement.Yaw), label);
                Assert.That(glyph.IsCorner,
                    Is.EqualTo(placement.Piece != wallPiece), label);
                Assert.That(glyph.RoundedCorner,
                    Is.EqualTo(glyph.IsCorner ? LevelCanvasWalls.GetRoundedCorner(placement.Yaw) : Vector2Int.zero),
                    label);
            }

            Assert.That(m_glyphs.Count, Is.EqualTo(next), level.Id);
        }

        private void AssertSameGlyphs(List<LevelWallGlyph> expected, string label)
        {
            Assert.That(m_glyphs.Count, Is.EqualTo(expected.Count), label);
            for (int index = 0; index < expected.Count; index++)
            {
                Assert.That(m_glyphs[index].Center, Is.EqualTo(expected[index].Center), label);
                Assert.That(m_glyphs[index].Yaw, Is.EqualTo(expected[index].Yaw), label);
                Assert.That(m_glyphs[index].IsCorner, Is.EqualTo(expected[index].IsCorner), label);
                Assert.That(m_glyphs[index].RoundedCorner, Is.EqualTo(expected[index].RoundedCorner), label);
            }
        }

        // The board spans half a cell around the cell centres 0..W-1 and 0..H-1. A glyph's centre lies at least
        // half a quadrant outside it (never on a board cell) and its far edge at most one cell outside it.
        private static void AssertInRingOutsideBoard(LevelWallGlyph glyph, LevelData level)
        {
            Vector2 offset = glyph.Center - GetBoardCentre(level);
            float outside = Mathf.Max(Mathf.Abs(offset.x) - level.Width * 0.5f,
                Mathf.Abs(offset.y) - level.Height * 0.5f);
            Assert.That(outside, Is.GreaterThanOrEqualTo(glyph.Size * 0.5f - k_tolerance),
                $"glyph at {glyph.Center} is on the board");
            Assert.That(outside, Is.LessThanOrEqualTo(1f - glyph.Size * 0.5f + k_tolerance),
                $"glyph at {glyph.Center} is past the one-cell ring");
        }

        private static bool IsRuleOutput(EnvironmentRuleSet rules, BoardPieceView piece)
        {
            for (int index = 0; index < rules.RuleCount; index++)
            {
                if (rules.GetRule(index).Output == piece)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 GetBoardCentre(LevelData level)
        {
            return new Vector2((level.Width - 1) * 0.5f, (level.Height - 1) * 0.5f);
        }

        private int CountWalls()
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                if (!glyph.IsCorner)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountCorners()
        {
            return m_glyphs.Count - CountWalls();
        }

        private int CountTopWalls(LevelData level)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                if (!glyph.IsCorner && glyph.Center.y > level.Height - 0.5f)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountAt(Vector2 center)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                if (Vector2.Distance(glyph.Center, center) < k_tolerance)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountAt(Vector2 center, bool isCorner)
        {
            int count = 0;
            foreach (LevelWallGlyph glyph in m_glyphs)
            {
                if (glyph.IsCorner == isCorner && Vector2.Distance(glyph.Center, center) < k_tolerance)
                {
                    count++;
                }
            }

            return count;
        }

        private static LevelData CreateLevel(int width, int height, params LevelDoorData[] doors)
        {
            return new LevelData("level-walls", "Walls", width, height, 60, doors,
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }
    }
}
