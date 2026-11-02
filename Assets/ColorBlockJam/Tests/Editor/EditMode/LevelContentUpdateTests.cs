using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the remote level catalog settings, the content update command's refusals and the startup update's
    /// async shape (DEC-162); everything except the read-only project check runs on throwaway settings.
    /// </summary>
    public sealed class LevelContentUpdateTests
    {
        private const string k_addressablesDirectory = "Assets/AddressableAssetsData";
        private const string k_testLoadPath = "https://level-content.test/[BuildTarget]";

        private string m_directory;
        // A level database is a folder directly under the level folder (DEC-196), so each test's database lives beside
        // the scratch folder and TearDown deletes it too.
        private string m_database;
        private AddressableAssetSettings m_settings;
        private Dictionary<string, byte[]> m_protectedFiles;

        [SetUp]
        public void SetUp()
        {
            m_protectedFiles = new Dictionary<string, byte[]>();
            foreach (string path in Directory.GetFiles(k_addressablesDirectory, "*", SearchOption.AllDirectories))
            {
                m_protectedFiles.Add(path, File.ReadAllBytes(path));
            }

            m_directory = LevelDatabaseAddressables.DatabaseDirectory + "/__ContentUpdateTests_"
                + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(LevelDatabaseAddressables.DatabaseDirectory, Path.GetFileName(m_directory));

            // Transient settings inside a test-owned container, as in LevelDatabaseAddressablesTests: the vendor's
            // import hooks never treat them as the project's settings.
            m_settings = AddressableAssetSettings.Create(m_directory, "Test Settings", false, false);
            TestAssetContainer container = ScriptableObject.CreateInstance<TestAssetContainer>();
            AssetDatabase.CreateAsset(container, m_directory + "/SettingsContainer.asset");
            AssetDatabase.AddObjectToAsset(m_settings, container);
            AssetDatabase.SaveAssetIfDirty(m_settings);
            Assert.That(m_settings.IsPersisted, Is.False);
        }

        [TearDown]
        public void TearDown()
        {
            List<AddressableAssetGroup> groups = m_settings != null
                ? new List<AddressableAssetGroup>(m_settings.groups)
                : new List<AddressableAssetGroup>();
            try
            {
                if (!string.IsNullOrEmpty(m_database) && AssetDatabase.IsValidFolder(m_database))
                {
                    AssetDatabase.DeleteAsset(m_database);
                }

                m_database = null;
                if (!string.IsNullOrEmpty(m_directory) && AssetDatabase.IsValidFolder(m_directory))
                {
                    Assert.That(AssetDatabase.DeleteAsset(m_directory), Is.True, "Could not clean the scratch folder.");
                }
            }
            finally
            {
                foreach (AddressableAssetGroup group in groups)
                {
                    if (group == null)
                    {
                        continue;
                    }

                    foreach (AddressableAssetGroupSchema schema in new List<AddressableAssetGroupSchema>(group.Schemas))
                    {
                        if (schema != null && !EditorUtility.IsPersistent(schema))
                        {
                            UnityEngine.Object.DestroyImmediate(schema);
                        }
                    }

                    if (!EditorUtility.IsPersistent(group))
                    {
                        UnityEngine.Object.DestroyImmediate(group);
                    }
                }

                CollectionAssert.AreEquivalent(m_protectedFiles.Keys,
                    Directory.GetFiles(k_addressablesDirectory, "*", SearchOption.AllDirectories),
                    "The project's Addressables file inventory changed.");
                foreach (KeyValuePair<string, byte[]> file in m_protectedFiles)
                {
                    Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
                }
            }
        }

        /// <summary>AC-1: the project's own settings, read only.</summary>
        [Test]
        public void ProjectSettingsShipLevelsLocallyAndCheckARemoteCatalog()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.SettingsExists
                ? AddressableAssetSettingsDefaultObject.GetSettings(false)
                : null;
            Assert.That(settings, Is.Not.Null, "Addressables settings are missing.");

            AssertRemoteCatalog(settings);
            AddressableAssetProfileSettings profiles = settings.profileSettings;
            string defaultId = profiles.GetProfileId("Default");
            string releaseId = profiles.GetProfileId(LevelContentUpdate.ReleaseProfileName);
            Assert.That(defaultId, Is.Not.Empty, "the Default profile stays for Hosting Service tests");
            Assert.That(releaseId, Is.Not.Empty);
            Assert.That(settings.activeProfileId, Is.EqualTo(releaseId),
                "player builds must bake the Release URL; switch back after a Hosting Service test");

            // Until the user supplies the hosting URL (docs/Level-Content-Updates.md); update this line with it.
            Assert.That(profiles.GetValueByName(releaseId, AddressableAssetSettings.kRemoteLoadPath),
                Is.EqualTo(LevelContentUpdate.PlaceholderLoadPath));
            AssertLocalStaticLevelGroup(settings);
        }

        [Test]
        public void ConfigureTurnsOnTheRemoteCatalogAndAStaticLocalLevelGroup()
        {
            CreateLevelGroup();
            string defaultId = m_settings.profileSettings.GetProfileId("Default");
            string defaultLoadPath = m_settings.profileSettings.GetValueByName(defaultId,
                AddressableAssetSettings.kRemoteLoadPath);

            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out string error), Is.True, error);

            AssertRemoteCatalog(m_settings);
            AssertLocalStaticLevelGroup(m_settings);
            string releaseId = m_settings.profileSettings.GetProfileId(LevelContentUpdate.ReleaseProfileName);
            Assert.That(m_settings.activeProfileId, Is.EqualTo(releaseId));
            Assert.That(m_settings.profileSettings.GetValueByName(releaseId, AddressableAssetSettings.kRemoteLoadPath),
                Is.EqualTo(LevelContentUpdate.PlaceholderLoadPath));
            Assert.That(m_settings.profileSettings.GetValueByName(defaultId, AddressableAssetSettings.kRemoteLoadPath),
                Is.EqualTo(defaultLoadPath), "Default keeps its Hosting Service URL");
        }

        [Test]
        public void ConfigureKeepsAnExistingReleaseUrlAndActiveProfile()
        {
            CreateLevelGroup();
            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out string error), Is.True, error);
            AddressableAssetProfileSettings profiles = m_settings.profileSettings;
            string releaseId = profiles.GetProfileId(LevelContentUpdate.ReleaseProfileName);
            string defaultId = profiles.GetProfileId("Default");
            profiles.SetValue(releaseId, AddressableAssetSettings.kRemoteLoadPath, k_testLoadPath);
            m_settings.activeProfileId = defaultId;

            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out error), Is.True, error);

            Assert.That(profiles.GetValueByName(releaseId, AddressableAssetSettings.kRemoteLoadPath),
                Is.EqualTo(k_testLoadPath));
            Assert.That(m_settings.activeProfileId, Is.EqualTo(defaultId));
        }

        [Test]
        public void ConfigureRefusesWithoutTheLevelGroupAndChangesNothing()
        {
            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out string error), Is.False);

            Assert.That(error, Does.Contain(LevelDatabaseAddressables.GroupName));
            Assert.That(m_settings.BuildRemoteCatalog, Is.False);
            Assert.That(m_settings.profileSettings.GetProfileId(LevelContentUpdate.ReleaseProfileName), Is.Empty);
        }

        /// <summary>AC-5: the placeholder URL refuses before anything builds and names the setting.</summary>
        [Test]
        public void PlaceholderUrlRefusesBeforeBuilding()
        {
            CreateLevelGroup();
            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out string error), Is.True, error);

            Assert.That(LevelContentUpdate.TryValidate(m_settings, out error), Is.False);
            Assert.That(error, Does.Contain("Remote.LoadPath"));
            Assert.That(error, Does.Contain(LevelContentUpdate.ReleaseProfileName));
            Assert.That(error, Does.Contain(LevelContentUpdate.PlaceholderHost));

            Assert.That(LevelContentUpdate.TryBuild(m_settings, m_directory + "/missing_state.bin",
                out string report, out string buildError), Is.False);
            Assert.That(report, Is.Null);
            Assert.That(buildError, Is.EqualTo(error));
            Assert.That(m_settings.FindGroup(LevelContentUpdate.UpdateGroupName), Is.Null);
        }

        /// <summary>AC-5: an invalid level JSON refuses before anything builds and names the file.</summary>
        [Test]
        public void InvalidLevelJsonRefusesAndNamesTheFile()
        {
            LevelDatabaseAddressables.Entry entry = CreateLevelGroup();
            ConfigureWithTestUrl();
            File.WriteAllText(entry.Path, "{ broken");

            // A database is a folder (DEC-196): the error names it and the broken file inside it.
            string folder = LevelDatabaseFiles.GetFolder(entry.Path);
            Assert.That(LevelContentUpdate.TryValidate(m_settings, out string error), Is.False);
            Assert.That(error, Does.Contain(folder).And.Contain(LevelJsonFiles.ManifestFile));

            Assert.That(LevelContentUpdate.TryBuild(m_settings, m_directory + "/missing_state.bin",
                out _, out string buildError), Is.False);
            Assert.That(buildError, Does.Contain(folder).And.Contain(LevelJsonFiles.ManifestFile));
            Assert.That(m_settings.FindGroup(LevelContentUpdate.UpdateGroupName), Is.Null);
        }

        [Test]
        public void MissingContentStateRefusesAndNamesThePath()
        {
            CreateLevelGroup();
            ConfigureWithTestUrl();
            string statePath = m_directory + "/addressables_content_state.bin";

            Assert.That(LevelContentUpdate.TryValidate(m_settings, out string error), Is.True, error);
            Assert.That(LevelContentUpdate.TryBuild(m_settings, statePath, out _, out error), Is.False);
            Assert.That(error, Does.Contain(statePath));
            Assert.That(m_settings.FindGroup(LevelContentUpdate.UpdateGroupName), Is.Null);
        }

        /// <summary>AC-6 (structure): the startup update and the level load never use async void.</summary>
        [Test]
        public void StartupUpdateUsesNoAsyncVoid()
        {
            Type updaterType = typeof(ProjectScopeInstaller).Assembly.GetType(
                "ColorBlockJam.Runtime.Levels.LevelContentUpdater");
            Assert.That(updaterType, Is.Not.Null, "the startup update lives in ColorBlockJam.Runtime.Levels");

            List<Type> types = new List<Type> { typeof(ProjectScopeInstaller), typeof(LevelCatalogLoader), updaterType };
            foreach (Type type in types)
            {
                foreach (MethodInfo method in type.GetMethods(BindingFlags.Instance | BindingFlags.Static
                             | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (method.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                    {
                        Assert.That(method.ReturnType, Is.Not.EqualTo(typeof(void)), $"{type.Name}.{method.Name}");
                    }
                }
            }
        }

        private LevelDatabaseAddressables.Entry CreateLevelGroup()
        {
            string name = "__ContentUpdateTests_Levels_" + Guid.NewGuid().ToString("N");
            m_database = LevelDatabaseAddressables.DatabaseDirectory + "/" + name;
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory,
                out LevelDatabaseAddressables.Entry entry, out string error), Is.True, error);
            return entry;
        }

        private void ConfigureWithTestUrl()
        {
            Assert.That(LevelContentUpdate.TryConfigure(m_settings, out string error), Is.True, error);
            m_settings.profileSettings.SetValue(m_settings.activeProfileId, AddressableAssetSettings.kRemoteLoadPath,
                k_testLoadPath);
        }

        private static void AssertRemoteCatalog(AddressableAssetSettings settings)
        {
            AddressableAssetProfileSettings profiles = settings.profileSettings;
            Assert.That(settings.BuildRemoteCatalog, Is.True);
            Assert.That(settings.RemoteCatalogBuildPath.Id,
                Is.EqualTo(profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteBuildPath).Id));
            Assert.That(settings.RemoteCatalogLoadPath.Id,
                Is.EqualTo(profiles.GetProfileDataByName(AddressableAssetSettings.kRemoteLoadPath).Id));
            Assert.That(settings.DisableCatalogUpdateOnStartup, Is.True, "the game checks once, explicitly");
            Assert.That(settings.CatalogRequestsTimeout, Is.EqualTo(LevelContentUpdate.RequestTimeoutSeconds));
        }

        private static void AssertLocalStaticLevelGroup(AddressableAssetSettings settings)
        {
            AddressableAssetGroup group = settings.FindGroup(LevelDatabaseAddressables.GroupName);
            Assert.That(group, Is.Not.Null);
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            Assert.That(bundled.BuildPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kLocalBuildPath));
            Assert.That(bundled.LoadPath.GetName(settings), Is.EqualTo(AddressableAssetSettings.kLocalLoadPath));
            Assert.That(group.GetSchema<ContentUpdateGroupSchema>().StaticContent, Is.True);
        }
    }
}
