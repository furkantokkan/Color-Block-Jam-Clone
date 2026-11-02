using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Xml;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies LevelDatabaseJson's write/read round trip across schema versions: field validation, void cells and
    /// doors, the authoring vs. runtime effect gate, project-scope loading, and link.xml DTO coverage.
    /// </summary>
    public sealed class LevelDatabaseJsonTests
    {
        [Test]
        public void WriteAndReadPreserveOrderAndGeometry()
        {
            LevelData[] source =
            {
                CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-2", "Second", new Vector2Int(1, 1))
            };

            string json = Write(source);
            Assert.That(json, Does.Contain("\"formatVersion\": 3"));
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] loaded, out string error),
                Is.True, error);
            Assert.That(loaded, Has.Length.EqualTo(2));
            Assert.That(loaded[0].Id, Is.EqualTo("level-1"));
            Assert.That(loaded[1].Id, Is.EqualTo("level-2"));
            Assert.That(loaded[1].GetBlockAt(0).Origin, Is.EqualTo(new Vector2Int(1, 1)));
            Assert.That(loaded[1].GetBlockAt(0).GetCellAt(1), Is.EqualTo(new Vector2Int(1, 0)));
            Assert.That(loaded[0].GetDoorAt(0).Side, Is.EqualTo(BoardSide.Right));
            Assert.That(loaded[0].GetDoorAt(0).ColorId, Is.EqualTo("Red"));
        }

        [Test]
        public void EmptyDatabaseJsonLoadsBeforeFirstLevelIsAuthored()
        {
            string json = Write(new LevelData[0]);
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] levels, out string error),
                Is.True, error);
            Assert.That(levels, Is.Empty);
        }

        [Test]
        public void ReadRejectsMalformedVersionMetadataAndUnknownFields()
        {
            string valid = Write(CreateLevel("level-1", "First", Vector2Int.zero));
            AssertReadRejected("{ broken");
            // Version 3 only (DEC-193): older and newer files are refused whole.
            AssertReadRejected(valid.Replace("\"formatVersion\": 3", "\"formatVersion\": 2"));
            AssertReadRejected(valid.Replace("\"formatVersion\": 3", "\"formatVersion\": 4"));
            AssertReadRejected(valid.Replace("\"formatVersion\": 3",
                "\"$type\": \"Unexpected\", \"formatVersion\": 3"));
            AssertReadRejected(valid.Replace("\"name\": \"First\"",
                "\"name\": \"First\", \"unknown\": true"));
            AssertReadRejected(valid.Replace("\"formatVersion\": 3",
                "\"formatVersion\": 3, \"formatVersion\": 3"));
            // An optional list is omitted when empty; an explicit null is refused.
            AssertReadRejected(valid.Replace("\"name\": \"First\"",
                "\"name\": \"First\", \"cellObjects\": null"));
        }

        [Test]
        public void ReadAndWriteRejectDuplicateLevelIds()
        {
            string valid = Write(
                CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-2", "Second", new Vector2Int(1, 0)));
            AssertReadRejected(valid.Replace("\"id\": \"level-2\"", "\"id\": \"level-1\""));

            LevelData[] duplicate =
            {
                CreateLevel("level-1", "First", Vector2Int.zero),
                CreateLevel("level-1", "Second", new Vector2Int(1, 0))
            };
            Assert.That(LevelDatabaseJson.TryWrite(duplicate, out _, out string error), Is.False);
            Assert.That(error, Does.Contain("level-1"));
        }

        [Test]
        public void ReadRejectsUnknownEffectTypesAndIncompleteRecords()
        {
            string valid = Write(CreateLevel("level-1", "First", Vector2Int.zero));
            JObject unknownBlockEffect = JObject.Parse(valid);
            unknownBlockEffect["levels"][0]["blocks"][0]["effects"] = JArray.Parse("[{\"type\":\"Ice\",\"params\":{}}]");
            Assert.That(AssertReadRejected(unknownBlockEffect.ToString()),
                Is.EqualTo("Level level-1 block block-1 effect Ice is not a known effect type."));
            Assert.That(ReadDoorEffect(JObject.Parse(valid), "{\"type\":\"ice\"}"),
                Is.EqualTo("Level level-1 door door-1 has an effect without type or params."));
            JObject emptyCellObject = JObject.Parse(valid);
            emptyCellObject["levels"][0]["cellObjects"] = JArray.Parse("[{}]");
            AssertReadRejected(emptyCellObject.ToString());
            Assert.That(ReadDoorEffect(JObject.Parse(valid), "{\"type\":\"star-door\",\"params\":{\"n\":5}}"),
                Is.EqualTo("Level level-1 door door-1 effect star-door parameter n needs a JSON string value."));
            Assert.That(ReadDoorEffect(JObject.Parse(valid), "{\"type\":\"star-door\",\"params\":[]}"),
                Does.StartWith("Invalid level JSON"));
        }

        [Test]
        public void ReadGivesLevelMessagesForEmptyNullAndMissingParameterStrings()
        {
            // Newtonsoft turns "" into null for a JToken member; the reader must keep "" so the
            // validator's level, entity, type and key message is the one the author sees.
            JObject document = JObject.Parse(Write(CreateLevel("level-1", "First", Vector2Int.zero)));

            Assert.That(ReadDoorEffect(document, "{\"type\":\"toggle-door\",\"params\":[{\"key\":\"startsOpen\",\"value\":\"\"}]}"),
                Is.EqualTo("Level level-1 door door-1 effect toggle-door key startsOpen has value ''; expected true or false."));
            Assert.That(ReadDoorEffect(document, "{\"type\":\"colorful-door\",\"params\":[{\"key\":\"colorSequence\",\"value\":\"\"}]}"),
                Is.EqualTo("Level level-1 door door-1 effect colorful-door key colorSequence has 0 items in ''; expected 1 or more items."));
            Assert.That(ReadDoorEffect(document, "{\"type\":\"toggle-door\",\"params\":[{\"key\":\"\",\"value\":\"true\"}]}"),
                Is.EqualTo("Level level-1 door door-1 effect toggle-door has an empty key."));
            Assert.That(ReadDoorEffect(document, "{\"type\":\"toggle-door\",\"params\":[{\"key\":\"startsOpen\",\"value\":null}]}"),
                Is.EqualTo("Level level-1 door door-1 effect toggle-door parameter startsOpen needs a JSON string key and value."));
            Assert.That(ReadDoorEffect(document, "{\"type\":\"toggle-door\",\"params\":[{\"key\":\"startsOpen\"}]}"),
                Is.EqualTo("Level level-1 door door-1 effect toggle-door parameter startsOpen needs a JSON string key and value."));
            Assert.That(ReadDoorEffect(document, "{\"type\":\"toggle-door\",\"params\":[{\"key\":\"startsOpen\",\"value\":true}]}"),
                Is.EqualTo("Level level-1 door door-1 effect toggle-door parameter startsOpen needs a JSON string key and value."));

            document["levels"][0]["doors"][0]["effects"] =
                JArray.Parse("[{\"type\":\"toggle-door\",\"params\":[{\"key\":\"startsOpen\",\"value\":\"true\"}]}]");
            string valid = document.ToString();
            Assert.That(LevelDatabaseJson.TryRead(valid, LevelReadMode.Authoring, out LevelData[] levels,
                out string error), Is.True, error);
            Assert.That(levels[0].GetDoorAt(0).GetEffectAt(0).GetParamAt(0).Value, Is.EqualTo("true"));
        }

        [Test]
        public void ReadRequiresNamedBoardSide()
        {
            string valid = Write(CreateLevel("level-1", "First", Vector2Int.zero));
            string numericSide = valid.Replace("\"side\": \"Right\"", "\"side\": \"3\"");
            Assert.That(numericSide, Is.Not.EqualTo(valid));
            AssertReadRejected(numericSide);
        }

        [Test]
        public void ReferenceLevelsLoadForAuthoringWithVoidCellsTypedArrowsAndIdenticalRoundTrip()
        {
            string json = File.ReadAllText(GetReferencePath("reference-levels.json"));
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] levels,
                out string error), Is.True, error);
            Assert.That(levels, Has.Length.EqualTo(10));

            LevelData levelNine = levels[8];
            Assert.That(levelNine.Id, Is.EqualTo("level-09"));
            Assert.That(levelNine.VoidCellCount, Is.EqualTo(2));
            Assert.That(levelNine.GetVoidCellAt(0), Is.EqualTo(new Vector2Int(0, 0)));
            Assert.That(levelNine.GetVoidCellAt(1), Is.EqualTo(new Vector2Int(5, 0)));
            Assert.That(levelNine.IsFloor(new Vector2Int(0, 0)), Is.False);
            Assert.That(levelNine.IsFloor(new Vector2Int(1, 0)), Is.True);

            LevelData levelSeven = levels[6];
            Assert.That(levelSeven.GetBlockAt(0).TryGetEffect(out ArrowEffect horizontal), Is.True);
            Assert.That(horizontal.Axis, Is.EqualTo(LevelEffectAxis.Horizontal));
            Assert.That(levelSeven.GetBlockAt(2).TryGetEffect(out ArrowEffect vertical), Is.True);
            Assert.That(vertical.Axis, Is.EqualTo(LevelEffectAxis.Vertical));

            string written = Write(levels);
            Assert.That(JToken.DeepEquals(JToken.Parse(json), JToken.Parse(written)), Is.True);
            Assert.That(LevelDatabaseJson.TryRead(written, LevelReadMode.Authoring, out LevelData[] again,
                out error), Is.True, error);
            Assert.That(Write(again), Is.EqualTo(written));
        }

        [Test]
        public void RuntimeReadRefusesReferenceLevelsWithAnUnimplementedEffect()
        {
            // The expected message follows the registry flags, so a gameplay owner's one-line flip keeps this green.
            string json = File.ReadAllText(GetReferencePath("reference-levels.json"));
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] authored,
                out string error), Is.True, error);
            string expected = DescribeFirstUnimplemented(authored);

            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Runtime, out LevelData[] levels, out error),
                Is.EqualTo(expected == null), error);
            Assert.That(levels == null, Is.EqualTo(expected != null));
            Assert.That(error, Is.EqualTo(expected));
        }

        [Test]
        public void ProjectScopeDatabaseLoadsTheProjectJsonAndItsRewriteAndRefusesUnimplementedEffects()
        {
            // The game's read of the shipped database: its manifest, then every pack with the runtime gate (DEC-196).
            LevelDatabaseSnapshot shipped = ShippedLevels.ReadDatabase();
            LevelCatalog catalog = new LevelCatalog();
            Assert.That(catalog.LoadManifestJson(shipped.ManifestJson, out string error), Is.True, error);
            for (int index = 0; index < shipped.Packs.Count; index++)
            {
                Assert.That(catalog.LoadPackJson(index, shipped.Packs[index].Json, LevelReadMode.Runtime, out error),
                    Is.True, error);
            }

            int count = catalog.Count;
            LevelData[] levels = ShippedLevels.Read();
            string v2 = Write(levels);
            Assert.That(catalog.LoadJson(v2, LevelReadMode.Runtime, out error), Is.True, error);
            Assert.That(catalog.Count, Is.EqualTo(count));

            // LevelCatalogLoader.LoadPackAsync reads with LevelReadMode.Runtime; the 2-argument LoadJson is authoring.
            string planned = File.ReadAllText(GetReferencePath("reference-levels.json"));
            Assert.That(catalog.LoadJson(planned, out error), Is.True, error);
            Assert.That(LevelDatabaseJson.TryRead(planned, out LevelData[] authored, out error), Is.True, error);
            string expected = DescribeFirstUnimplemented(authored);
            Assert.That(catalog.LoadJson(planned, LevelReadMode.Runtime, out error), Is.EqualTo(expected == null),
                error);
            Assert.That(error, Is.EqualTo(expected));
        }

        // D-001: every Level Editor read path is an authoring read, so a level with effects and a board
        // object survives Save, Reload and a second Save; only the project-scope runtime read refuses it.
        [Test]
        public void EditorPathsSaveReloadAndSaveAgainAnArrowAndBarrierDatabase()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ColorBlockJamLevelTests", Guid.NewGuid().ToString("N"))
                .Replace('\\', '/');
            string backupFolder = $"{directory}/Backup";
            LevelCatalog catalog = new LevelCatalog();
            try
            {
                Directory.CreateDirectory(directory);
                Assert.That(LevelDatabaseFiles.TryCreate(directory, "Levels", out string path, out string error), Is.True,
                    error);
                Assert.That(LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot created, out error), Is.True, error);

                LevelData level = LevelEffectTests.Build(
                    b3: new[] { LevelEffectTests.Effect("arrow", ("axis", "Vertical")) },
                    objects: new[]
                    {
                        LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                            new Vector2Int(4, 1), new Vector2Int(4, 2))
                    });
                Assert.That(LevelDatabaseFiles.TrySave(created, new[] { level }, backupFolder, null,
                    out LevelDatabaseSnapshot first, out _, out error), Is.True, error);
                string firstSave = first.Packs[0].Json;

                // Reload, then Save again from the reloaded files (the step that failed before the fix).
                Assert.That(LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot reloaded, out error), Is.True, error);
                Assert.That(reloaded.Packs[0].Json, Is.EqualTo(firstSave));
                IReadOnlyList<LevelData> levels = reloaded.Levels;
                Assert.That(levels[0].GetBlockAt(2).TryGetEffect(out ArrowEffect arrow), Is.True);
                Assert.That(arrow.Axis, Is.EqualTo(LevelEffectAxis.Vertical));
                Assert.That(levels[0].GetCellObjectAt(0).TryGetEffect(out BarrierObject barrier), Is.True);
                Assert.That(barrier.StartsOpen, Is.False);
                Assert.That(LevelDatabaseFiles.TrySave(reloaded, levels, backupFolder, null,
                    out LevelDatabaseSnapshot second, out _, out error), Is.True, error);
                string secondSave = second.Packs[0].Json;
                Assert.That(secondSave, Is.EqualTo(firstSave));
                Assert.That(LevelDatabaseFiles.TryRead(path, out _, out error), Is.True, error);
                Assert.That(catalog.LoadJson(secondSave, out error), Is.True, error);
                Assert.That(catalog.Count, Is.EqualTo(1));

                // The project-scope read names the first effect whose gameplay is not implemented.
                string expected = DescribeFirstUnimplemented(levels);
                Assert.That(catalog.LoadJson(secondSave, LevelReadMode.Runtime, out error),
                    Is.EqualTo(expected == null), error);
                Assert.That(error, Is.EqualTo(expected));
                // Arrow is implemented (BLK-01), so the gate passes block b3 and names the barrier while it is not.
                Assert.That(LevelEffectTests.IsImplemented("arrow"), Is.True);
                if (!LevelEffectTests.IsImplemented("barrier"))
                {
                    Assert.That(error, Is.EqualTo("Level fx cell object o1 effect barrier is not implemented in gameplay yet."));
                }
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        // The game loads into its own catalog (DEC-192), so a failed project-scope load cannot empty the levels an
        // open Level Editor reads, even when Play Mode starts without a domain reload. This covers the missing-source
        // failure; the runtime-gate refusal comes after the Addressables read, which needs a Level Database group
        // entry and is not reachable from this test.
        [Test]
        public void FailedProjectScopeLoadLeavesTheEditorCatalogAlone()
        {
            LevelCatalog editorLevels = new LevelCatalog();
            LevelData level = LevelEffectTests.Build(
                b3: new[] { LevelEffectTests.Effect("arrow", ("axis", "Vertical")) },
                objects: new[]
                {
                    LevelEffectTests.Object("o1", LevelEffectTests.Effect("barrier", ("startsOpen", "false")),
                        new Vector2Int(4, 1), new Vector2Int(4, 2))
                });
            Assert.That(editorLevels.LoadJson(Write(level), out string error), Is.True, error);

            LevelCatalog gameLevels = new LevelCatalog(null);
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await LevelCatalogLoader.LoadManifestAsync(gameLevels));

            Assert.Throws<InvalidOperationException>(() => { _ = gameLevels.Count; });
            Assert.That(editorLevels.Count, Is.EqualTo(1));
            Assert.That(editorLevels.GetAt(0).GetCellObjectAt(0).TryGetEffect(out BarrierObject _), Is.True);
        }

        [Test]
        public void EveryCurrentLevelKeepsDoorsWithinOneToThreeCells()
        {
            // Level totals change as designers edit, so this checks door spans only, never a count.
            AssertDoorsWithinOneToThreeCells(ShippedLevels.Read());
            AssertDoorsWithinOneToThreeCells(GetReferencePath("reference-levels.json"));
            AssertDoorsWithinOneToThreeCells(GetReferencePath("reference-levels.v1.json"));
        }

        private static void AssertDoorsWithinOneToThreeCells(string path)
        {
            Assert.That(LevelDatabaseJson.TryRead(File.ReadAllText(path), LevelReadMode.Authoring,
                out LevelData[] levels, out string error), Is.True, $"{path}: {error}");
            AssertDoorsWithinOneToThreeCells(levels);
        }

        private static void AssertDoorsWithinOneToThreeCells(IReadOnlyList<LevelData> levels)
        {
            foreach (LevelData level in levels)
            {
                for (int index = 0; index < level.DoorCount; index++)
                {
                    Assert.That(level.GetDoorAt(index).Length, Is.InRange(1, LevelData.MaxDoorLength), level.Id);
                }
            }
        }

        private static string DescribeFirstUnimplemented(IReadOnlyList<LevelData> levels)
        {
            for (int index = 0; index < levels.Count; index++)
            {
                string message = LevelEffectTests.DescribeFirstUnimplemented(levels[index]);
                if (message != null)
                {
                    return message;
                }
            }

            return null;
        }

        [Test]
        public void DoorLongerThanThreeCellsIsRejected()
        {
            LevelData level = new LevelData("wide", "Wide", 5, 5, 60,
                new[] { new LevelDoorData("d1", BoardSide.Top, 0, 4, "Red") },
                new[] { new LevelBlockData("b1", "Red", Vector2Int.zero, new[] { Vector2Int.zero }) });
            Assert.That(level.Validate(out string error), Is.False);
            Assert.That(error, Is.EqualTo("Level wide door d1 spans 4 cells; gates span 1 to 3."));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out string json, out _), Is.False);
            Assert.That(json, Is.Null);
        }

        [TestCase("outside", "Level void void cell (5, 0) is outside the board.")]
        [TestCase("duplicate", "Level void lists void cell (4, 4) twice.")]
        [TestCase("no-floor", "Level void has no floor cell.")]
        [TestCase("disconnected", "Level void floor cells are not four-connected.")]
        [TestCase("block", "Level void block b1 has a cell on void cell (0, 0).")]
        [TestCase("door", "Level void door d1 opens onto void cell (4, 4).")]
        [TestCase("color", "Level void door d1 uses 'Wall', which is not a gate colour.")]
        public void VoidCellAndDoorRulesRejectWithLevelMessages(string rule, string expected)
        {
            LevelData level = CreateVoidLevel(rule);
            Assert.That(level.Validate(out string error), Is.False);
            Assert.That(error, Is.EqualTo(expected));
            Assert.That(LevelDatabaseJson.TryWrite(new[] { level }, out _, out string writeError), Is.False);
            Assert.That(writeError, Is.EqualTo(expected));
        }

        [Test]
        public void VoidCellsRoundTripInOrderThroughTheLevelApi()
        {
            LevelData level = CreateVoidLevel("valid");
            Assert.That(level.Validate(out string error), Is.True, error);
            string json = Write(level);
            Assert.That(LevelDatabaseJson.TryRead(json, out LevelData[] loaded, out error), Is.True, error);
            Assert.That(loaded[0].VoidCellCount, Is.EqualTo(2));
            Assert.That(loaded[0].GetVoidCellAt(0), Is.EqualTo(new Vector2Int(4, 4)));
            Assert.That(loaded[0].GetVoidCellAt(1), Is.EqualTo(new Vector2Int(3, 4)));
            Assert.That(loaded[0].Clone().GetVoidCellAt(1), Is.EqualTo(new Vector2Int(3, 4)));
        }

        // AC-7 static half: IL2CPP stripping keeps every type the level JSON path creates. The device
        // smoke build remains the proof; this test fails as soon as a DTO or typed parameter class is
        // added or renamed without its link.xml entry.
        [Test]
        public void LinkXmlPreservesEveryJsonDtoAndTypedEffectParameterClass()
        {
            XmlDocument linker = new XmlDocument();
            linker.Load(Path.Combine(Application.dataPath, "ColorBlockJam", "Runtime", "Core", "Levels", "link.xml"));
            Assembly runtime = typeof(LevelDatabaseJson).Assembly;
            HashSet<string> preserved = new HashSet<string>(StringComparer.Ordinal);
            foreach (XmlElement entry in linker.SelectNodes(
                         $"/linker/assembly[@fullname='{runtime.GetName().Name}']/type[@preserve='all']"))
            {
                string name = entry.GetAttribute("fullname");
                Assert.That(runtime.GetType(name.Replace('/', '+')), Is.Not.Null, $"Stale link.xml entry {name}.");
                preserved.Add(name);
            }

            List<Type> required = new List<Type>
            {
                typeof(LevelEffectData), typeof(LevelEffectParamData), typeof(LevelCellObjectData),
                typeof(LevelEffectParameters)
            };
            int dtoCount = 0;
            foreach (Type nested in typeof(LevelDatabaseJson).GetNestedTypes(BindingFlags.NonPublic))
            {
                if (nested.Name.EndsWith("Dto", StringComparison.Ordinal))
                {
                    required.Add(nested);
                    dtoCount++;
                }
            }

            int parameterCount = 0;
            foreach (Type type in runtime.GetTypes())
            {
                if (type.IsSubclassOf(typeof(LevelEffectParameters)))
                {
                    required.Add(type);
                    parameterCount++;
                }
            }

            // Lower bounds only guard the reflection scan; new types are covered by the loop below.
            Assert.That(dtoCount, Is.GreaterThanOrEqualTo(8));
            // DEC-177 left Arrow, Ice and the board objects' parameter classes.
            Assert.That(parameterCount, Is.GreaterThanOrEqualTo(11));
            foreach (Type type in required)
            {
                Assert.That(preserved, Does.Contain(type.FullName.Replace('+', '/')),
                    $"link.xml does not preserve {type.FullName}.");
            }
        }

        internal static string GetReferencePath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "levels", fileName));
        }

        private static LevelData CreateVoidLevel(string rule)
        {
            Vector2Int[] voidCells = { new Vector2Int(4, 4), new Vector2Int(3, 4) };
            string doorColor = "Red";
            int doorStart = 0;
            Vector2Int blockOrigin = Vector2Int.zero;
            switch (rule)
            {
                case "outside":
                    voidCells = new[] { new Vector2Int(5, 0) };
                    break;
                case "duplicate":
                    voidCells = new[] { new Vector2Int(4, 4), new Vector2Int(4, 4) };
                    break;
                case "no-floor":
                    voidCells = new Vector2Int[25];
                    for (int index = 0; index < 25; index++)
                    {
                        voidCells[index] = new Vector2Int(index % 5, index / 5);
                    }

                    break;
                case "disconnected":
                    // A void column at x = 2 splits the board into two floor halves.
                    voidCells = new Vector2Int[5];
                    for (int y = 0; y < 5; y++)
                    {
                        voidCells[y] = new Vector2Int(2, y);
                    }

                    break;
                case "block":
                    voidCells = new[] { new Vector2Int(0, 0) };
                    break;
                case "door":
                    doorStart = 4;
                    break;
                case "color":
                    doorColor = "Wall";
                    break;
            }

            return new LevelData("void", "Void", 5, 5, 60, voidCells,
                new[] { new LevelDoorData("d1", BoardSide.Top, doorStart, 1, doorColor) },
                new[] { new LevelBlockData("b1", "Red", blockOrigin, new[] { Vector2Int.zero }) },
                new LevelCellObjectData[0]);
        }

        private static string Write(params LevelData[] levels)
        {
            Assert.That(LevelDatabaseJson.TryWrite(levels, out string json, out string error),
                Is.True, error);
            return json;
        }

        private static string AssertReadRejected(string json)
        {
            Assert.That(LevelDatabaseJson.TryRead(json, LevelReadMode.Authoring, out LevelData[] levels,
                out string error), Is.False, json);
            Assert.That(levels, Is.Null);
            Assert.That(error, Is.Not.Empty);
            return error;
        }

        // Sets the first door's effects to one effect record and returns the authoring read's error.
        private static string ReadDoorEffect(JObject document, string effectJson)
        {
            document["levels"][0]["doors"][0]["effects"] = new JArray(JObject.Parse(effectJson));
            return AssertReadRejected(document.ToString());
        }

        private static LevelData CreateLevel(string id, string name, Vector2Int origin)
        {
            return new LevelData(id, name, 3, 3, 60,
                new[] { new LevelDoorData("door-1", BoardSide.Right, 0, 1, "Red") },
                new[]
                {
                    new LevelBlockData("block-1", "Red", origin,
                        new[] { Vector2Int.zero, new Vector2Int(1, 0) })
                });
        }
    }
}
