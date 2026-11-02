using System;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// Validates every effect of a level against the registry: type, target, duplicates, keys, values,
    /// references and level checks, plus the separate project-scope runtime support gate.
    /// Messages start with "Level {id} {entity}" and are listed in docs/Level-Data-Format.md.
    /// </summary>
    public static class LevelEffectValidator
    {
        public static bool Validate(LevelData level, out string error)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                LevelEffectOwner owner = new LevelEffectOwner(block, null, null);
                for (int effectIndex = 0; effectIndex < block.EffectCount; effectIndex++)
                {
                    if (!ValidateEffect(level, owner, LevelEffectTarget.Block,
                            block.GetEffectAt(effectIndex), effectIndex, out error))
                    {
                        return false;
                    }
                }
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                LevelEffectOwner owner = new LevelEffectOwner(null, door, null);
                for (int effectIndex = 0; effectIndex < door.EffectCount; effectIndex++)
                {
                    if (!ValidateEffect(level, owner, LevelEffectTarget.Door,
                            door.GetEffectAt(effectIndex), effectIndex, out error))
                    {
                        return false;
                    }
                }
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                LevelEffectOwner owner = new LevelEffectOwner(null, null, cellObject);
                if (!ValidateEffect(level, owner, LevelEffectTarget.CellObject,
                        cellObject.Effect, 0, out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        /// <summary>Refuses a Validate-clean level that uses a definition whose gameplay is not implemented.</summary>
        public static bool ValidateRuntimeSupport(LevelData level, out string error)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int effectIndex = 0; effectIndex < block.EffectCount; effectIndex++)
                {
                    if (!IsImplemented(level, "block " + block.Id, block.GetEffectAt(effectIndex), out error))
                    {
                        return false;
                    }
                }
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                for (int effectIndex = 0; effectIndex < door.EffectCount; effectIndex++)
                {
                    if (!IsImplemented(level, "door " + door.Id, door.GetEffectAt(effectIndex), out error))
                    {
                        return false;
                    }
                }
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                if (!IsImplemented(level, "cell object " + cellObject.Id, cellObject.Effect, out error))
                {
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static bool IsImplemented(LevelData level, string entity, LevelEffectData effect, out string error)
        {
            if (effect != null && LevelEffectRegistry.TryFind(effect.Type, out LevelEffectDefinition definition)
                && definition.IsRuntimeImplemented)
            {
                error = null;
                return true;
            }

            error = $"Level {level.Id} {entity} effect {effect?.Type} is not implemented in gameplay yet.";
            return false;
        }

        private static bool ValidateEffect(
            LevelData level,
            LevelEffectOwner owner,
            LevelEffectTarget target,
            LevelEffectData effect,
            int effectIndex,
            out string error)
        {
            string prefix = $"Level {level.Id} {owner.EntityName}";
            if (effect == null || string.IsNullOrWhiteSpace(effect.Type))
            {
                error = $"{prefix} has an empty effect.";
                return false;
            }

            string type = effect.Type;
            if (!LevelEffectRegistry.TryFind(type, out LevelEffectDefinition definition))
            {
                error = $"{prefix} effect {type} is not a known effect type.";
                return false;
            }

            if (definition.Target != target)
            {
                error = $"{prefix} effect {type} cannot be used on a {GetTargetName(target)}.";
                return false;
            }

            for (int earlier = 0; earlier < effectIndex; earlier++)
            {
                string earlierType = owner.GetEffectAt(earlier)?.Type;
                if (string.Equals(earlierType, type, StringComparison.Ordinal))
                {
                    error = $"{prefix} lists effect {type} twice.";
                    return false;
                }
            }

            string problem = LevelEffectRegistry.ReadValues(definition, effect, out string[] values);
            if (problem == null)
            {
                problem = CheckReferences(level, definition, values);
            }

            if (problem == null)
            {
                problem = definition.Check(level, owner, effect.Parameters);
            }

            if (problem != null)
            {
                error = $"{prefix} effect {type} {problem}";
                return false;
            }

            error = null;
            return true;
        }

        private static string CheckReferences(LevelData level, LevelEffectDefinition definition, string[] values)
        {
            for (int index = 0; index < definition.KeyCount; index++)
            {
                string value = values[index];
                if (value == null)
                {
                    continue;
                }

                LevelEffectKey key = definition.GetKeyAt(index);
                if (!key.IsList)
                {
                    string problem = CheckReference(level, key, value);
                    if (problem != null)
                    {
                        return problem;
                    }

                    continue;
                }

                string[] items = LevelEffectKey.SplitItems(value);
                for (int item = 0; item < items.Length; item++)
                {
                    string problem = CheckReference(level, key, items[item]);
                    if (problem != null)
                    {
                        return problem;
                    }
                }
            }

            return null;
        }

        private static string CheckReference(LevelData level, LevelEffectKey key, string id)
        {
            switch (key.Kind)
            {
                case LevelEffectValueKind.BlockId when level.FindBlock(id) == null:
                    return $"key {key.Name} names missing block {id}.";
                case LevelEffectValueKind.DoorId when level.FindDoor(id) == null:
                    return $"key {key.Name} names missing door {id}.";
                case LevelEffectValueKind.CellObjectId when level.FindCellObject(id) == null:
                    return $"key {key.Name} names missing cell object {id}.";
                default:
                    return null;
            }
        }

        private static string GetTargetName(LevelEffectTarget target)
        {
            switch (target)
            {
                case LevelEffectTarget.Block: return "block";
                case LevelEffectTarget.Door: return "door";
                default: return "cell object";
            }
        }
    }
}
