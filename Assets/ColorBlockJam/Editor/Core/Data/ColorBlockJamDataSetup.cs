using System;
using ColorBlockJam.Runtime.Boosters;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Currencies;
using ColorBlockJam.Editor.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Data
{
    /// <summary>
    /// Creates the project's editable data assets once and wires project scope.
    /// Existing authored values are never overwritten.
    /// </summary>
    public static class ColorBlockJamDataSetup
    {
        private const string k_dataRoot = "Assets/ColorBlockJam/Data";
        private const string k_boosterRoot = k_dataRoot + "/Boosters";
        private const string k_definitionRoot = k_boosterRoot + "/Definitions";
        private const string k_coinPath = k_dataRoot + "/Currencies/Coin.asset";
        private const string k_projectPrefabPath =
            "Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab";

        // DEC-78/DEC-85: one seeded charge per definition, a 10-coin offer granting one charge.
        private const int k_initialCharges = 1;
        private const int k_purchasePrice = 10;
        private const int k_purchaseAmount = 1;

        [MenuItem("Color Block Jam/Data/Create Default Assets")]
        public static void CreateDefaultAssets()
        {
            CreateFolder(k_definitionRoot);
            CreateFolder(LevelDatabaseAddressables.DatabaseDirectory);

            // Matches the shipped BoosterCatalog.asset and the pre-level order of DEC-138/DEC-167.
            BoosterDefinition[] definitions =
            {
                CreateDefinition<ExtraTimeBoosterDefinition>(
                    "ExtraTime", BoosterId.ExtraTime, "Extra Time",
                    "Add 10 seconds at the start of a level.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Gameplay/ic_clock.png",
                    BoosterUseContext.PreLevel, BoosterTargetKind.None, null),
                // Its own stock, apart from the in-game Rocket (DEC-167).
                CreateDefinition<TargetedBoosterDefinition>(
                    "StartingRocket", BoosterId.StartingRocket, "Rocket",
                    "Remove one random cell at the start of a level.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_rocket_purple.png",
                    BoosterUseContext.PreLevel, BoosterTargetKind.Cell,
                    "Assets/ColorBlockJam/Prefabs/Boosters/Effects/RocketPurpleEffect.prefab"),
                CreateDefinition<TimeFreezeBoosterDefinition>(
                    "TimeFreeze", BoosterId.TimeFreeze, "Time Freeze",
                    "Freeze the level countdown for 10 seconds.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_clock_freeze.png",
                    BoosterUseContext.InGame, BoosterTargetKind.None, null),
                CreateDefinition<TargetedBoosterDefinition>(
                    "Rocket", BoosterId.Rocket, "Rocket",
                    "Remove one cell from the selected block.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_rocket_purple.png",
                    BoosterUseContext.InGame,
                    BoosterTargetKind.Cell,
                    "Assets/ColorBlockJam/Prefabs/Boosters/Effects/RocketPurpleEffect.prefab"),
                // DEC-147: Hammer removes the tapped block whole, active Ice included, and never splits.
                CreateDefinition<TargetedBoosterDefinition>(
                    "Hammer", BoosterId.Hammer, "Hammer",
                    "Remove one whole block.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_hammer.png",
                    BoosterUseContext.InGame, BoosterTargetKind.Block,
                    "Assets/ColorBlockJam/Prefabs/Boosters/Effects/HammerEffect.prefab"),
                CreateDefinition<TargetedBoosterDefinition>(
                    "Vacuum", BoosterId.Vacuum, "Vacuum",
                    "Remove blocks matching the selected color.",
                    "Assets/ColorBlockJam/Art/UI/Supplied/Icons/Boosters/ic_vacuum.png",
                    BoosterUseContext.InGame, BoosterTargetKind.ColorFromBlock,
                    "Assets/ColorBlockJam/Prefabs/Boosters/Effects/VacuumEffect.prefab")
            };

            BoosterCatalog catalog = CreateCatalog(definitions);
            if (!catalog.Validate(out string error))
            {
                throw new InvalidOperationException($"Booster catalog is invalid: {error}");
            }

            string defaultLevelJsonGuid = CreateDefaultLevelJson();
            BindProjectData(catalog, defaultLevelJsonGuid);
            AssetDatabase.SaveAssets();
            Debug.Log("Color Block Jam data assets are ready under Data.");
        }

        private static T CreateDefinition<T>(
            string fileName, BoosterId id, string displayName, string description,
            string iconPath, BoosterUseContext contexts, BoosterTargetKind targetKind,
            string effectPrefabPath) where T : BoosterDefinition
        {
            string path = k_definitionRoot + "/" + fileName + ".asset";
            T definition = AssetDatabase.LoadAssetAtPath<T>(path);
            if (definition != null)
            {
                return definition;
            }

            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
            if (icon == null)
            {
                throw new InvalidOperationException($"Booster icon is missing: {iconPath}");
            }

            GameObject effectPrefab = null;
            if (effectPrefabPath != null)
            {
                effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(effectPrefabPath);
                if (effectPrefab == null)
                {
                    throw new InvalidOperationException(
                        $"Booster effect prefab is missing: {effectPrefabPath}");
                }
            }

            CurrencyDefinition coin = AssetDatabase.LoadAssetAtPath<CurrencyDefinition>(k_coinPath);
            if (coin == null)
            {
                throw new InvalidOperationException($"Booster purchase currency is missing: {k_coinPath}");
            }

            definition = ScriptableObject.CreateInstance<T>();
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("m_id").intValue = (int)id;
            serialized.FindProperty("m_displayName").stringValue = displayName;
            serialized.FindProperty("m_description").stringValue = description;
            serialized.FindProperty("m_icon").objectReferenceValue = icon;
            serialized.FindProperty("m_effectPrefab").objectReferenceValue = effectPrefab;
            serialized.FindProperty("m_useContexts").intValue = (int)contexts;
            serialized.FindProperty("m_targetKind").intValue = (int)targetKind;
            serialized.FindProperty("m_initialCharges").intValue = k_initialCharges;
            serialized.FindProperty("m_purchasePrice").intValue = k_purchasePrice;
            serialized.FindProperty("m_purchaseCurrency").objectReferenceValue = coin;
            serialized.FindProperty("m_purchaseAmount").intValue = k_purchaseAmount;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(definition, path);
            return definition;
        }

        private static BoosterCatalog CreateCatalog(BoosterDefinition[] definitions)
        {
            string path = k_boosterRoot + "/BoosterCatalog.asset";
            BoosterCatalog catalog = AssetDatabase.LoadAssetAtPath<BoosterCatalog>(path);
            if (catalog != null)
            {
                return catalog;
            }

            catalog = ScriptableObject.CreateInstance<BoosterCatalog>();
            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("m_definitions");
            entries.arraySize = definitions.Length;
            for (int index = 0; index < definitions.Length; index++)
            {
                entries.GetArrayElementAtIndex(index).objectReferenceValue = definitions[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        // Creates the default level database (folder and manifest) when missing, registers it with its packs in the Level
        // Database group and returns its manifest's GUID (DEC-196).
        private static string CreateDefaultLevelJson()
        {
            CreateFolder($"{LevelDatabaseAddressables.DatabaseDirectory}/{LevelDatabaseAddressables.DefaultDatabaseName}");
            if (!LevelDatabaseFiles.TryCreate(LevelDatabaseAddressables.DatabaseDirectory,
                    LevelDatabaseAddressables.DefaultDatabaseName, out string manifestPath, out string error))
            {
                throw new InvalidOperationException(error);
            }

            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceUpdate);
            if (!LevelDatabaseAddressables.TrySync(manifestPath, out error))
            {
                throw new InvalidOperationException(error);
            }

            return AssetDatabase.AssetPathToGUID(manifestPath);
        }

        private static void BindProjectData(BoosterCatalog catalog, string defaultLevelJsonGuid)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(k_projectPrefabPath);
            try
            {
                ProjectScopeInstaller installer = root.GetComponent<ProjectScopeInstaller>();
                if (installer == null)
                {
                    throw new InvalidOperationException("ProjectScopeInstaller is missing from ProjectContext.");
                }

                SerializedObject serialized = new SerializedObject(installer);
                SerializedProperty boosterProperty = serialized.FindProperty("m_boosterCatalog");
                SerializedProperty levelProperty = serialized.FindProperty(LevelDatabaseAddressables.LevelJsonGuidProperty);
                if (boosterProperty == null || levelProperty == null)
                {
                    throw new InvalidOperationException("Project data binding fields are missing.");
                }

                // The designer's database choice stays; only a missing one takes the default database (DEC-194).
                string levelJsonGuid = string.IsNullOrEmpty(levelProperty.stringValue)
                    ? defaultLevelJsonGuid
                    : levelProperty.stringValue;
                if (!LevelDatabaseFiles.TryRead(AssetDatabase.GUIDToAssetPath(levelJsonGuid), out _, out string error))
                {
                    throw new InvalidOperationException($"The level database is invalid: {error}");
                }

                if (boosterProperty.objectReferenceValue == catalog && levelProperty.stringValue == levelJsonGuid)
                {
                    return;
                }

                boosterProperty.objectReferenceValue = catalog;
                levelProperty.stringValue = levelJsonGuid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, k_projectPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void CreateFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            CreateFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
