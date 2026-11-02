using System;
using System.Collections.Generic;
using ColorBlockJam.Domain.Boosters;
using ColorBlockJam.Domain.Commands;
using ColorBlockJam.Domain.Hooks;
using ColorBlockJam.Runtime.Boosters;

namespace ColorBlockJam.Gameplay.Effects
{
    /// <summary>
    /// Turns the booster restriction matrix and one attempt's effect states into one Domain gate (DEC-171), with no
    /// effect-specific code: each state that reports presence gets the matrix column named by its TypeId. Runs on Home
    /// while the attempt's hooks are built, so its lists may allocate.
    /// </summary>
    public static class BoosterRestrictionHooks
    {
        // Matrix rows and the board command each one restricts.
        private static readonly BoosterTargetKind[] s_targets =
        {
            BoosterTargetKind.Cell, BoosterTargetKind.Block, BoosterTargetKind.ColorFromBlock
        };

        private static readonly CommandKind[] s_commands =
        {
            CommandKind.RemoveCell, CommandKind.RemoveBlock, CommandKind.RemoveColor
        };

        /// <summary>Adds the gate when the matrix closes at least one cell for an effect present in the attempt.</summary>
        public static void Add(BoosterRestrictionMatrix matrix, IBlockEffectState[] states, AttemptHooksBuilder hooks)
        {
            if (states == null) throw new ArgumentNullException(nameof(states));
            if (hooks == null) throw new ArgumentNullException(nameof(hooks));
            if (matrix == null || matrix.Count == 0)
            {
                return;
            }

            List<BoosterRestriction> restrictions = new List<BoosterRestriction>();
            for (int index = 0; index < states.Length; index++)
            {
                if (!(states[index] is IBlockEffectPresenceSource source) || source.Presence == null)
                {
                    continue;
                }

                for (int row = 0; row < s_targets.Length; row++)
                {
                    if (matrix.IsRestricted(s_targets[row], states[index].TypeId))
                    {
                        restrictions.Add(new BoosterRestriction(s_commands[row], source.Presence));
                    }
                }
            }

            if (restrictions.Count > 0)
            {
                hooks.Add(new BoosterRestrictionGate(restrictions.ToArray()));
            }
        }
    }
}
