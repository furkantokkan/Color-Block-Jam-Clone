using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Art;
using ColorBlockJam.Runtime.Levels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Covers the CBJ importer's acceptance criteria (production/tasks/TASK-20260924-210811262-93892dee-cbj-level-import):
    /// AC-1 the worked example, AC-2 an unmapped colour, AC-3 precise structural rejections, and AC-4 that
    /// "$type" never resolves a type. AC-5 (the interactive menu command) is a manual Editor check. The import writes
    /// block cells only and matches no library shape (DEC-193).
    /// </summary>
    public sealed class CbjLevelImportTests
    {
        // ----- AC-1: the stored worked example -----

        [Test]
        public void ExampleConvertsToWorkedResult_WithMappedColorTable()
        {
            string json = File.ReadAllText(GetFixturePath("cbj-example-level.json"));
            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(mechanics, Is.Empty);

            CbjColorTable table = CreateTable(WithBct11(PaletteColor.LightTurquoise));

            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, table,
                    "cbj-example", "CBJ Example", 60, out LevelData level, out List<string> warnings,
                    out string convertError),
                Is.True, convertError);

            Assert.That(level.Width, Is.EqualTo(5));
            Assert.That(level.Height, Is.EqualTo(7));
            Assert.That(level.VoidCellCount, Is.EqualTo(0));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings[0], Does.Contain("b6"));
            Assert.That(warnings[0], Does.Contain("duplicate"));

            Assert.That(level.DoorCount, Is.EqualTo(5));
            AssertDoor(level, 0, BoardSide.Top, 0, 1, "Pink");
            AssertDoor(level, 1, BoardSide.Top, 4, 1, "LightTurquoise");
            AssertDoor(level, 2, BoardSide.Top, 1, 3, "Red");
            AssertDoor(level, 3, BoardSide.Left, 3, 2, "Yellow");
            AssertDoor(level, 4, BoardSide.Left, 0, 3, "LightBlue");

            Assert.That(level.BlockCount, Is.EqualTo(8));
            AssertBlock(level, 0, "Pink", new[] { V(0, 0), V(0, 1), V(0, 2) });
            AssertBlock(level, 1, "LightTurquoise", new[] { V(0, 0), V(0, 1), V(0, 2) });
            AssertBlock(level, 2, "Pink", new[] { V(0, 0), V(0, 1), V(0, 2) });
            AssertBlock(level, 3, "LightTurquoise", new[] { V(0, 0), V(0, 1), V(0, 2) });
            AssertBlock(level, 4, "Pink", new[] { V(0, 0), V(0, 1), V(0, 2) });
            AssertBlock(level, 5, "Red", new[] { V(0, 0), V(0, 1), V(1, 1) });
            AssertBlock(level, 6, "Yellow", new[] { V(0, 0), V(1, 0), V(1, 1), V(2, 0) });
            AssertBlock(level, 7, "LightBlue", new[] { V(0, 1), V(1, 1), V(1, 0) });

            Assert.That(level.Validate(out string validateError), Is.True, validateError);
        }

        // ----- AC-2: the default colour table fails, naming BCT 11 and every entity that uses it -----

        [Test]
        public void ExampleWithDefaultColorTable_FailsNamingBct11AndItsUsers()
        {
            string json = File.ReadAllText(GetFixturePath("cbj-example-level.json"));
            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);

            CbjColorTable table = CreateTable(CbjColorTable.CreateDefaultEntries());

            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, table,
                    "cbj-example", "CBJ Example", 60, out LevelData level, out _, out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("11"));
            Assert.That(error, Does.Contain("b2"));
            Assert.That(error, Does.Contain("b4"));
            Assert.That(error, Does.Contain("d2"));
        }

        // ----- AC-5 (automated slice): the converted level appends through the existing validated save path -----

        [Test]
        public void ConvertedLevelAppendsThroughTheDatabaseSave()
        {
            string directory = Path.Combine(Path.GetTempPath(),
                "ColorBlockJamCbjImport-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            Directory.CreateDirectory(directory);
            string backupFolder = $"{directory}/backups";
            try
            {
                // The database save the importer uses writes the pack that changes (DEC-196).
                Assert.That(LevelDatabaseFiles.TryCreate(directory, "Levels", out string path, out string createError),
                    Is.True, createError);
                Assert.That(LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot initial, out string readError),
                    Is.True, readError);
                Assert.That(initial.Levels, Is.Empty);

                string json = File.ReadAllText(GetFixturePath("cbj-example-level.json"));
                Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                    out string cbjError), Is.True, cbjError);
                CbjColorTable table = CreateTable(WithBct11(PaletteColor.LightTurquoise));
                Assert.That(CbjLevelConverter.TryConvert(source, mechanics, table,
                        "cbj-example", "CBJ Example", 60, out LevelData level, out _, out string convertError),
                    Is.True, convertError);

                Assert.That(LevelDatabaseFiles.TrySave(initial, new[] { level }, backupFolder, null, out _, out _,
                    out string saveError), Is.True, saveError);
                Assert.That(LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot loaded, out readError),
                    Is.True, readError);
                Assert.That(loaded.Levels.Count, Is.EqualTo(1));
                Assert.That(loaded.Levels[0].Id, Is.EqualTo("cbj-example"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        // ----- AC-3: precise structural rejections -----

        [Test]
        public void MissingFloorCellBecomesVoidCell()
        {
            string json = BuildMinimalLevel(out Vector2Int[] floor, out JArray doors, out JArray blocks,
                removeFloorCell: new Vector2Int(1, 1));

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "void-test", "Void Test", 60, out LevelData level, out _, out string error),
                Is.True, error);
            Assert.That(level.VoidCellCount, Is.EqualTo(1));
            Assert.That(level.GetVoidCellAt(0), Is.EqualTo(V(1, 1)));
        }

        [Test]
        public void NonContiguousDoorCellsRejected()
        {
            JArray doors = new JArray { DoorToken(new[] { V(0, 3), V(2, 3) }, true, 0) };
            string json = BuildMinimalLevel(out _, out _, out JArray blocks, doorsOverride: doors);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "door-test", "Door Test", 60, out LevelData level, out _, out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("non-contiguous"));
        }

        [Test]
        public void CornerDoorCellRejected()
        {
            JArray doors = new JArray { DoorToken(new[] { V(-1, 3) }, true, 0) };
            string json = BuildMinimalLevel(out _, out _, out JArray blocks, doorsOverride: doors);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "corner-test", "Corner Test", 60, out LevelData level, out _, out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("corner"));
        }

        [Test]
        public void DoorIhContradictingSideRejected()
        {
            JArray doors = new JArray { DoorToken(new[] { V(1, 3) }, false, 0) };
            string json = BuildMinimalLevel(out _, out _, out JArray blocks, doorsOverride: doors);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "ih-test", "IH Test", 60, out LevelData level, out _, out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("IH"));
            Assert.That(error, Does.Contain("Top"));
        }

        [Test]
        public void ExtraWallCellRejected()
        {
            // Build the baseline first so the extra wall cell (the door's own cell) is unambiguous.
            string baseline = BuildMinimalLevel(out _, out JArray doors, out JArray blocks);
            JObject root = JObject.Parse(baseline);
            ((JArray)root["WMS"]).Add(WallOrFloorToken("WM", V(1, 3), 1));
            string json = root.ToString(Formatting.None);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "wall-test", "Wall Test", 60, out LevelData level, out _, out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("unsupported"));
        }

        [Test]
        public void NonDefaultMechanicFieldRejected()
        {
            JArray blocks = new JArray
            {
                BlockToken(new[] { V(0, 0) }, 0, new Dictionary<string, object> { ["BHS"] = true })
            };
            string json = BuildMinimalLevel(out _, out JArray doors, out _, blocksOverride: blocks);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(mechanics, Has.Count.EqualTo(1));
            Assert.That(mechanics[0], Does.Contain("BHS"));

            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "mech-field-test", "Mechanic Field Test", 60, out LevelData level, out _,
                    out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("BHS"));
        }

        [Test]
        public void NonEmptyMechanicArrayRejected()
        {
            string baseline = BuildMinimalLevel(out _, out _, out _);
            JObject root = JObject.Parse(baseline);
            ((JArray)root["GMS"]).Add(new JObject { ["$type"] = "GM" });
            string json = root.ToString(Formatting.None);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(mechanics, Has.Count.EqualTo(1));
            Assert.That(mechanics[0], Does.Contain("GMS"));

            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "mech-array-test", "Mechanic Array Test", 60, out LevelData level, out _,
                    out string error),
                Is.False);
            Assert.That(level, Is.Null);
            Assert.That(error, Does.Contain("GMS"));
        }

        // ----- AC-4: a "$type" naming a real .NET type is never resolved -----

        [Test]
        public void TypeMetadataNamingARealTypeIsIgnored()
        {
            string baseline = BuildMinimalLevel(out _, out _, out _);
            JObject root = JObject.Parse(baseline);
            root["$type"] = "System.Diagnostics.Process, System";
            ((JObject)((JArray)root["BMS"])[0])["$type"] = "System.IO.FileInfo, mscorlib";
            string json = root.ToString(Formatting.None);

            Assert.That(CbjLevelReader.TryRead(json, out CbjLevelSource source, out List<string> mechanics,
                out string readError), Is.True, readError);
            Assert.That(CbjLevelConverter.TryConvert(source, mechanics, CreateTable(CbjColorTable.CreateDefaultEntries()),
                    "type-test", "Type Test", 60, out LevelData level, out _, out string error),
                Is.True, error);
            Assert.That(level.BlockCount, Is.EqualTo(1));
        }

        // ----- Fixtures and helpers -----

        private static void AssertDoor(
            LevelData level, int index, BoardSide side, int start, int length, string colorId)
        {
            LevelDoorData door = level.GetDoorAt(index);
            Assert.That(door.Side, Is.EqualTo(side), $"door {index} side");
            Assert.That(door.Start, Is.EqualTo(start), $"door {index} start");
            Assert.That(door.Length, Is.EqualTo(length), $"door {index} length");
            Assert.That(door.ColorId, Is.EqualTo(colorId), $"door {index} colorId");
        }

        private static void AssertBlock(
            LevelData level, int index, string colorId, Vector2Int[] expectedRelativeCells)
        {
            LevelBlockData block = level.GetBlockAt(index);
            Assert.That(block.ColorId, Is.EqualTo(colorId), $"block {index} colorId");
            HashSet<Vector2Int> actual = new HashSet<Vector2Int>();
            for (int cellIndex = 0; cellIndex < block.CellCount; cellIndex++)
            {
                actual.Add(block.GetCellAt(cellIndex));
            }

            Assert.That(actual, Is.EquivalentTo(expectedRelativeCells), $"block {index} cells");
        }

        private static CbjColorTable CreateTable(IReadOnlyList<CbjColorTable.Entry> entries)
        {
            CbjColorTable table = ScriptableObject.CreateInstance<CbjColorTable>();
            table.SetEntries(entries);
            return table;
        }

        private static List<CbjColorTable.Entry> WithBct11(PaletteColor bct11Color)
        {
            List<CbjColorTable.Entry> entries = new List<CbjColorTable.Entry>(CbjColorTable.CreateDefaultEntries())
            {
                new CbjColorTable.Entry(11, bct11Color)
            };
            return entries;
        }

        private static Vector2Int V(int x, int y) => new Vector2Int(x, y);

        private static string GetFixturePath(string fileName)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "docs", "levels", fileName));
        }

        private static Vector2Int[] MakeRectangle(int minX, int minY, int maxX, int maxY)
        {
            List<Vector2Int> cells = new List<Vector2Int>();
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    cells.Add(new Vector2Int(x, y));
                }
            }

            return cells.ToArray();
        }

        // A 3x3 floor (x:0-2, y:0-2), one Top door (single cell at (1,3), IH=true, BCT 0) and one Single
        // block (BCT 0) at (0,0), unless overridden.
        private static string BuildMinimalLevel(
            out Vector2Int[] floor, out JArray doors, out JArray blocks,
            JArray doorsOverride = null, JArray blocksOverride = null, Vector2Int? removeFloorCell = null)
        {
            List<Vector2Int> floorCells = new List<Vector2Int>(MakeRectangle(0, 0, 2, 2));
            if (removeFloorCell.HasValue)
            {
                floorCells.Remove(removeFloorCell.Value);
            }

            floor = floorCells.ToArray();
            doors = doorsOverride ?? new JArray { DoorToken(new[] { V(1, 3) }, true, 0) };
            blocks = blocksOverride ?? new JArray { BlockToken(new[] { V(0, 0) }, 0) };
            return BuildLevel(floor, doors, blocks);
        }

        private static string BuildLevel(Vector2Int[] floorCells, JArray doors, JArray blocks)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (Vector2Int cell in floorCells)
            {
                minX = Math.Min(minX, cell.x);
                maxX = Math.Max(maxX, cell.x);
                minY = Math.Min(minY, cell.y);
                maxY = Math.Max(maxY, cell.y);
            }

            JArray cms = new JArray();
            foreach (Vector2Int cell in floorCells)
            {
                cms.Add(WallOrFloorToken("CM", cell));
            }

            HashSet<Vector2Int> doorCells = new HashSet<Vector2Int>();
            foreach (JObject door in doors)
            {
                foreach (JObject cell in (JArray)door["BPMS"])
                {
                    doorCells.Add(new Vector2Int(cell["x"].Value<int>(), cell["y"].Value<int>()));
                }
            }

            JArray wms = new JArray();
            foreach (Vector2Int ringCell in ComputeRing(minX, minY, maxX, maxY))
            {
                if (!doorCells.Contains(ringCell))
                {
                    wms.Add(WallOrFloorToken("WM", ringCell, 1));
                }
            }

            JObject root = new JObject
            {
                ["$type"] = "LM",
                ["BMS"] = blocks,
                ["DMS"] = doors,
                ["WMS"] = wms,
                ["CMS"] = cms,
                ["IWMS"] = new JArray(),
                ["GMS"] = new JArray(),
                ["EMS"] = new JArray(),
                ["CLMS"] = new JArray(),
                ["CCMS"] = new JArray(),
                ["GRM"] = new JArray(),
                ["CBMS"] = new JArray(),
                ["IBG"] = new JArray(),
                ["CCLMS"] = new JArray(),
                ["IMS"] = new JArray(),
                ["BSP"] = new JArray { Cell(minX - 1, minY - 1) }
            };
            return root.ToString(Formatting.None);
        }

        private static IEnumerable<Vector2Int> ComputeRing(int minX, int minY, int maxX, int maxY)
        {
            for (int x = minX - 1; x <= maxX + 1; x++)
            {
                yield return new Vector2Int(x, minY - 1);
                yield return new Vector2Int(x, maxY + 1);
            }

            for (int y = minY; y <= maxY; y++)
            {
                yield return new Vector2Int(minX - 1, y);
                yield return new Vector2Int(maxX + 1, y);
            }
        }

        private static JObject Cell(int x, int y)
        {
            return new JObject { ["$type"] = "BPM", ["x"] = x, ["y"] = y };
        }

        private static JObject WallOrFloorToken(string cbjType, Vector2Int cell, int? boardIndex = null)
        {
            JObject token = new JObject { ["$type"] = cbjType, ["BPM"] = Cell(cell.x, cell.y) };
            if (boardIndex.HasValue)
            {
                token["BI"] = boardIndex.Value;
            }

            return token;
        }

        private static JObject DoorToken(Vector2Int[] cells, bool isHorizontal, int bct)
        {
            JArray bpms = new JArray();
            foreach (Vector2Int cell in cells)
            {
                bpms.Add(Cell(cell.x, cell.y));
            }

            return new JObject
            {
                ["$type"] = "DM", ["BCT"] = bct, ["BPMS"] = bpms, ["IH"] = isHorizontal,
                ["BI"] = 1, ["DIC"] = 0, ["TBD"] = 0, ["DHS"] = false
            };
        }

        private static JObject BlockToken(Vector2Int[] cells, int bct, IDictionary<string, object> overrides = null)
        {
            JArray bpms = new JArray();
            foreach (Vector2Int cell in cells)
            {
                bpms.Add(Cell(cell.x, cell.y));
            }

            JObject token = new JObject
            {
                ["$type"] = "BM", ["BCT"] = bct, ["BPMS"] = bpms,
                ["BIC"] = 0, ["BAD"] = 0, ["KID"] = 0, ["LID"] = 0, ["BHS"] = false,
                ["BD"] = 0, ["ILE"] = false, ["LBCT"] = 0, ["LIC"] = 0, ["AD"] = 0
            };
            if (overrides != null)
            {
                foreach (KeyValuePair<string, object> pair in overrides)
                {
                    token[pair.Key] = JToken.FromObject(pair.Value);
                }
            }

            return token;
        }
    }
}
