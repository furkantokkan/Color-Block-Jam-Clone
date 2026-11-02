using System;
using ColorBlockJam.Runtime.Layout;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Reads the outer boundary of one level for EnvironmentComposer.ComposePlayArea (DEC-139): the matched
    /// wall rule and yaw of every quadrant, which void connects to the ring, which cells are one-cell voids and
    /// through which arm each ring corner beside one merges into a wall run. Its buffers only grow, so one
    /// instance serves every level load without a per-level allocation.
    /// </summary>
    internal sealed class PlayAreaBoundary
    {
        private const int k_noRule = -1;

        private readonly EnvironmentRuleMatcher m_matcher;
        // Per quadrant: the matched wall rule and yaw, and whether a brick or corner took it.
        private int[] m_wallRules = Array.Empty<int>();
        private int[] m_wallYaws = Array.Empty<int>();
        private bool[] m_usedWalls = Array.Empty<bool>();
        // Per quadrant: the arm through which an outer corner merges into a wall run (zero: none).
        private Vector2Int[] m_cornerJoins = Array.Empty<Vector2Int>();
        // Per cell of the board plus its ring: void connected to the ring (the outer boundary, notches
        // included), and the flood-fill queue that finds it.
        private bool[] m_outerVoid = Array.Empty<bool>();
        // Per cell: an outer void cell inside the board with no void cell beside it (a one-cell notch or stub),
        // whose surrounding corners merge into their walls.
        private bool[] m_singleVoid = Array.Empty<bool>();
        private int[] m_voidQueue = Array.Empty<int>();
        private RectInt m_quadrantBounds;
        private int m_width;
        private int m_height;

        /// <param name="matcher">The composer's matcher; Build reads the floor it has marked and its rule matches.</param>
        internal PlayAreaBoundary(EnvironmentRuleMatcher matcher)
        {
            m_matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        }

        /// <summary>
        /// Reads one level once the composer has reset the matcher over quadrantBounds and marked its floor;
        /// doorQuadrants holds the quadrants a gate takes instead of a wall.
        /// </summary>
        internal void Build(IBoardLayout layout, RectInt quadrantBounds, bool[] doorQuadrants)
        {
            m_quadrantBounds = quadrantBounds;
            m_width = layout.Width;
            m_height = layout.Height;
            MarkOuterVoid(layout);
            MarkSingleVoids(layout);
            RecordWallRules(layout, doorQuadrants);
            ChooseCornerJoins();
        }

        internal bool IsWall(int index)
        {
            return m_wallRules[index] != k_noRule;
        }

        internal int GetWallRule(int index)
        {
            return m_wallRules[index];
        }

        internal int GetWallYaw(int index)
        {
            return m_wallYaws[index];
        }

        internal bool IsUsed(int index)
        {
            return m_usedWalls[index];
        }

        internal void SetUsed(int index)
        {
            m_usedWalls[index] = true;
        }

        internal Vector2Int GetCornerJoin(int index)
        {
            return m_cornerJoins[index];
        }

        // Whether the corner beyond a run end merges into this run through that end.
        internal bool IsJoinedCorner(Vector2Int corner, Vector2Int towardRun)
        {
            return m_quadrantBounds.Contains(corner) && m_cornerJoins[GetQuadrantIndex(corner)] == towardRun;
        }

        // A quadrant of the outer boundary: its cell is void and connected to the ring.
        internal bool IsOuterQuadrant(Vector2Int position)
        {
            Vector2Int cell = GetCellOfQuadrant(position);
            return m_outerVoid[GetCellIndex(cell.x, cell.y)];
        }

        // The diagonal toward the floor of a quadrant with floor only diagonally (a ring corner), else zero.
        internal Vector2Int GetRingCornerFloor(Vector2Int position)
        {
            if (m_matcher.IsFloor(position.x - 1, position.y) || m_matcher.IsFloor(position.x + 1, position.y)
                || m_matcher.IsFloor(position.x, position.y - 1) || m_matcher.IsFloor(position.x, position.y + 1))
            {
                return Vector2Int.zero;
            }

            for (int dy = -1; dy <= 1; dy += 2)
            {
                for (int dx = -1; dx <= 1; dx += 2)
                {
                    if (m_matcher.IsFloor(position.x + dx, position.y + dy))
                    {
                        return new Vector2Int(dx, dy);
                    }
                }
            }

            return Vector2Int.zero;
        }

        // A corner quadrant has floor only diagonally (a ring corner) or on two adjacent sides (a notch corner).
        internal bool IsCorner(Vector2Int position)
        {
            int floorSides = CountFloorSides(position, out bool left, out bool right, out bool down, out bool up);
            return floorSides == 0 || floorSides == 2 && left != right && down != up;
        }

        // A straight wall quadrant has floor on exactly one side; its run goes across that side.
        internal bool TryGetStraightAxis(Vector2Int position, out Vector2Int axis)
        {
            int floorSides = CountFloorSides(position, out bool left, out bool right, out _, out _);
            axis = left || right ? Vector2Int.up : Vector2Int.right;
            return floorSides == 1;
        }

        internal bool IsSameRun(Vector2Int position, Vector2Int axis, int rule, int yaw)
        {
            if (!m_quadrantBounds.Contains(position))
            {
                return false;
            }

            int index = GetQuadrantIndex(position);
            return m_wallRules[index] == rule && m_wallYaws[index] == yaw && !m_usedWalls[index]
                && TryGetStraightAxis(position, out Vector2Int runAxis) && runAxis == axis;
        }

        // The floor state of the four quadrants beside position and how many of them are floor; shared by
        // IsCorner and TryGetStraightAxis, which each read the four sides differently.
        private int CountFloorSides(Vector2Int position, out bool left, out bool right, out bool down, out bool up)
        {
            left = m_matcher.IsFloor(position.x - 1, position.y);
            right = m_matcher.IsFloor(position.x + 1, position.y);
            down = m_matcher.IsFloor(position.x, position.y - 1);
            up = m_matcher.IsFloor(position.x, position.y + 1);
            return (left ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (up ? 1 : 0);
        }

        // The same quadrants and rule decisions as EnvironmentComposer's rule placements, stored so runs can be
        // grouped before placing.
        private void RecordWallRules(IBoardLayout layout, bool[] doorQuadrants)
        {
            int quadrantCount = m_quadrantBounds.width * m_quadrantBounds.height;
            if (m_wallRules.Length < quadrantCount)
            {
                m_wallRules = new int[quadrantCount];
                m_wallYaws = new int[quadrantCount];
                m_usedWalls = new bool[quadrantCount];
            }

            if (m_cornerJoins.Length < quadrantCount)
            {
                m_cornerJoins = new Vector2Int[quadrantCount];
            }

            for (int index = 0; index < quadrantCount; index++)
            {
                m_wallRules[index] = k_noRule;
                m_usedWalls[index] = false;
                m_cornerJoins[index] = Vector2Int.zero;
            }

            int ringCells = EnvironmentComposer.RingCells;
            for (int y = -ringCells; y < layout.Height + ringCells; y++)
            {
                for (int x = -ringCells; x < layout.Width + ringCells; x++)
                {
                    if (layout.IsFloor(x, y))
                    {
                        continue;
                    }

                    for (int quadrant = 0; quadrant < 4; quadrant++)
                    {
                        Vector2Int position = EnvironmentComposer.GetQuadrant(x, y, quadrant);
                        int index = GetQuadrantIndex(position);
                        if (doorQuadrants[index] || !m_matcher.TryMatch(position.x, position.y, out int rule, out int yaw))
                        {
                            continue;
                        }

                        m_wallRules[index] = rule;
                        m_wallYaws[index] = yaw;
                    }
                }
            }
        }

        // Flood fill over the void cells of the board and its one-cell ring, starting from the ring (which is
        // all void and connected around the board): reached cells are the outer boundary, the rest are holes.
        private void MarkOuterVoid(IBoardLayout layout)
        {
            int ringCells = EnvironmentComposer.RingCells;
            int width = m_width + 2 * ringCells;
            int cellCount = width * (m_height + 2 * ringCells);
            if (m_outerVoid.Length < cellCount)
            {
                m_outerVoid = new bool[cellCount];
                m_voidQueue = new int[cellCount];
            }
            else
            {
                Array.Clear(m_outerVoid, 0, cellCount);
            }

            // The ring cell below the board's first cell is void and on the ring.
            int count = 0;
            int start = GetCellIndex(0, -ringCells);
            m_outerVoid[start] = true;
            m_voidQueue[count++] = start;
            for (int next = 0; next < count; next++)
            {
                int x = m_voidQueue[next] % width - ringCells;
                int y = m_voidQueue[next] / width - ringCells;
                count = EnqueueVoid(layout, x - 1, y, count);
                count = EnqueueVoid(layout, x + 1, y, count);
                count = EnqueueVoid(layout, x, y - 1, count);
                count = EnqueueVoid(layout, x, y + 1, count);
            }
        }

        // A one-cell notch or stub: an outer void cell inside the board whose neighbours inside the board are floor.
        private void MarkSingleVoids(IBoardLayout layout)
        {
            int ringCells = EnvironmentComposer.RingCells;
            int cellCount = (m_width + 2 * ringCells) * (m_height + 2 * ringCells);
            if (m_singleVoid.Length < cellCount)
            {
                m_singleVoid = new bool[cellCount];
            }
            else
            {
                Array.Clear(m_singleVoid, 0, cellCount);
            }

            for (int y = 0; y < m_height; y++)
            {
                for (int x = 0; x < m_width; x++)
                {
                    int index = GetCellIndex(x, y);
                    m_singleVoid[index] = !layout.IsFloor(x, y) && m_outerVoid[index]
                        && !IsVoidInBoard(layout, x - 1, y) && !IsVoidInBoard(layout, x + 1, y)
                        && !IsVoidInBoard(layout, x, y - 1) && !IsVoidInBoard(layout, x, y + 1);
                }
            }
        }

        private static bool IsVoidInBoard(IBoardLayout layout, int x, int y)
        {
            return x >= 0 && y >= 0 && x < layout.Width && y < layout.Height && !layout.IsFloor(x, y);
        }

        // Whether a quadrant's cell is a one-cell void or borders one.
        private bool IsBesideSingleVoid(Vector2Int position)
        {
            Vector2Int cell = GetCellOfQuadrant(position);
            int x = cell.x;
            int y = cell.y;
            return IsSingleVoid(x, y) || IsSingleVoid(x - 1, y) || IsSingleVoid(x + 1, y)
                || IsSingleVoid(x, y - 1) || IsSingleVoid(x, y + 1);
        }

        private bool IsSingleVoid(int x, int y)
        {
            return x >= 0 && y >= 0 && x < m_width && y < m_height && m_singleVoid[GetCellIndex(x, y)];
        }

        private int EnqueueVoid(IBoardLayout layout, int x, int y, int count)
        {
            int ringCells = EnvironmentComposer.RingCells;
            if (x < -ringCells || y < -ringCells || x >= m_width + ringCells
                || y >= m_height + ringCells || layout.IsFloor(x, y))
            {
                return count;
            }

            int index = GetCellIndex(x, y);
            if (m_outerVoid[index])
            {
                return count;
            }

            m_outerVoid[index] = true;
            m_voidQueue[count] = index;
            return count + 1;
        }

        // Each ring corner (floor only diagonally) at a one-cell void merges into the shorter of the straight runs
        // that continue its two arms, so no unit there is shorter than one cell; inner corners, other corners and
        // a corner with no such run keep both arms rounded.
        private void ChooseCornerJoins()
        {
            for (int y = m_quadrantBounds.yMin; y < m_quadrantBounds.yMax; y++)
            {
                for (int x = m_quadrantBounds.xMin; x < m_quadrantBounds.xMax; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    int index = GetQuadrantIndex(position);
                    if (m_wallRules[index] == k_noRule || !IsOuterQuadrant(position)
                        || GetRingCornerFloor(position) == Vector2Int.zero || !IsBesideSingleVoid(position)
                        || !TryGetCornerArms(position, out Vector2Int first, out Vector2Int second))
                    {
                        continue;
                    }

                    int firstRun = GetArmRunLength(position, first);
                    int secondRun = GetArmRunLength(position, second);
                    if (firstRun > 0 && (secondRun == 0 || firstRun <= secondRun))
                    {
                        m_cornerJoins[index] = first;
                    }
                    else if (secondRun > 0)
                    {
                        m_cornerJoins[index] = second;
                    }
                }
            }
        }

        // The two directions in which a corner's walls continue: toward the floor's side on each axis for a
        // ring corner (floor only diagonally); away from the two floor sides for a notch corner.
        private bool TryGetCornerArms(Vector2Int position, out Vector2Int first, out Vector2Int second)
        {
            first = Vector2Int.zero;
            second = Vector2Int.zero;
            if (!IsCorner(position))
            {
                return false;
            }

            Vector2Int ring = GetRingCornerFloor(position);
            if (ring != Vector2Int.zero)
            {
                first = new Vector2Int(ring.x, 0);
                second = new Vector2Int(0, ring.y);
                return true;
            }

            int toFloorX = (m_matcher.IsFloor(position.x + 1, position.y) ? 1 : 0) - (m_matcher.IsFloor(position.x - 1, position.y) ? 1 : 0);
            int toFloorY = (m_matcher.IsFloor(position.x, position.y + 1) ? 1 : 0) - (m_matcher.IsFloor(position.x, position.y - 1) ? 1 : 0);
            first = new Vector2Int(-toFloorX, 0);
            second = new Vector2Int(0, -toFloorY);
            return first != Vector2Int.zero && second != Vector2Int.zero;
        }

        // How many quadrants the outer straight run continuing a corner's arm has (zero when a corner, a gate or
        // nothing continues it).
        private int GetArmRunLength(Vector2Int corner, Vector2Int arm)
        {
            Vector2Int next = corner + arm;
            if (!m_quadrantBounds.Contains(next))
            {
                return 0;
            }

            int index = GetQuadrantIndex(next);
            if (m_wallRules[index] == k_noRule || !IsOuterQuadrant(next)
                || !TryGetStraightAxis(next, out Vector2Int axis) || axis != arm && axis != -arm)
            {
                return 0;
            }

            int rule = m_wallRules[index];
            int yaw = m_wallYaws[index];
            int length = 0;
            for (Vector2Int step = next; IsSameRun(step, axis, rule, yaw); step += arm)
            {
                length++;
            }

            return length;
        }

        private int GetCellIndex(int x, int y)
        {
            int ringCells = EnvironmentComposer.RingCells;
            return (y + ringCells) * (m_width + 2 * ringCells) + x + ringCells;
        }

        // Quadrant (2x - 1 | 2x, 2y - 1 | 2y) belongs to cell (x, y) (see EnvironmentComposer.GetQuadrant).
        private static Vector2Int GetCellOfQuadrant(Vector2Int quadrant)
        {
            return new Vector2Int((quadrant.x + 1) >> 1, (quadrant.y + 1) >> 1);
        }

        private int GetQuadrantIndex(Vector2Int quadrant)
        {
            return EnvironmentComposer.GetQuadrantIndex(m_quadrantBounds, quadrant);
        }
    }
}
