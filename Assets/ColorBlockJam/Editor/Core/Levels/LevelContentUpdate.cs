using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Sets up the remote level catalog and publishes a level JSON fix as an Addressables content update against the
    /// released player's saved content state (DEC-162, docs/Level-Content-Updates.md).
    /// </summary>
    public static class LevelContentUpdate
    {
        public const string BuildMenuPath = "Color Block Jam/Data/Build Level Content Update";
        public const string ReleaseProfileName = "Release";
        public const string PlaceholderHost = "configure-level-content-url.invalid";
        public const string PlaceholderLoadPath = "https://" + PlaceholderHost + "/[BuildTarget]";
        public const string UpdateGroupName = "Level Content Update";

        /// <summary>Seconds a catalog request, or a bundle download without data, may take before it fails.</summary>
        public const int RequestTimeoutSeconds = 10;

        /// <summary>
        /// Turns on the remote catalog, adds the Release profile with the placeholder URL once (and makes it active),
        /// and keeps the Level Database group local and Cannot Change Post Release. An existing Release profile, its
        /// URL and the active profile choice are never replaced. Saves only the settings and the level group's schemas.
        /// </summary>
        public static bool TryConfigure(AddressableAssetSettings settings, out string error)
        {
            if (settings == null)
            {
                error = "Addressables settings are missing. Configure Addressables before level content updates.";
                return false;
            }

            AddressableAssetGroup group = settings.FindGroup(LevelDatabaseAddressables.GroupName);
            BundledAssetGroupSchema bundled = group == null ? null : group.GetSchema<BundledAssetGroupSchema>();
            ContentUpdateGroupSchema update = group == null ? null : group.GetSchema<ContentUpdateGroupSchema>();
            if (bundled == null || update == null)
            {
                error = $"The {LevelDatabaseAddressables.GroupName} Addressables group or its schemas are missing. "
                    + "Run Color Block Jam/Data/Configure Level Databases first.";
                return false;
            }

            AddressableAssetProfileSettings profiles = settings.profileSettings;
            if (profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath) == null
                || profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath) == null)
            {
                error = "The Addressables profiles have no Remote.BuildPath or Remote.LoadPath variable.";
                return false;
            }

            settings.BuildRemoteCatalog = true;
            settings.RemoteCatalogBuildPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings, AddressableAssetSettings.kRemoteLoadPath);
            settings.DisableCatalogUpdateOnStartup = true;
            settings.CatalogRequestsTimeout = RequestTimeoutSeconds;
            if (string.IsNullOrEmpty(profiles.GetProfileId(ReleaseProfileName)))
            {
                // A copy of Default (or of the root profile when Default was renamed), so only the URL differs.
                string releaseId = profiles.AddProfile(ReleaseProfileName, profiles.GetProfileId("Default"));
                profiles.SetValue(releaseId, AddressableAssetSettings.kRemoteLoadPath, PlaceholderLoadPath);

                // Player builds bake the active profile's URL; Default's LAN Hosting Service address would make a
                // device wait for a machine it cannot reach. Default stays available for Hosting Service tests.
                settings.activeProfileId = releaseId;
            }

            if (!HasVariable(settings, bundled.BuildPath, AddressableAssetSettings.kLocalBuildPath))
            {
                bundled.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                EditorUtility.SetDirty(bundled);
            }

            if (!HasVariable(settings, bundled.LoadPath, AddressableAssetSettings.kLocalLoadPath))
            {
                bundled.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
                EditorUtility.SetDirty(bundled);
            }

            update.StaticContent = true;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);

            // SaveAssetIfDirty, not SaveAssets: other work in the Editor may hold unsaved assets.
            AssetDatabase.SaveAssetIfDirty(settings);
            AssetDatabase.SaveAssetIfDirty(bundled);
            AssetDatabase.SaveAssetIfDirty(update);
            error = null;
            return true;
        }

        /// <summary>
        /// Refuses a content update before anything builds: the remote catalog must be on, the active profile's
        /// Remote.LoadPath must not be the placeholder, every level database must read whole with every pack passing
        /// the runtime read, and every manifest and pack must be registered with its address and label, with no other
        /// level JSON in the group (DEC-196).
        /// </summary>
        public static bool TryValidate(AddressableAssetSettings settings, out string error)
        {
            if (settings == null)
            {
                error = "Addressables settings are missing. Configure Addressables before level content updates.";
                return false;
            }

            if (!settings.BuildRemoteCatalog)
            {
                error = "Build Remote Catalog is off in the Addressables settings, so no player can receive an update.";
                return false;
            }

            string profileName = settings.profileSettings.GetProfileName(settings.activeProfileId);
            string loadPath = settings.RemoteCatalogLoadPath.GetValue(settings);
            if (string.IsNullOrWhiteSpace(loadPath))
            {
                error = $"The remote catalog load path (Remote.LoadPath) of the Addressables profile '{profileName}' is empty.";
                return false;
            }

            if (loadPath.IndexOf(PlaceholderHost, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                error = $"Remote.LoadPath of the Addressables profile '{profileName}' is still the placeholder "
                    + $"{loadPath}. Set the level content URL before publishing.";
                return false;
            }

            List<LevelDatabaseAddressables.Entry> entries = LevelDatabaseAddressables.GetEntries(settings);
            if (entries.Count == 0)
            {
                error = $"The {LevelDatabaseAddressables.GroupName} Addressables group has no level database.";
                return false;
            }

            AddressableAssetGroup group = settings.FindGroup(LevelDatabaseAddressables.GroupName);
            HashSet<string> listed = new HashSet<string>(StringComparer.Ordinal);
            foreach (LevelDatabaseAddressables.Entry entry in entries)
            {
                if (!LevelDatabaseFiles.TryRead(entry.Path, out LevelDatabaseSnapshot database, out error))
                {
                    // The folder plus the file the read names (manifest or pack) locate the broken file.
                    error = $"Level database {LevelDatabaseFiles.GetFolder(entry.Path)} is invalid: {error}";
                    return false;
                }

                if (!IsRegistered(settings, group, entry.Path, entry.Name, out error))
                {
                    return false;
                }

                listed.Add(entry.Path);
                for (int index = 0; index < database.Packs.Count; index++)
                {
                    LevelPackFile pack = database.Packs[index];
                    // The runtime gate is what the game applies to this pack after the update.
                    if (!LevelDatabaseJson.TryRead(pack.Json, LevelReadMode.Runtime, out _, out error))
                    {
                        error = $"{pack.Path} is invalid: {error}";
                        return false;
                    }

                    if (!IsRegistered(settings, group, pack.Path, entry.Name, out error))
                    {
                        return false;
                    }

                    listed.Add(pack.Path);
                }
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (!listed.Contains(entry.AssetPath))
                {
                    error = $"{entry.AssetPath} is in the {LevelDatabaseAddressables.GroupName} group, but no level "
                        + "database lists it. Run Color Block Jam > Data > Configure Level Databases.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // A level file ships only when the game can load it: in the Level Database group, at its derived address,
        // with the level-json label the startup download takes (DEC-196).
        private static bool IsRegistered(AddressableAssetSettings settings, AddressableAssetGroup group, string path,
            string databaseName, out string error)
        {
            AddressableAssetEntry entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path));
            string address = LevelJsonFiles.GetAddress(databaseName, Path.GetFileName(path));
            if (entry == null || entry.parentGroup != group || !string.Equals(entry.address, address, StringComparison.Ordinal)
                || !entry.labels.Contains(LevelJsonFiles.Label))
            {
                error = $"{path} is not registered at {address} with the {LevelJsonFiles.Label} label. Run Color Block "
                    + "Jam > Data > Configure Level Databases.";
                return false;
            }

            error = null;
            return true;
        }

        /// <summary>
        /// Builds the content update: moves each changed level JSON into a remote group, runs BuildContentUpdate
        /// against the player's content state and restores the groups. The report names the folder to upload.
        /// </summary>
        public static bool TryBuild(AddressableAssetSettings settings, string contentStatePath,
            out string report, out string error)
        {
            report = null;
            if (!TryValidate(settings, out error) || !TryReadContentState(settings, contentStatePath, out error))
            {
                return false;
            }

            AddressableAssetGroup levelGroup = settings.FindGroup(LevelDatabaseAddressables.GroupName);
            if (settings.FindGroup(UpdateGroupName) != null)
            {
                error = $"An Addressables group named {UpdateGroupName} already exists; remove it before publishing.";
                return false;
            }

            List<AddressableAssetEntry> changed = GatherChangedEntries(settings, contentStatePath, levelGroup, out error);
            if (changed == null)
            {
                return false;
            }

            AddressableAssetGroup updateGroup = null;
            try
            {
                ContentUpdateScript.CreateContentUpdateGroup(settings, changed, UpdateGroupName);
                updateGroup = settings.FindGroup(UpdateGroupName);
                BundledAssetGroupSchema bundled = updateGroup == null
                    ? null
                    : updateGroup.GetSchema<BundledAssetGroupSchema>();
                if (bundled == null)
                {
                    error = $"The {UpdateGroupName} group was not created.";
                    return false;
                }

                // Bounds the startup download of the updated JSON bundle (DEC-162).
                bundled.Timeout = RequestTimeoutSeconds;
                AddressablesPlayerBuildResult result = ContentUpdateScript.BuildContentUpdate(settings, contentStatePath);
                if (result == null || !string.IsNullOrEmpty(result.Error))
                {
                    error = $"The content update build failed: {(result == null ? "the content state is invalid" : result.Error)}";
                    return false;
                }
            }
            finally
            {
                RestoreGroups(settings, levelGroup, updateGroup, changed);
            }

            string folder = Path.GetFullPath(settings.RemoteCatalogBuildPath.GetValue(settings));
            report = $"Level content update built for {changed.Count} level JSON file(s). Upload every file in {folder} "
                + $"to {settings.RemoteCatalogLoadPath.GetValue(settings)}.";
            return true;
        }

        [MenuItem(BuildMenuPath)]
        private static void BuildFromMenu()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.SettingsExists
                ? AddressableAssetSettingsDefaultObject.GetSettings(false)
                : null;
            // Validate first: GetContentStateDataPath creates Assets/AddressableAssetsData/<Platform> as a side
            // effect (ContentUpdateScript), so a refused click (for example the still-placeholder Release URL)
            // must never reach it. TryBuild's own leading TryValidate re-parses the level JSON a second time on a
            // successful publish, which costs almost nothing for the few small level files this project has.
            string report = null;
            bool built = TryValidate(settings, out string error)
                && TryBuild(settings, ContentUpdateScript.GetContentStateDataPath(false, settings),
                    out report, out error);
            if (built)
            {
                Debug.Log(report);
                EditorUtility.DisplayDialog("Level Content Update", report, "OK");
                return;
            }

            Debug.LogWarning($"Level content update refused: {error}");
            EditorUtility.DisplayDialog("Level Content Update", error, "OK");
        }

        private static bool TryReadContentState(AddressableAssetSettings settings, string path, out string error)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                error = $"The released player's content state is missing: {path}. Keep the addressables_content_state.bin "
                    + "of each released platform build.";
                return false;
            }

            AddressablesContentState state;
            try
            {
                state = ContentUpdateScript.LoadContentState(path);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException
                                              || exception is System.Runtime.Serialization.SerializationException)
            {
                state = null;
            }

            if (state == null)
            {
                error = $"The content state {path} could not be read.";
                return false;
            }

            string loadPath = settings.RemoteCatalogLoadPath.GetValue(settings);
            if (!string.Equals(state.remoteCatalogLoadPath, loadPath, StringComparison.Ordinal))
            {
                error = $"The player in {path} reads its catalog from {state.remoteCatalogLoadPath}, but the active "
                    + $"profile builds for {loadPath}. Select the profile the player was built with.";
                return false;
            }

            error = null;
            return true;
        }

        // Only changed level JSON in the static level group is covered by DEC-162; anything else is refused.
        private static List<AddressableAssetEntry> GatherChangedEntries(AddressableAssetSettings settings,
            string contentStatePath, AddressableAssetGroup levelGroup, out string error)
        {
            List<AddressableAssetEntry> changed = new List<AddressableAssetEntry>();
            foreach (KeyValuePair<AddressableAssetEntry, List<AddressableAssetEntry>> modified in
                     ContentUpdateScript.GatherModifiedEntriesWithDependencies(settings, contentStatePath))
            {
                AddChanged(changed, modified.Key);
                foreach (AddressableAssetEntry dependency in modified.Value)
                {
                    AddChanged(changed, dependency);
                }
            }

            if (changed.Count == 0)
            {
                error = "No level JSON changed since the released player build, so there is nothing to publish.";
                return null;
            }

            foreach (AddressableAssetEntry entry in changed)
            {
                if (entry.parentGroup != levelGroup
                    || !string.Equals(Path.GetExtension(entry.AssetPath), ".json", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"The update would also move {entry.AssetPath} (group {entry.parentGroup?.Name}); "
                        + "only level JSON in the Level Database group can be published this way.";
                    return null;
                }
            }

            error = null;
            return changed;
        }

        private static void AddChanged(List<AddressableAssetEntry> changed, AddressableAssetEntry entry)
        {
            if (entry != null && !changed.Contains(entry))
            {
                changed.Add(entry);
            }
        }

        // Moves the published JSON back so the Level Editor and the next update see the authored layout.
        private static void RestoreGroups(AddressableAssetSettings settings, AddressableAssetGroup levelGroup,
            AddressableAssetGroup updateGroup, List<AddressableAssetEntry> changed)
        {
            settings.MoveEntries(changed, levelGroup);
            foreach (AddressableAssetEntry entry in changed)
            {
                entry.FlaggedDuringContentUpdateRestriction = false;
            }

            if (updateGroup != null)
            {
                settings.RemoveGroup(updateGroup);
            }

            EditorUtility.SetDirty(levelGroup);
            AssetDatabase.SaveAssetIfDirty(levelGroup);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        private static bool HasVariable(AddressableAssetSettings settings, ProfileValueReference reference, string name)
        {
            AddressableAssetProfileSettings.ProfileIdData variable = settings.profileSettings.GetProfileDataByName(name);
            return variable != null && string.Equals(reference.Id, variable.Id, StringComparison.Ordinal);
        }
    }
}
