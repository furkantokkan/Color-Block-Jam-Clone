using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Player;
using ColorBlockJam.Player.Storage;
using ColorBlockJam.Runtime.Boosters;
using NUnit.Framework;

namespace ColorBlockJam.Tests.PlayerData
{
    /// <summary>
    /// Verifies the version 1 player JSON: round trip, unknown keys kept, a newer format refused, malformed values
    /// rejected, and the playable level with its database entry (player-data-currencies AC-4, design 4.2, DEC-173).
    /// </summary>
    public sealed class PlayerSnapshotJsonTests
    {
        internal const string UnknownKeysJson = @"{
  ""formatVersion"": 1,
  ""revision"": 3,
  ""currentLevelIndex"": 2,
  ""currencies"": { ""life"": 5, ""coin"": 20, ""gem"": 7 },
  ""boosterStock"": { ""Rocket"": 4, ""Magnet"": 9 },
  ""settings"": { ""music"": true, ""sound"": false, ""vibration"": true },
  ""futureField"": { ""kept"": false }
}";

        [Test]
        public void ASnapshotRoundTrips()
        {
            PlayerSnapshot snapshot = new PlayerSnapshot(7, 3,
                new Dictionary<string, int> { ["life"] = 5, ["coin"] = 120 },
                new Dictionary<string, int> { ["Rocket"] = 2, ["Hammer"] = 0 },
                new PlayerSettings(true, false, true));

            string json = PlayerSnapshotJson.Write(snapshot);
            PlayerSnapshot read = PlayerSnapshotJson.Read(json);

            Assert.That(json, Does.Contain("\"formatVersion\": 1").And.Not.Contain("$type"));
            Assert.That(read.Revision, Is.EqualTo(7));
            Assert.That(read.CurrentLevelIndex, Is.EqualTo(3));
            Assert.That(read.Currencies, Is.EquivalentTo(snapshot.Currencies));
            Assert.That(read.BoosterStock, Is.EquivalentTo(snapshot.BoosterStock));
            Assert.That(read.Settings, Is.EqualTo(snapshot.Settings));
            Assert.That(PlayerSnapshotJson.Write(read), Is.EqualTo(json));
        }

        [Test]
        public void UnknownCurrencyAndBoosterKeysAreReadAndWrittenBack()
        {
            PlayerSnapshot read = PlayerSnapshotJson.Read(UnknownKeysJson);

            Assert.That(read.TryGetCurrency("gem", out int gems) && gems == 7, Is.True);
            Assert.That(read.BoosterStock["Magnet"], Is.EqualTo(9));
            Assert.That(read.TryGetBoosterStock(BoosterId.Rocket, out int rockets) && rockets == 4, Is.True);

            string written = PlayerSnapshotJson.Write(read.WithCurrency("coin", 30));
            Assert.That(written, Does.Contain("\"gem\": 7").And.Contain("\"Magnet\": 9").And.Contain("\"coin\": 30"));
        }

        [Test]
        public void ANewerFormatIsRefusedWithItsVersion()
        {
            PlayerSnapshotVersionException refusal = Assert.Throws<PlayerSnapshotVersionException>(
                () => PlayerSnapshotJson.Read(UnknownKeysJson.Replace("\"formatVersion\": 1", "\"formatVersion\": 2")));

            Assert.That(refusal.FormatVersion, Is.EqualTo(2));
        }

        [TestCase("\"coin\": 20", "\"coin\": -1")]
        [TestCase("\"coin\": 20", "\"coin\": \"20\"")]
        [TestCase("\"coin\": 20", "\"coin\": 1.5")]
        [TestCase("\"coin\": 20", "\"coin\": 3000000000")]
        [TestCase("\"revision\": 3", "\"revision\": -3")]
        [TestCase("\"currentLevelIndex\": 2", "\"currentLevelIndex\": null")]
        [TestCase("\"sound\": false", "\"sound\": 0")]
        [TestCase("\"revision\": 3", "\"Revision\": 3")]
        [TestCase("\"formatVersion\": 1", "\"formatVersion\": 0")]
        public void MalformedContentIsRejected(string original, string replacement)
        {
            Assert.Throws<InvalidDataException>(() => PlayerSnapshotJson.Read(UnknownKeysJson.Replace(original, replacement)));
        }

        [TestCase("")]
        [TestCase("[]")]
        [TestCase("{")]
        [TestCase("{} {}")]
        public void NonObjectTextIsRejected(string json)
        {
            Assert.Throws<InvalidDataException>(() => PlayerSnapshotJson.Read(json));
        }

        [TestCase(0, 5, 0)]
        [TestCase(4, 5, 4)]
        [TestCase(5, 5, 5)]
        [TestCase(99, 5, 99)]
        [TestCase(-2, 5, 0)]
        [TestCase(0, 0, -1)]
        public void ThePlayableLevelKeepsCountingPastTheLastLevel(int saved, int count, int expected)
        {
            Assert.That(PlayerProgress.GetPlayableLevelIndex(saved, count), Is.EqualTo(expected), "DEC-173");
        }

        [TestCase(0, 5, 0)]
        [TestCase(4, 5, 4)]
        [TestCase(5, 5, 0)]
        [TestCase(12, 5, 2)]
        [TestCase(-1, 5, -1)]
        [TestCase(3, 0, -1)]
        public void ALevelPastTheLastPlaysTheDatabaseAgainFromTheFirst(int levelIndex, int count, int expected)
        {
            Assert.That(PlayerProgress.GetDatabaseIndex(levelIndex, count), Is.EqualTo(expected), "DEC-173");
        }
    }
}
