using ColorBlockJam.Runtime.Art;
using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using ColorBlockJam.Runtime.Layout;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Turns a board layout into piece placements on the half-cell quadrant grid:
    /// one floor piece and one floor-base square per floor cell, wall and corner pieces chosen by the
    /// environment rule set, and door pieces in the door slots. Compose gives exactly the rule
    /// placements, which the Level Editor canvas draws. ComposePlayArea gives what the player sees: the
    /// same quadrants with each straight run of the outer boundary closed into one brick, each of its
    /// corners a closed corner unit, each gate closed by rounded ends, a dark base under every wall and gate,
    /// plus one gate span per authored door (DEC-139). PlayAreaBoundary finds that outer boundary. It runs once per level load; the view maps its grid positions to local space with the Grid.
    /// </summary>
    public sealed class EnvironmentComposer
    {
        // Quadrant grid: cell (x, y) covers grid cells (2x - 1 | 2x, 2y - 1 | 2y), so the
        // cell centre is the grid vertex (2x, 2y) and each quadrant is one grid cell.
        // Wall pieces fill the void quadrants next to floor.
        // Walls stand at most one cell outside the board; PlayAreaBoundary reads the same ring.
        internal const int RingCells = 1;
        // Grid z maps to local height. 0.02 quadrant units (0.01 at the 0.5 Grid cell) keep
        // each floor-base square just under its flat floor tile so the two never z-fight.
        private const float k_floorBaseDepth = 0.02f;
        // A one-quadrant run is closed by two half-length ends, each centred a quarter quadrant off centre.
        private const float k_halfEndOffset = 0.25f;
        // A one-slot gate is closed by two half-length Door_End pieces, each a quarter cell (half a quadrant) off centre.
        private const float k_halfDoorOffset = 0.5f;
        // The base under a wall quadrant stops this far short of a side that faces open void, so it never
        // shows outside the board or inside a hole; gaps between wall units and by the floor then read dark.
        private const float k_wallBaseInset = 0.25f;

        private readonly EnvironmentDatabase m_database;
        private readonly EnvironmentRuleMatcher m_matcher;
        // ComposePlayArea only: reused for every level, so its buffers grow instead of allocating per level.
        private readonly PlayAreaBoundary m_boundary;
        private bool[] m_doorQuadrants = Array.Empty<bool>();
        private RectInt m_quadrantBounds;

        public EnvironmentComposer(EnvironmentDatabase database)
        {
            if (database == null) throw new ArgumentNullException(nameof(database));
            if (!database.Validate(out string error)) throw new ArgumentException(error, nameof(database));

            m_database = database;
            m_matcher = new EnvironmentRuleMatcher(database.RuleSet);
            m_boundary = new PlayAreaBoundary(m_matcher);
        }

        /// <summary>The rule placements: one rule piece on every wall quadrant, as the rule set decides it.</summary>
        public void Compose(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            ComposeFloorAndDoorMask(layout, output);
            AddWalls(layout, output);
            AddPlainDoors(layout, output);
        }

        /// <summary>
        /// The play area the player sees (DEC-139): Compose's quadrants, with every maximal straight wall
        /// run of the outer boundary between corners and gates closed into one brick by Brick_End caps,
        /// every corner of the outer boundary a Corner_End unit, interior holes kept as one continuous rule
        /// frame, a floor-base square under every wall quadrant and door slot, and gates filled with one span
        /// per authored door in door order: that door's slot pieces (Door_End at both ends of the span,
        /// Door_1x1 between them), then its arrow, contiguous in output.
        /// </summary>
        public void ComposePlayArea(IBoardLayout layout, List<EnvironmentPiecePlacement> output,
            List<EnvironmentGateSpan> gates)
        {
            if (gates == null) throw new ArgumentNullException(nameof(gates));

            ComposeFloorAndDoorMask(layout, output);
            gates.Clear();
            AddBricks(layout, output);
            AddGateDoors(layout, output, gates);
        }

        // Shared setup for Compose and ComposePlayArea: validates the database, resets the matcher over
        // one level's quadrant bounds and marks the floor and door quadrants both pipelines match on.
        private void ComposeFloorAndDoorMask(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (output == null) throw new ArgumentNullException(nameof(output));
            // The matcher reads the rule set asset live, and a designer can edit it while a
            // view keeps this composer (LevelEditor preview, Editor Play Mode). Validating on
            // every compose names the broken rule; the success path allocates nothing.
            if (!m_database.Validate(out string error))
            {
                throw new InvalidOperationException("The environment database is invalid: " + error);
            }

            output.Clear();
            m_quadrantBounds = GetQuadrantBounds(layout);
            m_matcher.Reset(m_quadrantBounds);
            AddFloor(layout, output);
            MarkDoorQuadrants(layout);
        }

        private void AddFloor(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            AtlasColor floorColor = m_database.GetFloorColor();
            AtlasColor floorBaseColor = m_database.GetFloorBaseColor();
            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    if (!layout.IsFloor(x, y))
                    {
                        continue;
                    }

                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        Vector2Int position = GetQuadrant(x, y, quadrant);
                        m_matcher.SetFloor(position.x, position.y);
                    }

                    output.Add(new EnvironmentPiecePlacement(
                        m_database.FloorPiece, new Vector3(2 * x, 2 * y, 0f), 0, Vector3.one, floorColor));
                    // Void cells get no full base, so holes and notches show the background; the play area adds
                    // a partial base under each wall and gate (AddWallBases).
                    output.Add(new EnvironmentPiecePlacement(
                        m_database.FloorBasePiece, new Vector3(2 * x, 2 * y, -k_floorBaseDepth), 0, Vector3.one,
                        floorBaseColor));
                }
            }
        }

        private void MarkDoorQuadrants(IBoardLayout layout)
        {
            int quadrantCount = m_quadrantBounds.width * m_quadrantBounds.height;
            if (m_doorQuadrants.Length < quadrantCount)
            {
                m_doorQuadrants = new bool[quadrantCount];
            }
            else
            {
                Array.Clear(m_doorQuadrants, 0, quadrantCount);
            }

            for (int index = 0; index < layout.DoorSlotCount; index++)
            {
                GetDoorQuadrants(layout.GetDoorSlotAt(index), out Vector2Int first, out Vector2Int second);
                m_doorQuadrants[GetQuadrantIndex(first)] = true;
                m_doorQuadrants[GetQuadrantIndex(second)] = true;
            }
        }

        private void AddWalls(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            Vector3 scale = m_database.WallPieceScale;
            AtlasColor wallColor = m_database.GetWallColor();
            for (int y = -RingCells; y < layout.Height + RingCells; y++)
            {
                for (int x = -RingCells; x < layout.Width + RingCells; x++)
                {
                    if (layout.IsFloor(x, y))
                    {
                        continue;
                    }

                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        Vector2Int position = GetQuadrant(x, y, quadrant);
                        if (m_doorQuadrants[GetQuadrantIndex(position)])
                        {
                            continue;
                        }

                        if (!m_matcher.TryMatch(position.x, position.y, out int ruleIndex, out int angle))
                        {
                            continue;
                        }

                        output.Add(new EnvironmentPiecePlacement(
                            m_matcher.GetOutput(ruleIndex),
                            GetQuadrantCenter(position), angle, scale, wallColor));
                    }
                }
            }
        }

        // DEC-139: the rule decides every wall quadrant exactly as in AddWalls. On the outer boundary, a
        // quadrant with floor on one side only is straight; each maximal run of straight quadrants with the
        // same rule and yaw becomes one brick, and a corner is Corner_End, rounded on both arms, so a lowered gate
        // beside it shows a closed end. Around a one-cell void no unit is shorter than one cell: each ring corner
        // there (floor only diagonally) merges into its shorter neighbouring run as Corner_Join, whose joined arm
        // meets that run's plain Wall end; the void's inner corners stay Corner_End, rounded on both arms.
        // An interior hole keeps its rule pieces as one continuous frame, as in the reference game (references 02
        // and 17) and like a Barrier.
        private void AddBricks(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            m_boundary.Build(layout, m_quadrantBounds, m_doorQuadrants);
            Vector3 scale = m_database.WallPieceScale;
            AtlasColor wallColor = m_database.GetWallColor();
            for (int y = m_quadrantBounds.yMin; y < m_quadrantBounds.yMax; y++)
            {
                for (int x = m_quadrantBounds.xMin; x < m_quadrantBounds.xMax; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    int index = GetQuadrantIndex(position);
                    if (!m_boundary.IsWall(index) || m_boundary.IsUsed(index))
                    {
                        continue;
                    }

                    // A run never leaves its void region: quadrants side by side belong to one cell or to
                    // two neighbouring void cells, which the flood fill puts in the same region.
                    bool isOuter = m_boundary.IsOuterQuadrant(position);
                    if (isOuter && m_boundary.TryGetStraightAxis(position, out Vector2Int axis))
                    {
                        AddBrick(position, axis, scale, wallColor, output);
                        continue;
                    }

                    // Corner_End and Corner_Join keep Corner_1's mesh space, so they take the corner rule's yaw.
                    m_boundary.SetUsed(index);
                    if (isOuter && m_boundary.IsCorner(position))
                    {
                        AddCorner(position, index, scale, wallColor, output);
                        continue;
                    }

                    output.Add(new EnvironmentPiecePlacement(m_matcher.GetOutput(m_boundary.GetWallRule(index)),
                        GetQuadrantCenter(position), m_boundary.GetWallYaw(index), scale, wallColor));
                }
            }

            AddWallBases(layout, output);
        }

        // One floor-base square under each wall quadrant and door slot, in the floor base colour. Without it
        // the board has nothing below its walls and gates, and the background shows through the gaps between
        // wall units, past rounded wall corners and past the floor tiles' rounded corners at the wall line.
        private void AddWallBases(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            AtlasColor baseColor = m_database.GetFloorBaseColor();
            for (int y = m_quadrantBounds.yMin; y < m_quadrantBounds.yMax; y++)
            {
                for (int x = m_quadrantBounds.xMin; x < m_quadrantBounds.xMax; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    int index = GetQuadrantIndex(position);
                    if (!IsWallOrGate(index))
                    {
                        continue;
                    }

                    // The base reaches every side that meets floor, a wall or a gate, so no gap between them
                    // opens onto the background. A ring corner (floor only diagonally) always stops short on
                    // its two sides away from the floor: its outer arc curves away from them, so a base there
                    // would show past the arc even where a neighbouring wall meets that side.
                    Vector2Int ring = m_boundary.GetRingCornerFloor(position);
                    float left = ring.x > 0 || IsOpenVoid(position + Vector2Int.left) ? k_wallBaseInset : 0f;
                    float right = ring.x < 0 || IsOpenVoid(position + Vector2Int.right) ? k_wallBaseInset : 0f;
                    float down = ring.y > 0 || IsOpenVoid(position + Vector2Int.down) ? k_wallBaseInset : 0f;
                    float up = ring.y < 0 || IsOpenVoid(position + Vector2Int.up) ? k_wallBaseInset : 0f;
                    Vector3 center = GetQuadrantCenter(position)
                        + new Vector3(0.5f * (left - right), 0.5f * (down - up), -k_floorBaseDepth);
                    // FloorBase spans one cell (two quadrants) at scale 1, lying in local X and Z.
                    output.Add(new EnvironmentPiecePlacement(m_database.FloorBasePiece, center, 0,
                        new Vector3(0.5f * (1f - left - right), 1f, 0.5f * (1f - down - up)), baseColor));
                    if (ring != Vector2Int.zero)
                    {
                        AddRingCornerPatches(position, ring, baseColor, output);
                    }
                }
            }
        }

        // Where a wall meets a ring corner on a side away from the floor, the corner's rounded end pulls away
        // from that side next to the floor and leaves a small pocket between the two units. One inset-sized
        // square fills it: at the end of that side toward the floor, inside the joint, never outside the board.
        private void AddRingCornerPatches(Vector2Int position, Vector2Int ring, AtlasColor baseColor,
            List<EnvironmentPiecePlacement> output)
        {
            Vector3 corner = new Vector3(position.x, position.y, -k_floorBaseDepth);
            Vector3 scale = new Vector3(0.5f * k_wallBaseInset, 1f, 0.5f * k_wallBaseInset);
            float near = 0.5f * k_wallBaseInset;
            float far = 1f - near;
            // The side away from the floor along x, patched at its end toward the floor along y.
            if (!IsOpenVoid(position + new Vector2Int(-ring.x, 0)))
            {
                output.Add(new EnvironmentPiecePlacement(m_database.FloorBasePiece,
                    corner + new Vector3(ring.x > 0 ? near : far, ring.y > 0 ? far : near, 0f), 0, scale, baseColor));
            }

            // The side away from the floor along y, patched at its end toward the floor along x.
            if (!IsOpenVoid(position + new Vector2Int(0, -ring.y)))
            {
                output.Add(new EnvironmentPiecePlacement(m_database.FloorBasePiece,
                    corner + new Vector3(ring.x > 0 ? far : near, ring.y > 0 ? near : far, 0f), 0, scale, baseColor));
            }
        }

        private bool IsWallOrGate(int index)
        {
            return m_boundary.IsWall(index) || m_doorQuadrants[index];
        }

        // Void with no wall or gate on it: the background outside the board or inside a hole.
        private bool IsOpenVoid(Vector2Int position)
        {
            return !m_matcher.IsFloor(position.x, position.y)
                && (!m_quadrantBounds.Contains(position) || !IsWallOrGate(GetQuadrantIndex(position)));
        }

        // A corner joined to a run is Corner_Join. Its open arm faces local -X at the rule's yaw (Unity mirrors the
        // FBX's X axis) and its rounded arm local -Z (the prefab's Visual child turns X180); when the other arm must
        // join, a negative X scale mirrors the piece and the yaw turns on by 90 degrees so the outer arc stays put.
        private void AddCorner(Vector2Int position, int index, Vector3 scale, AtlasColor tint,
            List<EnvironmentPiecePlacement> output)
        {
            int yaw = m_boundary.GetWallYaw(index);
            Vector2Int join = m_boundary.GetCornerJoin(index);
            Vector3 center = GetQuadrantCenter(position);
            if (join != Vector2Int.zero)
            {
                GetYawAxes(yaw, out int cos, out int sin);
                if (join == new Vector2Int(-cos, sin))
                {
                    output.Add(new EnvironmentPiecePlacement(m_database.CornerJoinPiece, center, yaw, scale, tint));
                    return;
                }

                if (join == new Vector2Int(-sin, -cos))
                {
                    output.Add(new EnvironmentPiecePlacement(m_database.CornerJoinPiece, center, (yaw + 90) % 360,
                        new Vector3(-scale.x, scale.y, scale.z), tint));
                    return;
                }
            }

            output.Add(new EnvironmentPiecePlacement(m_database.CornerEndPiece, center, yaw, scale, tint));
        }

        private static void GetYawAxes(int yaw, out int cos, out int sin)
        {
            switch (yaw)
            {
                case 90: cos = 0; sin = 1; return;
                case 180: cos = -1; sin = 0; return;
                case 270: cos = 0; sin = -1; return;
                default: cos = 1; sin = 0; return;
            }
        }

        // One brick over the whole run through position: Brick_End on its first and last quadrants with
        // their mouths turned inward, and the rule's own wall piece on every quadrant between them. An end that a
        // merged corner joins keeps the plain wall piece instead of a cap.
        private void AddBrick(Vector2Int position, Vector2Int axis, Vector3 scale, AtlasColor tint,
            List<EnvironmentPiecePlacement> output)
        {
            int index = GetQuadrantIndex(position);
            int rule = m_boundary.GetWallRule(index);
            int yaw = m_boundary.GetWallYaw(index);
            Vector2Int first = position;
            while (m_boundary.IsSameRun(first - axis, axis, rule, yaw))
            {
                first -= axis;
            }

            int length = 0;
            for (Vector2Int next = first; m_boundary.IsSameRun(next, axis, rule, yaw); next += axis)
            {
                m_boundary.SetUsed(GetQuadrantIndex(next));
                length++;
            }

            BoardPieceView end = m_database.BrickEndPiece;
            BoardPieceView wall = m_matcher.GetOutput(rule);
            Vector2Int last = first + axis * (length - 1);
            bool firstJoined = m_boundary.IsJoinedCorner(first - axis, axis);
            bool lastJoined = m_boundary.IsJoinedCorner(last + axis, -axis);
            if (length == 1 && (firstJoined || lastJoined))
            {
                // One joined end: a full cap whose open mouth meets the corner; both joined: a plain wall piece.
                BoardPieceView piece = firstJoined && lastJoined ? wall : end;
                int pieceYaw = firstJoined && lastJoined ? yaw : GetBrickEndYaw(firstJoined ? -axis : axis);
                output.Add(new EnvironmentPiecePlacement(piece, GetQuadrantCenter(first), pieceYaw, scale, tint));
                return;
            }

            if (length == 1)
            {
                // Two half-length ends back to back keep a one-quadrant run closed and rounded at both ends.
                Vector3 halfScale = new Vector3(scale.x, scale.y, scale.z * 0.5f);
                Vector3 center = GetQuadrantCenter(first);
                Vector3 offset = new Vector3(axis.x, axis.y, 0f) * k_halfEndOffset;
                output.Add(new EnvironmentPiecePlacement(end, center - offset, GetBrickEndYaw(axis), halfScale, tint));
                output.Add(new EnvironmentPiecePlacement(end, center + offset, GetBrickEndYaw(-axis), halfScale, tint));
                return;
            }

            output.Add(firstJoined
                ? new EnvironmentPiecePlacement(wall, GetQuadrantCenter(first), yaw, scale, tint)
                : new EnvironmentPiecePlacement(end, GetQuadrantCenter(first), GetBrickEndYaw(axis), scale, tint));
            for (int step = 1; step < length - 1; step++)
            {
                output.Add(new EnvironmentPiecePlacement(wall, GetQuadrantCenter(first + axis * step), yaw, scale, tint));
            }

            output.Add(lastJoined
                ? new EnvironmentPiecePlacement(wall, GetQuadrantCenter(last), yaw, scale, tint)
                : new EnvironmentPiecePlacement(end, GetQuadrantCenter(last), GetBrickEndYaw(-axis), scale, tint));
        }

        // Brick_End keeps Wall's mesh space: at yaw 0 its open mouth faces local -Z (grid -y) and its
        // rounded end +Z. The view turns a piece clockwise seen from above by its yaw.
        private static int GetBrickEndYaw(Vector2Int mouth)
        {
            if (mouth.y < 0) return 0;
            if (mouth.x < 0) return 90;
            if (mouth.y > 0) return 180;
            return 270;
        }

        // Slots arrive grouped by door in authored order (BoardLayout), so each door's slots and arrow
        // are contiguous in output: one plain door piece per slot, as Compose's rule placements need.
        private void AddPlainDoors(IBoardLayout layout, List<EnvironmentPiecePlacement> output)
        {
            Vector3 scale = m_database.DoorPieceScale;
            int firstSlot = 0;
            for (int index = 0; index < layout.DoorSlotCount; index++)
            {
                DoorSlot slot = layout.GetDoorSlotAt(index);
                bool isLastSegment = index + 1 == layout.DoorSlotCount
                    || layout.GetDoorSlotAt(index + 1).DoorIndex != slot.DoorIndex;
                AtlasColor color = m_database.GetDoorColor(slot.Color);
                output.Add(new EnvironmentPiecePlacement(m_database.DoorPiece, GetDoorCenter(slot), slot.Yaw, scale, color));
                if (isLastSegment)
                {
                    AddDoorArrows(layout.GetDoorSlotAt(firstSlot), slot, output);
                    firstSlot = index + 1;
                }
            }
        }

        // Slots arrive grouped by door in authored order (BoardLayout), so each door's slots and arrow
        // are contiguous in output and its gate span names them by range. A gate is closed like a brick:
        // Door_End at its first and last slot with the rounded end facing out of the span, and two
        // half-length ends back to back on a one-slot gate.
        private void AddGateDoors(IBoardLayout layout, List<EnvironmentPiecePlacement> output, List<EnvironmentGateSpan> gates)
        {
            Vector3 scale = m_database.DoorPieceScale;
            int firstSlot = 0;
            int firstPlacement = output.Count;
            for (int index = 0; index < layout.DoorSlotCount; index++)
            {
                DoorSlot slot = layout.GetDoorSlotAt(index);
                bool isLastSegment = index + 1 == layout.DoorSlotCount
                    || layout.GetDoorSlotAt(index + 1).DoorIndex != slot.DoorIndex;
                AtlasColor color = m_database.GetDoorColor(slot.Color);
                AddGatePiece(layout, firstSlot, index, isLastSegment, scale, color, output);
                if (isLastSegment)
                {
                    AddDoorArrows(layout.GetDoorSlotAt(firstSlot), slot, output);
                    gates.Add(new EnvironmentGateSpan(slot.DoorIndex, firstPlacement, output.Count - firstPlacement));
                    firstSlot = index + 1;
                    firstPlacement = output.Count;
                }
            }
        }

        // One slot of a play-area gate: Door_End on the span's first and last slot, Door_1x1 between them.
        private void AddGatePiece(IBoardLayout layout, int firstSlot, int index, bool isLastSegment, Vector3 scale,
            AtlasColor color, List<EnvironmentPiecePlacement> output)
        {
            DoorSlot slot = layout.GetDoorSlotAt(index);
            Vector3 center = GetDoorCenter(slot);
            // A slot spans two quadrants along its side: grid x on the top and bottom sides, grid y on the others.
            Vector2Int along = slot.Yaw == 0 ? Vector2Int.right : Vector2Int.up;
            BoardPieceView end = m_database.DoorEndPiece;
            bool isFirstSegment = index == firstSlot;
            if (isFirstSegment && isLastSegment)
            {
                Vector3 halfScale = new Vector3(scale.x * 0.5f, scale.y, scale.z);
                Vector3 offset = new Vector3(along.x, along.y, 0f) * k_halfDoorOffset;
                output.Add(new EnvironmentPiecePlacement(end, center - offset, GetDoorEndYaw(-along), halfScale, color));
                output.Add(new EnvironmentPiecePlacement(end, center + offset, GetDoorEndYaw(along), halfScale, color));
                return;
            }

            if (!isFirstSegment && !isLastSegment)
            {
                output.Add(new EnvironmentPiecePlacement(m_database.DoorPiece, center, slot.Yaw, scale, color));
                return;
            }

            // The rounded end faces away from the rest of the span.
            DoorSlot other = layout.GetDoorSlotAt(isFirstSegment ? index + 1 : index - 1);
            Vector3 inward = GetDoorCenter(other) - center;
            Vector2Int outward = Vector3.Dot(inward, new Vector3(along.x, along.y, 0f)) > 0f ? -along : along;
            output.Add(new EnvironmentPiecePlacement(end, center, GetDoorEndYaw(outward), scale, color));
        }

        // Door_End keeps Door_1x1's mesh space. Unity mirrors the FBX's X axis on import, so at yaw 0 its rounded
        // end faces local +X (grid +x) and its open mouth -X. The view turns a piece clockwise seen from above by
        // its yaw.
        private static int GetDoorEndYaw(Vector2Int rounded)
        {
            if (rounded.x > 0) return 0;
            if (rounded.y < 0) return 90;
            if (rounded.x < 0) return 180;
            return 270;
        }

        /// <summary>
        /// Places one arrow marker per door at the middle of its span, pointing out
        /// of the board, as every door in the reference levels shows a single arrow.
        /// first and last are the door's first and last edge segments.
        /// </summary>
        private void AddDoorArrows(DoorSlot first, DoorSlot last, List<EnvironmentPiecePlacement> output)
        {
            Vector3 center = (GetDoorCenter(first) + GetDoorCenter(last)) * 0.5f;
            output.Add(new EnvironmentPiecePlacement(
                m_database.DoorArrowPiece, center, GetArrowYaw(first.Side), m_database.DoorArrowScale,
                m_database.GetDoorArrowColor(first.Color)));
        }

        // Door_Arrow points toward local -Z at yaw 0 after its X180 calibration.
        private static int GetArrowYaw(BoardSide side)
        {
            switch (side)
            {
                case BoardSide.Top: return 180;
                case BoardSide.Left: return 90;
                case BoardSide.Right: return 270;
                default: return 0;
            }
        }

        // The two ring quadrants facing a door slot's floor cell hold the door instead of a wall.
        private static void GetDoorQuadrants(DoorSlot slot, out Vector2Int first, out Vector2Int second)
        {
            Vector2Int outward = DoorSlot.GetOutward(slot.Side);
            Vector2Int ring = slot.Cell + outward;
            if (outward.x == 0)
            {
                int y = 2 * ring.y - 1 + (outward.y > 0 ? 0 : 1);
                first = new Vector2Int(2 * ring.x - 1, y);
                second = new Vector2Int(2 * ring.x, y);
            }
            else
            {
                int x = 2 * ring.x - 1 + (outward.x > 0 ? 0 : 1);
                first = new Vector2Int(x, 2 * ring.y - 1);
                second = new Vector2Int(x, 2 * ring.y);
            }
        }

        private static Vector3 GetDoorCenter(DoorSlot slot)
        {
            GetDoorQuadrants(slot, out Vector2Int first, out Vector2Int second);
            return (GetQuadrantCenter(first) + GetQuadrantCenter(second)) * 0.5f;
        }

        // Every quadrant from the ring cell before the board to the ring cell after it. Shared with
        // EnvironmentBarrierComposer, so both state the ring width once and compose on the same grid.
        internal static RectInt GetQuadrantBounds(IBoardLayout layout)
        {
            return new RectInt(
                -2 * RingCells - 1, -2 * RingCells - 1,
                2 * (layout.Width + 2 * RingCells), 2 * (layout.Height + 2 * RingCells));
        }

        // Shared with EnvironmentBarrierComposer, so both compose on the same quadrant grid.
        internal static Vector2Int GetQuadrant(int x, int y, int quadrant)
        {
            return new Vector2Int(2 * x - 1 + (quadrant & 1), 2 * y - 1 + (quadrant >> 1));
        }

        internal static Vector3 GetQuadrantCenter(Vector2Int quadrant)
        {
            return new Vector3(quadrant.x + 0.5f, quadrant.y + 0.5f, 0f);
        }

        private int GetQuadrantIndex(Vector2Int quadrant)
        {
            return GetQuadrantIndex(m_quadrantBounds, quadrant);
        }

        // Shared with PlayAreaBoundary, so both index one level's quadrants the same way.
        internal static int GetQuadrantIndex(RectInt bounds, Vector2Int quadrant)
        {
            return (quadrant.y - bounds.yMin) * bounds.width + quadrant.x - bounds.xMin;
        }
    }
}
