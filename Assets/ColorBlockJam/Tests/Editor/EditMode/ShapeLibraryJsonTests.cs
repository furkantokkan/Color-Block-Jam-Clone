using System;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies BlockShapeLibraryJson's read/write/merge schema (DEC-120): round-tripping, rejecting invalid or
    /// case-spoofed fields, key and name conflicts, and that the shipped library matches its canonical JSON. A template
    /// is a {name, cells} record whose key comes from its cells (DEC-193), so the enum identity, prefab binding and
    /// migration snapshot cases were removed.
    /// </summary>
    public sealed class ShapeLibraryJsonTests
    {
        private static BlockShapeLibrary.Shape Shape(string name, params Vector2Int[] cells) =>
            new BlockShapeLibrary.Shape(name, cells);

        [Test]
        public void RoundTripKeepsNamesOrderAndCopiedCells()
        {
            var cells = new[] { Vector2Int.zero, Vector2Int.right };
            var shape = Shape("Türkçe display name", cells);
            cells[0] = new Vector2Int(9, 9);
            var records = new[] { shape, Shape("Second", Vector2Int.zero) };
            var result = BlockShapeLibraryJson.Read(BlockShapeLibraryJson.Write(records));
            Assert.That(result.Length, Is.EqualTo(2));
            Assert.That(result[0].DisplayName, Is.EqualTo("Türkçe display name"));
            Assert.That(result[0].Cells[0], Is.EqualTo(Vector2Int.zero));
            Assert.That(result[1].DisplayName, Is.EqualTo("Second"));
        }

        [Test]
        public void AnEmptyVersionTwoLibraryReadsAsNoTemplates() =>
            Assert.That(BlockShapeLibraryJson.Read("{\"formatVersion\":2,\"shapes\":[]}"), Is.Empty);

        [TestCase("{}")]
        [TestCase("{\"formatVersion\":1,\"shapes\":[]}")]
        [TestCase("{\"formatVersion\":2,\"shapes\":null}")]
        [TestCase("{\"formatVersion\":2,\"formatVersion\":2,\"shapes\":[]}")]
        [TestCase("{\"formatVersion\":2,\"shapes\":[],\"$type\":\"System.Object\"}")]
        [TestCase("{\"formatVersion\":2,\"shapes\":[],\"prefab\":\"Assets/foreign.prefab\"}")]
        [TestCase("{\"formatVersion\":\"2\",\"shapes\":[]}")]
        [TestCase("{\"formatVersion\":2,\"shapes\":[{\"name\":\"Single\",\"cells\":\"0,0\",\"id\":0}]}")]
        [TestCase("{\"formatVersion\":2,\"shapes\":[{\"name\":\"Single\",\"cells\":[{\"x\":0,\"y\":0}]}]}")]
        public void InvalidSchemaCannotEnterLibrary(string json) =>
            Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Read(json));

        [TestCase("\"formatVersion\": 2", "\"formatVersion\": 2, \"FormatVersion\": \"1\"")]
        [TestCase("\"name\": \"Single\"", "\"name\": \"Single\", \"Name\": \"Square\"")]
        [TestCase("\"cells\": \"0,0\"", "\"cells\": \"0,0\", \"Cells\": \"0,0 1,0\"")]
        public void CaseVariantFieldsCannotOverrideValidatedValues(string original, string replacement)
        {
            string json = BlockShapeLibraryJson.Write(new[] { Shape("Single", Vector2Int.zero) });
            Assert.That(json, Does.Contain(original));
            Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Read(json.Replace(original, replacement)));
        }

        [Test]
        public void DuplicateIdentityDisconnectedDuplicateOrUnnormalizedCellsAreRejected()
        {
            var single = Shape("Single", Vector2Int.zero);
            Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Write(new[] { single, single }));
            foreach (var cells in new[]{
                new[]{Vector2Int.zero,new Vector2Int(3,0)}, new[]{Vector2Int.zero,Vector2Int.zero},
                new[]{Vector2Int.right},new[]{new Vector2Int(-1,0)},Array.Empty<Vector2Int>()})
                Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Write(new[] { Shape("Invalid", cells) }));
        }

        [Test]
        public void MergeSkipsIdenticalCellSetsAppendsNewAndRejectsKeyOrNameConflicts()
        {
            var first = Shape("Double", Vector2Int.zero, Vector2Int.right);
            var reversed = Shape("Double", Vector2Int.right, Vector2Int.zero);
            var second = Shape("Single", Vector2Int.zero);
            var merged = BlockShapeLibraryJson.Merge(new[] { first }, new[] { reversed, second });
            Assert.That(merged.Length, Is.EqualTo(2));
            Assert.That(merged[0], Is.SameAs(first));
            Assert.That(merged[1].DisplayName, Is.EqualTo("Single"));
            // The same piece (key) under another name, and another piece under the same name.
            Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Merge(new[] { first },
                new[] { Shape("Conflicting name", first.Cells[0], first.Cells[1]) }));
            Assert.Throws<ArgumentException>(() => BlockShapeLibraryJson.Merge(new[] { first },
                new[] { Shape("Double", Vector2Int.zero) }));
        }

        [Test]
        public void ImportUsesReplacementAndMergeUsesUnion()
        {
            var library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            try
            {
                ShapeLibraryAuthoring.Import(library, BlockShapeLibraryJson.Write(new[] { Shape("Single", Vector2Int.zero) }), false);
                // A key is the footprint, so the second piece needs its own cells (DEC-193).
                string incoming = BlockShapeLibraryJson.Write(new[] { Shape("Cross", new Vector2Int(1, 0),
                    new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(1, 2)) });
                ShapeLibraryAuthoring.Import(library, incoming, true);
                Assert.That(library.Shapes.Count, Is.EqualTo(2));
                ShapeLibraryAuthoring.Import(library, incoming, false);
                Assert.That(library.Shapes.Count, Is.EqualTo(1));
                Assert.That(library.Shapes[0].DisplayName, Is.EqualTo("Cross"));
                ShapeLibraryAuthoring.Import(library, "{\"formatVersion\":2,\"shapes\":[]}", false);
                Assert.That(library.Shapes, Is.Empty);
            }
            finally { ShapeLibraryAuthoring.ClearHistory(library); Object.DestroyImmediate(library); }
        }

        [Test]
        public void ProductionLibraryMatchesCanonicalJson()
        {
            var library = BlockShapeLibrary.LoadProject();
            Assert.That(library.JsonPath, Is.EqualTo(BoardKitAssets.ShapesJsonPath));
            var expected = BlockShapeLibraryJson.Read(File.ReadAllText(library.JsonPath));
            Assert.That(library.Shapes.Count, Is.EqualTo(expected.Length));
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(BlockShapeLibraryJson.Equal(library.Shapes[i], expected[i]), Is.True);
            }

            Assert.That(new SerializedObject(library).FindProperty("m_shapes"), Is.Null);
        }

        [Test]
        public void ExportWritesPortableSavedJsonAndRefusesProjectAssets()
        {
            var library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            string path = Path.Combine(Path.GetTempPath(), "piece-export-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                ShapeLibraryAuthoring.SavePiece(library, default, "Single", new[] { Vector2Int.zero }, null);
                string saved = library.LoadedJson;
                ShapeLibraryAuthoring.Export(library, path);
                Assert.That(File.ReadAllText(path), Is.EqualTo(saved));
                Assert.That(library.LoadedJson, Is.EqualTo(saved));
                Assert.Throws<ArgumentException>(() => ShapeLibraryAuthoring.Export(library, BoardKitAssets.ShapesJsonPath));
            }
            finally
            {
                ShapeLibraryAuthoring.ClearHistory(library); Object.DestroyImmediate(library);
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
