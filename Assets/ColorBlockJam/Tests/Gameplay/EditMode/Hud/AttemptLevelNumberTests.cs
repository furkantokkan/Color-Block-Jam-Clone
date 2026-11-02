using System;
using ColorBlockJam.Gameplay.Hud;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.Gameplay.Hud
{
    /// <summary>
    /// The attempt's Level number (contract AC-2): the 1-based position of the level's ID in the active database, as a
    /// Hub launch, direct Play and the Level Editor's Test Level (which knows only the ID) all resolve it, with its badge
    /// and popup texts; three-digit numbers are kept whole and an unknown level shows a placeholder.
    /// </summary>
    public sealed class AttemptLevelNumberTests
    {
        private LevelCatalog m_levels;

        // NUnit reuses the fixture instance, so the catalog is rebuilt here.
        [SetUp]
        public void SetUp()
        {
            LevelData[] levels =
            {
                CreateLevel("level-01", "First"),
                CreateLevel("level-02", "Second"),
                CreateLevel("level-test-played", "Third")
            };
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error), Is.True, error);
            m_levels = new LevelCatalog();
            Assert.That(m_levels.LoadJson(json, out error), Is.True, error);
        }

        [TestCase("level-01", 1)]
        [TestCase("level-02", 2)]
        [TestCase("level-test-played", 3)]
        public void FindReturnsTheOneBasedPositionOfTheLevelId(string levelId, int expected)
        {
            Assert.That(AttemptLevelNumber.Find(m_levels, levelId), Is.EqualTo(expected));
        }

        [TestCase("level-99")]
        [TestCase("LEVEL-01")]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase((string)null)]
        public void AnIdOutsideTheDatabaseIsUnknown(string levelId)
        {
            Assert.That(AttemptLevelNumber.Find(m_levels, levelId), Is.EqualTo(AttemptLevelNumber.k_unknown));
        }

        [Test]
        public void FindNeedsADatabase()
        {
            Assert.Throws<ArgumentNullException>(() => AttemptLevelNumber.Find(null, "level-01"));
        }

        [Test]
        public void TheTextsStartUnknown()
        {
            AttemptLevelNumber number = new AttemptLevelNumber();

            Assert.That(number.Value, Is.EqualTo(AttemptLevelNumber.k_unknown));
            Assert.That(number.NumberText, Is.EqualTo("-"));
            Assert.That(number.Title, Is.EqualTo("Level"));
        }

        [TestCase(2, "2", "Level 2")]
        [TestCase(29, "29", "Level 29")]
        [TestCase(123, "123", "Level 123")]
        public void SetBuildsTheBadgeAndTitleTexts(int value, string numberText, string title)
        {
            AttemptLevelNumber number = new AttemptLevelNumber();

            number.Set(value);

            Assert.That(number.Value, Is.EqualTo(value));
            Assert.That(number.NumberText, Is.EqualTo(numberText));
            Assert.That(number.Title, Is.EqualTo(title));
        }

        [Test]
        public void AnUnchangedNumberKeepsItsStringsAndANegativeOneIsUnknown()
        {
            AttemptLevelNumber number = new AttemptLevelNumber();
            number.Set(AttemptLevelNumber.Find(m_levels, "level-02"));
            string text = number.NumberText;
            string title = number.Title;

            number.Set(2);
            Assert.That(number.NumberText, Is.SameAs(text), "no string is rebuilt for the same number");
            Assert.That(number.Title, Is.SameAs(title));

            number.Set(-4);
            Assert.That(number.Value, Is.EqualTo(AttemptLevelNumber.k_unknown));
            Assert.That(number.NumberText, Is.EqualTo("-"));
            Assert.That(number.Title, Is.EqualTo("Level"));
        }

        private static LevelData CreateLevel(string id, string name)
        {
            return new LevelData(id, name, 3, 3, 60,
                new[] { new LevelDoorData("door-1", BoardSide.Right, 0, 1, "Red") },
                new[]
                {
                    new LevelBlockData("block-1", "Red", Vector2Int.zero,
                        new[] { Vector2Int.zero, new Vector2Int(1, 0) })
                });
        }
    }
}
