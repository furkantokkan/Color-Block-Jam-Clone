using System;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// The fixed list of catalog effect and board-object definitions (DEC-MECH-1). It is immutable,
    /// built without reflection, and converts authored string pairs into typed parameters.
    /// </summary>
    public static class LevelEffectRegistry
    {
        // Runtime support gate (docs/Level-Data-Format.md): exactly one line per definition. When a
        // gameplay rule lands, its owner flips that one line to true. Project scope refuses any level
        // that uses a definition whose line is still false; authoring reads ignore it.
        private const bool k_arrowRuntimeImplemented = true;
        private const bool k_iceRuntimeImplemented = true;
        private const bool k_colorfulPathRuntimeImplemented = false;
        private const bool k_crateRuntimeImplemented = false;
        private const bool k_moveableCrateRuntimeImplemented = false;
        private const bool k_flowerCrateRuntimeImplemented = false;
        private const bool k_barrierRuntimeImplemented = false;
        private const bool k_colorfulCrateRuntimeImplemented = false;
        private const bool k_colorfulMovingPathRuntimeImplemented = false;
        private const bool k_ivyRuntimeImplemented = false;
        private const bool k_elevatorRuntimeImplemented = false;

        private const LevelEffectTarget k_block = LevelEffectTarget.Block;
        private const LevelEffectTarget k_object = LevelEffectTarget.CellObject;
        private const LevelCellObjectRole k_noRole = LevelCellObjectRole.None;
        private const int k_noMaximum = int.MaxValue;

        private static readonly string[] s_axes = { "Horizontal", "Vertical" };

        private static readonly LevelEffectDefinition[] s_definitions =
        {
            // Block effects (catalog section 3). Only Arrow and Ice exist, and doors carry no effect
            // type (DEC-177); a door that lists an effect fails as an unknown effect type.
            new LevelEffectDefinition("arrow", "BLK-01", k_block, k_noRole, k_arrowRuntimeImplemented,
                new[] { LevelEffectKey.Enum("axis", true, s_axes) },
                values => new ArrowEffect((LevelEffectAxis)values.GetEnumIndex("axis"))),
            new LevelEffectDefinition("ice", "BLK-03", k_block, k_noRole, k_iceRuntimeImplemented,
                new[] { LevelEffectKey.Integer("clearsToThaw", true, 1, 20) },
                values => new IceEffect(values.GetInteger("clearsToThaw", 0))),

            // Board objects (catalog section 5).
            new LevelEffectDefinition("colorful-path", "OBJ-01", k_object, LevelCellObjectRole.ColorGate,
                k_colorfulPathRuntimeImplemented,
                new[] { LevelEffectKey.Color("colorId", true) },
                values => new ColorfulPathObject(values.GetColor("colorId")),
                LevelEffectChecks.CheckPathColor),
            new LevelEffectDefinition("crate", "OBJ-02", k_object, LevelCellObjectRole.Wall,
                k_crateRuntimeImplemented,
                new[] { LevelEffectKey.Integer("clearsToOpen", true, 1, k_noMaximum) },
                values => new CrateObject(values.GetInteger("clearsToOpen", 0))),
            new LevelEffectDefinition("moveable-crate", "OBJ-03", k_object, LevelCellObjectRole.Movable,
                k_moveableCrateRuntimeImplemented,
                new[] { LevelEffectKey.Integer("gatePassesToOpen", true, 1, k_noMaximum) },
                values => new MoveableCrateObject(values.GetInteger("gatePassesToOpen", 0))),
            new LevelEffectDefinition("flower-crate", "OBJ-04", k_object, LevelCellObjectRole.Movable,
                k_flowerCrateRuntimeImplemented,
                new[] { LevelEffectKey.ReferenceList("leaves", true, LevelEffectValueKind.BlockId, 1, k_noMaximum) },
                values => new FlowerCrateObject(values.GetTexts("leaves")),
                LevelEffectChecks.CheckFlowerCrateLeaves),
            new LevelEffectDefinition("barrier", "OBJ-05", k_object, LevelCellObjectRole.WallOrFloor,
                k_barrierRuntimeImplemented,
                new[] { LevelEffectKey.Bool("startsOpen", false) },
                values => new BarrierObject(values.GetBool("startsOpen", false)),
                LevelEffectChecks.CheckBarrier),
            new LevelEffectDefinition("colorful-crate", "OBJ-06", k_object, LevelCellObjectRole.Wall,
                k_colorfulCrateRuntimeImplemented,
                new[] { LevelEffectKey.Color("colorId", true) },
                values => new ColorfulCrateObject(values.GetColor("colorId"))),
            new LevelEffectDefinition("colorful-moving-path", "OBJ-07", k_object, LevelCellObjectRole.ColorGate,
                k_colorfulMovingPathRuntimeImplemented,
                new[]
                {
                    LevelEffectKey.Color("colorId", true),
                    LevelEffectKey.Integer("platformIndex", true, 0, k_noMaximum)
                },
                values => new ColorfulMovingPathObject(values.GetColor("colorId"),
                    values.GetInteger("platformIndex", 0)),
                LevelEffectChecks.CheckMovingPath),
            new LevelEffectDefinition("ivy", "OBJ-08", k_object, LevelCellObjectRole.Wall,
                k_ivyRuntimeImplemented,
                null, values => new IvyObject()),
            new LevelEffectDefinition("elevator", "OBJ-09", k_object, LevelCellObjectRole.Floor,
                k_elevatorRuntimeImplemented,
                null, values => new ElevatorObject())
        };

        public static int DefinitionCount => s_definitions.Length;

        public static LevelEffectDefinition GetDefinitionAt(int index)
        {
            return s_definitions[index];
        }

        public static bool TryFind(string typeId, out LevelEffectDefinition definition)
        {
            for (int index = 0; index < s_definitions.Length; index++)
            {
                if (string.Equals(s_definitions[index].TypeId, typeId, StringComparison.Ordinal))
                {
                    definition = s_definitions[index];
                    return true;
                }
            }

            definition = null;
            return false;
        }

        /// <summary>Converts an effect whose type and values follow their rules; level references are not resolved.</summary>
        public static bool TryConvert(LevelEffectData effect, out LevelEffectParameters parameters)
        {
            parameters = null;
            if (effect == null || !TryFind(effect.Type, out LevelEffectDefinition definition)
                || ReadValues(definition, effect, out string[] values) != null)
            {
                return false;
            }

            parameters = definition.Convert(values);
            return parameters != null;
        }

        /// <summary>
        /// Orders an effect's values by definition key. Returns null when every key follows its rule,
        /// otherwise the message tail after "Level {id} {entity} effect {type} ".
        /// </summary>
        internal static string ReadValues(LevelEffectDefinition definition, LevelEffectData effect, out string[] values)
        {
            values = new string[definition.KeyCount];
            for (int index = 0; index < effect.ParamCount; index++)
            {
                LevelEffectParamData param = effect.GetParamAt(index);
                if (param == null || string.IsNullOrWhiteSpace(param.Key))
                {
                    return "has an empty key.";
                }

                int keyIndex = definition.IndexOfKey(param.Key);
                if (keyIndex < 0)
                {
                    return $"has unknown key {param.Key}.";
                }

                if (values[keyIndex] != null)
                {
                    return $"lists key {param.Key} twice.";
                }

                string problem = definition.GetKeyAt(keyIndex).FindProblem(param.Value);
                if (problem != null)
                {
                    return problem;
                }

                values[keyIndex] = param.Value;
            }

            for (int index = 0; index < definition.KeyCount; index++)
            {
                LevelEffectKey key = definition.GetKeyAt(index);
                if (key.IsRequired && values[index] == null)
                {
                    return $"is missing required key {key.Name}.";
                }
            }

            return null;
        }
    }
}
