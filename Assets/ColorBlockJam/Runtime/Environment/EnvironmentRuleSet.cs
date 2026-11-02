using System;
using System.Collections.Generic;
using ColorBlockJam.Presentation.Board;
using UnityEngine;

namespace ColorBlockJam.Environment
{
    /// <summary>
    /// The single authority for the play area's wall and corner rules: an ordered
    /// list of rules, each with an output piece prefab, the Floor/Void/Any pattern
    /// of the eight quadrants around it and a Rotated flag. EnvironmentRuleMatcher
    /// evaluates it; its custom inspector edits it.
    /// </summary>
    [CreateAssetMenu(fileName = "EnvironmentRuleSet", menuName = "Color Block Jam/Environment/Rule Set")]
    public sealed class EnvironmentRuleSet : ScriptableObject
    {
        public const int NeighborCount = 8;

        [SerializeField] private List<Rule> m_rules = new List<Rule>();

        public int RuleCount => m_rules.Count;

        public Rule GetRule(int index)
        {
            return m_rules[index];
        }

        /// <summary>
        /// Grid offset of neighbour index 0-7, in the order RuleTile uses for its
        /// default 3x3 neighbourhood: the upper row left to right, the middle row
        /// left then right, and the lower row left to right. +y is the board's +Z.
        /// </summary>
        public static Vector2Int GetNeighborOffset(int index)
        {
            switch (index)
            {
                case 0: return new Vector2Int(-1, 1);
                case 1: return new Vector2Int(0, 1);
                case 2: return new Vector2Int(1, 1);
                case 3: return new Vector2Int(-1, 0);
                case 4: return new Vector2Int(1, 0);
                case 5: return new Vector2Int(-1, -1);
                case 6: return new Vector2Int(0, -1);
                case 7: return new Vector2Int(1, -1);
                default: throw new ArgumentOutOfRangeException(nameof(index), index, "A rule has eight neighbours.");
            }
        }

        public bool Validate(out string error)
        {
            if (m_rules == null || m_rules.Count == 0)
            {
                error = "The environment rule set has no rules.";
                return false;
            }

            for (int index = 0; index < m_rules.Count; index++)
            {
                if (!m_rules[index].Validate(out string ruleError))
                {
                    error = $"Environment rule {index}: {ruleError}";
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>One wall or corner rule: which piece to place and the neighbour pattern it needs.</summary>
        [Serializable]
        public sealed class Rule
        {
            [SerializeField] private BoardPieceView m_output;
            [SerializeField] private EnvironmentNeighbor[] m_neighbors = new EnvironmentNeighbor[NeighborCount];
            // Rotated rules are also tried at 90, 180 and 270 degrees. Mirroring is not
            // offered because it would flip the 3D mesh winding.
            [SerializeField] private bool m_isRotated = true;

            public BoardPieceView Output => m_output;
            public bool IsRotated => m_isRotated;

            public EnvironmentNeighbor GetNeighbor(int index)
            {
                return m_neighbors[index];
            }

            public bool Validate(out string error)
            {
                if (m_output == null)
                {
                    error = "it needs an output piece prefab.";
                    return false;
                }

                if (m_neighbors == null || m_neighbors.Length != NeighborCount)
                {
                    error = $"it needs exactly {NeighborCount} neighbours.";
                    return false;
                }

                bool hasCheck = false;
                for (int index = 0; index < NeighborCount; index++)
                {
                    EnvironmentNeighbor neighbor = m_neighbors[index];
                    if (neighbor != EnvironmentNeighbor.Any && neighbor != EnvironmentNeighbor.Floor
                        && neighbor != EnvironmentNeighbor.Void)
                    {
                        error = $"neighbour {index} has the unknown value {(int)neighbor}.";
                        return false;
                    }

                    hasCheck |= neighbor != EnvironmentNeighbor.Any;
                }

                if (!hasCheck)
                {
                    // An all-Any rule would match every void quadrant and hide every later rule.
                    error = "it needs at least one Floor or Void neighbour.";
                    return false;
                }

                error = null;
                return true;
            }
        }
    }
}
