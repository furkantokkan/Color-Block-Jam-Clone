using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Currencies
{
    /// <summary>
    /// Ordered registry of every currency the game knows; the order is the Home top-bar order. It stores no
    /// balance. Adding a currency means adding its asset here, with no code change (design 3.3).
    /// </summary>
    [CreateAssetMenu(
        fileName = "CurrencyCatalog",
        menuName = "Color Block Jam/Currencies/Catalog")]
    public sealed class CurrencyCatalog : ScriptableObject
    {
        [SerializeField] private CurrencyDefinition[] m_definitions;

        public int Count => m_definitions?.Length ?? 0;

        public CurrencyDefinition GetAt(int index)
        {
            return m_definitions[index];
        }

        public bool TryGet(string id, out CurrencyDefinition definition)
        {
            if (m_definitions != null && id != null)
            {
                for (int index = 0; index < m_definitions.Length; index++)
                {
                    CurrencyDefinition candidate = m_definitions[index];
                    if (candidate != null && string.Equals(candidate.Id, id, StringComparison.Ordinal))
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
                error = "The currency catalog lists no currencies. Add at least one currency definition.";
                return false;
            }

            for (int index = 0; index < m_definitions.Length; index++)
            {
                CurrencyDefinition definition = m_definitions[index];
                if (definition == null)
                {
                    error = $"Currency catalog entry {index + 1} is empty. Assign a currency definition or remove the entry.";
                    return false;
                }

                if (!definition.Validate(out error))
                {
                    return false;
                }

                for (int previous = 0; previous < index; previous++)
                {
                    if (string.Equals(m_definitions[previous].Id, definition.Id, StringComparison.Ordinal))
                    {
                        error = $"Currency ID '{definition.Id}' is used by both '{m_definitions[previous].DisplayName}' "
                                + $"and '{definition.DisplayName}'. Every currency needs its own ID.";
                        return false;
                    }
                }
            }

            error = null;
            return true;
        }
    }
}
