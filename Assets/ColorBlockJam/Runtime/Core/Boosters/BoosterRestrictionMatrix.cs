using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Runtime.Boosters
{
    /// <summary>
    /// Which board boosters a block effect keeps off its block (DEC-171): rows are booster target kinds, columns are
    /// block effect type IDs, and each listed pair is a closed matrix cell, such as Rocket (Cell) on Ice. Rows use
    /// the target kind, not the booster ID, because the starting and the in-game Rocket are one RemoveCell command.
    /// The Game scene reads it once per attempt on Home; nothing reads it during Playing.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BoosterRestrictionMatrix",
        menuName = "Color Block Jam/Boosters/Restriction Matrix")]
    public sealed class BoosterRestrictionMatrix : ScriptableObject
    {
        [Serializable]
        private struct Restriction
        {
            [SerializeField] private BoosterTargetKind m_target;
            [SerializeField] private string m_effectTypeId;

            public Restriction(BoosterTargetKind target, string effectTypeId)
            {
                m_target = target;
                m_effectTypeId = effectTypeId;
            }

            public BoosterTargetKind Target => m_target;
            public string EffectTypeId => m_effectTypeId;

            public bool Matches(BoosterTargetKind target, string effectTypeId)
                => m_target == target && string.Equals(m_effectTypeId, effectTypeId, StringComparison.Ordinal);
        }

        [SerializeField] private List<Restriction> m_restricted = new List<Restriction>();

        /// <summary>The booster target kinds a matrix row can name: Rocket, Hammer and Vacuum.</summary>
        public static bool IsRestrictableTarget(BoosterTargetKind target)
            => target == BoosterTargetKind.Cell || target == BoosterTargetKind.Block
                || target == BoosterTargetKind.ColorFromBlock;

        /// <summary>A matrix column must be a block effect of the level format (LevelEffectRegistry).</summary>
        public static bool IsRestrictableEffect(string effectTypeId)
            => LevelEffectRegistry.TryFind(effectTypeId, out LevelEffectDefinition definition)
                && definition.Target == LevelEffectTarget.Block;

        public int Count => m_restricted.Count;

        public BoosterTargetKind GetTargetAt(int index) => m_restricted[index].Target;

        public string GetEffectTypeIdAt(int index) => m_restricted[index].EffectTypeId;

        public bool IsRestricted(BoosterTargetKind target, string effectTypeId)
        {
            for (int i = 0; i < m_restricted.Count; i++)
            {
                if (m_restricted[i].Matches(target, effectTypeId))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Closes or opens one matrix cell; the editor records Undo before calling it.</summary>
        public void SetRestricted(BoosterTargetKind target, string effectTypeId, bool restricted)
        {
            if (!IsRestrictableTarget(target))
            {
                throw new ArgumentOutOfRangeException(nameof(target), "Only Rocket, Hammer and Vacuum rows can be restricted.");
            }

            if (!IsRestrictableEffect(effectTypeId))
            {
                throw new ArgumentException($"'{effectTypeId}' is not a block effect.", nameof(effectTypeId));
            }

            for (int i = m_restricted.Count - 1; i >= 0; i--)
            {
                if (m_restricted[i].Matches(target, effectTypeId))
                {
                    if (restricted)
                    {
                        return;
                    }

                    m_restricted.RemoveAt(i);
                }
            }

            if (restricted)
            {
                m_restricted.Add(new Restriction(target, effectTypeId));
            }
        }

        /// <summary>Every row is Rocket, Hammer or Vacuum, every column a block effect, and no cell is listed twice.</summary>
        public bool Validate(out string error)
        {
            for (int i = 0; i < m_restricted.Count; i++)
            {
                Restriction restriction = m_restricted[i];
                if (!IsRestrictableTarget(restriction.Target))
                {
                    error = $"Restriction {i + 1} names target kind {restriction.Target}; use Cell (Rocket), Block (Hammer) or Color From Block (Vacuum).";
                    return false;
                }

                if (!IsRestrictableEffect(restriction.EffectTypeId))
                {
                    error = $"Restriction {i + 1} names '{restriction.EffectTypeId}', which is not a block effect.";
                    return false;
                }

                for (int j = 0; j < i; j++)
                {
                    if (m_restricted[j].Matches(restriction.Target, restriction.EffectTypeId))
                    {
                        error = $"{restriction.Target} on '{restriction.EffectTypeId}' is listed twice.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
