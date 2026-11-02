using UnityEngine;

namespace ColorBlockJam.Runtime.Boosters
{
    /// <summary>
    /// Explicit authoring registry for booster definitions. It stores no stock,
    /// purchased state, active effect, or level data.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BoosterCatalog",
        menuName = "Color Block Jam/Boosters/Catalog")]
    public sealed class BoosterCatalog : ScriptableObject
    {
        [SerializeField] private BoosterDefinition[] m_definitions;

        public int Count => m_definitions?.Length ?? 0;

        public BoosterDefinition GetAt(int index)
        {
            return m_definitions[index];
        }

        public bool TryGet(BoosterId id, out BoosterDefinition definition)
        {
            if (m_definitions != null)
            {
                for (int index = 0; index < m_definitions.Length; index++)
                {
                    BoosterDefinition candidate = m_definitions[index];

                    if (candidate != null && candidate.Id == id)
                    {
                        definition = candidate;
                        return true;
                    }
                }
            }

            definition = null;
            return false;
        }

        public bool Validate(out string error)
        {
            if (m_definitions == null || m_definitions.Length == 0)
            {
                error = "Booster catalog has no definitions.";
                return false;
            }

            for (int index = 0; index < m_definitions.Length; index++)
            {
                BoosterDefinition definition = m_definitions[index];

                if (definition == null)
                {
                    error = $"Booster catalog entry {index} is empty.";
                    return false;
                }

                if (!definition.Validate(out error))
                {
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (m_definitions[previous].Id == definition.Id)
                    {
                        error = $"Booster ID {definition.Id} is registered twice.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
