using System;
using ColorBlockJam.Editor.BoardKit;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ColorBlockJam.Tests.EditMode
{
    /// <summary>
    /// Verifies ShapeLibraryAuthoring.SavePiece's editing rules: cell normalization, refusing a footprint the library
    /// already holds and an empty piece, and replacing a template by its key. Templates have no enum identity any more
    /// (DEC-193), so the enum, type-name and grid-size cases were removed.
    /// </summary>
    public sealed class ShapeLibraryAuthoringTests
    {
        private static readonly Vector2Int[] s_l = { Vector2Int.zero, Vector2Int.right, Vector2Int.up };

        [Test]
        public void ShapeAuthoringNormalizesCellsAndRejectsDuplicateShapes()
        {
            var library = ScriptableObject.CreateInstance<BlockShapeLibrary>();
            try
            {
                ShapeLibraryAuthoring.SavePiece(library, default, "New Piece",
                    new[] { new Vector2Int(3, 4), new Vector2Int(4, 4) }, null);
                Assert.That(library.Shapes[0].Cells[0], Is.EqualTo(Vector2Int.zero));
                Assert.That(library.Shapes[0].Cells[1], Is.EqualTo(Vector2Int.right));
                Assert.Throws<ArgumentException>(() => ShapeLibraryAuthoring.SavePiece(library, default, "Duplicate",
                    new[] { Vector2Int.zero, Vector2Int.right }, null));
                Assert.Throws<ArgumentException>(() => ShapeLibraryAuthoring.SavePiece(library, default, "Invalid",
                    Array.Empty<Vector2Int>(), null));
                ShapeLibraryAuthoring.SavePiece(library, library.Shapes[0].Key, "Edited", s_l, null);
                Assert.That(library.Shapes.Count, Is.EqualTo(1));
                Assert.That(library.Shapes[0].Cells.Count, Is.EqualTo(3));
            }
            finally { ShapeLibraryAuthoring.ClearHistory(library); Object.DestroyImmediate(library); }
        }
    }
}
