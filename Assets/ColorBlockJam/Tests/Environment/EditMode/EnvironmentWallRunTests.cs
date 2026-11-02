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
    /// <summary>
    /// Proves the play-area bricks of DEC-139 (environment-runs-gate-motion AC-1): ComposePlayArea keeps every
    /// rule quadrant of Compose, closes each maximal straight run of the outer boundary between corners and
    /// gates with Brick_End caps whose mouths face into the run, closes a one-quadrant run with two half-length
    /// caps, turns every corner of the outer boundary into Corner_End with the corner rule's yaw, keeps interior
    /// holes as one continuous rule frame, keeps floor and doors exactly, puts one dark base under each wall
    /// quadrant and door slot that reaches floor, walls and gates but stops short of open void (D-002), closes
    /// every gate with rounded Door_End pieces facing out of the span (D-003), and is symmetric under transposition. Nominal quadrants prove topology, not the
    /// rendered roof; the portrait captures decide the look.
    /// </summary>
    public sealed class EnvironmentWallRunTests
    {
        private EnvironmentDatabase m_database;
        private EnvironmentComposer m_composer;
        private BoardPieceView m_wall;
        private BoardPieceView m_corner;
        private readonly List<EnvironmentPiecePlacement> m_rules = new List<EnvironmentPiecePlacement>();
        private readonly List<EnvironmentPiecePlacement> m_playArea = new List<EnvironmentPiecePlacement>();
        private readonly List<EnvironmentGateSpan> m_gates = new List<EnvironmentGateSpan>();

        [SetUp]
        public void SetUp()
        {
            m_database = AssetDatabase.LoadAssetAtPath<EnvironmentDatabase>(EnvironmentComposerTests.DatabasePath);
            m_composer = new EnvironmentComposer(m_database);
            m_wall = m_database.RuleSet.GetRule(1).Output;
            m_corner = m_database.RuleSet.GetRule(0).Output;
            Assert.That(m_database.RuleSet.GetRule(2).Output, Is.SameAs(m_corner), "both corner rules place Corner_1");
            Assert.That(m_database.BrickEndPiece, Is.Not.Null, "the database binds Brick_End");
            Assert.That(m_database.CornerEndPiece, Is.Not.Null, "the database binds Corner_End");
            Assert.That(m_database.CornerJoinPiece, Is.Not.Null, "the database binds Corner_Join");
        }

        private static IEnumerable<BoardLayout> Fixtures()
        {
            yield return BoardLayoutTests.Create(4, 5, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 3, "LightBlue"),
                BoardLayoutTests.Door("bottom", BoardSide.Bottom, 0, 3, "Pink"));
            yield return BoardLayoutTests.Create(5, 5, BoardLayoutTests.Cells((4, 4)));
            yield return BoardLayoutTests.CreateHoleAndNotchLayout();
            yield return BoardLayoutTests.Create(3, 3, BoardLayoutTests.Cells((1, 0), (0, 1)));
            yield return BoardLayoutTests.Create(5, 3, BoardLayoutTests.Cells((0, 0), (1, 0), (2, 0), (3, 0), (4, 0),
                (0, 2), (1, 2), (2, 2), (3, 2), (4, 2)));
            yield return BoardLayoutTests.Create(4, 3, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 1, "Red"));
            yield return BoardLayoutTests.Create(1, 1);
            // One-cell stubs on the camera side: two ring corners meet side by side at each stub mouth.
            yield return BoardLayoutTests.Create(5, 5, BoardLayoutTests.Cells((0, 0), (4, 0), (2, 0)));
        }

        [Test]
        public void ThePlayAreaKeepsEveryRuleQuadrantFloorAndDoor()
        {
            foreach (BoardLayout layout in Fixtures())
            {
                Compose(layout);
                string name = $"{layout.Width}x{layout.Height}";
                Assert.That(Describe(m_playArea), Is.EquivalentTo(Describe(m_rules)),
                    $"{name}: floor, floor base and arrows are the rule placements");

                Dictionary<Vector2Int, EnvironmentPiecePlacement> rules = GetRuleWalls();
                HashSet<Vector2Int> shown = new HashSet<Vector2Int>();
                foreach (EnvironmentPiecePlacement piece in m_playArea)
                {
                    if (!IsWall(piece)) continue;
                    Vector2Int quadrant = GetQuadrant(piece);
                    Assert.That(rules.TryGetValue(quadrant, out EnvironmentPiecePlacement rule), Is.True,
                        $"{name}: {piece.Piece.name} at {quadrant} stands on a rule wall quadrant");
                    shown.Add(quadrant);
                    Assert.That(piece.Tint, Is.EqualTo(m_database.GetWallColor()));
                    if (piece.Piece == m_database.BrickEndPiece)
                    {
                        Assert.That(rule.Piece, Is.SameAs(m_wall), $"{name}: a cap closes a straight run at {quadrant}");
                        continue;
                    }

                    if (piece.Piece == m_database.CornerJoinPiece)
                    {
                        // Its yaw and a mirroring scale follow the joined arm (CornersBesideAOneCellVoidJoin...).
                        Assert.That(rule.Piece, Is.SameAs(m_corner), $"{name}: Corner_Join replaces a corner at {quadrant}");
                        Assert.That(piece.GridPosition, Is.EqualTo(rule.GridPosition));
                        continue;
                    }

                    if (piece.Piece == m_database.CornerEndPiece)
                    {
                        Assert.That(rule.Piece, Is.SameAs(m_corner), $"{name}: Corner_End replaces a corner at {quadrant}");
                    }
                    else
                    {
                        Assert.That(piece.Piece, Is.SameAs(rule.Piece), $"{name}: {quadrant} keeps its rule piece");
                    }

                    Assert.That(piece.Yaw, Is.EqualTo(rule.Yaw));
                    Assert.That(piece.Scale, Is.EqualTo(rule.Scale));
                    Assert.That(piece.GridPosition, Is.EqualTo(rule.GridPosition));
                }

                Assert.That(shown, Is.EquivalentTo(rules.Keys), $"{name}: every rule wall quadrant is shown");
            }
        }

        [TestCase(1, 1)]
        [TestCase(4, 3)]
        [TestCase(12, 2)]
        public void ARectangleHasFourBricksIndependentOfItsSize(int width, int height)
        {
            Compose(BoardLayoutTests.Create(width, height));
            Assert.That(Count(m_database.BrickEndPiece), Is.EqualTo(8), "four runs, two caps each");
            Assert.That(Count(m_wall), Is.EqualTo(4 * (width + height) - 8), "every other straight quadrant");
            Assert.That(Count(m_database.CornerEndPiece), Is.EqualTo(4), "four corner units");
            Assert.That(Count(m_corner), Is.Zero, "no rule corner is left on the outer boundary");
            AssertCapsCloseTheirRuns("rectangle");
        }

        [Test]
        public void EveryOuterCornerIsACornerEndWhileHoleCornersKeepCorner1()
        {
            // Cells (2..3, 2..3) are an interior hole; (0, 5) and (1, 5) are a notch open to the outer ring.
            Compose(BoardLayoutTests.CreateHoleAndNotchLayout());
            Dictionary<Vector2Int, EnvironmentPiecePlacement> rules = GetRuleWalls();
            int outerCorners = 0;
            int holeCorners = 0;
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (!IsWall(piece)) continue;
                Vector2Int quadrant = GetQuadrant(piece);
                EnvironmentPiecePlacement rule = rules[quadrant];
                if (rule.Piece != m_corner) continue;
                Vector2Int cell = GetCell(quadrant);
                bool inHole = cell.x >= 2 && cell.x <= 3 && cell.y >= 2 && cell.y <= 3;
                Assert.That(piece.Piece, Is.SameAs(inHole ? m_corner : m_database.CornerEndPiece),
                    $"the corner at {quadrant} {(inHole ? "keeps Corner_1 in the hole" : "is its own unit")}");
                Assert.That(piece.Yaw, Is.EqualTo(rule.Yaw), "Corner_End shares Corner_1's mesh space");
                Assert.That(piece.Scale, Is.EqualTo(rule.Scale));
                Assert.That(piece.GridPosition, Is.EqualTo(rule.GridPosition));
                if (inHole) holeCorners++; else outerCorners++;
            }

            Assert.That(holeCorners, Is.EqualTo(4), "the hole's four corners stay in its rule frame");
            // Five convex floor corners (cells (0, 0), (5, 0), (5, 5), (2, 5) and (0, 4)) and the notch's one
            // concave corner, in void cell (1, 5).
            Assert.That(outerCorners, Is.EqualTo(6));
            Assert.That(Count(m_database.CornerEndPiece), Is.EqualTo(6));
        }

        [Test]
        public void EachWallQuadrantAndDoorSlotStandsOnOneDarkBaseThatStopsShortOfOpenVoid()
        {
            foreach (BoardLayout layout in Fixtures())
            {
                Compose(layout);
                string name = $"{layout.Width}x{layout.Height}";
                HashSet<Vector2Int> walls = new HashSet<Vector2Int>();
                foreach (EnvironmentPiecePlacement piece in m_playArea)
                {
                    if (IsWall(piece))
                    {
                        walls.Add(GetQuadrant(piece));
                    }
                }

                // Door slots come from the rule placements: each spans the two quadrants on either side of its
                // centre along its length.
                foreach (EnvironmentPiecePlacement piece in m_rules)
                {
                    if (piece.Piece != m_database.DoorPiece) continue;
                    Vector2 along = piece.Yaw % 180 == 0 ? new Vector2(0.5f, 0f) : new Vector2(0f, 0.5f);
                    Vector2 center = new Vector2(piece.GridPosition.x, piece.GridPosition.y);
                    walls.Add(Vector2Int.FloorToInt(center - along));
                    walls.Add(Vector2Int.FloorToInt(center + along));
                }

                Dictionary<Vector2Int, EnvironmentPiecePlacement> bases = new Dictionary<Vector2Int, EnvironmentPiecePlacement>();
                foreach (EnvironmentPiecePlacement piece in m_playArea)
                {
                    if (!IsWallBase(piece)) continue;
                    Vector2Int quadrant = GetQuadrant(piece);
                    if (IsRingCornerPatch(piece))
                    {
                        AssertRingCornerPatch(layout, walls, piece, name);
                        continue;
                    }

                    Assert.That(bases.ContainsKey(quadrant), Is.False, $"{name}: one base at {quadrant}");
                    bases.Add(quadrant, piece);
                    Assert.That(piece.Tint, Is.EqualTo(m_database.GetFloorBaseColor()), $"{name}: the base is dark");
                    Assert.That(piece.GridPosition.z, Is.EqualTo(-0.02f).Within(1e-6f), $"{name}: at the floor base depth");
                    Assert.That(piece.Yaw, Is.Zero);
                    // FloorBase spans one cell (two quadrants) at scale 1: sizes here are in quadrants.
                    Vector2 size = new Vector2(piece.Scale.x * 2f, piece.Scale.z * 2f);
                    Vector2 low = new Vector2(piece.GridPosition.x, piece.GridPosition.y) - size * 0.5f;
                    Vector2 high = low + size;
                    Assert.That(low.x, Is.GreaterThanOrEqualTo(quadrant.x - 1e-5f), $"{name}: the base at {quadrant} stays inside it");
                    Assert.That(low.y, Is.GreaterThanOrEqualTo(quadrant.y - 1e-5f));
                    Assert.That(high.x, Is.LessThanOrEqualTo(quadrant.x + 1 + 1e-5f));
                    Assert.That(high.y, Is.LessThanOrEqualTo(quadrant.y + 1 + 1e-5f));
                    // A side that meets floor, a wall or a gate is covered to its edge; one that faces open void
                    // stops a quarter quadrant short.
                    AssertBaseSide(layout, walls, quadrant, Vector2Int.left, low.x - quadrant.x, $"{name}: {quadrant} left");
                    AssertBaseSide(layout, walls, quadrant, Vector2Int.right, quadrant.x + 1 - high.x, $"{name}: {quadrant} right");
                    AssertBaseSide(layout, walls, quadrant, Vector2Int.down, low.y - quadrant.y, $"{name}: {quadrant} below");
                    AssertBaseSide(layout, walls, quadrant, Vector2Int.up, quadrant.y + 1 - high.y, $"{name}: {quadrant} above");
                }

                Assert.That(bases.Keys, Is.EquivalentTo(walls),
                    $"{name}: exactly the wall quadrants and door slots have a base");
            }
        }

        [Test]
        public void EveryGateIsClosedByRoundedEndsFacingOutOfItsSpan()
        {
            // Gates of 3, 2 and 1 slots on three sides.
            Compose(BoardLayoutTests.Create(5, 4, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 3, "Red"),
                BoardLayoutTests.Door("right", BoardSide.Right, 1, 2, "Blue"),
                BoardLayoutTests.Door("bottom", BoardSide.Bottom, 4, 1, "Yellow")));
            Assert.That(m_gates.Count, Is.EqualTo(3));
            int[] expected = { 3, 2, 2 };
            for (int door = 0; door < m_gates.Count; door++)
            {
                EnvironmentGateSpan span = m_gates[door];
                Assert.That(span.PlacementCount, Is.EqualTo(expected[door] + 1));
                // The span's slot centres: the pieces must cover the same line the rule doors do.
                Vector2 low = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 high = new Vector2(float.MinValue, float.MinValue);
                for (int piece = 0; piece < expected[door]; piece++)
                {
                    Vector3 position = m_playArea[span.FirstPlacement + piece].GridPosition;
                    low = Vector2.Min(low, new Vector2(position.x, position.y));
                    high = Vector2.Max(high, new Vector2(position.x, position.y));
                }

                Vector2 middle = (low + high) * 0.5f;
                for (int piece = 0; piece < expected[door]; piece++)
                {
                    EnvironmentPiecePlacement placement = m_playArea[span.FirstPlacement + piece];
                    Assert.That(placement.Tint, Is.EqualTo(m_playArea[span.FirstPlacement].Tint), "one door colour");
                    if (placement.Piece == m_database.DoorPiece) continue;
                    Assert.That(placement.Piece, Is.SameAs(m_database.DoorEndPiece));
                    // Door_End's rounded end faces local +X at yaw 0 (the FBX import mirrors X); each yaw turns it
                    // clockwise seen from above.
                    Vector2 rounded = GetDoorEndRounded(placement.Yaw);
                    Vector2 outward = new Vector2(placement.GridPosition.x, placement.GridPosition.y) - middle;
                    Assert.That(Vector2.Dot(rounded, outward), Is.GreaterThan(0f),
                        $"gate {door}: the end at {placement.GridPosition} rounds away from its span");
                    bool half = expected[door] == 2 && Mathf.Approximately(placement.Scale.x, m_database.DoorPieceScale.x * 0.5f);
                    Assert.That(half, Is.EqualTo(door == 2), $"gate {door}: only a one-slot gate uses half-length ends");
                }
            }
        }

        [Test]
        public void BoardCornersBesideAOneCellVoidJoinTheirShorterWall()
        {
            // A one-cell notch at the top-right corner of a 5 x 5 board.
            BoardLayout layout = BoardLayoutTests.Create(5, 5, BoardLayoutTests.Cells((4, 4)));
            Compose(layout);
            Dictionary<Vector2Int, EnvironmentPiecePlacement> rules = GetRuleWalls();
            int joins = 0;
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (piece.Piece != m_database.CornerJoinPiece) continue;
                joins++;
                Vector2Int quadrant = GetQuadrant(piece);
                Vector2Int cell = GetCell(quadrant);
                Assert.That(Mathf.Abs(cell.x - 4) + Mathf.Abs(cell.y - 4), Is.LessThanOrEqualTo(1),
                    $"the Corner_Join at {quadrant} is in or beside the one-cell void");
                Assert.That(GetRingCornerFloor(layout, quadrant), Is.Not.EqualTo(Vector2Int.zero),
                    $"only a board corner (floor only diagonally) merges, not the void's inner corner ({quadrant})");
                // Its open arm meets a plain Wall of its run, or a full cap whose mouth meets the corner.
                Vector2Int neighbour = quadrant + GetOpenArm(piece);
                EnvironmentPiecePlacement joined = FindWall(neighbour);
                bool plainWall = joined.Piece == m_wall;
                bool cap = joined.Piece == m_database.BrickEndPiece && !IsHalf(joined)
                    && neighbour + GetMouth(joined.Yaw) == quadrant;
                Assert.That(plainWall || cap, Is.True, $"the Corner_Join at {quadrant} joins {neighbour}");
                Assert.That(rules[neighbour].Piece, Is.SameAs(m_wall), "it joins a straight run");
            }

            Assert.That(joins, Is.EqualTo(2), "the notch's two board corners merge");
            // The notch's inner corner and the board's other three corners stay separate rounded units.
            Assert.That(Count(m_database.CornerEndPiece), Is.EqualTo(4));

            Compose(BoardLayoutTests.Create(4, 3));
            Assert.That(Count(m_database.CornerJoinPiece), Is.Zero, "a board with no one-cell void keeps every corner separate");
        }

        [Test]
        public void AStraightWallBaseReachesTheFloorEdge()
        {
            Compose(BoardLayoutTests.Create(4, 3));
            int checkedBases = 0;
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (!IsWallBase(piece) || !Mathf.Approximately(Mathf.Max(piece.Scale.x, piece.Scale.z), 0.5f)) continue;
                // A bottom-row base (quadrant row -2) reaches up to the floor at grid y = -1.
                if (GetQuadrant(piece).y != -2) continue;
                Assert.That(piece.GridPosition.y + piece.Scale.z, Is.EqualTo(-1f).Within(1e-5f));
                checkedBases++;
            }

            Assert.That(checkedBases, Is.EqualTo(8), "every straight quadrant of the bottom side");
        }

        [Test]
        public void AGateSplitsItsSideIntoTwoBricks()
        {
            Compose(BoardLayoutTests.Create(4, 3, Array.Empty<Vector2Int>(),
                BoardLayoutTests.Door("top", BoardSide.Top, 1, 1, "Red")));
            Assert.That(Count(m_database.BrickEndPiece), Is.EqualTo(10), "the top side becomes two bricks");
            Assert.That(Count(m_database.DoorPiece), Is.Zero, "a one-slot gate has no middle slot");
            Assert.That(Count(m_database.DoorEndPiece), Is.EqualTo(2), "two half-length gate ends");
            Assert.That(Count(m_database.DoorArrowPiece), Is.EqualTo(1));
            Assert.That(m_gates.Count, Is.EqualTo(1));
            AssertCapsCloseTheirRuns("gate");
        }

        [Test]
        public void EveryFixtureClosesEachStraightRunAtBothEnds()
        {
            foreach (BoardLayout layout in Fixtures())
            {
                Compose(layout);
                AssertCapsCloseTheirRuns($"{layout.Width}x{layout.Height}");
            }
        }

        [Test]
        public void AOneQuadrantRunGetsTwoHalfLengthCapsBackToBack()
        {
            // The notch step of the hole fixture leaves one straight quadrant between two turns.
            Compose(BoardLayoutTests.CreateHoleAndNotchLayout());
            Dictionary<Vector2Int, List<EnvironmentPiecePlacement>> halves = new Dictionary<Vector2Int, List<EnvironmentPiecePlacement>>();
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (piece.Piece != m_database.BrickEndPiece || !IsHalf(piece)) continue;
                Assert.That(piece.Scale, Is.EqualTo(new Vector3(m_database.WallPieceScale.x, m_database.WallPieceScale.y,
                    m_database.WallPieceScale.z * 0.5f)));
                Vector2Int quadrant = GetQuadrant(piece);
                if (!halves.TryGetValue(quadrant, out List<EnvironmentPiecePlacement> pair))
                {
                    halves[quadrant] = pair = new List<EnvironmentPiecePlacement>();
                }

                pair.Add(piece);
            }

            Assert.That(halves.Count, Is.GreaterThan(0), "the fixture has a one-quadrant run");
            foreach (KeyValuePair<Vector2Int, List<EnvironmentPiecePlacement>> pair in halves)
            {
                Assert.That(pair.Value.Count, Is.EqualTo(2), $"two half caps at {pair.Key}");
                Vector2 center = new Vector2(pair.Key.x + 0.5f, pair.Key.y + 0.5f);
                foreach (EnvironmentPiecePlacement half in pair.Value)
                {
                    Vector2 offset = new Vector2(half.GridPosition.x, half.GridPosition.y) - center;
                    Assert.That(offset.magnitude, Is.EqualTo(0.25f).Within(1e-5f), "a quarter quadrant off centre");
                    Vector2Int mouth = GetMouth(half.Yaw);
                    Assert.That(new Vector2(mouth.x, mouth.y), Is.EqualTo(-offset.normalized), "its mouth meets the other half");
                }

                Assert.That(GetMouth(pair.Value[0].Yaw), Is.EqualTo(-GetMouth(pair.Value[1].Yaw)));
            }
        }

        [Test]
        public void AnInteriorHoleKeepsOneContinuousRuleFrameWhileANotchIsBricked()
        {
            // Cells (2..3, 2..3) are an interior hole; (0, 5) and (1, 5) are a notch open to the outer ring.
            Compose(BoardLayoutTests.CreateHoleAndNotchLayout());
            Dictionary<Vector2Int, EnvironmentPiecePlacement> rules = GetRuleWalls();
            int holeQuadrants = 0;
            int notchCaps = 0;
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (!IsWall(piece)) continue;
                Vector2Int quadrant = GetQuadrant(piece);
                Vector2Int cell = GetCell(quadrant);
                if (cell.y == 5 && cell.x <= 1 && piece.Piece == m_database.BrickEndPiece) notchCaps++;
                if (cell.x < 2 || cell.x > 3 || cell.y < 2 || cell.y > 3) continue;
                holeQuadrants++;
                EnvironmentPiecePlacement rule = rules[quadrant];
                Assert.That(piece.Piece, Is.SameAs(rule.Piece), $"the hole keeps its rule piece at {quadrant}");
                Assert.That(piece.Yaw, Is.EqualTo(rule.Yaw));
                Assert.That(piece.Scale, Is.EqualTo(rule.Scale));
                Assert.That(piece.GridPosition, Is.EqualTo(rule.GridPosition));
            }

            Assert.That(holeQuadrants, Is.EqualTo(12), "a 2 x 2 hole is ringed by 8 straight and 4 corner quadrants");
            Assert.That(notchCaps, Is.GreaterThan(0), "the notch belongs to the outer boundary");
        }

        [Test]
        public void TransposingTheBoardTransposesThePlayArea()
        {
            Compose(BoardLayoutTests.Create(7, 5, BoardLayoutTests.Cells((2, 2), (3, 2), (4, 2), (0, 4))));
            HashSet<string> original = Signatures(transpose: true);
            Compose(BoardLayoutTests.Create(5, 7, BoardLayoutTests.Cells((2, 2), (2, 3), (2, 4), (4, 0))));
            Assert.That(Signatures(transpose: false).SetEquals(original), Is.True);
        }

        [Test]
        public void ComposeStillGivesOnlyRulePieces()
        {
            m_composer.Compose(BoardLayoutTests.CreateHoleAndNotchLayout(), m_rules);
            foreach (EnvironmentPiecePlacement piece in m_rules)
            {
                Assert.That(piece.Piece, Is.Not.SameAs(m_database.BrickEndPiece), "the Level Editor canvas draws rule pieces");
                Assert.That(piece.Piece, Is.Not.SameAs(m_database.CornerEndPiece), "the Level Editor canvas draws rule corners");
                Assert.That(piece.Piece, Is.Not.SameAs(m_database.CornerJoinPiece), "the Level Editor canvas draws rule corners");
                Assert.That(IsWallBase(piece), Is.False, "the rule placements put no base under walls");
            }
        }

        private void Compose(BoardLayout layout)
        {
            m_composer.Compose(layout, m_rules);
            m_composer.ComposePlayArea(layout, m_playArea, m_gates);
        }

        // Every full cap's mouth opens onto the next quadrant of its own run (a straight rule wall with the same
        // yaw) or onto a Corner_Join merged into it, and its rounded end meets something else: a corner, a gate or
        // void. A half cap's run is one quadrant.
        private void AssertCapsCloseTheirRuns(string name)
        {
            Dictionary<Vector2Int, EnvironmentPiecePlacement> rules = GetRuleWalls();
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (piece.Piece != m_database.BrickEndPiece) continue;
                Vector2Int quadrant = GetQuadrant(piece);
                Vector2Int mouth = GetMouth(piece.Yaw);
                int yaw = rules[quadrant].Yaw;
                Assert.That(IsRun(rules, quadrant - mouth, yaw), Is.False, $"{name}: the cap at {quadrant} ends its run");
                Assert.That(IsRun(rules, quadrant + mouth, yaw) || IsJoinedTo(quadrant + mouth, quadrant),
                    Is.EqualTo(!IsHalf(piece)), $"{name}: the cap at {quadrant} opens onto its run or a merged corner");
            }
        }

        // Whether a Corner_Join stands at corner with its open arm toward target.
        private bool IsJoinedTo(Vector2Int corner, Vector2Int target)
        {
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (piece.Piece == m_database.CornerJoinPiece && GetQuadrant(piece) == corner)
                {
                    return corner + GetOpenArm(piece) == target;
                }
            }

            return false;
        }

        // Corner_Join's open arm faces local -X at yaw 0 (Unity mirrors the FBX's X axis); a negative X scale mirrors
        // it to local +X. Each yaw turns it clockwise seen from above.
        private static Vector2Int GetOpenArm(EnvironmentPiecePlacement piece)
        {
            int cos = piece.Yaw == 0 ? 1 : piece.Yaw == 180 ? -1 : 0;
            int sin = piece.Yaw == 90 ? 1 : piece.Yaw == 270 ? -1 : 0;
            return piece.Scale.x < 0f ? new Vector2Int(cos, -sin) : new Vector2Int(-cos, sin);
        }

        private bool IsRun(Dictionary<Vector2Int, EnvironmentPiecePlacement> rules, Vector2Int quadrant, int yaw)
        {
            return rules.TryGetValue(quadrant, out EnvironmentPiecePlacement rule) && rule.Piece == m_wall && rule.Yaw == yaw;
        }

        private static Vector2 GetDoorEndRounded(int yaw)
        {
            switch (yaw)
            {
                case 0: return Vector2.right;
                case 90: return Vector2.down;
                case 180: return Vector2.left;
                case 270: return Vector2.up;
                default: throw new ArgumentOutOfRangeException(nameof(yaw), yaw, "Piece yaws are 0, 90, 180 or 270.");
            }
        }

        // A side stops a quarter quadrant short when it faces open void, or when the quadrant is a ring corner
        // (floor only diagonally) and the side faces away from that floor, where its outer arc curves away.
        private static void AssertBaseSide(BoardLayout layout, HashSet<Vector2Int> walls, Vector2Int quadrant,
            Vector2Int side, float gap, string where)
        {
            Vector2Int neighbour = quadrant + side;
            bool open = !IsFloorAt(layout, neighbour) && !walls.Contains(neighbour);
            Vector2Int diagonal = GetRingCornerFloor(layout, quadrant);
            bool awayFromRingFloor = diagonal != Vector2Int.zero && side.x * diagonal.x + side.y * diagonal.y < 0;
            bool shortened = open || awayFromRingFloor;
            Assert.That(gap, Is.EqualTo(shortened ? 0.25f : 0f).Within(1e-5f),
                $"{where}: {(shortened ? "stops short" : "reaches floor, a wall or a gate")}");
        }

        // A quarter-by-quarter base square: the pocket patch of a ring corner.
        private static bool IsRingCornerPatch(EnvironmentPiecePlacement piece)
        {
            return Mathf.Approximately(piece.Scale.x, 0.125f) && Mathf.Approximately(piece.Scale.z, 0.125f);
        }

        // A patch sits in a ring corner, in the quadrant corner between a side away from the floor that meets a
        // wall or a gate and the side toward the floor.
        private void AssertRingCornerPatch(BoardLayout layout, HashSet<Vector2Int> walls, EnvironmentPiecePlacement piece,
            string name)
        {
            Vector2Int quadrant = GetQuadrant(piece);
            Vector2Int ring = GetRingCornerFloor(layout, quadrant);
            Assert.That(ring, Is.Not.EqualTo(Vector2Int.zero), $"{name}: a patch at {quadrant} is in a ring corner");
            Assert.That(walls.Contains(quadrant), Is.True, $"{name}: the patch at {quadrant} is under a wall");
            Vector2 local = new Vector2(piece.GridPosition.x - quadrant.x, piece.GridPosition.y - quadrant.y);
            bool onAwayX = Mathf.Approximately(local.x, ring.x > 0 ? 0.125f : 0.875f)
                && Mathf.Approximately(local.y, ring.y > 0 ? 0.875f : 0.125f);
            bool onAwayY = Mathf.Approximately(local.x, ring.x > 0 ? 0.875f : 0.125f)
                && Mathf.Approximately(local.y, ring.y > 0 ? 0.125f : 0.875f);
            Assert.That(onAwayX || onAwayY, Is.True, $"{name}: the patch at {piece.GridPosition} sits by the floor end of an away side");
            Vector2Int side = onAwayX ? new Vector2Int(-ring.x, 0) : new Vector2Int(0, -ring.y);
            Vector2Int neighbour = quadrant + side;
            Assert.That(walls.Contains(neighbour) || IsFloorAt(layout, neighbour), Is.True,
                $"{name}: the patch at {piece.GridPosition} faces a wall or a gate, not open void");
        }

        private EnvironmentPiecePlacement FindWall(Vector2Int quadrant)
        {
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (IsWall(piece) && GetQuadrant(piece) == quadrant) return piece;
            }

            Assert.Fail($"no wall piece at {quadrant}");
            return default;
        }

        private static Vector2Int GetRingCornerFloor(BoardLayout layout, Vector2Int quadrant)
        {
            if (IsFloorAt(layout, quadrant + Vector2Int.left) || IsFloorAt(layout, quadrant + Vector2Int.right)
                || IsFloorAt(layout, quadrant + Vector2Int.down) || IsFloorAt(layout, quadrant + Vector2Int.up))
            {
                return Vector2Int.zero;
            }

            foreach (Vector2Int diagonal in new[] { new Vector2Int(-1, -1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(1, 1) })
            {
                if (IsFloorAt(layout, quadrant + diagonal)) return diagonal;
            }

            return Vector2Int.zero;
        }

        private static bool IsFloorAt(BoardLayout layout, Vector2Int quadrant)
        {
            Vector2Int cell = GetCell(quadrant);
            return layout.IsFloor(cell.x, cell.y);
        }

        // Quadrant (2x - 1 | 2x, 2y - 1 | 2y) belongs to cell (x, y).
        private static Vector2Int GetCell(Vector2Int quadrant)
        {
            return new Vector2Int((quadrant.x + 1) >> 1, (quadrant.y + 1) >> 1);
        }

        // Gate slot pieces differ by design: the play area closes each gate with Door_End pieces.
        private bool IsGateSlot(EnvironmentPiecePlacement piece)
        {
            return piece.Piece == m_database.DoorPiece || piece.Piece == m_database.DoorEndPiece;
        }

        private bool IsWallBase(EnvironmentPiecePlacement piece)
        {
            return piece.Piece == m_database.FloorBasePiece && piece.Scale != Vector3.one;
        }

        private bool IsHalf(EnvironmentPiecePlacement piece)
        {
            return !Mathf.Approximately(piece.Scale.z, m_database.WallPieceScale.z);
        }

        // Brick_End opens toward grid -y at yaw 0; each yaw turns it clockwise seen from above.
        private static Vector2Int GetMouth(int yaw)
        {
            switch (yaw)
            {
                case 0: return new Vector2Int(0, -1);
                case 90: return new Vector2Int(-1, 0);
                case 180: return new Vector2Int(0, 1);
                case 270: return new Vector2Int(1, 0);
                default: throw new ArgumentOutOfRangeException(nameof(yaw), yaw, "Piece yaws are 0, 90, 180 or 270.");
            }
        }

        private HashSet<string> Signatures(bool transpose)
        {
            HashSet<string> signatures = new HashSet<string>();
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (!IsWall(piece)) continue;
                float x = transpose ? piece.GridPosition.y : piece.GridPosition.x;
                float y = transpose ? piece.GridPosition.x : piece.GridPosition.y;
                string shape = piece.Piece.name;
                if (piece.Piece == m_database.BrickEndPiece)
                {
                    Vector2Int mouth = GetMouth(piece.Yaw);
                    shape += transpose ? $"{mouth.y},{mouth.x}" : $"{mouth.x},{mouth.y}";
                }
                else if (piece.Piece == m_wall)
                {
                    shape += (piece.Yaw % 180 == 0) != transpose ? "-y" : "-x";
                }

                signatures.Add($"{shape}@{x:F2},{y:F2}/{piece.Scale.z:F3}");
            }

            return signatures;
        }

        private Dictionary<Vector2Int, EnvironmentPiecePlacement> GetRuleWalls()
        {
            Dictionary<Vector2Int, EnvironmentPiecePlacement> quadrants = new Dictionary<Vector2Int, EnvironmentPiecePlacement>();
            foreach (EnvironmentPiecePlacement piece in m_rules)
            {
                if (IsWall(piece))
                {
                    quadrants.Add(GetQuadrant(piece), piece);
                }
            }

            return quadrants;
        }

        // The non-wall placements, which the play area shares with the rule placements (wall bases are its own).
        private List<string> Describe(List<EnvironmentPiecePlacement> placements)
        {
            List<string> descriptions = new List<string>();
            foreach (EnvironmentPiecePlacement piece in placements)
            {
                if (IsWall(piece) || IsWallBase(piece) || IsGateSlot(piece)) continue;
                descriptions.Add($"{piece.Piece.name}@{piece.GridPosition.x:F3},{piece.GridPosition.y:F3},{piece.GridPosition.z:F3}" +
                    $"/{piece.Yaw}/{piece.Scale}/{piece.Tint}");
            }

            return descriptions;
        }

        private int Count(BoardPieceView prefab)
        {
            int count = 0;
            foreach (EnvironmentPiecePlacement piece in m_playArea)
            {
                if (piece.Piece == prefab) count++;
            }

            return count;
        }

        private bool IsWall(EnvironmentPiecePlacement piece)
        {
            return piece.Piece != m_database.FloorPiece && piece.Piece != m_database.FloorBasePiece
                && piece.Piece != m_database.DoorPiece && piece.Piece != m_database.DoorEndPiece
                && piece.Piece != m_database.DoorArrowPiece;
        }

        private static Vector2Int GetQuadrant(EnvironmentPiecePlacement piece)
        {
            return new Vector2Int(Mathf.FloorToInt(piece.GridPosition.x), Mathf.FloorToInt(piece.GridPosition.y));
        }
    }
}
