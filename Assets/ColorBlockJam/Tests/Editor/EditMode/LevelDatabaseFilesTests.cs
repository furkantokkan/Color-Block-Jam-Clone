using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the level database files in a temporary folder (DEC-196): an empty database, a save that writes only the
    /// packs it changes and the manifest when the level list changes, the external-change refusal, the whole-database
    /// backup behind Restore Previous Save, the manifest-to-pack check and the deletion of leftover packs.
    /// </summary>
    public sealed class LevelDatabaseFilesTests
    {
        private string m_root;
        private string m_backup;

        [SetUp]
        public void SetUp()
        {
            m_root = Path.Combine(Path.GetTempPath(), "cbj-level-database-" + Guid.NewGuid().ToString("N"))
                .Replace('\\', '/');
            Directory.CreateDirectory(m_root);
            m_backup = $"{m_root}/Backups/manifest-guid";
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(m_root))
            {
                Directory.Delete(m_root, true);
            }
        }

        [Test]
        public void CreateMakesAnEmptyDatabaseThatReadsBack()
        {
            LevelDatabaseSnapshot snapshot = Create();

            Assert.That(snapshot.ManifestPath, Is.EqualTo($"{m_root}/Main/manifest.json"));
            Assert.That(snapshot.Folder, Is.EqualTo($"{m_root}/Main"));
            Assert.That(snapshot.Levels.Count, Is.Zero);
            Assert.That(snapshot.Packs.Count, Is.Zero);
            Assert.That(LevelDatabaseFiles.TryCreate(m_root, "Bad Name", out _, out string error), Is.False);
            Assert.That(error, Does.Contain("letters, digits"));
        }

        [Test]
        public void FirstSaveWritesOnePackAndTheManifest()
        {
            LevelData[] levels = Levels(1, 3);

            LevelPackWriteReport report = Save(Create(), levels, out LevelDatabaseSnapshot saved);

            Assert.That(report.Created, Is.EqualTo(new[] { $"{m_root}/Main/pack-001.json" }));
            Assert.That(report.Written, Is.Empty);
            Assert.That(report.ManifestWritten, Is.True);
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string expected, out string error), Is.True, error);
            Assert.That(File.ReadAllText($"{m_root}/Main/pack-001.json"), Is.EqualTo(expected),
                "A pack is the canonical level JSON of its levels.");
            AssertLevels(Read(), "level-0001", "level-0002", "level-0003");
            Assert.That(saved.Manifest.Count, Is.EqualTo(3));
        }

        [Test]
        public void AnEditRewritesOnlyItsPack()
        {
            Save(Create(), Levels(1, 150), out LevelDatabaseSnapshot first);
            string firstPack = File.ReadAllText($"{m_root}/Main/pack-001.json");
            string manifest = File.ReadAllText($"{m_root}/Main/manifest.json");
            List<LevelData> edited = new List<LevelData>(first.Levels);
            edited[120] = LevelCatalogTests.CreateLevel("level-0121", "Renamed", Vector2Int.zero);

            LevelPackWriteReport report = Save(first, edited, out _);

            Assert.That(report.Written, Is.EqualTo(new[] { $"{m_root}/Main/pack-002.json" }));
            Assert.That(report.Created, Is.Empty);
            Assert.That(report.ManifestWritten, Is.False, "The level list did not change.");
            Assert.That(File.ReadAllText($"{m_root}/Main/pack-001.json"), Is.EqualTo(firstPack));
            Assert.That(File.ReadAllText($"{m_root}/Main/manifest.json"), Is.EqualTo(manifest));
            Assert.That(Read().Levels[120].Name, Is.EqualTo("Renamed"));
        }

        [Test]
        public void AppendAndDeleteRewriteThePackAndTheManifestAndAnEmptiedPackGoes()
        {
            Save(Create(), Levels(1, 100), out LevelDatabaseSnapshot first);

            List<LevelData> appended = new List<LevelData>(first.Levels) { Level(101) };
            LevelPackWriteReport append = Save(first, appended, out LevelDatabaseSnapshot second);
            Assert.That(append.Created, Is.EqualTo(new[] { $"{m_root}/Main/pack-002.json" }));
            Assert.That(append.Written, Is.Empty, "The full first pack stays as it is.");
            Assert.That(append.ManifestWritten, Is.True);

            List<LevelData> deleted = new List<LevelData>(second.Levels);
            deleted.RemoveAt(100);
            LevelPackWriteReport delete = Save(second, deleted, out _);
            Assert.That(delete.Deleted, Is.EqualTo(new[] { $"{m_root}/Main/pack-002.json" }));
            Assert.That(delete.ManifestWritten, Is.True);
            Assert.That(File.Exists($"{m_root}/Main/pack-002.json"), Is.False);
            Assert.That(Read().Manifest.PackCount, Is.EqualTo(1));
        }

        [Test]
        public void AnExternalChangeRefusesTheSaveAndWritesNothing()
        {
            Save(Create(), Levels(1, 2), out LevelDatabaseSnapshot saved);
            string pack = $"{m_root}/Main/pack-001.json";
            File.WriteAllText(pack, File.ReadAllText(pack) + " ");
            string changed = File.ReadAllText(pack);

            Assert.That(LevelDatabaseFiles.TrySave(saved, Levels(1, 3), m_backup, null, out LevelDatabaseSnapshot next,
                out LevelPackWriteReport report, out string error), Is.False);

            Assert.That(error, Is.EqualTo(LevelDatabaseFiles.ExternalChangeError));
            Assert.That(next, Is.Null);
            Assert.That(report, Is.Null);
            Assert.That(File.ReadAllText(pack), Is.EqualTo(changed));
        }

        [Test]
        public void TheBackupHoldsThePreviousSaveAndASaveWithoutChangesKeepsIt()
        {
            Save(Create(), Levels(1, 2), out LevelDatabaseSnapshot first);
            Assert.That(LevelDatabaseFiles.HasBackup(m_backup), Is.True, "The first save backs up the empty database.");

            List<LevelData> edited = new List<LevelData>(first.Levels)
            {
                [1] = LevelCatalogTests.CreateLevel("level-0002", "Renamed", Vector2Int.zero)
            };
            Save(first, edited, out LevelDatabaseSnapshot second);
            Save(second, second.Levels, out _);

            Assert.That(LevelDatabaseFiles.TryReadBackup(m_backup, out LevelData[] previous, out string error), Is.True,
                error);
            Assert.That(previous.Length, Is.EqualTo(2));
            Assert.That(previous[1].Name, Is.EqualTo("Level 2"), "The unchanged save kept the backup of the edit.");
            Assert.That(LevelDatabaseFiles.TryReadBackup($"{m_root}/Nothing", out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("There is no previous save to restore."));
        }

        [Test]
        public void ReadRefusesAPackThatDoesNotMatchItsManifestAndAFolderWithAnotherName()
        {
            Save(Create(), Levels(1, 2), out _);
            Assert.That(LevelDatabaseJson.TryWrite(Levels(1, 1), out string onePack, out string error), Is.True, error);
            File.WriteAllText($"{m_root}/Main/pack-001.json", onePack);

            Assert.That(LevelDatabaseFiles.TryRead($"{m_root}/Main/manifest.json", out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("pack-001.json: Pack pack-001.json holds 1 level(s), but the manifest lists 2."));

            Directory.CreateDirectory($"{m_root}/Other");
            File.Copy($"{m_root}/Main/manifest.json", $"{m_root}/Other/manifest.json");
            Assert.That(LevelDatabaseFiles.TryRead($"{m_root}/Other/manifest.json", out _, out error), Is.False);
            Assert.That(error, Does.Contain("names the database Main"));
        }

        [Test]
        public void LeftoverPacksOfAnInterruptedSaveAreDeletedByTheNextSave()
        {
            Save(Create(), Levels(1, 2), out LevelDatabaseSnapshot saved);
            File.WriteAllText($"{m_root}/Main/pack-009.json", "{}");
            File.WriteAllText($"{m_root}/Main/pack-009.json.meta", "meta");
            File.WriteAllText($"{m_root}/Main/notes.json", "{}");

            LevelPackWriteReport report = Save(saved, Levels(1, 3), out _);

            Assert.That(report.Deleted, Is.EqualTo(new[] { $"{m_root}/Main/pack-009.json" }));
            Assert.That(File.Exists($"{m_root}/Main/pack-009.json"), Is.False);
            Assert.That(File.Exists($"{m_root}/Main/pack-009.json.meta"), Is.False);
            Assert.That(File.Exists($"{m_root}/Main/notes.json"), Is.True, "Only pack-NNN files are packs.");
        }

        [Test]
        public void SaveRefusesARepeatedIdAndABackupInsideTheDatabase()
        {
            LevelDatabaseSnapshot snapshot = Create();

            Assert.That(LevelDatabaseFiles.TrySave(snapshot, new[] { Level(1), Level(1) }, m_backup, null, out _,
                out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Level ID level-0001 appears more than once."));

            Assert.That(LevelDatabaseFiles.TrySave(snapshot, Levels(1, 1), $"{m_root}/Main/backup", null, out _,
                out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("A separate backup folder is required for level saves."));
            Assert.That(Directory.GetFiles($"{m_root}/Main"), Has.Length.EqualTo(1), "Nothing but the manifest.");
        }

        private LevelDatabaseSnapshot Create()
        {
            Assert.That(LevelDatabaseFiles.TryCreate(m_root, "Main", out string manifestPath, out string error), Is.True,
                error);
            Assert.That(LevelDatabaseFiles.TryRead(manifestPath, out LevelDatabaseSnapshot snapshot, out error), Is.True,
                error);
            return snapshot;
        }

        private LevelDatabaseSnapshot Read()
        {
            Assert.That(LevelDatabaseFiles.TryRead($"{m_root}/Main/manifest.json", out LevelDatabaseSnapshot snapshot,
                out string error), Is.True, error);
            return snapshot;
        }

        private LevelPackWriteReport Save(LevelDatabaseSnapshot expected, IReadOnlyList<LevelData> levels,
            out LevelDatabaseSnapshot saved)
        {
            Assert.That(LevelDatabaseFiles.TrySave(expected, levels, m_backup, null, out saved,
                out LevelPackWriteReport report, out string error), Is.True, error);
            return report;
        }

        private static LevelData[] Levels(int first, int last)
        {
            LevelData[] levels = new LevelData[last - first + 1];
            for (int index = 0; index < levels.Length; index++)
            {
                levels[index] = Level(first + index);
            }

            return levels;
        }

        private static LevelData Level(int number)
        {
            return LevelCatalogTests.CreateLevel($"level-{number:D4}", $"Level {number}", Vector2Int.zero);
        }

        private static void AssertLevels(LevelDatabaseSnapshot snapshot, params string[] ids)
        {
            Assert.That(snapshot.Levels.Count, Is.EqualTo(ids.Length));
            for (int index = 0; index < ids.Length; index++)
            {
                Assert.That(snapshot.Levels[index].Id, Is.EqualTo(ids[index]));
            }
        }
    }
}
