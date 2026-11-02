using System;
using ColorBlockJam.Domain.Board;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;

namespace ColorBlockJam.Domain.Boosters
{
    /// <summary>One closed cell of the booster restriction matrix: the command kind a block with the effect refuses.</summary>
    public readonly struct BoosterRestriction
    {
        public BoosterRestriction(CommandKind kind, IBlockEffectPresence effect)
        {
            if (kind != CommandKind.RemoveCell && kind != CommandKind.RemoveBlock && kind != CommandKind.RemoveColor)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), "Only board booster commands can be restricted.");
            }

            Kind = kind;
            Effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public CommandKind Kind { get; }

        public IBlockEffectPresence Effect { get; }
    }

    /// <summary>
    /// Refuses a board booster on a block that carries a restricted effect right now (DEC-171). The restrictions are
    /// copied once on Home and checked with a plain loop, so a call allocates nothing; the effect's own state decides
    /// presence, so a thawed Ice block becomes a target again.
    /// </summary>
    public sealed class BoosterRestrictionGate : IBoosterTargetGate
    {
        private readonly BoosterRestriction[] m_restrictions;

        public BoosterRestrictionGate(BoosterRestriction[] restrictions)
        {
            if (restrictions == null)
            {
                throw new ArgumentNullException(nameof(restrictions));
            }

            m_restrictions = (BoosterRestriction[])restrictions.Clone();
        }

        public int Count => m_restrictions.Length;

        public bool AllowsBoosterTarget(IBoardReader board, CommandKind kind, int block)
        {
            for (int i = 0; i < m_restrictions.Length; i++)
            {
                if (m_restrictions[i].Kind == kind && m_restrictions[i].Effect.HasEffect(board, block))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
