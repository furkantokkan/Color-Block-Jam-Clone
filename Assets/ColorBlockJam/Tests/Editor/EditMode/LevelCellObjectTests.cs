using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Board objects (cellObjects): structure rules, the Barrier and the runtime support gate.</summary>
    public sealed class LevelCellObjectTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void BarrierRoundTripsAndExposesTypedStartsOpen(bool startsOpen)
        {
            LevelData level = LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1",
                    LevelEffectTests.Effect("barrier", ("startsOpen", startsOpen ? "true" : "false")),
                    new Vector2Int(4, 1), new Vector2Int(4, 2), new Vector2Int(3, 2))
            });
            Assert.That(level.Validate(out string error), Is.True, error);

            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out error), Is.True, error);
            JToken written = JObject.Parse(json)["levels"][0]["cellObjects"][0];
            Assert.That((string)written["id"], Is.EqualTo("o1"));
            Assert.That((string)written["effect"]["type"], Is.EqualTo("barrier"));
            Assert.That((string)written["effect"]["params"]["startsOpen"], Is.EqualTo(startsOpen ? "true" : "false"));

            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] loaded, out error),
                Is.True, error);
            LevelData read = loaded[0];
            Assert.That(read.CellObjectCount, Is.EqualTo(1));
            LevelCellObjectData barrier = read.GetCellObjectAt(0);
            Assert.That(barrier.Id, Is.EqualTo("o1"));
            Assert.That(barrier.CellCount, Is.EqualTo(3));
            Assert.That(barrier.GetCellAt(2), Is.EqualTo(new Vector2Int(3, 2)));
            Assert.That(barrier.TryGetEffect(out BarrierObject typed), Is.True);
            Assert.That(typed.StartsOpen, Is.EqualTo(startsOpen));
            Assert.That(LevelDatabaseJson.TryWrite(loaded, out string again, out error), Is.True, error);
            Assert.That(again, Is.EqualTo(json));
        }

        [Test]
        public void BarrierStartsOpenDefaultsToFalse()
        {
            LevelData level = LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier"), new Vector2Int(4, 1))
            });
            Assert.That(level.Validate(out string error), Is.True, error);
            Assert.That(level.GetCellObjectAt(0).TryGetEffect(out BarrierObject barrier), Is.True);
            Assert.That(barrier.StartsOpen, Is.False);
        }

        [Test]
        public void ClosedBarrierOnABlockIsRejectedAndAnOpenOneIsAccepted()
        {
            LevelData closed = BarrierOverBlock(null);
            Assert.That(closed.Validate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("Level fx cell object o1 effect barrier starts closed on block b3 at (3, 3)."));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { closed }, out _, out error), Is.False);

            Assert.That(BarrierOverBlock("true").Validate(out error), Is.True, error);
        }

        [Test]
        public void BarrierCellsMustBeFourConnected()
        {
            LevelData level = LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(4, 1), new Vector2Int(3, 2))
            });
            Assert.That(level.Validate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("Level fx cell object o1 effect barrier cells are not four-connected."));
        }

        [TestCase("no-id", "Level fx cell object 0 needs an ID.")]
        [TestCase("duplicate-id", "Level fx has duplicate cell object ID o1.")]
        [TestCase("no-cells", "Level fx cell object o1 has no cells.")]
        [TestCase("outside", "Level fx cell object o1 has cell (5, 1) outside the board.")]
        [TestCase("void", "Level fx cell object o1 has cell (4, 4) on a void cell.")]
        [TestCase("twice", "Level fx cell object o1 lists cell (4, 1) twice.")]
        [TestCase("shared", "Level fx cell objects o1 and o2 share cell (4, 1).")]
        [TestCase("no-effect", "Level fx cell object o1 has an empty effect.")]
        public void CellObjectStructureRulesRejectWithLevelMessages(string rule, string expected)
        {
            LevelEffectData ivy = LevelEffectTests.Effect("ivy");
            Vector2Int cell = new Vector2Int(4, 1);
            LevelCellObjectData[] objects;
            switch (rule)
            {
                case "no-id": objects = new[] { LevelEffectTests.Object(" ", ivy, cell) }; break;
                case "duplicate-id":
                    objects = new[]
                    {
                        LevelEffectTests.Object("o1", ivy, cell),
                        LevelEffectTests.Object("o1", ivy, new Vector2Int(4, 2))
                    };
                    break;
                case "no-cells": objects = new[] { LevelEffectTests.Object("o1", ivy) }; break;
                case "outside": objects = new[] { LevelEffectTests.Object("o1", ivy, new Vector2Int(5, 1)) }; break;
                case "void": objects = new[] { LevelEffectTests.Object("o1", ivy, new Vector2Int(4, 4)) }; break;
                case "twice": objects = new[] { LevelEffectTests.Object("o1", ivy, cell, cell) }; break;
                case "shared":
                    objects = new[]
                    {
                        LevelEffectTests.Object("o1", ivy, cell),
                        LevelEffectTests.Object("o2", ivy, new Vector2Int(4, 2), cell)
                    };
                    break;
                default: objects = new[] { LevelEffectTests.Object("o1", null, cell) }; break;
            }

            LevelData level = LevelEffectTests.Build(objects: objects, voidCells: new[] { new Vector2Int(4, 4) });
            Assert.That(level.Validate(out string error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out _), Is.False);
            Assert.That(json, Is.Null);
        }

        [Test]
        public void ProjectScopeRefusesTheBarrierUntilGameplayImplementsIt()
        {
            LevelData level = LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(4, 1))
            });
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out string error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out _, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, out _, out error), Is.True, error);

            // Follows the registry flag, so the gameplay owner's one-line flip keeps this green.
            bool implemented = LevelEffectTests.IsImplemented("barrier");
            string expected = implemented ? null : "Level fx cell object o1 effect barrier is not implemented in gameplay yet.";
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Runtime, out LevelData[] levels, out error),
                Is.EqualTo(implemented), error);
            Assert.That(levels == null, Is.EqualTo(!implemented));
            Assert.That(error, Is.EqualTo(expected));

            // LevelCatalogLoader.LoadPackAsync, the game's level pack load (DEC-196), uses this runtime read.
            LevelCatalog catalog = new LevelCatalog();
            Assert.That(catalog.LoadJson(json, out error), Is.True, error);
            Assert.That(catalog.LoadJson(json, LevelReadMode.Runtime, out error), Is.EqualTo(implemented), error);
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void CellObjectsSurviveUnitySerializationAndClone()
        {
            LevelData level = LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "true")),
                    new Vector2Int(4, 1), new Vector2Int(4, 2))
            });
            LevelData copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level));
            Assert.That(copy.CellObjectCount, Is.EqualTo(1));
            Assert.That(copy.GetCellObjectAt(0).GetCellAt(1), Is.EqualTo(new Vector2Int(4, 2)));
            Assert.That(copy.GetCellObjectAt(0).TryGetEffect(out BarrierObject barrier), Is.True);
            Assert.That(barrier.StartsOpen, Is.True);

            LevelData clone = level.Clone();
            Assert.That(clone.GetCellObjectAt(0), Is.Not.SameAs(level.GetCellObjectAt(0)));
            Assert.That(clone.GetCellObjectAt(0).Effect, Is.Not.SameAs(level.GetCellObjectAt(0).Effect));
            Assert.That(clone.Validate(out string error), Is.True, error);
        }

        private static LevelData BarrierOverBlock(string startsOpen)
        {
            LevelEffectData effect = startsOpen == null
                ? LevelEffectTests.Effect("barrier")
                : LevelEffectTests.Effect("barrier", ("startsOpen", startsOpen));
            return LevelEffectTests.Build(objects: new[]
            {
                LevelEffectTests.Object("o1", effect, new Vector2Int(3, 3), new Vector2Int(4, 3))
            });
        }
    }
}
