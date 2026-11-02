using System.Collections.Generic;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the pack rule of a level database save (DEC-196): a saved level keeps its pack, an inserted level joins
    /// its neighbour's pack, appended levels fill the last pack to 100 before a new one opens, emptied packs go, and a
    /// new pack takes the next number.
    /// </summary>
    public sealed class LevelPackLayoutTests
    {
        [Test]
        public void ANewDatabaseFillsPacksOfAHundredInOrder()
        {
            LevelManifest planned = Plan(null, Ids(1, 250));

            AssertPacks(planned, ("pack-001.json", 0, 100), ("pack-002.json", 100, 100), ("pack-003.json", 200, 50));
        }

        [Test]
        public void AnEditKeepsEveryLevelInItsPack()
        {
            LevelManifest current = Plan(null, Ids(1, 150));

            LevelManifest planned = Plan(current, Ids(1, 150));

            AssertPacks(planned, ("pack-001.json", 0, 100), ("pack-002.json", 100, 50));
        }

        [Test]
        public void AppendedLevelsFillTheLastPackBeforeANewOneOpens()
        {
            LevelManifest current = Plan(null, Ids(1, 150));

            LevelManifest planned = Plan(current, Ids(1, 210));

            AssertPacks(planned, ("pack-001.json", 0, 100), ("pack-002.json", 100, 100), ("pack-003.json", 200, 10));
        }

        [Test]
        public void ADeletedLevelLeavesTheOtherPacksAsTheyWereAndAnEmptiedPackGoes()
        {
            LevelManifest current = Manifest(("pack-001.json", new[] { "a", "b", "c" }), ("pack-002.json", new[] { "d" }),
                ("pack-003.json", new[] { "e", "f" }));

            LevelManifest withoutB = Plan(current, new[] { "a", "c", "d", "e", "f" });
            AssertPacks(withoutB, ("pack-001.json", 0, 2), ("pack-002.json", 2, 1), ("pack-003.json", 3, 2));

            LevelManifest withoutD = Plan(current, new[] { "a", "b", "c", "e", "f" });
            AssertPacks(withoutD, ("pack-001.json", 0, 3), ("pack-003.json", 3, 2));
        }

        [Test]
        public void ALevelInsertedAmongSavedOnesJoinsThePackOfTheLevelBeforeIt()
        {
            LevelManifest current = Manifest(("pack-001.json", new[] { "a", "b" }), ("pack-002.json", new[] { "d", "e" }));

            LevelManifest planned = Plan(current, new[] { "a", "b", "c", "d", "e" });

            AssertPacks(planned, ("pack-001.json", 0, 3), ("pack-002.json", 3, 2));
            Assert.That(planned.GetLevelIdAt(2), Is.EqualTo("c"));
        }

        [Test]
        public void ALevelInsertedBeforeTheFirstJoinsItsPack()
        {
            LevelManifest current = Manifest(("pack-001.json", new[] { "a", "b" }), ("pack-002.json", new[] { "d" }));

            LevelManifest planned = Plan(current, new[] { "x", "a", "b", "d" });

            AssertPacks(planned, ("pack-001.json", 0, 3), ("pack-002.json", 3, 1));
            Assert.That(planned.GetLevelIdAt(0), Is.EqualTo("x"));
        }

        [Test]
        public void ANewPackTakesTheNumberAfterTheHighestPack()
        {
            LevelManifest current = Manifest(("pack-007.json", Ids(1, 100)));

            LevelManifest planned = Plan(current, Ids(1, 101));

            AssertPacks(planned, ("pack-007.json", 0, 100), ("pack-008.json", 100, 1));
        }

        [Test]
        public void AReorderKeepsPacksContiguous()
        {
            LevelManifest current = Manifest(("pack-001.json", new[] { "a", "b" }), ("pack-002.json", new[] { "c", "d" }));

            LevelManifest planned = Plan(current, new[] { "c", "a", "b", "d" });

            AssertPacks(planned, ("pack-002.json", 0, 4));
        }

        [Test]
        public void ARepeatedIdIsRefusedAndPackNamesParse()
        {
            Assert.That(LevelPackLayout.TryPlan("Main", null, new[] { "a", "a" }, out LevelManifest planned,
                out string error), Is.False);
            Assert.That(planned, Is.Null);
            Assert.That(error, Is.EqualTo("Level ID a is listed twice in the manifest."));

            Assert.That(LevelPackLayout.GetPackFileName(12), Is.EqualTo("pack-012.json"));
            Assert.That(LevelPackLayout.TryGetPackNumber("pack-1234.json", out int number) && number == 1234, Is.True);
            Assert.That(LevelPackLayout.TryGetPackNumber("pack-12.json", out _), Is.False);
            Assert.That(LevelPackLayout.TryGetPackNumber("pack-000.json", out _), Is.False);
            Assert.That(LevelPackLayout.TryGetPackNumber("levels.json", out _), Is.False);
        }

        private static LevelManifest Plan(LevelManifest current, IReadOnlyList<string> ids)
        {
            Assert.That(LevelPackLayout.TryPlan("Main", current, ids, out LevelManifest planned, out string error),
                Is.True, error);
            return planned;
        }

        private static LevelManifest Manifest(params (string file, string[] ids)[] packs)
        {
            LevelManifestPack[] manifestPacks = new LevelManifestPack[packs.Length];
            for (int index = 0; index < packs.Length; index++)
            {
                manifestPacks[index] = new LevelManifestPack(packs[index].file, packs[index].ids);
            }

            Assert.That(LevelManifest.TryCreate("Main", manifestPacks, out LevelManifest manifest, out string error),
                Is.True, error);
            return manifest;
        }

        private static string[] Ids(int first, int last)
        {
            string[] ids = new string[last - first + 1];
            for (int index = 0; index < ids.Length; index++)
            {
                ids[index] = $"level-{first + index:D4}";
            }

            return ids;
        }

        private static void AssertPacks(LevelManifest manifest, params (string file, int first, int count)[] expected)
        {
            Assert.That(manifest.PackCount, Is.EqualTo(expected.Length), "pack count");
            for (int index = 0; index < expected.Length; index++)
            {
                LevelPackInfo pack = manifest.GetPackAt(index);
                Assert.That((pack.File, pack.FirstLevelIndex, pack.LevelCount), Is.EqualTo(expected[index]),
                    $"pack {index}");
            }
        }
    }
}
