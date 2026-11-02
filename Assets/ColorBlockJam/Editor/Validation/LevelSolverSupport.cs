using System;
using ColorBlockJam.Runtime.Levels;

namespace ColorBlockJam.Editor.Levels.Validation
{
    /// <summary>
    /// The solver support table, keyed by effect type id (contract D4). The check never reports Solvable for
    /// rules it ignores (DEC-MECH-12), so a level using any effect outside this table is Not checked. A
    /// mechanic joins the table only together with its solver hook.
    /// </summary>
    internal static class LevelSolverSupport
    {
        // Arrow (BLK-01): the block slides only along its axis and exits only through a side that axis points to.
        // Ice (BLK-03): the search holds a frozen block while max(0, clearsToThaw - exits of other blocks) > 0.
        private static readonly string[] s_supportedTypes = { "arrow", "ice" };

        public static bool IsSupported(string typeId)
        {
            for (int i = 0; i < s_supportedTypes.Length; i++)
            {
                if (string.Equals(s_supportedTypes[i], typeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Finds the first effect the check cannot model, blocks first, then doors, then board objects, and
        /// names its mechanic in designer words. A board object always carries its type in its one effect.
        /// </summary>
        public static bool TryFindUnsupported(LevelData level, out string mechanicName)
        {
            for (int b = 0; b < level.BlockCount; b++)
            {
                LevelBlockData block = level.GetBlockAt(b);
                for (int e = 0; block != null && e < block.EffectCount; e++)
                {
                    LevelEffectData effect = block.GetEffectAt(e);
                    if (effect != null && !IsSupported(effect.Type))
                    {
                        mechanicName = LevelDesignerText.GetMechanicName(effect.Type, LevelEffectTarget.Block);
                        return true;
                    }
                }
            }

            for (int d = 0; d < level.DoorCount; d++)
            {
                LevelDoorData door = level.GetDoorAt(d);
                for (int e = 0; door != null && e < door.EffectCount; e++)
                {
                    LevelEffectData effect = door.GetEffectAt(e);
                    if (effect != null && !IsSupported(effect.Type))
                    {
                        mechanicName = LevelDesignerText.GetMechanicName(effect.Type, LevelEffectTarget.Door);
                        return true;
                    }
                }
            }

            for (int o = 0; o < level.CellObjectCount; o++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(o);
                if (cellObject == null)
                {
                    continue;
                }

                string type = cellObject.Effect?.Type;
                if (!IsSupported(type))
                {
                    mechanicName = LevelDesignerText.GetMechanicName(type, LevelEffectTarget.CellObject);
                    return true;
                }
            }

            mechanicName = null;
            return false;
        }
    }
}
