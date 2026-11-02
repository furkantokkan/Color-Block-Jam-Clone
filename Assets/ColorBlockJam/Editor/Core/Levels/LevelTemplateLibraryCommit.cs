using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using UnityEditor;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Writes the pending template footprints of a Save Level to the piece library in one library commit: each record
    /// keeps its name and takes the new cells, so its key follows them (DEC-125, DEC-193). The commit records no library
    /// undo step: a Ctrl+Z after Save Level must not revert the library under the saved levels.
    /// </summary>
    internal static class LevelTemplateLibraryCommit
    {
        private const string k_undoLabel = "Save template footprint";

        /// <summary>
        /// Returns <paramref name="current"/> with each edited record replaced by the same name with the edit's
        /// normalized cells; the other records are the same instances in the same order. A record is found by the
        /// edit's template key, else by its name, since a commit this window made gave it a new key. Throws
        /// <see cref="ArgumentException"/> naming the template for a missing record, a template edited twice, an
        /// invalid footprint or a footprint another record already has. It writes nothing.
        /// </summary>
        internal static List<BlockShapeLibrary.Shape> BuildShapes(IReadOnlyList<BlockShapeLibrary.Shape> current,
            IReadOnlyList<LevelTemplateEdit> edits)
        {
            if (current == null)
            {
                throw new ArgumentNullException(nameof(current));
            }

            if (edits == null)
            {
                throw new ArgumentNullException(nameof(edits));
            }

            var result = new List<BlockShapeLibrary.Shape>(current);
            var editedIndices = new HashSet<int>();
            for (int editIndex = 0; editIndex < edits.Count; editIndex++)
            {
                LevelTemplateEdit edit = edits[editIndex];
                if (edit == null)
                {
                    throw new ArgumentException($"Template change {editIndex + 1} is missing.");
                }

                string entry = edit.DisplayName;
                int index = IndexOfTemplate(current, edit.TemplateKey, edit.DisplayName);
                if (index < 0)
                {
                    throw new ArgumentException($"{entry}: the piece library has no such template any more.");
                }

                if (!editedIndices.Add(index))
                {
                    throw new ArgumentException($"{entry}: this template is changed more than once in this save.");
                }

                if (edit.Cells == null || edit.Cells.Count == 0)
                {
                    throw new ArgumentException($"{entry}: paint at least one cell.");
                }

                try
                {
                    result[index] = new BlockShapeLibrary.Shape(current[index].DisplayName,
                        LevelCustomPiece.Normalize(edit.Cells));
                }
                catch (ArgumentException error)
                {
                    throw new ArgumentException($"{entry}: {error.Message}", error);
                }
            }

            // One record per key and unique names, as every library write requires.
            BlockShapeLibraryJson.Validate(result);
            return result;
        }

        /// <summary>
        /// Replaces the footprints of <paramref name="edits"/> in <paramref name="library"/> through one
        /// <c>ShapeLibraryAuthoring.Commit</c> with no undo record: the piece JSON and the library asset. When every
        /// record already holds its edit's cells, for example on a retry after a commit that succeeded, nothing is
        /// committed. Play Mode, an invalid edit or a failed write returns false with the reason and leaves the library
        /// as it was (the transaction rolls back).
        /// </summary>
        internal static bool TryReplaceFootprints(BlockShapeLibrary library, IReadOnlyList<LevelTemplateEdit> edits,
            out string issue)
        {
            issue = null;
            try
            {
                if (library == null)
                {
                    throw new InvalidOperationException("The piece library is not loaded.");
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("Stop Play Mode before saving template footprints.");
                }

                if (edits == null || edits.Count == 0)
                {
                    return true;
                }

                ShapeLibraryAuthoring.Load(library);
                IReadOnlyList<BlockShapeLibrary.Shape> current = library.Shapes;
                List<BlockShapeLibrary.Shape> shapes = BuildShapes(current, edits);
                if (HasChanges(current, shapes))
                {
                    ShapeLibraryAuthoring.Commit(library, shapes, k_undoLabel, recordUndo: false);
                }

                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
                                          error is IOException || error is UnauthorizedAccessException)
            {
                issue = error.Message;
                return false;
            }
        }

        private static bool HasChanges(IReadOnlyList<BlockShapeLibrary.Shape> current,
            IReadOnlyList<BlockShapeLibrary.Shape> shapes)
        {
            for (int index = 0; index < current.Count; index++)
            {
                if (!ReferenceEquals(current[index], shapes[index]) &&
                    !BlockShapeLibraryJson.Equal(current[index], shapes[index]))
                {
                    return true;
                }
            }

            return false;
        }

        // The record the library holds under this key, else the one with this name, ignoring case; -1 for neither.
        private static int IndexOfTemplate(IReadOnlyList<BlockShapeLibrary.Shape> shapes, ShapeKey templateKey,
            string displayName)
        {
            for (int index = 0; templateKey.IsValid && index < shapes.Count; index++)
            {
                if (shapes[index].Key == templateKey)
                {
                    return index;
                }
            }

            for (int index = 0; !string.IsNullOrWhiteSpace(displayName) && index < shapes.Count; index++)
            {
                if (string.Equals(shapes[index].DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }
    }
}
