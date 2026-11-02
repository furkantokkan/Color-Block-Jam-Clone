using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies that editor database creation, selection and deletion share the runtime key, and that a database's
    /// manifest and packs are registered at their derived addresses with the level-json label (DEC-196). A test-owned
    /// prefab's project scope installer stands in for ProjectContext.prefab's, which names the active database
    /// (DEC-194). Databases are folders directly under Data/Json/Levels with unique test names, deleted in TearDown.
    /// </summary>
    public sealed class LevelDatabaseAddressablesTests
    {
        private ProjectScopeInstaller m_projectScope;
        private LevelDatabaseAddressables.Entry m_entry;
        private AddressableAssetSettings m_settings;
        private string m_directory;
        private TestAssetContainer m_sentinel;
        private string m_sentinelPath;
        private byte[] m_sentinelBytes;
        private Dictionary<string, byte[]> m_protectedFiles;
        private Dictionary<UnityEngine.Object, bool> m_protectedDirty;
        private int m_groupContainerCount;
        private readonly List<string> m_databaseFolders = new List<string>();
        private const string k_addressablesDirectory = "Assets/AddressableAssetsData";

        [SetUp]
        public void SetUp()
        {
            CaptureProtectedAssets();
            m_directory = LevelDatabaseAddressables.DatabaseDirectory + "/__ScopedSaveTests_"
                + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder(LevelDatabaseAddressables.DatabaseDirectory,
                Path.GetFileName(m_directory));
            // Main settings/group assets activate vendor import hooks for the real project settings.
            // Temporary settings skip validation/bootstrap; a test-owned main asset hides subassets
            // from that hook while still giving SaveAssetIfDirty a real, test-owned file to persist.
            m_settings = AddressableAssetSettings.Create(m_directory, "Test Settings", false, false);
            Assert.That(m_settings.IsPersisted, Is.False);
            Assert.That(AssetDatabase.GetAssetPath(m_settings), Is.Empty);
            Assert.That(m_settings.groups, Is.Empty);
            TestAssetContainer container = CreateContainer("SettingsContainer");
            Assert.That(AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(container)),
                Is.EqualTo(typeof(TestAssetContainer)));
            AssetDatabase.AddObjectToAsset(m_settings, container);
            AssertIsolation();
            AssetDatabase.SaveAssetIfDirty(m_settings);
            m_projectScope = CreateProjectScopePrefab();
            m_sentinel = ScriptableObject.CreateInstance<TestAssetContainer>();
            m_sentinelPath = m_directory + "/Unrelated.asset";
            AssetDatabase.CreateAsset(m_sentinel, m_sentinelPath);
            AssetDatabase.SaveAssetIfDirty(m_sentinel);
            m_sentinelBytes = File.ReadAllBytes(m_sentinelPath);
            using SerializedObject serialized = new SerializedObject(m_sentinel);
            serialized.FindProperty("m_value").stringValue = "unsaved-sentinel";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(m_sentinel);
            m_entry = default;
            m_groupContainerCount = 0;
            AssertProtectedAssets();
        }

        [TearDown]
        public void TearDown()
        {
            List<AddressableAssetGroup> groups = m_settings != null
                ? new List<AddressableAssetGroup>(m_settings.groups)
                : new List<AddressableAssetGroup>();
            List<AddressableAssetGroupSchema> schemas = new List<AddressableAssetGroupSchema>();
            foreach (AddressableAssetGroup group in groups)
            {
                schemas.AddRange(group.Schemas);
            }

            try
            {
                foreach (string folder in m_databaseFolders)
                {
                    if (AssetDatabase.IsValidFolder(folder))
                    {
                        AssetDatabase.DeleteAsset(folder);
                    }
                }

                m_databaseFolders.Clear();
                if (!string.IsNullOrEmpty(m_directory) && AssetDatabase.IsValidFolder(m_directory))
                {
                    Assert.That(AssetDatabase.DeleteAsset(m_directory), Is.True,
                        "Could not clean the test-owned scratch folder.");
                }
            }
            finally
            {
                foreach (AddressableAssetGroupSchema schema in schemas)
                {
                    if (schema != null && !EditorUtility.IsPersistent(schema))
                    {
                        UnityEngine.Object.DestroyImmediate(schema);
                    }
                }

                foreach (AddressableAssetGroup group in groups)
                {
                    if (group != null && !EditorUtility.IsPersistent(group))
                    {
                        UnityEngine.Object.DestroyImmediate(group);
                    }
                }

                AssertProtectedAssets();
            }
        }

        [Test]
        public void CreateRegistersJsonAndSelectionSetsRuntimeGuid()
        {
            string name = Name("Test");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out m_entry,
                out string createError), Is.True, createError);

            bool listed = false;
            foreach (LevelDatabaseAddressables.Entry entry in LevelDatabaseAddressables.GetEntries(m_settings))
            {
                listed |= entry.Guid == m_entry.Guid;
            }

            Assert.That(listed, Is.True);
            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope, m_entry.Guid,
                out string selectError), Is.True, selectError);
            Assert.That(m_projectScope.LevelJsonGuid, Is.EqualTo(m_entry.Guid));
            // The installer names the new manifest and holds no levels itself (DEC-192, DEC-194); the database starts
            // empty, with no pack (DEC-196).
            Assert.That(LevelDatabaseFiles.TryRead(LevelDatabaseAddressables.GetActivePath(m_projectScope),
                out LevelDatabaseSnapshot database, out string readError), Is.True, readError);
            Assert.That(database.Levels, Is.Empty);
            Assert.That(m_entry.Name, Is.EqualTo(name));
        }

        [Test]
        public void RejectedSelectionPreservesActiveGuid()
        {
            string name = Name("Test");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out m_entry,
                out string createError), Is.True, createError);
            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope, m_entry.Guid,
                out string selectError), Is.True, selectError);

            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope,
                Guid.NewGuid().ToString("N"), out string error), Is.False);
            Assert.That(error, Does.Contain("not in"));
            Assert.That(m_projectScope.LevelJsonGuid, Is.EqualTo(m_entry.Guid));
        }

        [Test]
        public void DuplicateNameDoesNotReplaceExistingJson()
        {
            string name = Name("Test");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out m_entry,
                out string createError), Is.True, createError);
            string originalJson = File.ReadAllText(m_entry.Path);

            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out _,
                out string duplicateError), Is.False);
            Assert.That(duplicateError, Does.Contain("already exists"));
            Assert.That(File.ReadAllText(m_entry.Path), Is.EqualTo(originalJson));
        }

        [Test]
        public void InvalidJsonDoesNotChangeActiveGuid()
        {
            string name = Name("Test");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out m_entry,
                out string createError), Is.True, createError);
            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope, m_entry.Guid,
                out string selectError), Is.True, selectError);

            File.WriteAllText(m_entry.Path, "{ broken");
            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope, m_entry.Guid,
                out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(m_projectScope.LevelJsonGuid, Is.EqualTo(m_entry.Guid));
        }

        [Test]
        public void DeletingActiveDatabaseSwitchesToDefaultAndRemovesJson()
        {
            string name = Name("Test");
            Assert.That(LevelDatabaseAddressables.TrySync(m_settings,
                LevelDatabaseAddressables.DefaultManifestPath, out string defaultError), Is.True, defaultError);
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory, out m_entry,
                out string createError), Is.True, createError);
            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope, m_entry.Guid,
                out string selectError), Is.True, selectError);

            Assert.That(LevelDatabaseAddressables.TryDelete(m_settings, m_projectScope, m_entry.Guid,
                out string deleteError), Is.True, deleteError);

            Assert.That(File.Exists(m_entry.Path), Is.False);
            Assert.That(Directory.Exists(Path.GetDirectoryName(m_entry.Path)), Is.False, "The whole folder goes.");
            Assert.That(m_projectScope.LevelJsonGuid, Is.EqualTo(
                AssetDatabase.AssetPathToGUID(LevelDatabaseAddressables.DefaultManifestPath)));
            foreach (LevelDatabaseAddressables.Entry entry in LevelDatabaseAddressables.GetEntries(m_settings))
            {
                Assert.That(entry.Guid, Is.Not.EqualTo(m_entry.Guid));
            }
        }

        [Test]
        public void DefaultDatabaseCannotBeDeleted()
        {
            string defaultGuid = AssetDatabase.AssetPathToGUID(LevelDatabaseAddressables.DefaultManifestPath);

            Assert.That(LevelDatabaseAddressables.TryDelete(m_settings, m_projectScope, defaultGuid,
                out string error), Is.False);

            Assert.That(error, Does.Contain("default"));
            Assert.That(File.Exists(LevelDatabaseAddressables.DefaultManifestPath), Is.True);
        }

        [Test]
        public void DatabaseActionsSaveOwnedAssetsAndPreserveUnrelatedDirtyAsset()
        {
            AddressableAssetGroup group = m_settings.CreateGroup(
                LevelDatabaseAddressables.GroupName, false, false, false, null);
            PersistGroup(group);
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Owned"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AssertSentinelPreserved();
            Assert.That(EditorUtility.IsDirty(group), Is.False);
            Assert.That(EditorUtility.IsDirty(m_settings), Is.False);

            Assert.That(LevelDatabaseAddressables.TrySync(m_settings, m_entry.Path, out error), Is.True, error);
            Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(group)),
                Does.Contain(m_entry.Guid));
            AssertSentinelPreserved();

            Assert.That(LevelDatabaseAddressables.TrySetActive(m_settings, m_projectScope,
                m_entry.Guid, out error), Is.True, error);
            Assert.That(EditorUtility.IsDirty(m_projectScope), Is.False);
            AssertSentinelPreserved();

            Assert.That(LevelDatabaseAddressables.TrySync(m_settings,
                LevelDatabaseAddressables.DefaultManifestPath, out error), Is.True, error);
            Assert.That(LevelDatabaseAddressables.TryDelete(m_settings, m_projectScope,
                m_entry.Guid, out error), Is.True, error);
            Assert.That(EditorUtility.IsDirty(group), Is.False);
            Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(group)),
                Does.Not.Contain(m_entry.Guid));
            Assert.That(EditorUtility.IsDirty(m_projectScope), Is.False);
            AssertSentinelPreserved();
        }

        [Test]
        public void SyncRegistersTheManifestAndEveryPackAtTheirAddressWithTheLabelAndDropsUnlistedPacks()
        {
            string name = Name("Packs");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, name, LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            Assert.That(LevelDatabaseFiles.TryRead(m_entry.Path, out LevelDatabaseSnapshot empty, out error), Is.True,
                error);
            LevelData[] levels = new LevelData[101];
            for (int index = 0; index < levels.Length; index++)
            {
                levels[index] = LevelCatalogTests.CreateLevel($"sync-{index:D3}", $"Sync {index}", Vector2Int.zero);
            }

            // The pure file save, imported by hand, so only this test's settings are synced.
            Assert.That(LevelDatabaseFiles.TrySave(empty, levels, $"Temp/{name}-backup", AssetDatabase.DeleteAsset,
                out LevelDatabaseSnapshot saved, out LevelPackWriteReport report, out error), Is.True, error);
            Assert.That(report.Created, Has.Count.EqualTo(2));
            foreach (string path in report.Created)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            AssetDatabase.ImportAsset(m_entry.Path, ImportAssetOptions.ForceUpdate);
            Assert.That(LevelDatabaseAddressables.TrySync(m_settings, m_entry.Path, out error), Is.True, error);

            AddressableAssetGroup group = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            AssertRegistered(group, m_entry.Path, $"LevelDatabase/{name}/manifest.json");
            AssertRegistered(group, saved.Packs[0].Path, $"LevelDatabase/{name}/pack-001.json");
            AssertRegistered(group, saved.Packs[1].Path, $"LevelDatabase/{name}/pack-002.json");
            List<LevelDatabaseAddressables.Entry> entries = LevelDatabaseAddressables.GetEntries(m_settings);
            Assert.That(entries.FindAll(entry => entry.Guid == m_entry.Guid), Has.Count.EqualTo(1),
                "Only the manifest is listed as a database.");

            // Dropping the last level deletes pack-002; the next sync drops its entry.
            Assert.That(LevelDatabaseFiles.TrySave(saved, new List<LevelData>(levels).GetRange(0, 100),
                $"Temp/{name}-backup", AssetDatabase.DeleteAsset, out _, out report, out error), Is.True, error);
            Assert.That(report.Deleted, Is.EqualTo(new[] { saved.Packs[1].Path }));
            AssetDatabase.ImportAsset(m_entry.Path, ImportAssetOptions.ForceUpdate);
            Assert.That(LevelDatabaseAddressables.TrySync(m_settings, m_entry.Path, out error), Is.True, error);
            foreach (AddressableAssetEntry entry in group.entries)
            {
                Assert.That(entry.address, Is.Not.EqualTo($"LevelDatabase/{name}/pack-002.json"));
            }

            AssertSentinelPreserved();
        }

        private static void AssertRegistered(AddressableAssetGroup group, string path, string address)
        {
            AddressableAssetEntry entry = group.GetAssetEntry(AssetDatabase.AssetPathToGUID(path));
            Assert.That(entry, Is.Not.Null, path);
            Assert.That(entry.address, Is.EqualTo(address));
            Assert.That(entry.labels, Does.Contain(LevelJsonFiles.Label));
        }

        [Test]
        public void RegistrationCreatesValidSchemasWithoutSavingUnrelatedAssets()
        {
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Owned"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AddressableAssetGroup group = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();
            Assert.That(bundled, Is.Not.Null);
            Assert.That(bundled.Group, Is.SameAs(group));
            Assert.That(bundled.BuildPath.GetValue(m_settings), Is.Not.Empty);
            Assert.That(bundled.LoadPath.GetValue(m_settings), Is.Not.Empty);
            Assert.That(bundled.GetBundleCachedProviderId(), Does.Contain("AssetBundleProvider"));
            Assert.That(bundled.GetAssetCachedProviderId(), Does.Contain("BundledAssetProvider"));
            Assert.That(group.GetSchema<ContentUpdateGroupSchema>().Group, Is.SameAs(group));
            foreach (AddressableAssetGroupSchema schema in group.Schemas)
            {
                Assert.That(AssetDatabase.GetAssetPath(schema), Is.Empty,
                    "Temporary settings must keep newly created schemas transient.");
            }

            AssertSentinelPreserved();
        }

        [Test]
        public void CreatedGroupKeepsShippedLevelsLocalAndStatic()
        {
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Owned"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AddressableAssetGroup group = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            BundledAssetGroupSchema bundled = group.GetSchema<BundledAssetGroupSchema>();

            Assert.That(bundled.BuildPath.GetName(m_settings), Is.EqualTo(AddressableAssetSettings.kLocalBuildPath));
            Assert.That(bundled.LoadPath.GetName(m_settings), Is.EqualTo(AddressableAssetSettings.kLocalLoadPath));
            Assert.That(group.GetSchema<ContentUpdateGroupSchema>().StaticContent, Is.True,
                "the shipped level JSON must be Cannot Change Post Release (DEC-162)");
            AssertSentinelPreserved();
        }

        [Test]
        public void RegistrationSavesSourceGroupAfterMovingEntry()
        {
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Owned"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AddressableAssetGroup source = m_settings.CreateGroup("Source", false, false, false, null);
            PersistGroup(source);
            m_settings.CreateOrMoveEntry(m_entry.Guid, source);
            EditorUtility.SetDirty(source);
            AssetDatabase.SaveAssetIfDirty(source);
            AddressableAssetGroup destination = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            PersistGroup(destination);
            AssetDatabase.SaveAssetIfDirty(destination);
            AssetDatabase.SaveAssetIfDirty(m_settings);
            Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(source)),
                Does.Contain(m_entry.Guid));

            Assert.That(LevelDatabaseAddressables.TrySync(m_settings, m_entry.Path, out error), Is.True, error);
            Assert.That(source.GetAssetEntry(m_entry.Guid), Is.Null);
            Assert.That(File.ReadAllText(AssetDatabase.GetAssetPath(source)),
                Does.Not.Contain(m_entry.Guid));
            Assert.That(EditorUtility.IsDirty(source), Is.False);
            Assert.That(EditorUtility.IsDirty(destination), Is.False);
            AssertSentinelPreserved();
        }

        [Test]
        public void MissingGroupClonesDefaultSchemasWithoutSavingTheirDirtySources()
        {
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Seed"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AddressableAssetGroup source = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            PersistGroup(source);
            // Name's vendor setter moves the main asset and can rename the real project group by
            // filename. Change this test-owned subasset's serialized label without moving its container.
            using (SerializedObject serialized = new SerializedObject(source))
            {
                serialized.FindProperty("m_GroupName").stringValue = "Seed Group";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            source.name = "Seed Group";
            PersistSchemas(source);
            m_settings.DefaultGroup = source;
            AssetDatabase.SaveAssetIfDirty(source);
            AssetDatabase.SaveAssetIfDirty(m_settings);
            BundledAssetGroupSchema sourceSchema = source.GetSchema<BundledAssetGroupSchema>();
            AssetDatabase.SaveAssetIfDirty(sourceSchema);
            string sourcePath = AssetDatabase.GetAssetPath(sourceSchema);
            byte[] sourceBytes = File.ReadAllBytes(sourcePath);
            sourceSchema.IncludeInBuild = !sourceSchema.IncludeInBuild;
            EditorUtility.SetDirty(sourceSchema);

            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Clone"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out error), Is.True, error);
            AddressableAssetGroup group = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            BundledAssetGroupSchema clone = group.GetSchema<BundledAssetGroupSchema>();
            Assert.That(clone, Is.Not.SameAs(sourceSchema));
            Assert.That(clone.Group, Is.SameAs(group));
            Assert.That(clone.IncludeInBuild, Is.EqualTo(sourceSchema.IncludeInBuild));
            Assert.That(clone.BuildPath.GetValue(m_settings), Is.EqualTo(sourceSchema.BuildPath.GetValue(m_settings)));
            Assert.That(clone.LoadPath.GetValue(m_settings), Is.EqualTo(sourceSchema.LoadPath.GetValue(m_settings)));
            Assert.That(AssetDatabase.GetAssetPath(clone), Is.Empty);
            Assert.That(EditorUtility.IsDirty(sourceSchema), Is.True);
            Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(sourceBytes));
            AssertSentinelPreserved();
        }

        [Test]
        public void RegisterAndDeletePreserveExistingDirtySchema()
        {
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, Name("Owned"), LevelDatabaseAddressables.DatabaseDirectory,
                out m_entry, out string error), Is.True, error);
            AddressableAssetGroup group = m_settings.FindGroup(LevelDatabaseAddressables.GroupName);
            PersistGroup(group);
            PersistSchemas(group);
            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            string path = AssetDatabase.GetAssetPath(schema);
            byte[] original = File.ReadAllBytes(path);
            schema.IncludeInBuild = !schema.IncludeInBuild;
            EditorUtility.SetDirty(schema);
            Assert.That(LevelDatabaseAddressables.TrySync(m_settings, m_entry.Path, out error), Is.True, error);
            Assert.That(EditorUtility.IsDirty(schema), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(LevelDatabaseAddressables.TryDelete(m_settings, m_projectScope,
                m_entry.Guid, out error), Is.True, error);
            Assert.That(EditorUtility.IsDirty(schema), Is.True);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            AssertSentinelPreserved();
        }

        [Test]
        public void MissingSettingsRefusesCreationAndRegistrationWithoutWrites()
        {
            string name = Name("Missing");
            Assert.That(LevelDatabaseAddressables.TryCreate(null, name, LevelDatabaseAddressables.DatabaseDirectory,
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("Configure Addressables"));
            Assert.That(Directory.Exists(LevelDatabaseAddressables.DatabaseDirectory + "/" + name), Is.False);
            Assert.That(LevelDatabaseAddressables.TrySync(null,
                LevelDatabaseAddressables.DefaultManifestPath, out error), Is.False);
            Assert.That(error, Does.Contain("Configure Addressables"));
            Assert.That(m_settings.groups, Is.Empty);
            AssertSentinelPreserved();
        }

        [Test]
        public void InvalidDirectoryRefusesCreationWithoutWrites()
        {
            string directory = "Library/__ScopedSaveTests_" + Guid.NewGuid().ToString("N");
            Assert.That(LevelDatabaseAddressables.TryCreate(m_settings, "Invalid", directory,
                out _, out string error), Is.False);
            Assert.That(error, Does.Contain("must be under"));
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(m_settings.groups, Is.Empty);
            AssertSentinelPreserved();
        }

        // A unique database name under Data/Json/Levels; TearDown deletes its folder.
        private string Name(string label)
        {
            string name = "__ScopedSaveTests_" + label + "_" + Guid.NewGuid().ToString("N");
            m_databaseFolders.Add(LevelDatabaseAddressables.DatabaseDirectory + "/" + name);
            return name;
        }

        private void AssertSentinelPreserved()
        {
            AssertIsolation();
            AssertProtectedAssets();
            Assert.That(EditorUtility.IsDirty(m_sentinel), Is.True);
            Assert.That(m_sentinel.Value, Is.EqualTo("unsaved-sentinel"));
            Assert.That(File.ReadAllBytes(m_sentinelPath), Is.EqualTo(m_sentinelBytes));
        }

        private TestAssetContainer CreateContainer(string name)
        {
            TestAssetContainer container = ScriptableObject.CreateInstance<TestAssetContainer>();
            string path = m_directory + "/" + name + ".asset";
            Assert.That(path.StartsWith(m_directory + "/", StringComparison.Ordinal), Is.True);
            AssetDatabase.CreateAsset(container, path);
            return container;
        }

        // A test-owned prefab whose installer stands in for ProjectContext.prefab's: a database choice saves it.
        private ProjectScopeInstaller CreateProjectScopePrefab()
        {
            GameObject source = new GameObject("ProjectScope");
            try
            {
                source.AddComponent<ProjectScopeInstaller>();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, m_directory + "/ProjectScope.prefab");
                return prefab.GetComponent<ProjectScopeInstaller>();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private void PersistGroup(AddressableAssetGroup group)
        {
            AssertIsolation();
            Assert.That(group.Settings, Is.SameAs(m_settings));
            Assert.That(AssetDatabase.GetAssetPath(group), Is.Empty);
            TestAssetContainer container = CreateContainer("GroupContainer" + m_groupContainerCount++);
            Assert.That(AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(container)),
                Is.EqualTo(typeof(TestAssetContainer)));
            AssetDatabase.AddObjectToAsset(group, container);
            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssetIfDirty(group);
        }

        private void PersistSchemas(AddressableAssetGroup group)
        {
            AssertIsolation();
            foreach (AddressableAssetGroupSchema schema in group.Schemas)
            {
                Assert.That(schema.Group, Is.SameAs(group));
                Assert.That(AssetDatabase.GetAssetPath(schema), Is.Empty);
                TestAssetContainer container = CreateContainer("SchemaContainer" + m_groupContainerCount++);
                Assert.That(AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(container)),
                    Is.EqualTo(typeof(TestAssetContainer)));
                AssetDatabase.AddObjectToAsset(schema, container);
                EditorUtility.SetDirty(schema);
                AssetDatabase.SaveAssetIfDirty(schema);
            }

            EditorUtility.SetDirty(group);
            AssetDatabase.SaveAssetIfDirty(group);
        }

        private void AssertIsolation()
        {
            Assert.That(m_settings.IsPersisted, Is.False,
                "Temporary settings must never call the vendor's persisted group creation path.");
            string path = AssetDatabase.GetAssetPath(m_settings);
            Assert.That(path, Is.EqualTo(m_directory + "/SettingsContainer.asset"));
            Assert.That(m_settings.AssetPath.Replace('\\', '/'), Is.EqualTo(path));
            Assert.That(m_settings.ConfigFolder.Replace('\\', '/'), Is.EqualTo(m_directory));
            Assert.That(AssetDatabase.GetMainAssetTypeAtPath(path), Is.EqualTo(typeof(TestAssetContainer)));
            foreach (AddressableAssetGroup group in m_settings.groups)
            {
                Assert.That(group.Settings, Is.SameAs(m_settings));
                string groupPath = AssetDatabase.GetAssetPath(group);
                if (!string.IsNullOrEmpty(groupPath))
                {
                    Assert.That(groupPath, Does.StartWith(m_directory + "/"));
                    Assert.That(AssetDatabase.GetMainAssetTypeAtPath(groupPath),
                        Is.EqualTo(typeof(TestAssetContainer)));
                }
            }
        }

        private void CaptureProtectedAssets()
        {
            m_protectedFiles = new Dictionary<string, byte[]>();
            m_protectedDirty = new Dictionary<UnityEngine.Object, bool>();
            foreach (string path in Directory.GetFiles(k_addressablesDirectory, "*", SearchOption.AllDirectories))
            {
                m_protectedFiles.Add(path, File.ReadAllBytes(path));
                if (path.EndsWith(".asset", StringComparison.Ordinal))
                {
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path.Replace('\\', '/')))
                    {
                        m_protectedDirty[asset] = EditorUtility.IsDirty(asset);
                    }
                }
            }
        }

        private void AssertProtectedAssets()
        {
            Assert.That(m_protectedFiles, Is.Not.Null);
            CollectionAssert.AreEquivalent(m_protectedFiles.Keys,
                Directory.GetFiles(k_addressablesDirectory, "*", SearchOption.AllDirectories),
                "The authored Addressables file inventory changed.");
            foreach (KeyValuePair<string, byte[]> file in m_protectedFiles)
            {
                Assert.That(File.ReadAllBytes(file.Key), Is.EqualTo(file.Value), file.Key);
            }

            foreach (KeyValuePair<UnityEngine.Object, bool> asset in m_protectedDirty)
            {
                Assert.That(asset.Key != null, Is.True, "An authored Addressables object was destroyed.");
                Assert.That(EditorUtility.IsDirty(asset.Key), Is.EqualTo(asset.Value),
                    AssetDatabase.GetAssetPath(asset.Key));
            }
        }
    }
}
