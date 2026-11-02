using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the level database manifest, formatVersion 1 (DEC-196): its round trip, its play order and pack lookup,
    /// and the refusals of the strict reader in designer words.
    /// </summary>
    public sealed class LevelManifestJsonTests
    {
        private const string k_twoPacks = "{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [ "
            + "{ \"file\": \"pack-001.json\", \"levels\": [ \"a\", \"b\" ] }, "
            + "{ \"file\": \"pack-002.json\", \"levels\": [ \"c\" ] } ] }";

        [Test]
        public void ReadGivesThePlayOrderAndThePackOfEveryLevel()
        {
            LevelManifest manifest = Read(k_twoPacks);

            Assert.That(manifest.Name, Is.EqualTo("Main"));
            Assert.That(manifest.Count, Is.EqualTo(3));
            Assert.That(manifest.PackCount, Is.EqualTo(2));
            Assert.That(manifest.GetLevelIdAt(2), Is.EqualTo("c"));
            Assert.That(manifest.IndexOf("b"), Is.EqualTo(1));
            Assert.That(manifest.IndexOf("missing"), Is.EqualTo(-1));
            Assert.That(manifest.IndexOf(null), Is.EqualTo(-1));
            Assert.That(manifest.GetPackIndex(0), Is.EqualTo(0));
            Assert.That(manifest.GetPackIndex(1), Is.EqualTo(0));
            Assert.That(manifest.GetPackIndex(2), Is.EqualTo(1));
            Assert.That(manifest.GetPackAt(1).FirstLevelIndex, Is.EqualTo(2));
            Assert.That(manifest.GetPackAt(1).LevelCount, Is.EqualTo(1));
            Assert.That(manifest.GetPackAt(1).File, Is.EqualTo("pack-002.json"));
        }

        [Test]
        public void WriteThenReadKeepsTheManifestAndAnEmptyDatabaseHasNoPacks()
        {
            Assert.That(LevelManifestJson.TryWrite(Read(k_twoPacks), out string json, out string error), Is.True, error);
            LevelManifest again = Read(json);
            Assert.That(again.Count, Is.EqualTo(3));
            Assert.That(again.GetPackAt(0).File, Is.EqualTo("pack-001.json"));
            Assert.That(LevelManifestJson.TryWrite(again, out string second, out error), Is.True, error);
            Assert.That(second, Is.EqualTo(json), "A written manifest is canonical.");

            LevelManifest empty = Read("{ \"formatVersion\": 1, \"name\": \"Empty\", \"packs\": [] }");
            Assert.That(empty.Count, Is.Zero);
            Assert.That(empty.PackCount, Is.Zero);
        }

        [TestCase("{ \"formatVersion\": 3, \"levels\": [] }", "Expected level manifest formatVersion 1")]
        [TestCase("{ \"formatVersion\": 1, \"name\": \"Main\" }", "Required property 'packs'")]
        [TestCase("{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [], \"extra\": 1 }", "extra")]
        [TestCase("{ \"formatVersion\": 1, \"name\": \"Main\", \"name\": \"Other\", \"packs\": [] }", "name")]
        [TestCase("{ \"$type\": \"x\", \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [] }", "type metadata")]
        [TestCase("{ \"formatVersion\": 1, \"name\": \"Main Levels\", \"packs\": [] }", "letters, digits")]
        [TestCase("{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [ null ] }", "Pack 1 of the manifest is empty.")]
        public void ReadRefusesAnotherFormatAndABrokenManifest(string json, string expected)
        {
            Assert.That(LevelManifestJson.TryRead(json, out LevelManifest manifest, out string error), Is.False);
            Assert.That(manifest, Is.Null);
            Assert.That(error, Does.Contain(expected));
        }

        [TestCase("../pack-001.json", "which is not a .json file name without folders")]
        [TestCase("manifest.json", "which is not a .json file name without folders")]
        [TestCase("pack-001.txt", "which is not a .json file name without folders")]
        public void ReadRefusesAPackFileThatIsNotAPlainJsonName(string file, string expected)
        {
            string json = "{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": [ { \"file\": \"" + file
                + "\", \"levels\": [ \"a\" ] } ] }";

            Assert.That(LevelManifestJson.TryRead(json, out _, out string error), Is.False);
            Assert.That(error, Does.Contain(expected));
        }

        [Test]
        public void ReadRefusesRepeatedFilesEmptyPacksAndRepeatedOrMissingIds()
        {
            AssertRefused("[ { \"file\": \"pack-001.json\", \"levels\": [ \"a\" ] }, "
                + "{ \"file\": \"PACK-001.json\", \"levels\": [ \"b\" ] } ]", "Pack file PACK-001.json is listed twice.");
            AssertRefused("[ { \"file\": \"pack-001.json\", \"levels\": [] } ]", "Pack pack-001.json lists no level.");
            AssertRefused("[ { \"file\": \"pack-001.json\", \"levels\": [ \"a\", \" \" ] } ]",
                "Pack pack-001.json lists a level without an ID.");
            AssertRefused("[ { \"file\": \"pack-001.json\", \"levels\": [ \"a\" ] }, "
                + "{ \"file\": \"pack-002.json\", \"levels\": [ \"a\" ] } ]", "Level ID a is listed twice in the manifest.");
        }

        private static void AssertRefused(string packs, string expected)
        {
            string json = "{ \"formatVersion\": 1, \"name\": \"Main\", \"packs\": " + packs + " }";
            Assert.That(LevelManifestJson.TryRead(json, out _, out string error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
        }

        private static LevelManifest Read(string json)
        {
            Assert.That(LevelManifestJson.TryRead(json, out LevelManifest manifest, out string error), Is.True, error);
            return manifest;
        }
    }
}
