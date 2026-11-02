using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Levels;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// Owns loaded-source state and persistence sequencing. A confirmed save names the level's level pieces in its
    /// pieces table (DEC-193), writes JSON before transferring pending footprints, establishing the draft baseline and
    /// committing template footprints. Level pieces stay in their level and are never committed to the piece library
    /// (DEC-180). The loaded source is a whole level database, every pack in memory, and a save writes only the packs it
    /// changes (DEC-196). View notifications are synchronous so controls reflect the saved draft before a library commit.
    /// </summary>
    internal sealed class LevelSaveController
    {
        private readonly LevelDraftController m_draft;
        private readonly LevelPieceEditController m_pieces;
        // The loaded source's saved levels: this window's own catalog, never the game's (DEC-192).
        private readonly LevelCatalog m_levels = new LevelCatalog();
        // The database files as read or last saved; a save compares the disk with it first.
        private LevelDatabaseSnapshot m_loadedSource;
        private string m_loadedGuid;
        private bool m_sourceValid;

        internal LevelSaveController(LevelDraftController draft, LevelPieceEditController pieces)
        {
            m_draft = draft;
            m_pieces = pieces;
        }

        internal LevelDatabaseSnapshot LoadedSource => m_loadedSource;

        /// <summary>The loaded database's content hash, or null; the window's key for "the same saved source".</summary>
        internal string LoadedSourceHash => m_loadedSource?.ContentHash;

        internal string LoadedGuid => m_loadedGuid;
        internal bool SourceValid => m_sourceValid;

        /// <summary>The loaded source's saved levels; read them only while <see cref="SourceValid"/> is true.</summary>
        internal LevelCatalog Levels => m_levels;

        /// <summary>
        /// Loads the level database the game plays, whose manifest <paramref name="activeGuid"/> names (DEC-194), with
        /// every pack.
        /// </summary>
        internal bool LoadSource(string activeGuid, IReadOnlyList<LevelDatabaseAddressables.Entry> entries,
            out string error)
        {
            m_loadedGuid = activeGuid;
            m_loadedSource = null;
            m_sourceValid = ReadSource(activeGuid, entries, out error);
            return m_sourceValid;
        }

        private bool ReadSource(string activeGuid, IReadOnlyList<LevelDatabaseAddressables.Entry> entries,
            out string error)
        {
            string path = GetPath(activeGuid);
            if (string.IsNullOrEmpty(path))
            {
                error = "The active level database is missing. Select a database or create one.";
                return false;
            }

            bool inGroup = false;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].Guid == activeGuid)
                {
                    inGroup = true;
                    break;
                }
            }

            if (!inGroup)
            {
                error = "The active level database is not in the Level Database Addressables group.";
                return false;
            }

            if (!LevelDatabaseFiles.TryRead(path, out LevelDatabaseSnapshot snapshot, out error)
                || !snapshot.LoadInto(m_levels, out error))
            {
                return false;
            }

            m_loadedSource = snapshot;
            return true;
        }

        internal LevelSaveValidation Validate(int selectedIndex)
        {
            if (!m_sourceValid || m_draft.Draft == null || selectedIndex < 0)
            {
                return new LevelSaveValidation(null, null, false);
            }

            LevelData candidate = m_draft.Snapshot();
            if (!candidate.Validate(out string error))
            {
                return new LevelSaveValidation(null, error, true);
            }

            error = GetBarrierSaveIssue(candidate);
            if (error != null)
            {
                return new LevelSaveValidation(null, error, false);
            }

            for (int index = 0; index < m_levels.Count; index++)
            {
                if (index != selectedIndex
                    && string.Equals(m_levels.GetAt(index)?.Id, candidate.Id, StringComparison.Ordinal))
                {
                    return new LevelSaveValidation(null, $"Level ID {candidate.Id} is already in the database.", true);
                }
            }

            return new LevelSaveValidation(candidate, null, false);
        }

        internal static string GetBarrierSaveIssue(LevelData candidate)
        {
            return LevelBarrierCommands.GetSaveIssue(candidate);
        }

        /// <summary>
        /// Called after validation and the view's catalog refresh; no dialogs or writes occur here. The level as written
        /// takes its resolved pieces table: one name per level piece a block uses, the others pruned (DEC-193).
        /// </summary>
        internal LevelSavePlan Prepare(int selectedIndex, LevelData candidate)
        {
            List<LevelData> others = new List<LevelData>(m_levels.Count);
            for (int index = 0; index < m_levels.Count; index++)
            {
                if (index != selectedIndex)
                {
                    others.Add(m_levels.GetAt(index));
                }
            }

            candidate = LevelRecordCopy.WithPieceNames(candidate,
                m_pieces.ResolvePieceNames(m_loadedGuid, candidate, others));
            LevelTemplateSavePlan templates = PlanTemplates(selectedIndex, candidate);
            List<LevelData> levels = CopyLevels(m_levels);
            levels[selectedIndex] = candidate;
            for (int index = 0; templates != null && index < templates.Restamped.Count; index++)
            {
                levels[templates.Restamped[index].Index] = templates.Restamped[index].Level;
            }

            // Levels left on an old footprint keep the template's name for it, written in the same write.
            for (int index = 0; templates != null && index < templates.Named.Count; index++)
            {
                levels[templates.Named[index].Index] = templates.Named[index].Level;
            }

            return new LevelSavePlan(candidate, templates, levels);
        }

        private LevelTemplateSavePlan PlanTemplates(int selectedIndex, LevelData candidate)
        {
            List<LevelTemplateEdit> saved = m_pieces.GetPendingTemplates(m_loadedGuid);
            if (saved.Count == 0 && m_draft.Draft.TemplateEdits.Count == 0)
            {
                return null;
            }

            List<LevelTemplateSavePlan.OtherLevel> others = LevelTemplateSavePlan.GetOtherLevels(m_levels, selectedIndex);
            return m_pieces.CreateTemplatePlan(m_loadedGuid, candidate, others, saved, m_draft.Draft.TemplateEdits);
        }

        internal LevelSaveResult Save(LevelSavePlan plan, string databaseName,
            Func<IReadOnlyList<LevelData>, LevelFileWriteResult> write,
            Func<IReadOnlyList<LevelTemplateEdit>, string> commitTemplates,
            Action<LevelSaveProgress> notify)
        {
            // A refusal cannot accidentally become a write if a caller skipped the confirmation presentation.
            if (plan.Templates != null && plan.Templates.Refusals.Count > 0)
            {
                return new LevelSaveResult(new LevelFileWriteResult(false, false,
                    "Save Level wrote nothing. " + string.Join(" ", plan.Templates.Refusals)), false);
            }

            LevelFileWriteResult file = write(plan.Levels);
            if (!file.Reloaded)
            {
                return new LevelSaveResult(file, false);
            }

            m_pieces.InvalidateOtherLevels();
            m_pieces.MoveTemplateEditsToSession(m_loadedGuid);
            // The draft takes the written pieces table without an undo step, so it reads clean after the save.
            m_draft.SetSavedLevel(plan.Level);
            string message = $"Saved {plan.Level.Name} to {databaseName}.";
            notify?.Invoke(new LevelSaveProgress(LevelSaveStage.LevelWritten, message));

            // A library failure never turns a successfully written level into a failed JSON save.
            bool templatesCommitted = CommitTemplates(plan.Templates, commitTemplates, notify, ref message);
            return new LevelSaveResult(file, templatesCommitted);
        }

        private bool CommitTemplates(LevelTemplateSavePlan plan,
            Func<IReadOnlyList<LevelTemplateEdit>, string> commit, Action<LevelSaveProgress> notify, ref string message)
        {
            if (plan == null)
            {
                return true;
            }

            message += DescribeTemplateLevels(plan);
            List<LevelTemplateEdit> pending = m_pieces.GetPendingTemplates(m_loadedGuid);
            string issue = plan.ToCommit.Count > 0
                ? (commit ?? ReplaceTemplatesInLibrary)(plan.ToCommit)
                : null;
            m_pieces.MarkTemplateCommit(m_loadedGuid, pending, issue);
            string names = LevelPieceEditController.JoinTemplateNames(plan.ToCommit);
            if (issue != null)
            {
                message += $" {names} stayed out of the piece library: {issue} Save Level again to retry.";
            }
            else if (plan.ToCommit.Count > 0)
            {
                message += $" Wrote the new footprint of {names} to the piece library.";
            }

            notify?.Invoke(new LevelSaveProgress(LevelSaveStage.TemplatesCommitted, message));
            return issue == null;
        }

        /// <summary>
        /// Saves <paramref name="levels"/> into the loaded database, writing only the packs they change (DEC-196), and
        /// reloads this window's catalog from what was written. Refused when the files changed on disk since they were
        /// read, or when another database than the loaded one is active.
        /// </summary>
        internal LevelFileWriteResult WriteLevels(string activeGuid, IReadOnlyList<LevelData> levels)
        {
            if (m_loadedSource == null
                || !string.Equals(GetPath(activeGuid), m_loadedSource.ManifestPath, StringComparison.Ordinal))
            {
                return new LevelFileWriteResult(false, false, LevelDatabaseFiles.ExternalChangeError);
            }

            if (!LevelDatabaseAssets.TrySave(m_loadedSource, levels, GetBackupFolder(activeGuid),
                    out LevelDatabaseSnapshot saved, out string note, out string error))
            {
                return new LevelFileWriteResult(false, false, error);
            }

            if (note != null)
            {
                Debug.LogWarning($"Level database {saved.Manifest.Name}: {note}");
            }

            if (!saved.LoadInto(m_levels, out error))
            {
                m_sourceValid = false;
                return new LevelFileWriteResult(true, false, $"Saved the level database but could not reload it: {error}");
            }

            m_loadedSource = saved;
            return new LevelFileWriteResult(true, true, null);
        }

        internal bool ReadBackup(string activeGuid, out LevelData[] levels, out string error)
        {
            return LevelDatabaseFiles.TryReadBackup(GetBackupFolder(activeGuid), out levels, out error);
        }

        internal string ForgetUnheldTemplates()
        {
            return m_pieces.ForgetUnheldTemplates(m_loadedGuid, m_levels, m_sourceValid);
        }

        internal static List<LevelData> CopyLevels(LevelCatalog source)
        {
            List<LevelData> levels = new List<LevelData>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                levels.Add(source.GetAt(index));
            }

            return levels;
        }

        /// <summary>The folder that keeps one previous version of the database whose manifest has this GUID.</summary>
        internal static string GetBackupFolder(string sourceGuid)
        {
            return LevelDatabaseAssets.GetBackupFolder(sourceGuid);
        }

        private static string GetPath(string guid)
        {
            return string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
        }

        internal static string DescribeTemplateLevels(LevelTemplateSavePlan plan)
        {
            string text = string.Empty;
            if (plan.Restamped.Count > 0)
            {
                string[] restamped = new string[plan.Restamped.Count];
                for (int index = 0; index < restamped.Length; index++)
                {
                    LevelTemplateSavePlan.RestampedLevel level = plan.Restamped[index];
                    restamped[index] = $"{level.Name} ({level.BlockCount} block(s))";
                }

                text += $" Re-stamped {string.Join(", ", restamped)}.";
            }

            if (plan.UnfitLevels.Count > 0)
            {
                string[] unfit = new string[plan.UnfitLevels.Count];
                for (int index = 0; index < unfit.Length; index++)
                {
                    unfit[index] = plan.UnfitLevels[index].Name;
                }

                text += $" Left {string.Join(", ", unfit)} on the old footprint, whose blocks keep the template's name; " +
                        "Re-stamp them there.";
            }

            return text;
        }

        private string ReplaceTemplatesInLibrary(IReadOnlyList<LevelTemplateEdit> edits)
        {
            return LevelTemplateLibraryCommit.TryReplaceFootprints(m_pieces.Library, edits, out string issue)
                ? null
                : issue;
        }
    }

    /// <summary>
    /// One save-time validation outcome: the level ready to save, or why it is not and how that reason should be
    /// shown.
    /// </summary>
    internal readonly struct LevelSaveValidation
    {
        internal LevelData Candidate { get; }
        internal string Error { get; }

        /// <summary>
        /// True for an invalid level or a duplicate ID, which stop the designer with a dialog (Validate); false for
        /// a barrier issue, which LevelDatabaseWindow.SaveLevel reports in the status line instead so a resolvable
        /// board problem does not interrupt with a modal.
        /// </summary>
        internal bool ShowDialog { get; }

        internal LevelSaveValidation(LevelData candidate, string error, bool showDialog)
        {
            Candidate = candidate;
            Error = error;
            ShowDialog = showDialog;
        }
    }

    /// <summary>What Save writes: the validated level, its template plan, and every level to save.</summary>
    internal sealed class LevelSavePlan
    {
        /// <summary>
        /// The open level as written: its level pieces stay in it (DEC-180), named in its pieces table (DEC-193).
        /// </summary>
        internal LevelData Level { get; }
        internal LevelTemplateSavePlan Templates { get; }
        internal IReadOnlyList<LevelData> Levels { get; }

        internal LevelSavePlan(LevelData level, LevelTemplateSavePlan templates, IReadOnlyList<LevelData> levels)
        {
            Level = level;
            Templates = templates;
            Levels = levels;
        }
    }

    /// <summary>Whether the level JSON was written and reloaded into the Level Editor's catalog, or the reason it was not.</summary>
    internal readonly struct LevelFileWriteResult
    {
        internal bool JsonWritten { get; }
        internal bool Reloaded { get; }
        internal string Error { get; }

        internal LevelFileWriteResult(bool jsonWritten, bool reloaded, string error)
        {
            JsonWritten = jsonWritten;
            Reloaded = reloaded;
            Error = error;
        }
    }

    /// <summary>The outcome of one Save: the file write, and whether its template commit also succeeded.</summary>
    internal readonly struct LevelSaveResult
    {
        internal LevelFileWriteResult File { get; }
        internal bool LibraryCommitted { get; }

        internal LevelSaveResult(LevelFileWriteResult file, bool libraryCommitted)
        {
            File = file;
            LibraryCommitted = libraryCommitted;
        }
    }

    /// <summary>The point one Save has reached, for the progress notifications Save raises as it runs.</summary>
    internal enum LevelSaveStage
    {
        LevelWritten,
        TemplatesCommitted
    }

    /// <summary>One Save progress notification: its stage and the status message built so far.</summary>
    internal readonly struct LevelSaveProgress
    {
        internal LevelSaveStage Stage { get; }
        internal string Message { get; }

        internal LevelSaveProgress(LevelSaveStage stage, string message)
        {
            Stage = stage;
            Message = message;
        }
    }
}
