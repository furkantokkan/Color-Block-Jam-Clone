using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Keeps authored level databases in one local Addressables group, and sets which one the game plays: the manifest
    /// reference of the project scope installer on ProjectContext.prefab (DEC-194). A database is a folder with a
    /// manifest and its packs (DEC-196); every file of it is an entry with its derived address and the level-json label,
    /// and only manifests are listed as databases.
    /// </summary>
    public static class LevelDatabaseAddressables
    {
        public const string DatabaseDirectory = LevelJsonFiles.Folder;

        /// <summary>The database every project has; it cannot be deleted.</summary>
        public const string DefaultDatabaseName = "Main";

        public const string DefaultManifestPath =
            DatabaseDirectory + "/" + DefaultDatabaseName + "/" + LevelJsonFiles.ManifestFile;

        public const string GroupName = "Level Database";
        /// <summary>The prefab whose project scope installer names the level database the game plays.</summary>
        public const string ProjectContextPath = "Assets/ColorBlockJam/Resources/Onity/ProjectContext.prefab";
        /// <summary>The installer's manifest GUID; m_AssetGUID is the field AssetReference serializes it in.</summary>
        internal const string LevelJsonGuidProperty = "m_levelJson.m_AssetGUID";
        internal const string MissingProjectScopeMessage =
            "ProjectContext.prefab has no ProjectScopeInstaller to hold the active level database.";

        /// <summary>One level database of the group: its manifest's GUID and path.</summary>
        public readonly struct Entry
        {
            public Entry(string guid, string path)
            {
                Guid = guid;
                Path = path;
            }

            public string Guid { get; }

            /// <summary>The manifest's asset path.</summary>
            public string Path { get; }

            /// <summary>The database name: its folder's name.</summary>
            public string Name => GetDatabaseName(Path);
        }

        /// <summary>
        /// Registers every database under <see cref="DatabaseDirectory"/> with its packs, and makes the default one the
        /// game's when none is chosen.
        /// </summary>
        [MenuItem("Color Block Jam/Data/Configure Level Databases")]
        public static void ConfigureDefault()
        {
            AddressableAssetSettings settings = GetSettings();
            if (!TrySync(settings, DefaultManifestPath, out string error))
            {
                throw new InvalidOperationException(error);
            }

            foreach (string manifestPath in FindManifests())
            {
                if (!string.Equals(manifestPath, DefaultManifestPath, StringComparison.Ordinal)
                    && !TrySync(settings, manifestPath, out error))
                {
                    Debug.LogWarning($"Level database {GetDatabaseName(manifestPath)} was not registered: {error}");
                }
            }

            ProjectScopeInstaller projectScope = LoadProjectScope();
            if (projectScope != null && string.IsNullOrEmpty(projectScope.LevelJsonGuid)
                && !TrySetActive(settings, projectScope, AssetDatabase.AssetPathToGUID(DefaultManifestPath), out error))
            {
                throw new InvalidOperationException(error);
            }

            Debug.Log($"Level databases are registered in Addressables group {GroupName}.");
        }

        /// <summary>The project scope installer on ProjectContext.prefab, or null when either is missing.</summary>
        public static ProjectScopeInstaller LoadProjectScope()
        {
            GameObject projectContext = AssetDatabase.LoadAssetAtPath<GameObject>(ProjectContextPath);
            return projectContext != null ? projectContext.GetComponent<ProjectScopeInstaller>() : null;
        }

        /// <summary>The database name of the manifest at <paramref name="manifestPath"/>: its folder's name.</summary>
        public static string GetDatabaseName(string manifestPath)
        {
            return string.IsNullOrEmpty(manifestPath) ? null : System.IO.Path.GetFileName(LevelDatabaseFiles.GetFolder(manifestPath));
        }

        /// <summary>The databases of the group, by name.</summary>
        public static List<Entry> GetEntries()
        {
            return GetEntries(GetSettings());
        }

        internal static List<Entry> GetEntries(AddressableAssetSettings settings)
        {
            List<Entry> result = new List<Entry>();
            AddressableAssetGroup group = settings?.FindGroup(GroupName);
            if (group == null)
            {
                return result;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                string path = entry.AssetPath;
                if (LevelJsonFiles.IsManifest(path)
                    && AssetDatabase.GetMainAssetTypeAtPath(path) == typeof(TextAsset))
                {
                    result.Add(new Entry(entry.guid, path));
                }
            }

            result.Sort((left, right) => string.Compare(left.Name, right.Name,
                StringComparison.OrdinalIgnoreCase));
            return result;
        }

        /// <summary>
        /// Registers the database whose manifest is at <paramref name="manifestPath"/>: the manifest and every pack it
        /// lists become entries of the group with their address and the level-json label, and entries of this database's
        /// files the manifest no longer lists (or whose file is gone) are removed. Idempotent; the files must be imported.
        /// </summary>
        public static bool TrySync(string manifestPath, out string error)
        {
            return TrySync(GetSettings(), manifestPath, out error);
        }

        internal static bool TrySync(AddressableAssetSettings settings, string manifestPath, out string error)
        {
            if (settings == null)
            {
                error = "Addressables settings are missing. Configure Addressables before registering level databases.";
                return false;
            }

            if (!LevelJsonFiles.IsManifest(manifestPath))
            {
                error = $"A level database manifest must be {DatabaseDirectory}/<name>/{LevelJsonFiles.ManifestFile}: "
                        + manifestPath;
                return false;
            }

            // The manifest alone names every file; the packs are validated where they are read and saved.
            if (!LevelDatabaseFiles.TryReadManifest(manifestPath, out LevelManifest manifest, out error))
            {
                return false;
            }

            settings.BuildAddressablesWithPlayerBuild =
                AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (group == null)
            {
                group = CreateGroup(settings);
            }

            string name = manifest.Name;
            string folder = LevelDatabaseFiles.GetFolder(manifestPath);
            HashSet<AddressableAssetGroup> touched = new HashSet<AddressableAssetGroup> { group };
            HashSet<string> listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { manifestPath };
            if (!TryRegisterFile(settings, group, manifestPath, LevelJsonFiles.GetAddress(name, LevelJsonFiles.ManifestFile),
                    touched, out error))
            {
                return false;
            }

            for (int index = 0; index < manifest.PackCount; index++)
            {
                string file = manifest.GetPackAt(index).File;
                string path = $"{folder}/{file}";
                listed.Add(path);
                if (!TryRegisterFile(settings, group, path, LevelJsonFiles.GetAddress(name, file), touched, out error))
                {
                    return false;
                }
            }

            RemoveUnlisted(group, folder, listed);
            foreach (AddressableAssetGroup changed in touched)
            {
                EditorUtility.SetDirty(changed);
                SaveGroup(changed);
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
            error = null;
            return true;
        }

        /// <summary>
        /// Returns why <paramref name="name"/> cannot name a new level database, or null when it can.
        /// </summary>
        public static string GetNewNameError(string name)
        {
            return GetNewNameError(name, DatabaseDirectory);
        }

        private static string GetNewNameError(string name, string directory)
        {
            if (!LevelJsonFiles.IsValidName(name))
            {
                return "Use a database name containing only letters, digits, hyphens or underscores.";
            }

            return Directory.Exists($"{directory}/{name}") ? $"A level database named {name} already exists." : null;
        }

        /// <summary>Creates an empty level database: its folder, a manifest without packs, and its group entry.</summary>
        public static bool TryCreate(string name, out Entry entry, out string error)
        {
            return TryCreate(GetSettings(), name, DatabaseDirectory, out entry, out error);
        }

        internal static bool TryCreate(AddressableAssetSettings settings, string name,
            string directory, out Entry entry, out string error)
        {
            entry = default;
            if (settings == null)
            {
                error = "Addressables settings are missing. Configure Addressables before creating level databases.";
                return false;
            }

            if (!LevelJsonFiles.IsManifest($"{directory}/{name}/{LevelJsonFiles.ManifestFile}"))
            {
                error = $"Level databases must be under {DatabaseDirectory}.";
                return false;
            }

            error = GetNewNameError(name, directory);
            if (error != null)
            {
                return false;
            }

            string folder = $"{directory}/{name}";
            BoardKitAssets.Folder(folder);
            if (!LevelDatabaseFiles.TryCreate(directory, name, out string manifestPath, out error))
            {
                AssetDatabase.DeleteAsset(folder);
                return false;
            }

            AssetDatabase.ImportAsset(manifestPath, ImportAssetOptions.ForceUpdate);
            if (!TrySync(settings, manifestPath, out error))
            {
                AssetDatabase.DeleteAsset(folder);
                return false;
            }

            entry = new Entry(AssetDatabase.AssetPathToGUID(manifestPath), manifestPath);
            return true;
        }

        public static bool TrySetActive(ProjectScopeInstaller projectScope, string guid, out string error)
        {
            return TrySetActive(GetSettings(), projectScope, guid, out error);
        }

        internal static bool TrySetActive(AddressableAssetSettings settings, ProjectScopeInstaller projectScope,
            string guid, out string error)
        {
            if (projectScope == null)
            {
                error = MissingProjectScopeMessage;
                return false;
            }

            if (!TryFindEntry(settings, guid, out Entry selected))
            {
                error = $"Level database {guid} is not in the {GroupName} Addressables group.";
                return false;
            }

            // Only a readable, valid database becomes the game's; the installer keeps its manifest GUID, not its levels
            // (DEC-192, DEC-194).
            if (!LevelDatabaseFiles.TryRead(selected.Path, out _, out error))
            {
                return false;
            }

            Undo.RecordObject(projectScope, "Select Active Level Database");
            using SerializedObject serialized = new SerializedObject(projectScope);
            serialized.FindProperty(LevelJsonGuidProperty).stringValue = guid;
            serialized.ApplyModifiedProperties();
            SaveProjectScope(projectScope);
            return true;
        }

        /// <summary>
        /// Deletes one level database (its folder, every file and every group entry). The default database is
        /// protected; when the deleted database is active, the game switches to the default first.
        /// </summary>
        public static bool TryDelete(ProjectScopeInstaller projectScope, string guid, out string error)
        {
            return TryDelete(GetSettings(), projectScope, guid, out error);
        }

        internal static bool TryDelete(AddressableAssetSettings settings, ProjectScopeInstaller projectScope,
            string guid, out string error)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(path, DefaultManifestPath, StringComparison.Ordinal))
            {
                error = "The default level database cannot be deleted.";
                return false;
            }

            if (!TryFindEntry(settings, guid, out _))
            {
                error = $"Level database {guid} is not in the {GroupName} Addressables group.";
                return false;
            }

            // Switch the game away first, so a failed switch leaves the files and the group untouched.
            if (projectScope != null
                && string.Equals(projectScope.LevelJsonGuid, guid, StringComparison.Ordinal)
                && !TrySetActive(settings, projectScope, AssetDatabase.AssetPathToGUID(DefaultManifestPath), out error))
            {
                return false;
            }

            // Deletion postprocessors may remove the entries before DeleteAsset returns; the rest go below.
            string folder = LevelDatabaseFiles.GetFolder(path);
            AddressableAssetGroup group = settings.FindGroup(GroupName);
            if (!AssetDatabase.DeleteAsset(folder))
            {
                error = $"Could not delete {folder}.";
                return false;
            }

            if (group != null)
            {
                RemoveUnlisted(group, folder, new HashSet<string>());
                EditorUtility.SetDirty(group);
                SaveGroup(group);
            }

            AssetDatabase.SaveAssetIfDirty(settings);
            error = null;
            return true;
        }

        /// <summary>The manifest path of the database the game plays, or null when none is chosen.</summary>
        public static string GetActivePath(ProjectScopeInstaller projectScope)
        {
            return projectScope == null || string.IsNullOrEmpty(projectScope.LevelJsonGuid)
                ? null
                : AssetDatabase.GUIDToAssetPath(projectScope.LevelJsonGuid);
        }

        private static bool TryFindEntry(AddressableAssetSettings settings, string guid, out Entry found)
        {
            foreach (Entry entry in GetEntries(settings))
            {
                if (string.Equals(entry.Guid, guid, StringComparison.Ordinal))
                {
                    found = entry;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private static List<string> FindManifests()
        {
            List<string> manifests = new List<string>();
            if (!Directory.Exists(DatabaseDirectory))
            {
                return manifests;
            }

            foreach (string folder in Directory.GetDirectories(DatabaseDirectory))
            {
                string manifestPath = $"{DatabaseDirectory}/{System.IO.Path.GetFileName(folder)}/{LevelJsonFiles.ManifestFile}";
                if (File.Exists(manifestPath))
                {
                    manifests.Add(manifestPath);
                }
            }

            manifests.Sort(StringComparer.Ordinal);
            return manifests;
        }

        private static bool TryRegisterFile(AddressableAssetSettings settings, AddressableAssetGroup group, string path,
            string address, HashSet<AddressableAssetGroup> touched, out string error)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid) || AssetDatabase.GetMainAssetTypeAtPath(path) != typeof(TextAsset))
            {
                error = $"Level JSON has not been imported as a TextAsset: {path}";
                return false;
            }

            AddressableAssetGroup sourceGroup = settings.FindAssetEntry(guid)?.parentGroup;
            if (sourceGroup != null)
            {
                touched.Add(sourceGroup);
            }

            AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
            if (entry == null)
            {
                error = $"Could not register level JSON in {GroupName}: {path}";
                return false;
            }

            entry.SetAddress(address, false);
            entry.SetLabel(LevelJsonFiles.Label, true, true, false);
            error = null;
            return true;
        }

        // Removes the entries of files in the database folder that are not listed, and entries whose file is gone.
        private static void RemoveUnlisted(AddressableAssetGroup group, string folder, HashSet<string> listed)
        {
            List<AddressableAssetEntry> stale = new List<AddressableAssetEntry>();
            foreach (AddressableAssetEntry entry in group.entries)
            {
                string path = entry.AssetPath;
                bool gone = string.IsNullOrEmpty(path);
                bool unlisted = !gone && path.StartsWith(folder + "/", StringComparison.Ordinal) && !listed.Contains(path);
                if (gone || unlisted)
                {
                    stale.Add(entry);
                }
            }

            for (int index = 0; index < stale.Count; index++)
            {
                group.RemoveAssetEntry(stale[index], false);
            }
        }

        // Writes the installer's prefab at once, as a database choice always wrote its file; a test's stand-in outside
        // any prefab has no file to write.
        private static void SaveProjectScope(ProjectScopeInstaller projectScope)
        {
            if (EditorUtility.IsPersistent(projectScope))
            {
                PrefabUtility.SavePrefabAsset(projectScope.transform.root.gameObject);
            }
        }

        private static AddressableAssetSettings GetSettings()
        {
            // The vendor's missing/legacy configuration paths can save every dirty asset.
            return AddressableAssetSettingsDefaultObject.SettingsExists
                ? AddressableAssetSettingsDefaultObject.GetSettings(false)
                : null;
        }

        private static AddressableAssetGroup CreateGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup source = null;
            using SerializedObject serializedSettings = new SerializedObject(settings);
            string defaultGuid = serializedSettings.FindProperty("m_DefaultGroup").stringValue;
            foreach (AddressableAssetGroup candidate in settings.groups)
            {
                if (candidate != null && candidate.Guid == defaultGuid)
                {
                    source = candidate;
                    break;
                }
            }

            // CreateGroup/AddSchema convenience overloads call global SaveAssets in Addressables 1.22.3.
            AddressableAssetGroup group = settings.CreateGroup(GroupName, false, false, false, null);
            AddressableAssetGroupSchemaSet schemas = new AddressableAssetGroupSchemaSet();
            Func<Type, string> schemaPath = type => settings.IsPersisted
                ? $"{settings.GroupSchemaFolder}/{group.Name}_{type.Name}.asset"
                : string.Empty;
            if (source != null)
            {
                foreach (AddressableAssetGroupSchema schema in source.Schemas)
                {
                    schemas.AddSchema(schema, schemaPath);
                }
            }
            else
            {
                schemas.AddSchema(typeof(BundledAssetGroupSchema), schemaPath);
                schemas.AddSchema(typeof(ContentUpdateGroupSchema), schemaPath);
            }

            foreach (AddressableAssetGroupSchema schema in schemas.Schemas)
            {
                using SerializedObject serialized = new SerializedObject(schema);
                serialized.FindProperty("m_Group").objectReferenceValue = group;
                if (schema is ContentUpdateGroupSchema)
                {
                    // Shipped levels must survive every content update: Cannot Change Post Release (DEC-162).
                    serialized.FindProperty("m_StaticContent").boolValue = true;
                }

                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (source == null && schema is BundledAssetGroupSchema bundled)
                {
                    bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                    bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                }

                group.Schemas.Add(schema);
                EditorUtility.SetDirty(schema);
                AssetDatabase.SaveAssetIfDirty(schema);
            }

            EditorUtility.SetDirty(group);
            return group;
        }

        private static void SaveGroup(AddressableAssetGroup group)
        {
            if (group == null)
            {
                return;
            }

            AssetDatabase.SaveAssetIfDirty(group);
        }
    }
}
