using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Editor.Levels;
using ColorBlockJam.Runtime.Board;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies the Save Level template commit: the replaced records keep their name and order, one commit writes every
    /// pending footprint with no library undo step, a retry commits nothing, and a refused edit changes nothing. Every
    /// commit targets a transient library, so no JSON or library asset is written; SHA-256 guards on the live files
    /// prove it.
    /// </summary>
    public sealed class LevelTemplateLibraryCommitTests
    {
        private static readonly Vector2Int[] s_single = { Vector2Int.zero };
        private static readonly Vector2Int[] s_domino = { Vector2Int.zero, Vector2Int.right };
        private static readonly Vector2Int[] s_triple = { Vector2Int.zero, Vector2Int.right, new Vector2Int(2, 0) };
        private static readonly Vector2Int[] s_corner = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };
        private static readonly Vector2Int[] s_lTetromino =
        {
            Vector2Int.zero, Vector2Int.right, Vector2Int.up, new Vector2Int(0, 2)
        };
        private static readonly Vector2Int[] s_disconnected = { Vector2Int.zero, new Vector2Int(2, 0) };
        private static readonly string[] s_liveFiles =
        {
            BoardKitAssets.ShapesJsonPath
        };

        private BlockShapeLibrary m_library;
        private string[] m_liveHashes;
        private int m_commits;

        [SetUp]
        public void RememberLiveLibrary()
        {
            m_liveHashes = Hashes(s_liveFiles);
            m_commits = 0;
            ShapeLibraryAuthoring.LibraryCommitted += CountCommit;
        }

        [TearDown]
        public void RemoveLibrary()
        {
            ShapeLibraryAuthoring.LibraryCommitted -= CountCommit;
            if (m_library != null)
            {
                ShapeLibraryAuthoring.ClearHistory(m_library);
                Object.DestroyImmediate(m_library);
                m_library = null;
            }

            Assert.That(Hashes(s_liveFiles), Is.EqualTo(m_liveHashes),
                "No test writes the live piece JSON or library asset.");
        }

        [Test]
        public void BuildShapesReplacesTheEditedRecordAndKeepsItsNameAndIndex()
        {
            BlockShapeLibrary.Shape[] current =
            {
                Shape("One", s_single),
                Shape("Domino", s_domino),
                Shape("Corner", s_corner)
            };
            var edits = new[] { Edit("Renamed elsewhere", s_domino, s_triple) };

            List<BlockShapeLibrary.Shape> shapes = LevelTemplateLibraryCommit.BuildShapes(current, edits);

            Assert.That(shapes, Has.Count.EqualTo(3));
            Assert.That(shapes[0], Is.SameAs(current[0]), "Other records are left untouched.");
            Assert.That(shapes[2], Is.SameAs(current[2]), "Other records are left untouched.");
            Assert.That(shapes[1].DisplayName, Is.EqualTo("Domino"), "The library's display name is kept.");
            Assert.That(shapes[1].Cells, Is.EquivalentTo(s_triple));
            Assert.That(current[1].Cells, Is.EquivalentTo(s_domino), "BuildShapes writes nothing.");
        }

        [Test]
        public void BuildShapesReplacesEveryEditedRecordInOnePass()
        {
            BlockShapeLibrary.Shape[] current =
            {
                Shape("Domino", s_domino),
                Shape("Corner", s_corner)
            };
            var edits = new[]
            {
                Edit("Corner", s_corner, s_lTetromino),
                Edit("Domino", s_domino, s_triple)
            };

            List<BlockShapeLibrary.Shape> shapes = LevelTemplateLibraryCommit.BuildShapes(current, edits);

            Assert.That(shapes[0].DisplayName, Is.EqualTo("Domino"));
            Assert.That(shapes[0].Cells, Is.EquivalentTo(s_triple));
            Assert.That(shapes[1].DisplayName, Is.EqualTo("Corner"));
            Assert.That(shapes[1].Cells, Is.EquivalentTo(s_lTetromino));
        }

        [Test]
        public void BuildShapesRefusesAMissingRecordATemplateEditedTwiceAndAnInvalidFootprint()
        {
            BlockShapeLibrary.Shape[] current =
            {
                Shape("One", s_single),
                Shape("Domino", s_domino)
            };

            var missing = Assert.Throws<ArgumentException>(() => LevelTemplateLibraryCommit.BuildShapes(current,
                new[] { Edit("Corner", s_corner, s_lTetromino) }));
            Assert.That(missing.Message, Does.Contain("Corner"));

            var twice = Assert.Throws<ArgumentException>(() => LevelTemplateLibraryCommit.BuildShapes(current,
                new[]
                {
                    Edit("Domino", s_domino, s_triple),
                    Edit("Domino", s_domino, s_corner)
                }));
            Assert.That(twice.Message, Does.Contain("Domino"));

            var invalid = Assert.Throws<ArgumentException>(() => LevelTemplateLibraryCommit.BuildShapes(current,
                new[] { Edit("Domino", s_domino, s_disconnected) }));
            Assert.That(invalid.Message, Does.Contain("Domino"));
        }

        [Test]
        public void TryReplaceFootprintsCommitsOnceWithoutAnUndoStep()
        {
            UseLibrary(Shape("One", s_single),
                Shape("Domino", s_domino),
                Shape("Corner", s_corner));
            var edits = new[]
            {
                Edit("Domino", s_domino, s_triple),
                Edit("Corner", s_corner, s_lTetromino)
            };

            Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(m_library, edits, out string issue), Is.True,
                issue);

            Assert.That(issue, Is.Null);
            Assert.That(m_commits, Is.EqualTo(1), "One library commit for every pending template.");
            Assert.That(m_library.Shapes, Has.Count.EqualTo(3));
            Assert.That(m_library.Shapes[0].Cells, Is.EquivalentTo(s_single));
            Assert.That(m_library.Shapes[1].DisplayName, Is.EqualTo("Domino"));
            Assert.That(m_library.Shapes[1].Cells, Is.EquivalentTo(s_triple));
            Assert.That(m_library.Shapes[2].Cells, Is.EquivalentTo(s_lTetromino));
            Assert.That(HasUndoState(m_library), Is.False, "The template commit records no library undo step.");
        }

        [Test]
        public void TryReplaceFootprintsCommitsNothingOnARetryOrWithoutEdits()
        {
            UseLibrary(Shape("Domino", s_domino));
            var edits = new[] { Edit("Domino", s_domino, s_triple) };
            Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(m_library, edits, out string issue), Is.True,
                issue);
            string committed = m_library.LoadedJson;

            Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(m_library, edits, out issue), Is.True, issue);
            Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(m_library,
                Array.Empty<LevelTemplateEdit>(), out issue), Is.True, issue);

            Assert.That(m_commits, Is.EqualTo(1), "A retry of a committed footprint commits nothing.");
            Assert.That(m_library.LoadedJson, Is.EqualTo(committed));
        }

        [Test]
        public void TryReplaceFootprintsReturnsAnIssueAndChangesNothingForARefusedEdit()
        {
            UseLibrary(Shape("One", s_single),
                Shape("Domino", s_domino));
            string saved = m_library.LoadedJson;
            var refused = new[]
            {
                new[] { Edit("Domino", s_domino, s_disconnected) },
                new[] { Edit("Corner", s_corner, s_lTetromino) },
                // A valid edit next to a refused one is not committed either.
                new[]
                {
                    Edit("One", s_single, s_domino),
                    Edit("Domino", s_domino, s_disconnected)
                }
            };

            foreach (LevelTemplateEdit[] edits in refused)
            {
                Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(m_library, edits, out string issue),
                    Is.False, edits[edits.Length - 1].DisplayName);
                Assert.That(issue, Is.Not.Empty);
                Assert.That(m_library.LoadedJson, Is.EqualTo(saved), "A refused save changes nothing.");
            }

            Assert.That(LevelTemplateLibraryCommit.TryReplaceFootprints(null,
                new[] { Edit("One", s_single, s_domino) }, out string noLibrary), Is.False);
            Assert.That(noLibrary, Is.Not.Empty);
            Assert.That(m_commits, Is.Zero, "A refused save raises no LibraryCommitted.");
            Assert.That(HasUndoState(m_library), Is.False);
        }

        private void CountCommit()
        {
            m_commits++;
        }

        // A transient library stands in for the project asset, so a commit writes no file.
        private void UseLibrary(params BlockShapeLibrary.Shape[] shapes)
        {
            m_library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            m_library.Load(BlockShapeLibraryJson.Write(shapes));
            Assume.That(AssetDatabase.Contains(m_library), Is.False);
        }

        private static BlockShapeLibrary.Shape Shape(string displayName, Vector2Int[] cells)
        {
            return new BlockShapeLibrary.Shape(displayName, cells);
        }

        // A pending change of the template whose library key is the key of baseCells.
        private static LevelTemplateEdit Edit(string displayName, Vector2Int[] baseCells, Vector2Int[] cells)
        {
            return new LevelTemplateEdit(LevelTestShapes.Key(baseCells), displayName, baseCells, cells);
        }

        private static bool HasUndoState(BlockShapeLibrary library)
        {
            foreach (ShapeLibraryUndoState state in Resources.FindObjectsOfTypeAll<ShapeLibraryUndoState>())
            {
                using (var data = new SerializedObject(state))
                {
                    if (data.FindProperty("m_library").objectReferenceValue == library)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static string[] Hashes(string[] paths)
        {
            var hashes = new string[paths.Length];
            using (SHA256 sha = SHA256.Create())
            {
                for (int index = 0; index < paths.Length; index++)
                {
                    hashes[index] = Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(paths[index])));
                }
            }

            return hashes;
        }
    }
}
