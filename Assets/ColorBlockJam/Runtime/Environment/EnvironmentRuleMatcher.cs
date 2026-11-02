using System;
using ColorBlockJam.Presentation.Board;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// Evaluates the environment rule set against a reusable floor mask on the
    /// quadrant grid. It keeps RuleTile's semantics: rules in order, angles 0, 90,
    /// 180 and 270 in order, and a clockwise turn of each neighbour offset. The
    /// composer owns one matcher and resets its mask on every compose.
    /// </summary>
    public sealed class EnvironmentRuleMatcher
    {
        public const int RotationStep = 90;

        private readonly EnvironmentRuleSet m_rules;
        private bool[] m_floor = Array.Empty<bool>();
        private RectInt m_bounds;

        public EnvironmentRuleMatcher(EnvironmentRuleSet rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            if (!rules.Validate(out string error)) throw new ArgumentException(error, nameof(rules));

            m_rules = rules;
        }

        public int RuleCount => m_rules.RuleCount;

        public BoardPieceView GetOutput(int ruleIndex)
        {
            return m_rules.GetRule(ruleIndex).Output;
        }

        /// <summary>Marks every quadrant in bounds as void. The mask grows here only.</summary>
        public void Reset(RectInt bounds)
        {
            if (bounds.width <= 0 || bounds.height <= 0) throw new ArgumentOutOfRangeException(nameof(bounds));

            int count = bounds.width * bounds.height;
            if (m_floor.Length < count)
            {
                m_floor = new bool[count];
            }
            else
            {
                Array.Clear(m_floor, 0, count);
            }

            m_bounds = bounds;
        }

        public void SetFloor(int x, int y)
        {
            if (!m_bounds.Contains(new Vector2Int(x, y)))
            {
                throw new ArgumentOutOfRangeException(nameof(x), $"Quadrant {x},{y} is outside the mask.");
            }

            m_floor[GetIndex(x, y)] = true;
        }

        /// <summary>A quadrant outside the mask is void, as an empty tile was for RuleTile.</summary>
        public bool IsFloor(int x, int y)
        {
            return m_bounds.Contains(new Vector2Int(x, y)) && m_floor[GetIndex(x, y)];
        }

        /// <summary>
        /// Finds the first rule and angle that match the quadrant. The rule set turns a
        /// rule clockwise on the grid, which is Unity's positive yaw on the XZ board, so
        /// the angle is the piece yaw directly.
        /// </summary>
        public bool TryMatch(int x, int y, out int ruleIndex, out int angle)
        {
            for (ruleIndex = 0; ruleIndex < m_rules.RuleCount; ruleIndex++)
            {
                int angleEnd = m_rules.GetRule(ruleIndex).IsRotated ? 360 : RotationStep;
                for (angle = 0; angle < angleEnd; angle += RotationStep)
                {
                    if (IsMatch(ruleIndex, x, y, angle))
                    {
                        return true;
                    }
                }
            }

            ruleIndex = -1;
            angle = 0;
            return false;
        }

        public bool IsMatch(int ruleIndex, int x, int y, int angle)
        {
            EnvironmentRuleSet.Rule rule = m_rules.GetRule(ruleIndex);
            for (int index = 0; index < EnvironmentRuleSet.NeighborCount; index++)
            {
                EnvironmentNeighbor neighbor = rule.GetNeighbor(index);
                if (neighbor == EnvironmentNeighbor.Any)
                {
                    continue;
                }

                Vector2Int offset = GetRotatedOffset(EnvironmentRuleSet.GetNeighborOffset(index), angle);
                bool isFloor = IsFloor(x + offset.x, y + offset.y);
                if (isFloor != (neighbor == EnvironmentNeighbor.Floor))
                {
                    return false;
                }
            }

            return true;
        }

        // Same turn as RuleTile.GetRotatedPosition on a rectangular grid.
        private static Vector2Int GetRotatedOffset(Vector2Int offset, int angle)
        {
            switch (angle)
            {
                case 0: return offset;
                case 90: return new Vector2Int(offset.y, -offset.x);
                case 180: return new Vector2Int(-offset.x, -offset.y);
                case 270: return new Vector2Int(-offset.y, offset.x);
                default: throw new ArgumentOutOfRangeException(nameof(angle), angle, "Angles are multiples of 90.");
            }
        }

        private int GetIndex(int x, int y)
        {
            return (y - m_bounds.yMin) * m_bounds.width + x - m_bounds.xMin;
        }
    }
}
