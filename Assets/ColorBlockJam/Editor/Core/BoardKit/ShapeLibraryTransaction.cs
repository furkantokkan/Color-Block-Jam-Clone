using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace ColorBlockJam.Editor.BoardKit
{
    /// <summary>
    /// Writes one piece library change to its JSON after checking the file is writable and unchanged on disk, and
    /// restores the JSON and the loaded library if the save fails. The library is editor-only data with no wrapper asset,
    /// so nothing else is written or generated from it (DEC-193, DEC-195).
    /// </summary>
    internal sealed class ShapeLibraryTransaction
    {
        private readonly BlockShapeLibrary m_library;
        private readonly string m_previousJson;
        private readonly string m_nextJson;
        private readonly string m_expectedDisk;
        private readonly string m_jsonPath;
        private readonly bool m_isPersistent;
        private bool m_jsonWritten;

        public ShapeLibraryTransaction(BlockShapeLibrary library,
            IReadOnlyList<BlockShapeLibrary.Shape> shapes, string expectedDisk)
        {
            m_library = library;
            m_previousJson = library.LoadedJson;
            m_nextJson = BlockShapeLibraryJson.Write(shapes);
            m_expectedDisk = expectedDisk ?? m_previousJson;
            // A library bound to no file is a transient one: it changes in memory only.
            m_isPersistent = library.JsonPath != null;
            if (!m_isPersistent)
                return;

            m_jsonPath = ShapeLibraryAuthoring.GetJsonPath(library);
            BoardKitAssetFiles.ValidateWritable(m_jsonPath);
            ShapeLibraryFile.CheckCurrent(m_jsonPath, m_expectedDisk);
        }

        public void Save(string undoLabel, bool recordUndo)
        {
            try
            {
                SaveJson();
                m_library.Load(m_nextJson);

                if (recordUndo)
                    ShapeLibraryUndoState.Record(m_library, m_previousJson, m_nextJson, undoLabel);
            }
            catch (Exception error)
            {
                var recoveryErrors = Rollback();
                if (recoveryErrors.Count > 0)
                {
                    recoveryErrors.Insert(0, error);
                    throw new IOException("Piece transaction failed; recovery needs attention.",
                        new AggregateException(recoveryErrors));
                }
                throw;
            }
        }

        private void SaveJson()
        {
            if (!m_isPersistent)
                return;

            ShapeLibraryFile.Write(m_jsonPath, m_expectedDisk, m_nextJson);
            m_jsonWritten = true;
            AssetDatabase.ImportAsset(m_jsonPath, ImportAssetOptions.ForceUpdate);
        }

        private List<Exception> Rollback()
        {
            var errors = new List<Exception>();
            if (m_jsonWritten)
                TryRestore(RestoreJson, errors);
            TryRestore(() => m_library.Load(m_previousJson), errors);
            return errors;
        }

        private void RestoreJson()
        {
            ShapeLibraryFile.Write(m_jsonPath, m_nextJson, m_expectedDisk);
            AssetDatabase.ImportAsset(m_jsonPath, ImportAssetOptions.ForceUpdate);
        }

        private static void TryRestore(Action restore, List<Exception> errors)
        {
            // One failed recovery must not prevent restoring the other outputs.
            try { restore(); }
            catch (Exception error) { errors.Add(error); }
        }
    }
}
