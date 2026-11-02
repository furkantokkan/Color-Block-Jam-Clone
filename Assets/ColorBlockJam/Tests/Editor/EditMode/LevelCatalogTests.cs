using System;
using System.Threading.Tasks;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies LevelCatalog's JSON load, reload-failure recovery, its manifest and resident packs (DEC-196), the game
    /// load's refusals and level validation.
    /// </summary>
    public sealed class LevelCatalogTests
    {
        private const string k_twoPacks = "{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [ "
            + "{ \"file\": \"pack-001.json\", \"levels\": [ \"level-1\", \"level-2\" ] }, "
            + "{ \"file\": \"pack-002.json\", \"levels\": [ \"level-3\" ] } ] }";

        private LevelCatalog m_levels;

        [SetUp]
        public void SetUp()
        {
            m_levels = new LevelCatalog();
        }

        [Test]
        public void LoadKeepsJsonOrderAndReturnsCopies()
        {
            AssertLoad(CreateJson(
                CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-2", "Second", new Vector2Int(1, 0))));

            Assert.That(m_levels.Count, Is.EqualTo(2));
            Assert.That(m_levels.GetAt(0).Id, Is.EqualTo("level-1"));
            Assert.That(m_levels.GetAt(1).Id, Is.EqualTo("level-2"));
            Assert.That(m_levels.IndexOf("level-2"), Is.EqualTo(1));
            Assert.That(m_levels.GetAt(m_levels.IndexOf("level-2")).Name, Is.EqualTo("Second"));
            Assert.That(m_levels.IndexOf("missing"), Is.EqualTo(-1));
            Assert.That(m_levels.IsResident(0) && m_levels.IsResident(1), Is.True,
                "A single-file level JSON is one resident pack.");

            LevelData firstCopy = m_levels.GetAt(0);
            LevelData secondCopy = m_levels.GetAt(0);
            Assert.That(firstCopy, Is.Not.SameAs(secondCopy));
            Assert.That(firstCopy.GetBlockAt(0), Is.Not.SameAs(secondCopy.GetBlockAt(0)));
        }

        [Test]
        public void FailedReloadClearsOldLevelCache()
        {
            AssertLoad(CreateJson(CreateLevel("level-1", "First", Vector2Int.zero)));
            Assert.That(m_levels.Count, Is.EqualTo(1));

            Assert.That(m_levels.LoadJson("{ broken", out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.Throws<InvalidOperationException>(() => { _ = m_levels.Count; });
        }

        [Test]
        public void MissingSourceAndEmptyDatabaseHaveClearErrors()
        {
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await LevelCatalogLoader.LoadManifestAsync(m_levels));
            Assert.That(exception.Message, Does.Contain("missing"));

            AssertLoad(CreateJson());
            Assert.That(m_levels.Count, Is.Zero);
        }

        [Test]
        public void JsonGuidNamesTheLevelJsonAndIsNullForText()
        {
            LevelCatalog named = new LevelCatalog("json-guid");
            Assert.That(named.LoadJson(CreateJson(CreateLevel("level-1", "First", Vector2Int.zero)), out string error),
                Is.True, error);

            Assert.That(named.JsonGuid, Is.EqualTo("json-guid"));
            Assert.That(m_levels.JsonGuid, Is.Null);
        }

        [Test]
        public void ManifestGivesCountAndIndicesWithNoPackResident()
        {
            AssertManifest(k_twoPacks);

            Assert.That(m_levels.Count, Is.EqualTo(3));
            Assert.That(m_levels.IndexOf("level-3"), Is.EqualTo(2));
            Assert.That(m_levels.Manifest.GetPackIndex(2), Is.EqualTo(1));
            Assert.That(m_levels.IsResident(0), Is.False);
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => m_levels.GetAt(2));
            Assert.That(exception.Message, Does.Contain("level-3").And.Contain("pack-002.json"));
        }

        [Test]
        public void LoadedPackIsResidentAndReleaseKeepsOnlyOnePack()
        {
            AssertManifest(k_twoPacks);

            Assert.That(m_levels.LoadPackJson(1, CreateJson(CreateLevel("level-3", "Third", Vector2Int.zero)),
                LevelReadMode.Runtime, out string error), Is.True, error);
            Assert.That(m_levels.GetAt(2).Name, Is.EqualTo("Third"));
            Assert.That(m_levels.IsResident(0), Is.False);

            Assert.That(m_levels.LoadPackJson(0, CreateJson(CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-2", "Second", Vector2Int.zero)), LevelReadMode.Runtime, out error), Is.True, error);
            m_levels.ReleasePacksExcept(0);

            Assert.That(m_levels.IsResident(2), Is.False);
            Assert.That(m_levels.GetAt(1).Id, Is.EqualTo("level-2"));
        }

        [Test]
        public void PackWhoseLevelsDifferFromTheManifestIsRefusedAndChangesNothing()
        {
            AssertManifest(k_twoPacks);

            Assert.That(m_levels.LoadPackJson(1, CreateJson(CreateLevel("level-3", "Third", Vector2Int.zero),
                CreateLevel("level-4", "Fourth", Vector2Int.zero)), LevelReadMode.Authoring, out string error), Is.False);
            Assert.That(error, Is.EqualTo("Pack pack-002.json holds 2 level(s), but the manifest lists 1."));

            Assert.That(m_levels.LoadPackJson(1, CreateJson(CreateLevel("level-9", "Other", Vector2Int.zero)),
                LevelReadMode.Authoring, out error), Is.False);
            Assert.That(error, Is.EqualTo("Pack pack-002.json holds level level-9 where the manifest lists level-3."));

            Assert.That(m_levels.IsResident(2), Is.False);
            Assert.That(m_levels.Count, Is.EqualTo(3));
        }

        [Test]
        public void LoadLevelsHoldsAWholeDatabaseAndRefusesLevelsOutOfOrder()
        {
            Assert.That(LevelManifestJson.TryRead(k_twoPacks, out LevelManifest manifest, out string error), Is.True,
                error);
            LevelData[] levels =
            {
                CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-2", "Second", Vector2Int.zero),
                CreateLevel("level-3", "Third", Vector2Int.zero)
            };

            Assert.That(m_levels.LoadLevels(manifest, levels, out error), Is.True, error);
            Assert.That(m_levels.IsResident(0) && m_levels.IsResident(2), Is.True);
            Assert.That(m_levels.GetAt(2), Is.Not.SameAs(levels[2]), "The catalog hands out copies.");

            Assert.That(m_levels.LoadLevels(manifest, new[] { levels[1], levels[0], levels[2] }, out error), Is.False);
            Assert.That(error, Does.Contain("level-2").And.Contain("level-1"));
            Assert.Throws<InvalidOperationException>(() => { _ = m_levels.Count; });
        }

        [Test]
        public void PackLoadReturnsAtOnceForAResidentLevelAndNamesAnIndexOutsideTheDatabase()
        {
            AssertLoad(CreateJson(CreateLevel("level-1", "First", Vector2Int.zero)));

            Task<string> resident = LevelCatalogLoader.LoadPackAsync(m_levels, 0);
            Assert.That(resident.IsCompleted, Is.True, "A resident pack needs no Addressables load.");
            Assert.That(resident.Result, Is.Null);
            Assert.That(LevelCatalogLoader.LoadPackAsync(m_levels, 5).Result, Does.Contain("outside"));
        }

        [Test]
        public void FailedManifestLoadLeavesTheCatalogUnloaded()
        {
            AssertLoad(CreateJson(CreateLevel("level-1", "First", Vector2Int.zero)));

            Assert.That(m_levels.LoadManifestJson(CreateJson(CreateLevel("level-1", "First", Vector2Int.zero)),
                out string error), Is.False, "A level JSON is no manifest.");
            Assert.That(error, Does.Contain("formatVersion 1"));
            Assert.Throws<InvalidOperationException>(() => { _ = m_levels.Count; });
        }

        [Test]
        public void LevelValidationRejectsOverlappingBlocksAndDoors()
        {
            LevelData overlappingBlocks = new LevelData(
                "level-1", "Overlap", 3, 3, 60,
                new LevelDoorData[0],
                new[]
                {
                    CreateBlock("block-1", Vector2Int.zero),
                    CreateBlock("block-2", Vector2Int.zero)
                });

            Assert.That(overlappingBlocks.Validate(out string blockError), Is.False);
            Assert.That(blockError, Does.Contain("overlap"));

            LevelData overlappingDoors = new LevelData(
                "level-2", "Doors", 3, 3, 60,
                new[]
                {
                    new LevelDoorData("door-1", BoardSide.Top, 0, 2, "Red"),
                    new LevelDoorData("door-2", BoardSide.Top, 1, 2, "Blue")
                },
                new[] { CreateBlock("block-1", Vector2Int.zero) });

            Assert.That(overlappingDoors.Validate(out string doorError), Is.False);
            Assert.That(doorError, Does.Contain("overlap"));
        }

        [Test]
        public void LevelValidationRejectsDisconnectedAndOutsideCells()
        {
            LevelData disconnected = new LevelData(
                "level-1", "Disconnected", 3, 3, 60,
                new LevelDoorData[0],
                new[]
                {
                    new LevelBlockData("block-1", "Red", Vector2Int.zero,
                        new[] { Vector2Int.zero, new Vector2Int(2, 0) })
                });
            Assert.That(disconnected.Validate(out string disconnectedError), Is.False);
            Assert.That(disconnectedError, Does.Contain("disconnected"));

            LevelData outside = CreateLevel("level-2", "Outside", new Vector2Int(3, 0));
            Assert.That(outside.Validate(out string outsideError), Is.False);
            Assert.That(outsideError, Does.Contain("outside"));
        }

        private void AssertLoad(string json)
        {
            Assert.That(m_levels.LoadJson(json, out string error), Is.True, error);
        }

        private void AssertManifest(string json)
        {
            Assert.That(m_levels.LoadManifestJson(json, out string error), Is.True, error);
        }

        private static string CreateJson(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error),
                Is.True, error);
            return json;
        }

        // Also used by LevelDraftIssuesTests, which needs the same 3 x 3 one-block fixture.
        internal static LevelData CreateLevel(string id, string name, Vector2Int origin)
        {
            return new LevelData(id, name, 3, 3, 60,
                new LevelDoorData[0], new[] { CreateBlock("block-1", origin) });
        }

        private static LevelBlockData CreateBlock(string id, Vector2Int origin)
        {
            return LevelTestData.SingleBlock(id, origin);
        }
    }
}
