using System;
using System.Collections.Generic;
using System.IO;
using ColorBlockJam.Editor.BoardKit;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Composition;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Carries a piece-library template delete that level blocks use outside the Level Editor window (DEC-126): it
    /// routes the Piece Editor's "Delete selected piece" to an open Level Editor, or runs the same plan on the active
    /// level database's saved JSON when none is open, and holds the library checks both paths share. BoardKit sees
    /// only a plain delegate, so no BoardKit type references a Level Editor type.
    /// </summary>
    [InitializeOnLoad]
    internal static class LevelPieceDeleteRoute
    {
        private const string k_title = "Delete Piece";

        /// <summary>The active level database as read from its manifest and packs (DEC-196).</summary>
        internal sealed class ActiveDatabase
        {
            public ActiveDatabase(string guid, string path, LevelDatabaseSnapshot source, LevelData[] levels)
            {
                Guid = guid;
                Path = path;
                Source = source;
                Levels = levels;
            }

            public string Guid { get; }

            /// <summary>The manifest path.</summary>
            public string Path { get; }

            public string Name => LevelDatabaseAddressables.GetDatabaseName(Path);
            /// <summary>The files the write expects to replace, or null when they could not be read.</summary>
            public LevelDatabaseSnapshot Source { get; }
            /// <summary>The saved levels, or null when the database could not be read.</summary>
            public LevelData[] Levels { get; }
        }

        // Stop point: none needed; the delegate is assigned again on every domain load and holds no state.
        static LevelPieceDeleteRoute()
        {
            PieceEditorWindow.DeleteLibraryPiece = Route;
        }

        /// <summary>
        /// The editor calls this route makes, grouped so every default is written once; tests replace individual
        /// seams through the forwarding properties below, and <see cref="ResetSeams"/> discards this whole instance
        /// to restore every default at once.
        /// </summary>
        private sealed class Seams
        {
            public Func<LevelDatabaseWindow> FindLevelEditor { get; set; } = FindOpenLevelEditor;
            public Func<ActiveDatabase> ReadActiveDatabase { get; set; } = ReadActiveDatabaseFile;
            public Func<ActiveDatabase, IReadOnlyList<LevelData>, string> WriteActiveDatabase { get; set; } =
                WriteActiveDatabaseFile;
            public Func<BlockShapeLibrary> LoadLibrary { get; set; } =
                () => BlockShapeLibrary.LoadProject();
            public Func<BlockShapeLibrary, ShapeKey, string> DeleteFromLibrary { get; set; } = DeleteLibraryRecord;
            public Func<LevelPieceDeleteQuestion, LevelPieceDeleteChoice> ChoosePieceDelete { get; set; } =
                LevelDatabaseWindow.AskPieceDelete;
            public Action<string> ShowMessage { get; set; } =
                message => EditorUtility.DisplayDialog(k_title, message, "OK");
            public Func<string, ShapeKey, List<LevelPieceDeletePlan.OtherDatabase>> CountOtherDatabases { get; set; } =
                CountOtherDatabaseFiles;
        }

        private static Seams s_seams = new Seams();

        /// <summary>The Level Editor window whose GUI is built, or null. Tests replace it.</summary>
        internal static Func<LevelDatabaseWindow> FindLevelEditor
        {
            get => s_seams.FindLevelEditor;
            set => s_seams.FindLevelEditor = value;
        }

        /// <summary>Reads the active level database; null when none is active. Tests replace it.</summary>
        internal static Func<ActiveDatabase> ReadActiveDatabase
        {
            get => s_seams.ReadActiveDatabase;
            set => s_seams.ReadActiveDatabase = value;
        }

        /// <summary>
        /// Writes every level of the active database in one atomic replace with the expected-disk check and the
        /// backup; returns null or the error. Tests replace it so no level JSON is written.
        /// </summary>
        internal static Func<ActiveDatabase, IReadOnlyList<LevelData>, string> WriteActiveDatabase
        {
            get => s_seams.WriteActiveDatabase;
            set => s_seams.WriteActiveDatabase = value;
        }

        /// <summary>The project piece library. Tests replace it with a transient library.</summary>
        internal static Func<BlockShapeLibrary> LoadLibrary
        {
            get => s_seams.LoadLibrary;
            set => s_seams.LoadLibrary = value;
        }

        /// <summary>Deletes a template, by key, from the library; returns null or the reason. Tests replace it.</summary>
        internal static Func<BlockShapeLibrary, ShapeKey, string> DeleteFromLibrary
        {
            get => s_seams.DeleteFromLibrary;
            set => s_seams.DeleteFromLibrary = value;
        }

        /// <summary>The three-choice question. Tests replace it so no modal dialog opens.</summary>
        internal static Func<LevelPieceDeleteQuestion, LevelPieceDeleteChoice> ChoosePieceDelete
        {
            get => s_seams.ChoosePieceDelete;
            set => s_seams.ChoosePieceDelete = value;
        }

        /// <summary>Tells the designer why nothing or not everything happened. Tests replace it.</summary>
        internal static Action<string> ShowMessage
        {
            get => s_seams.ShowMessage;
            set => s_seams.ShowMessage = value;
        }

        /// <summary>The other level databases' blocks of a template key. Tests replace it.</summary>
        internal static Func<string, ShapeKey, List<LevelPieceDeletePlan.OtherDatabase>> CountOtherDatabases
        {
            get => s_seams.CountOtherDatabases;
            set => s_seams.CountOtherDatabases = value;
        }

        /// <summary>Puts every seam back to its project default; tests call it in TearDown.</summary>
        internal static void ResetSeams()
        {
            s_seams = new Seams();
        }

        /// <summary>
        /// The Piece Editor's delete of a library template of the active piece database, by its key. When this
        /// handled it (deleted, cancelled, refused or failed), it returns the status the Piece Editor shows, which says
        /// what happened; null when no block of the active level database uses the template, or that database cannot
        /// be read, so the Piece Editor keeps its own question and delete.
        /// </summary>
        internal static string Route(ShapeKey key)
        {
            LevelDatabaseWindow window = FindLevelEditor();
            return window != null ? window.DeleteLibraryPiece(key) : DeleteWithoutWindow(key);
        }

        /// <summary>
        /// The template delete with no Level Editor open: the same plan, question and order on the active level
        /// database's saved JSON: one level save (the packs it changes, DEC-196), then the undoable library delete. Returns the outcome's
        /// status, or null when the Piece Editor keeps its own delete. No pending template edit can exist here: those
        /// live only in an open Level Editor's draft and session, so there is nothing to refuse.
        /// </summary>
        internal static string DeleteWithoutWindow(ShapeKey key)
        {
            ActiveDatabase database = ReadActiveDatabase();
            if (database == null || database.Levels == null
                || LevelPieceDeletePlan.CountBlocks(database.Levels, key) == 0)
            {
                return null;
            }

            BlockShapeLibrary library = LoadLibrary();
            LevelShapeChoice shape = LevelShapeCatalog.Read(library).Find(key);
            if (shape == null)
            {
                return null;
            }

            string issue = GetLibraryIssue(library, key);
            if (issue != null)
            {
                return Tell($"Deleted nothing. {shape.DisplayName} cannot be deleted from the piece library now: {issue}");
            }

            LevelPieceDeletePlan plan = LevelPieceDeletePlan.Create(new LevelPieceDeletePlan.Input
            {
                TemplateKey = key,
                DisplayName = shape.DisplayName,
                DatabaseName = database.Name,
                SavedLevels = database.Levels,
                OtherDatabases = CountOtherDatabases(database.Guid, key)
            });
            LevelPieceDeleteResult result = LevelPieceDeleteOperation.Delete(plan, ChoosePieceDelete,
                levels =>
                {
                    string error = WriteActiveDatabase(database, levels);
                    return new LevelFileWriteResult(error == null, error == null, error);
                }, null, () => DeleteFromLibrary(library, key));
            switch (result.Status)
            {
                case LevelPieceDeleteStatus.Refused:
                    return Tell(plan.GetRefusalStatus());
                case LevelPieceDeleteStatus.Cancelled:
                    return LevelPieceDeletePlan.k_cancelledStatus;
                case LevelPieceDeleteStatus.WriteFailed:
                case LevelPieceDeleteStatus.NotReloaded:
                    return Tell("Deleted nothing from the piece library. " + result.Error);
                case LevelPieceDeleteStatus.LibraryFailed:
                    return Tell(plan.GetLibraryFailedStatus(result.Choice, result.Error));
            }

            string done = plan.GetDoneStatus(result.Choice);
            Debug.Log("Piece Editor: " + done);
            return done;
        }

        // A refusal or failure is also shown in a dialog, as before; the Piece Editor's status repeats it.
        private static string Tell(string message)
        {
            ShowMessage(message);
            return message;
        }

        /// <summary>
        /// Why the library cannot delete this template now, or null: Play Mode, a missing record, and for a saved
        /// library read-only files or a piece JSON changed on disk. It only reads, so it runs before the question and
        /// before any level write.
        /// </summary>
        internal static string GetLibraryIssue(BlockShapeLibrary library, ShapeKey key)
        {
            try
            {
                if (library == null)
                {
                    return "The piece library is not loaded.";
                }

                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return "Stop Play Mode before deleting a piece.";
                }

                ShapeLibraryAuthoring.Load(library);
                if (library.IndexOf(key) < 0)
                {
                    return "The piece library has no such template any more.";
                }

                // A transient library writes no file.
                if (library.JsonPath == null)
                {
                    return null;
                }

                string jsonPath = ShapeLibraryAuthoring.GetJsonPath(library);
                BoardKitAssetFiles.ValidateWritable(jsonPath);
                ShapeLibraryFile.CheckCurrent(jsonPath, library.LoadedJson);
                return null;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
                                          error is IOException || error is UnauthorizedAccessException)
            {
                return error.Message;
            }
        }

        /// <summary>
        /// The project piece library's delete of the template with this key: the undoable ShapeLibraryAuthoring.Delete.
        /// Returns null or the reason it failed; a failed transaction changes nothing.
        /// </summary>
        internal static string DeleteLibraryRecord(BlockShapeLibrary library, ShapeKey key)
        {
            try
            {
                if (library == null)
                {
                    return "The piece library is not loaded.";
                }

                ShapeLibraryAuthoring.Load(library);
                int index = library.IndexOf(key);
                if (index < 0)
                {
                    return "The piece library has no such template any more.";
                }

                ShapeLibraryAuthoring.Delete(library, index);
                return null;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
                                          error is IOException || error is UnauthorizedAccessException)
            {
                return error.Message;
            }
        }

        /// <summary>
        /// Every level database of the Level Database group except the active one, with its blocks of this key (-1
        /// when it cannot be read). It only reads; those databases are never changed here.
        /// </summary>
        internal static List<LevelPieceDeletePlan.OtherDatabase> CountOtherDatabaseFiles(string activeGuid,
            ShapeKey key)
        {
            List<LevelPieceDeletePlan.OtherDatabase> result = new List<LevelPieceDeletePlan.OtherDatabase>();
            List<LevelDatabaseAddressables.Entry> entries = LevelDatabaseAddressables.GetEntries();
            for (int index = 0; index < entries.Count; index++)
            {
                LevelDatabaseAddressables.Entry entry = entries[index];
                if (string.Equals(entry.Guid, activeGuid, StringComparison.Ordinal))
                {
                    continue;
                }

                int count = LevelDatabaseFiles.TryRead(entry.Path, out LevelDatabaseSnapshot snapshot, out _)
                    ? LevelPieceDeletePlan.CountBlocks(snapshot.Levels, key)
                    : -1;
                result.Add(new LevelPieceDeletePlan.OtherDatabase(entry.Name, count));
            }

            return result;
        }

        private static LevelDatabaseWindow FindOpenLevelEditor()
        {
            if (!EditorWindow.HasOpenInstances<LevelDatabaseWindow>())
            {
                return null;
            }

            foreach (LevelDatabaseWindow window in Resources.FindObjectsOfTypeAll<LevelDatabaseWindow>())
            {
                if (window.HasGui)
                {
                    return window;
                }
            }

            return null;
        }

        private static ActiveDatabase ReadActiveDatabaseFile()
        {
            ProjectScopeInstaller projectScope = LevelDatabaseAddressables.LoadProjectScope();
            string path = LevelDatabaseAddressables.GetActivePath(projectScope);
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (!LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot snapshot, out _))
            {
                return new ActiveDatabase(projectScope.LevelJsonGuid, path, null, null);
            }

            LevelData[] levels = new LevelData[snapshot.Levels.Count];
            for (int index = 0; index < levels.Length; index++)
            {
                levels[index] = snapshot.Levels[index];
            }

            return new ActiveDatabase(projectScope.LevelJsonGuid, path, snapshot, levels);
        }

        // The same write and backup as the Level Editor's Save Level: only the packs that change (DEC-196). No Level
        // Editor GUI is open here, and one built later reads the files again, so nothing else keeps the old levels
        // (DEC-192).
        private static string WriteActiveDatabaseFile(ActiveDatabase database, IReadOnlyList<LevelData> levels)
        {
            if (database.Source == null)
            {
                return "The level database could not be read.";
            }

            if (!LevelDatabaseAssets.TrySave(database.Source, levels, LevelDatabaseWindow.GetBackupFolder(database.Guid),
                    out _, out string note, out string error))
            {
                return error;
            }

            if (note != null)
            {
                Debug.LogWarning($"Level database {database.Name}: {note}");
            }

            return null;
        }
    }
}
