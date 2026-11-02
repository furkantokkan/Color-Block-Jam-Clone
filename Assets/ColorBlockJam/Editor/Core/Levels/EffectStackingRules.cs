using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Which effect pairs may share one block or one gate while authoring (DEC-177). It lists allowed pairs only, so a
    /// pair nobody allowed, including every pair with a newly registered effect, cannot stack: by default each block and
    /// gate takes at most one effect. A type never stacks with itself. The Effect Constraints window edits it and the
    /// Effect Editor's Add Effect reads it.
    /// </summary>
    public sealed class EffectStackingRules : ScriptableObject
    {
        [Serializable]
        private struct AllowedPair
        {
            [SerializeField] private LevelEffectTarget m_target;
            [SerializeField] private string m_first;
            [SerializeField] private string m_second;

            // The two types are stored in ordinal order, so (a, b) and (b, a) are one pair.
            public AllowedPair(LevelEffectTarget target, string first, string second)
            {
                bool ordered = string.CompareOrdinal(first, second) <= 0;
                m_target = target;
                m_first = ordered ? first : second;
                m_second = ordered ? second : first;
            }

            public bool Matches(in AllowedPair other)
                => m_target == other.m_target && string.Equals(m_first, other.m_first, StringComparison.Ordinal)
                    && string.Equals(m_second, other.m_second, StringComparison.Ordinal);
        }

        [SerializeField] private List<AllowedPair> m_allowed = new List<AllowedPair>();

        /// <summary>True when the pair can never stack: an effect is added to a block or gate once.</summary>
        public static bool IsFixed(string first, string second) => string.Equals(first, second, StringComparison.Ordinal);

        /// <summary>Whether an effect of type <paramref name="type"/> may join every effect in <paramref name="existing"/>.</summary>
        public static bool CanJoin(EffectStackingRules rules, LevelEffectTarget target, IReadOnlyList<string> existing,
            string type)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                if (rules == null || !rules.CanStack(target, existing[i], type))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanStack(LevelEffectTarget target, string first, string second)
        {
            if (IsFixed(first, second))
            {
                return false;
            }

            AllowedPair pair = new AllowedPair(target, first, second);
            for (int i = 0; i < m_allowed.Count; i++)
            {
                if (m_allowed[i].Matches(pair))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Allows or forbids one pair; the window records Undo before calling it. A fixed pair refuses.</summary>
        public void SetCanStack(LevelEffectTarget target, string first, string second, bool canStack)
        {
            if (IsFixed(first, second))
            {
                throw new ArgumentException($"{first} and {second} can never stack.");
            }

            AllowedPair pair = new AllowedPair(target, first, second);
            for (int i = m_allowed.Count - 1; i >= 0; i--)
            {
                if (m_allowed[i].Matches(pair))
                {
                    if (canStack)
                    {
                        return;
                    }

                    m_allowed.RemoveAt(i);
                }
            }

            if (canStack)
            {
                m_allowed.Add(pair);
            }
        }

        /// <summary>Allows or forbids every pair of <paramref name="type"/> with the other types; the type itself is skipped.</summary>
        public void SetAllFor(LevelEffectTarget target, string type, IReadOnlyList<string> others, bool canStack)
        {
            for (int i = 0; i < others.Count; i++)
            {
                if (!IsFixed(type, others[i]))
                {
                    SetCanStack(target, type, others[i], canStack);
                }
            }
        }

        /// <summary>How many of <paramref name="others"/> may share a block or gate with <paramref name="type"/>.</summary>
        public int CountPartners(LevelEffectTarget target, string type, IReadOnlyList<string> others)
        {
            int count = 0;
            for (int i = 0; i < others.Count; i++)
            {
                if (CanStack(target, type, others[i]))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
