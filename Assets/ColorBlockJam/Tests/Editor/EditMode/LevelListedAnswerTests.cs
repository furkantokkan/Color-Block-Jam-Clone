using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies when the validation panel's listed answer still belongs to the draft: the same content at the same
    /// level index of the same database keeps it, a change of any of them makes it stale, and Clear empties it.
    /// </summary>
    public sealed class LevelListedAnswerTests
    {
        private const string k_database = "database-guid";

        [Test]
        public void TheSameContentAtTheSameLevelKeepsTheAnswer()
        {
            LevelListedAnswer answer = new LevelListedAnswer();
            Assert.That(answer.IsEmpty, Is.True);

            answer.Set(Level(60), k_database, 2, null);

            Assert.That(answer.IsEmpty, Is.False);
            Assert.That(answer.Level.TimerSeconds, Is.EqualTo(60));
            Assert.That(answer.IsFor(Level(60), k_database, 2), Is.True, "A reloaded copy with the same content.");
        }

        [Test]
        public void AnotherContentLevelOrDatabaseMakesItStale()
        {
            LevelListedAnswer answer = new LevelListedAnswer();
            answer.Set(Level(60), k_database, 2, null);

            Assert.That(answer.IsFor(Level(61), k_database, 2), Is.False, "An edit.");
            Assert.That(answer.IsFor(Level(60), k_database, 3), Is.False, "Another level index.");
            Assert.That(answer.IsFor(Level(60), "other-database", 2), Is.False, "Another database.");
            Assert.That(answer.IsFor(null, k_database, 2), Is.False, "No level open.");
        }

        [Test]
        public void ClearEmptiesTheAnswer()
        {
            LevelListedAnswer answer = new LevelListedAnswer();
            answer.Set(Level(60), k_database, 0, null);

            answer.Clear();

            Assert.That(answer.IsEmpty, Is.True);
            Assert.That(answer.Level, Is.Null);
            Assert.That(answer.Result, Is.Null);
            Assert.That(answer.IsFor(Level(60), k_database, 0), Is.False);
        }

        private static LevelData Level(int seconds)
        {
            return new LevelData("level-01", "Answer fixture", 4, 4, seconds, new LevelDoorData[0],
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
        }
    }
}
