using ColorBlockJam.Gameplay.Composition;
using NUnit.Framework;

namespace ColorBlockJam.Tests.Gameplay
{
    /// <summary>
    /// Proves which level the Game scene plays (contract scenario 7, design 9.3, DEC-173): Hub's launch request, then the
    /// Level Editor's Test Level, then the installer's serialized index; a requested level past the last database level
    /// plays the database again from the first and keeps its number.
    /// </summary>
    public sealed class LevelLaunchResolutionTests
    {
        [Test]
        public void NoRequest_PlaysTheSerializedIndex()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(null, false, 1, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.Serialized));
            Assert.That(resolution.LevelIndex, Is.EqualTo(1));
            Assert.That(resolution.LevelNumber, Is.EqualTo(2));
        }

        [Test]
        public void NoRequest_DefaultSerializedIndex_PlaysTheFirstLevel()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(null, false, 0, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.Serialized), "direct Play of Game (DEC-116)");
            Assert.That(resolution.LevelIndex, Is.EqualTo(0));
            Assert.That(resolution.LevelNumber, Is.EqualTo(1));
        }

        [TestCase(5, 5)]
        [TestCase(9, 5)]
        [TestCase(0, 0)]
        public void NoRequest_SerializedIndexOutsideTheDatabase_PlaysNothing(int serializedIndex, int levelCount)
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(null, false, serializedIndex, levelCount);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.None), "direct Play keeps its skip rule");
            Assert.That(resolution.LevelIndex, Is.EqualTo(-1));
            Assert.That(resolution.LevelNumber, Is.EqualTo(0));
        }

        [Test]
        public void Request_PlaysTheRequestedIndex()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(3, false, 0, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.Request));
            Assert.That(resolution.LevelIndex, Is.EqualTo(3));
            Assert.That(resolution.LevelNumber, Is.EqualTo(4));
        }

        [Test]
        public void Request_WinsOverTestPlayAndTheSerializedIndex()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(2, true, 4, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.Request));
            Assert.That(resolution.LevelIndex, Is.EqualTo(2));
            Assert.That(resolution.LevelNumber, Is.EqualTo(3));
        }

        [TestCase(5, 0, 6)]
        [TestCase(8, 3, 9)]
        [TestCase(40, 0, 41)]
        [TestCase(-3, 0, 1)]
        public void Request_PastTheLastLevel_PlaysTheDatabaseAgainAndKeepsItsNumber(int requested, int expectedIndex,
            int expectedNumber)
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(requested, false, 0, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.Request));
            Assert.That(resolution.LevelIndex, Is.EqualTo(expectedIndex), "the database entry");
            Assert.That(resolution.LevelNumber, Is.EqualTo(expectedNumber), "the number the HUD shows");
        }

        [Test]
        public void Request_WithAnEmptyDatabase_PlaysNothing()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(0, true, 0, 0);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.None), "no fallback to Test Level or the index");
            Assert.That(resolution.LevelIndex, Is.EqualTo(-1));
            Assert.That(resolution.LevelNumber, Is.EqualTo(0));
        }

        [Test]
        public void TestPlay_WinsOverTheSerializedIndex()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(null, true, 1, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.TestPlay));
            Assert.That(resolution.LevelIndex, Is.EqualTo(-1), "Test Level names its level by stable ID");
            Assert.That(resolution.LevelNumber, Is.EqualTo(0), "the loader finds its number by ID");
        }

        [Test]
        public void TestPlay_WinsOverASerializedIndexOutsideTheDatabase()
        {
            LevelLaunchResolution resolution = LevelLaunchResolution.Resolve(null, true, 9, 5);

            Assert.That(resolution.Source, Is.EqualTo(LevelLaunchSource.TestPlay));
        }
    }
}
