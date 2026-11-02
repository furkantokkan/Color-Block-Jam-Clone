using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>Registry coverage plus one valid and one invalid fixture per registered effect type.</summary>
    public sealed class LevelEffectTests
    {
        // Every registered type id with its target: the Arrow and Ice block effects, no gate effect (DEC-177), and the
        // board objects (docs/Color-Block-Jam-PRD-Mechanics-Catalog.md).
        private static readonly (string Type, LevelEffectTarget Target)[] s_catalog =
        {
            ("arrow", LevelEffectTarget.Block), ("ice", LevelEffectTarget.Block),
            ("colorful-path", LevelEffectTarget.CellObject), ("crate", LevelEffectTarget.CellObject),
            ("moveable-crate", LevelEffectTarget.CellObject), ("flower-crate", LevelEffectTarget.CellObject),
            ("barrier", LevelEffectTarget.CellObject),
            ("colorful-crate", LevelEffectTarget.CellObject),
            ("colorful-moving-path", LevelEffectTarget.CellObject), ("ivy", LevelEffectTarget.CellObject),
            ("elevator", LevelEffectTarget.CellObject)
        };

        [Test]
        public void RegistryHasOneDefinitionPerCatalogTypeWithItsTarget()
        {
            Assert.That(LevelEffectRegistry.DefinitionCount, Is.EqualTo(s_catalog.Length));
            HashSet<string> seen = new HashSet<string>();
            for (int index = 0; index < LevelEffectRegistry.DefinitionCount; index++)
            {
                Assert.That(seen.Add(LevelEffectRegistry.GetDefinitionAt(index).TypeId), Is.True);
                Assert.That(LevelEffectRegistry.GetDefinitionAt(index).Target, Is.Not.EqualTo(LevelEffectTarget.Door),
                    "No gate effect type is registered.");
            }

            foreach ((string type, LevelEffectTarget target) in s_catalog)
            {
                Assert.That(LevelEffectRegistry.TryFind(type, out LevelEffectDefinition definition), Is.True, type);
                Assert.That(definition.Target, Is.EqualTo(target), type);
                Assert.That(definition.Role != LevelCellObjectRole.None,
                    Is.EqualTo(target == LevelEffectTarget.CellObject), type);
            }

            Assert.That(LevelEffectRegistry.TryFind("barrier", out LevelEffectDefinition barrier), Is.True);
            Assert.That(barrier.Role, Is.EqualTo(LevelCellObjectRole.WallOrFloor));
        }

        [TestCaseSource(nameof(ValidFixtures))]
        public void ValidFixtureIsAcceptedAndRoundTrips(string name, Func<LevelData> create, Type typed)
        {
            LevelData level = create();
            Assert.That(level.Validate(out string error), Is.True, error);
            Assert.That(FindParameters(level, name), Is.InstanceOf(typed));

            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] loaded, out error),
                Is.True, error);
            Assert.That(LevelDatabaseJson.TryWrite(loaded, out string again, out error), Is.True, error);
            Assert.That(again, Is.EqualTo(json));
            Assert.That(FindParameters(loaded[0], name), Is.InstanceOf(typed));
        }

        [TestCaseSource(nameof(InvalidFixtures))]
        public void InvalidFixtureIsRejectedNamingLevelEntityTypeAndKey(string name, Func<LevelData> create,
            string expected)
        {
            LevelData level = create();
            Assert.That(level.Validate(out string error), Is.False, name);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out string writeError),
                Is.False);
            Assert.That(json, Is.Null);
            Assert.That(writeError, Is.EqualTo(expected));
        }

        // Expectations follow each definition's flag, so a gameplay owner's one-line flip keeps this green.
        [TestCaseSource(nameof(ValidFixtures))]
        public void RuntimeSupportGateNamesTheUnimplementedEffectWhileAuthoringAccepts(string name,
            Func<LevelData> create, Type typed)
        {
            LevelData level = create();
            string expected = DescribeFirstUnimplemented(level);
            Assert.That(level.Validate(out string error), Is.True, error);
            Assert.That(level.ValidateRuntimeSupport(out error), Is.EqualTo(expected == null), error);
            Assert.That(error, Is.EqualTo(expected));

            // The 3-argument read is the authoring read (D-001); only LevelReadMode.Runtime applies the gate.
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out _, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, out _, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Runtime, out LevelData[] runtime, out error),
                Is.EqualTo(expected == null), error);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(runtime == null, Is.EqualTo(expected != null));

            Assert.That(Build().ValidateRuntimeSupport(out error), Is.True, error);
        }

        [Test]
        public void ListParametersExposeTypedItemsInAuthoredOrder()
        {
            LevelData level = Build(
                objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "b3,b1")), new Vector2Int(4, 1)) });
            Assert.That(level.Validate(out string error), Is.True, error);

            Assert.That(level.GetCellObjectAt(0).TryGetEffect(out FlowerCrateObject crate), Is.True);
            Assert.That(crate.LeafCount, Is.EqualTo(2));
            Assert.That(crate.GetLeafIdAt(0), Is.EqualTo("b3"));
            Assert.That(crate.GetLeafIdAt(1), Is.EqualTo("b1"));

            // Canonical writing keeps each list value unchanged.
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out error), Is.True, error);
            Assert.That(json, Does.Contain("\"leaves\": \"b3,b1\""));
        }

        [Test]
        public void WriterOrdersParamsByDefinitionAndKeepsEffectOrder()
        {
            LevelData level = Build(
                b1: new[] { Effect("ice", ("clearsToThaw", "2")), Effect("arrow", ("axis", "Horizontal")) },
                objects: new[]
                {
                    Object("o1", Effect("colorful-moving-path", ("platformIndex", "2"), ("colorId", "Red")),
                        new Vector2Int(4, 0), new Vector2Int(4, 1), new Vector2Int(4, 2))
                });
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out string error), Is.True, error);
            int ice = json.IndexOf("\"ice\"", StringComparison.Ordinal);
            int arrow = json.IndexOf("\"arrow\"", StringComparison.Ordinal);
            int path = json.IndexOf("\"colorful-moving-path\"", StringComparison.Ordinal);
            int color = json.IndexOf("\"colorId\"", path, StringComparison.Ordinal);
            int platform = json.IndexOf("\"platformIndex\"", path, StringComparison.Ordinal);
            Assert.That(ice, Is.LessThan(arrow));
            Assert.That(color, Is.LessThan(platform));
        }

        [Test]
        public void TypedAccessorsAndUnitySerializationKeepEffects()
        {
            LevelData level = Build(
                b3: new[] { Effect("arrow", ("axis", "Vertical")) },
                voidCells: new[] { new Vector2Int(4, 4) });
            Assert.That(level.GetBlockAt(2).TryGetEffect(out ArrowEffect arrow), Is.True);
            Assert.That(arrow.Axis, Is.EqualTo(LevelEffectAxis.Vertical));
            Assert.That(level.GetBlockAt(2).TryGetEffect(out IceEffect _), Is.False);

            // The Level Editor draft, Undo and dirty check use Unity serialization.
            LevelData copy = JsonUtility.FromJson<LevelData>(JsonUtility.ToJson(level));
            Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(JsonUtility.ToJson(level)));
            Assert.That(copy.VoidCellCount, Is.EqualTo(1));
            Assert.That(copy.GetBlockAt(2).EffectCount, Is.EqualTo(1));
            Assert.That(copy.GetBlockAt(2).GetEffectAt(0).FindValue("axis"), Is.EqualTo("Vertical"));
            Assert.That(copy.GetBlockAt(2).TryGetEffect(out ArrowEffect copiedArrow), Is.True);
            Assert.That(copiedArrow.Axis, Is.EqualTo(LevelEffectAxis.Vertical));
            Assert.That(copy.Validate(out string error), Is.True, error);
        }

        [Test]
        public void ClonesDoNotShareEffectRecords()
        {
            LevelData level = Build(b3: new[] { Effect("ice", ("clearsToThaw", "3")) });
            LevelData clone = level.Clone();
            Assert.That(clone.GetBlockAt(2).GetEffectAt(0), Is.Not.SameAs(level.GetBlockAt(2).GetEffectAt(0)));
            Assert.That(clone.GetBlockAt(2).GetEffectAt(0).GetParamAt(0),
                Is.Not.SameAs(level.GetBlockAt(2).GetEffectAt(0).GetParamAt(0)));
            Assert.That(clone.GetBlockAt(2).TryGetEffect(out IceEffect ice), Is.True);
            Assert.That(ice.ClearsToThaw, Is.EqualTo(3));
        }

        private static IEnumerable<TestCaseData> ValidFixtures()
        {
            yield return Valid("arrow", typeof(ArrowEffect), () => Build(b3: new[] { Effect("arrow", ("axis", "Horizontal")) }));
            yield return Valid("ice", typeof(IceEffect), () => Build(b3: new[] { Effect("ice", ("clearsToThaw", "20")) }));
            yield return Valid("colorful-path", typeof(ColorfulPathObject), () => Build(objects: new[] { Object("o1", Effect("colorful-path", ("colorId", "Red")), new Vector2Int(4, 1), new Vector2Int(4, 2)) }));
            yield return Valid("crate", typeof(CrateObject), () => Build(objects: new[] { Object("o1", Effect("crate", ("clearsToOpen", "2")), new Vector2Int(4, 1)) }));
            yield return Valid("moveable-crate", typeof(MoveableCrateObject), () => Build(objects: new[] { Object("o1", Effect("moveable-crate", ("gatePassesToOpen", "1")), new Vector2Int(4, 1)) }));
            yield return Valid("barrier", typeof(BarrierObject), () => Build(objects: new[] { Object("o1", Effect("barrier"), new Vector2Int(4, 1), new Vector2Int(4, 2)) }));
            yield return Valid("colorful-crate", typeof(ColorfulCrateObject), () => Build(objects: new[] { Object("o1", Effect("colorful-crate", ("colorId", "Yellow")), new Vector2Int(4, 1)) }));
            yield return Valid("colorful-moving-path", typeof(ColorfulMovingPathObject), () => Build(objects: new[] { Object("o1", Effect("colorful-moving-path", ("colorId", "Red"), ("platformIndex", "2")), new Vector2Int(4, 0), new Vector2Int(4, 1), new Vector2Int(4, 2)) }));
            yield return Valid("ivy", typeof(IvyObject), () => Build(objects: new[] { Object("o1", Effect("ivy"), new Vector2Int(4, 2), new Vector2Int(4, 1)) }));
            yield return Valid("elevator", typeof(ElevatorObject), () => Build(objects: new[] { Object("o1", Effect("elevator"), new Vector2Int(4, 1)) }));
            // List parameters (user decision 2026-09-25): "b1,b2" lists.
            yield return Valid("flower-crate", typeof(FlowerCrateObject), () => Build(
                objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "b1,b2")), new Vector2Int(4, 3)) }));
        }

        private static IEnumerable<TestCaseData> InvalidFixtures()
        {
            yield return Invalid("arrow", "Level fx block b3 effect arrow key axis has value 'Diagonal'; expected one of Horizontal, Vertical.",
                () => Build(b3: new[] { Effect("arrow", ("axis", "Diagonal")) }));
            yield return Invalid("ice", "Level fx block b3 effect ice key clearsToThaw has value '0'; expected an integer from 1 to 20.",
                () => Build(b3: new[] { Effect("ice", ("clearsToThaw", "0")) }));
            yield return Invalid("colorful-path", "Level fx door d1 effect colorful-path cannot be used on a door.",
                () => Build(d1: new[] { Effect("colorful-path", ("colorId", "Red")) }));
            yield return Invalid("crate", "Level fx cell object o1 effect crate key clearsToOpen has value '0'; expected an integer of 1 or more.",
                () => Build(objects: new[] { Object("o1", Effect("crate", ("clearsToOpen", "0")), new Vector2Int(4, 1)) }));
            yield return Invalid("moveable-crate", "Level fx cell object o1 effect moveable-crate is missing required key gatePassesToOpen.",
                () => Build(objects: new[] { Object("o1", Effect("moveable-crate"), new Vector2Int(4, 1)) }));
            yield return Invalid("barrier", "Level fx cell object o1 effect barrier starts closed on block b3 at (3, 3).",
                () => Build(objects: new[] { Object("o1", Effect("barrier", ("startsOpen", "false")), new Vector2Int(3, 3)) }));
            yield return Invalid("colorful-crate", "Level fx cell object o1 effect colorful-crate key colorId has value 'Ground'; expected a gate colour name.",
                () => Build(objects: new[] { Object("o1", Effect("colorful-crate", ("colorId", "Ground")), new Vector2Int(4, 1)) }));
            yield return Invalid("colorful-moving-path", "Level fx cell object o1 effect colorful-moving-path needs a straight track of neighbouring cells in order.",
                () => Build(objects: new[] { Object("o1", Effect("colorful-moving-path", ("colorId", "Red"), ("platformIndex", "0")), new Vector2Int(4, 0), new Vector2Int(4, 1), new Vector2Int(3, 1)) }));
            yield return Invalid("ivy", "Level fx cell object o1 effect ivy has unknown key length.",
                () => Build(objects: new[] { Object("o1", Effect("ivy", ("length", "2")), new Vector2Int(4, 1)) }));
            yield return Invalid("elevator", "Level fx block b3 effect elevator cannot be used on a block.",
                () => Build(b3: new[] { Effect("elevator") }));
            yield return Invalid("flower-crate", "Level fx cell object o1 effect flower-crate key leaves has 0 items in ''; expected 1 or more items.",
                () => Build(objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "")), new Vector2Int(4, 1)) }));
            // Level-wide and generic errors.
            yield return Invalid("arrow-twice", "Level fx block b3 lists effect arrow twice.",
                () => Build(b3: new[] { Effect("arrow", ("axis", "Vertical")), Effect("arrow", ("axis", "Vertical")) }));
            yield return Invalid("ice-key-twice", "Level fx block b3 effect ice lists key clearsToThaw twice.",
                () => Build(b3: new[] { Effect("ice", ("clearsToThaw", "2"), ("clearsToThaw", "2")) }));
            yield return Invalid("colorful-path-under-block", "Level fx cell object o1 effect colorful-path cell (3, 3) is under block b3 of another colour.",
                () => Build(objects: new[] { Object("o1", Effect("colorful-path", ("colorId", "Red")), new Vector2Int(3, 3)) }));
            yield return Invalid("unknown", "Level fx block b3 effect Ice is not a known effect type.",
                () => Build(b3: new[] { Effect("Ice") }));
            // Only Arrow and Ice remain as block effects and no gate effect exists (DEC-177): removed types are unknown.
            yield return Invalid("removed-block-effect", "Level fx block b3 effect star is not a known effect type.",
                () => Build(b3: new[] { Effect("star") }));
            yield return Invalid("removed-gate-effect", "Level fx door d1 effect iced-door is not a known effect type.",
                () => Build(d1: new[] { Effect("iced-door", ("clearsToOpen", "1")) }));
            // The solid Block obstacle was removed on 2026-09-25; the Barrier is the one board obstacle.
            yield return Invalid("solid-block-removed", "Level fx cell object o1 effect solid-block is not a known effect type.",
                () => Build(objects: new[] { Object("o1", Effect("solid-block"), new Vector2Int(4, 1)) }));
            yield return Invalid("empty", "Level fx block b3 has an empty effect.",
                () => Build(b3: new[] { Effect(" ") }));

            // Malformed list and enum values.
            yield return Invalid("list-empty-item", "Level fx cell object o1 effect flower-crate key leaves item '' in 'b1,,b2' is not a block ID.",
                () => Build(objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "b1,,b2")), new Vector2Int(4, 1)) }));
            yield return Invalid("enum-spelling", "Level fx block b3 effect arrow key axis has value 'vertical'; expected one of Horizontal, Vertical.",
                () => Build(b3: new[] { Effect("arrow", ("axis", "vertical")) }));
            yield return Invalid("leaves-twice", "Level fx cell object o1 effect flower-crate key leaves lists block b1 twice.",
                () => Build(objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "b1,b1")), new Vector2Int(4, 1)) }));
            yield return Invalid("leaves-missing-block", "Level fx cell object o1 effect flower-crate key leaves names missing block b9.",
                () => Build(objects: new[] { Object("o1", Effect("flower-crate", ("leaves", "b9")), new Vector2Int(4, 1)) }));
        }

        private static TestCaseData Valid(string name, Type typed, Func<LevelData> create)
        {
            return new TestCaseData(name, create, typed).SetName($"Valid_{name}");
        }

        private static TestCaseData Invalid(string name, string expected, Func<LevelData> create)
        {
            return new TestCaseData(name, create, expected).SetName($"Invalid_{name}");
        }

        private static LevelEffectParameters FindParameters(LevelData level, string type)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int effect = 0; effect < block.EffectCount; effect++)
                {
                    if (block.GetEffectAt(effect).Type == type) return block.GetEffectAt(effect).Parameters;
                }
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                for (int effect = 0; effect < door.EffectCount; effect++)
                {
                    if (door.GetEffectAt(effect).Type == type) return door.GetEffectAt(effect).Parameters;
                }
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelEffectData effect = level.GetCellObjectAt(index).Effect;
                if (effect.Type == type) return effect.Parameters;
            }

            return null;
        }

        /// <summary>The runtime gate message for the first effect whose gameplay is not implemented, or null.</summary>
        internal static string DescribeFirstUnimplemented(LevelData level)
        {
            for (int index = 0; index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                for (int effect = 0; effect < block.EffectCount; effect++)
                {
                    string type = block.GetEffectAt(effect).Type;
                    if (!IsImplemented(type)) return $"Level {level.Id} block {block.Id} effect {type} is not implemented in gameplay yet.";
                }
            }

            for (int index = 0; index < level.DoorCount; index++)
            {
                LevelDoorData door = level.GetDoorAt(index);
                for (int effect = 0; effect < door.EffectCount; effect++)
                {
                    string type = door.GetEffectAt(effect).Type;
                    if (!IsImplemented(type)) return $"Level {level.Id} door {door.Id} effect {type} is not implemented in gameplay yet.";
                }
            }

            for (int index = 0; index < level.CellObjectCount; index++)
            {
                LevelCellObjectData cellObject = level.GetCellObjectAt(index);
                string type = cellObject.Effect.Type;
                if (!IsImplemented(type)) return $"Level {level.Id} cell object {cellObject.Id} effect {type} is not implemented in gameplay yet.";
            }

            return null;
        }

        internal static bool IsImplemented(string type)
        {
            return LevelTestData.IsRuntimeImplemented(type);
        }

        // Kept as a forwarding wrapper: several other test fixtures (GateSegmentRemoveTests, LevelCellObjectTests,
        // LevelCornerStepsTests, LevelTemplateEditTests and others) still call Effect/Object/Build/IsImplemented on
        // this class. LevelTestData holds the one implementation; this method only forwards to it.
        internal static LevelEffectData Effect(string type, params (string Key, string Value)[] parameters)
        {
            return LevelTestData.Effect(type, parameters);
        }

        internal static LevelCellObjectData Object(string id, LevelEffectData effect, params Vector2Int[] cells)
        {
            return LevelTestData.Object(id, effect, cells);
        }

        /// <summary>
        /// A 5 x 5 fixture: b1 Red at (0,0)-(1,0), b2 Blue at (0,1)-(1,1) touching b1, b3 Yellow at (3,3);
        /// d1 Red on Top 0-1 (span 2), d2 Blue on Bottom 3 (span 1; left out when oneDoor is true).
        /// </summary>
        internal static LevelData Build(
            LevelEffectData[] b1 = null,
            LevelEffectData[] b2 = null,
            LevelEffectData[] b3 = null,
            LevelEffectData[] d1 = null,
            LevelEffectData[] d2 = null,
            LevelCellObjectData[] objects = null,
            Vector2Int[] voidCells = null,
            bool oneDoor = false)
        {
            return LevelTestData.EffectsFixture(b1, b2, b3, d1, d2, objects, voidCells, oneDoor);
        }
    }
}
