using System;
using UnityEngine;

namespace ColorBlockJam.Runtime.Levels
{
    /// <summary>
    /// One authored effect parameter: a key and its string value, exactly as the level JSON stores it.
    /// </summary>
    [Serializable]
    public sealed class LevelEffectParamData
    {
        [SerializeField] private string m_key;
        [SerializeField] private string m_value;

        public string Key => m_key;
        public string Value => m_value;

        public LevelEffectParamData()
        {
        }

        public LevelEffectParamData(string key, string value)
        {
            m_key = key;
            m_value = value;
        }
    }

    /// <summary>
    /// One authored effect (DEC-MECH-1): a catalog type id plus string parameter pairs. The typed,
    /// immutable parameters are converted from those pairs on first use and never change afterwards.
    /// </summary>
    [Serializable]
    public sealed class LevelEffectData : ISerializationCallbackReceiver
    {
        [SerializeField] private string m_type;
        [SerializeField] private LevelEffectParamData[] m_params = Array.Empty<LevelEffectParamData>();

        // Derived from m_type and m_params; cleared when Unity rewrites the serialized fields (Undo, Inspector).
        [NonSerialized] private LevelEffectParameters m_parameters;

        public string Type => m_type;
        public int ParamCount => m_params?.Length ?? 0;

        /// <summary>The typed parameters, or null when the type is unknown or a value breaks its rule.</summary>
        public LevelEffectParameters Parameters
        {
            get
            {
                if (m_parameters == null)
                {
                    LevelEffectRegistry.TryConvert(this, out m_parameters);
                }

                return m_parameters;
            }
        }

        public LevelEffectData()
        {
        }

        public LevelEffectData(string type, LevelEffectParamData[] parameters)
        {
            m_type = type;
            m_params = CloneParams(parameters);
        }

        public LevelEffectParamData GetParamAt(int index)
        {
            return m_params[index];
        }

        /// <summary>The authored value of a key, or null when the key is absent.</summary>
        public string FindValue(string key)
        {
            for (int index = 0; index < ParamCount; index++)
            {
                LevelEffectParamData param = m_params[index];
                if (param != null && string.Equals(param.Key, key, StringComparison.Ordinal))
                {
                    return param.Value;
                }
            }

            return null;
        }

        public LevelEffectData Clone()
        {
            return new LevelEffectData(m_type, m_params);
        }

        // Unity can rewrite serialized fields in place (editor drafts), so copies never share pair objects.
        private static LevelEffectParamData[] CloneParams(LevelEffectParamData[] parameters)
        {
            if (parameters == null)
            {
                return null;
            }

            LevelEffectParamData[] copy = new LevelEffectParamData[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                LevelEffectParamData param = parameters[index];
                copy[index] = param == null ? null : new LevelEffectParamData(param.Key, param.Value);
            }

            return copy;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            m_parameters = null;
        }

        internal static LevelEffectData[] CloneAll(LevelEffectData[] effects)
        {
            if (effects == null)
            {
                return null;
            }

            LevelEffectData[] copy = new LevelEffectData[effects.Length];
            for (int index = 0; index < effects.Length; index++)
            {
                copy[index] = effects[index]?.Clone();
            }

            return copy;
        }

        internal static bool TryGet<T>(LevelEffectData[] effects, out T parameters)
            where T : LevelEffectParameters
        {
            for (int index = 0; effects != null && index < effects.Length; index++)
            {
                if (effects[index]?.Parameters is T match)
                {
                    parameters = match;
                    return true;
                }
            }

            parameters = null;
            return false;
        }
    }
}
