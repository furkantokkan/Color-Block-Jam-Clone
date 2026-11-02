using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>Loads and saves only the effect constraints asset; the rules model owns pair behavior.</summary>
    internal static class EffectStackingRulesStore
    {
        private const string k_assetPath = "Assets/ColorBlockJam/Data/Levels/EffectStackingRules.asset";

        internal static EffectStackingRules Load() => AssetDatabase.LoadAssetAtPath<EffectStackingRules>(k_assetPath);

        internal static EffectStackingRules LoadOrCreate() => LoadOrCreate(k_assetPath);

        internal static EffectStackingRules LoadOrCreate(string path)
        {
            EffectStackingRules rules = AssetDatabase.LoadAssetAtPath<EffectStackingRules>(path);
            if (rules != null)
            {
                return rules;
            }

            rules = ScriptableObject.CreateInstance<EffectStackingRules>();
            AssetDatabase.CreateAsset(rules, path);
            Save(rules);
            return rules;
        }

        internal static void Save(EffectStackingRules rules)
        {
            EditorUtility.SetDirty(rules);
            AssetDatabase.SaveAssetIfDirty(rules);
        }
    }
}
