using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>The designer's answer to a template delete that blocks use (DEC-126, DEC-193).</summary>
    internal enum LevelPieceDeleteChoice
    {
        Cancel,
        /// <summary>The template goes; its blocks keep their cells as level pieces with the template's name.</summary>
        DeleteTemplate,
        /// <summary>The template and every block of it go.</summary>
        DeleteBlocks
    }

    /// <summary>The template delete question: its text and the options it offers (DEC-126).</summary>
    internal sealed class LevelPieceDeleteQuestion
    {
        public LevelPieceDeleteQuestion(string message, bool canDeleteTemplate, bool canDeleteBlocks)
        {
            Message = message;
            CanDeleteTemplate = canDeleteTemplate;
            CanDeleteBlocks = canDeleteBlocks;
        }

        public string Message { get; }
        public bool CanDeleteTemplate { get; }
        public bool CanDeleteBlocks { get; }

        /// <summary>The answer when it is Cancel or an offered option; any other answer counts as Cancel.</summary>
        public LevelPieceDeleteChoice Accept(LevelPieceDeleteChoice choice)
        {
            return (choice == LevelPieceDeleteChoice.DeleteTemplate && CanDeleteTemplate)
                   || (choice == LevelPieceDeleteChoice.DeleteBlocks && CanDeleteBlocks)
                ? choice
                : LevelPieceDeleteChoice.Cancel;
        }
    }

    /// <summary>
    /// Plans the delete of a piece-library template that blocks of the active level database use (DEC-126): the
    /// blocks per level (the blocks whose cells form the template's key, DEC-193), the Delete template and Delete
    /// template and its blocks results for every saved level, the open draft and its saved baseline, the levels the
    /// second deletes because no block remains, why an option is impossible, and the question and status texts. Delete
    /// template needs no relabel: the blocks keep their cells and key and become level pieces, and each level that
    /// uses them names them after the template. It only reads; the window and the Piece Editor route write what it
    /// returns.
    /// </summary>
    internal sealed class LevelPieceDeletePlan
    {
        internal const string k_emptiedHeading = "These levels will be deleted because no block would remain:";

        /// <summary>The status after the designer cancelled the question, on every path.</summary>
        internal const string k_cancelledStatus = "Deleted nothing: the delete was cancelled.";

        /// <summary>Another level database's use of the piece: counted and named, never changed.</summary>
        internal readonly struct OtherDatabase
        {
            public OtherDatabase(string name, int blockCount)
            {
                Name = name;
                BlockCount = blockCount;
            }

            public string Name { get; }
            /// <summary>The blocks that use the piece, or -1 when the database could not be read.</summary>
            public int BlockCount { get; }
        }

        /// <summary>One level that uses the piece: its database index, name and blocks.</summary>
        internal sealed class LevelUse
        {
            public LevelUse(int index, string name, int blockCount, int savedBlockCount, bool isOpen)
            {
                Index = index;
                Name = name;
                BlockCount = blockCount;
                SavedBlockCount = savedBlockCount;
                IsOpen = isOpen;
            }

            public int Index { get; }
            public string Name { get; }
            /// <summary>The blocks the designer sees: the draft's for the open level.</summary>
            public int BlockCount { get; }
            /// <summary>The blocks of the saved record.</summary>
            public int SavedBlockCount { get; }
            public bool IsOpen { get; }
        }

        /// <summary>What the plan is made from.</summary>
        internal sealed class Input
        {
            /// <summary>The template's key: its blocks are the blocks whose cells form it.</summary>
            public ShapeKey TemplateKey { get; set; }
            /// <summary>The template's name, which its blocks take as level pieces.</summary>
            public string DisplayName { get; set; }
            public string DatabaseName { get; set; }
            /// <summary>Every saved level of the active database, the open one as saved.</summary>
            public IReadOnlyList<LevelData> SavedLevels { get; set; }
            /// <summary>The index of the level open in the Level Editor, or -1.</summary>
            public int OpenIndex { get; set; } = -1;
            /// <summary>The open level's draft, or null.</summary>
            public LevelData Draft { get; set; }
            public bool DraftHasChanges { get; set; }
            public IReadOnlyList<OtherDatabase> OtherDatabases { get; set; }
        }

        private readonly List<LevelUse> m_levels = new List<LevelUse>();
        private readonly List<string> m_keepRefusals = new List<string>();
        private readonly List<string> m_deleteRefusals = new List<string>();
        private readonly List<string> m_deletedNames = new List<string>();
        private IReadOnlyList<LevelData> m_saved;
        private LevelData[] m_kept;
        private LevelData[] m_removed;
        private bool[] m_deleted;

        private LevelPieceDeletePlan()
        {
        }

        public ShapeKey TemplateKey { get; private set; }
        public string DisplayName { get; private set; }
        public string DatabaseName { get; private set; }
        public int OpenIndex { get; private set; }
        /// <summary>The blocks that use the piece: the draft's for the open level, the saved ones elsewhere.</summary>
        public int BlockCount { get; private set; }
        public IReadOnlyList<LevelUse> Levels => m_levels;
        public bool CanDeleteTemplate => m_keepRefusals.Count == 0;
        public bool CanDeleteBlocks => m_deleteRefusals.Count == 0;
        public IReadOnlyList<string> KeepRefusals => m_keepRefusals;
        public IReadOnlyList<string> DeleteRefusals => m_deleteRefusals;
        /// <summary>The levels Delete template and its blocks deletes because their written record keeps no block.</summary>
        public IReadOnlyList<string> DeletedLevelNames => m_deletedNames;
        /// <summary>True when Delete template and its blocks deletes the open level.</summary>
        public bool DeletesOpenLevel => OpenIndex >= 0 && OpenIndex < m_deleted.Length && m_deleted[OpenIndex];
        /// <summary>True when a saved record uses the piece, so either option writes the database.</summary>
        public bool ChangesSavedLevels { get; private set; }
        public LevelPieceDeleteQuestion Question { get; private set; }

        public static LevelPieceDeletePlan Create(Input input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            LevelPieceDeletePlan plan = new LevelPieceDeletePlan
            {
                TemplateKey = input.TemplateKey,
                DisplayName = string.IsNullOrWhiteSpace(input.DisplayName)
                    ? input.TemplateKey.ToString()
                    : input.DisplayName,
                DatabaseName = input.DatabaseName,
                OpenIndex = input.OpenIndex,
                m_saved = input.SavedLevels ?? Array.Empty<LevelData>()
            };
            plan.PlanLevels(input.Draft);
            plan.Question = new LevelPieceDeleteQuestion(plan.GetMessage(input.DraftHasChanges, input.OtherDatabases),
                plan.CanDeleteTemplate, plan.CanDeleteBlocks);
            return plan;
        }

        /// <summary>The blocks of these levels whose cells form this key.</summary>
        public static int CountBlocks(IReadOnlyList<LevelData> levels, ShapeKey key)
        {
            return LevelBlockQueries.CountBlocks(levels, key);
        }

        /// <summary>The blocks of this level whose cells form this key.</summary>
        public static int CountBlocks(LevelData level, ShapeKey key)
        {
            return LevelBlockQueries.CountBlocks(level, key);
        }

        public bool Allows(LevelPieceDeleteChoice choice)
        {
            return Question.Accept(choice) == choice && choice != LevelPieceDeleteChoice.Cancel;
        }

        /// <summary>
        /// The database's records after the choice: changed records replace their saved ones, and Delete template and
        /// its blocks leaves out every level whose record keeps no block. Throws for Cancel or an option that is not
        /// possible.
        /// </summary>
        public List<LevelData> GetLevels(LevelPieceDeleteChoice choice)
        {
            RequireAllowed(choice);
            List<LevelData> levels = new List<LevelData>(m_saved.Count);
            for (int index = 0; index < m_saved.Count; index++)
            {
                if (choice == LevelPieceDeleteChoice.DeleteBlocks && m_deleted[index])
                {
                    continue;
                }

                LevelData changed = choice == LevelPieceDeleteChoice.DeleteTemplate ? m_kept[index] : m_removed[index];
                levels.Add(changed ?? m_saved[index]);
            }

            return levels;
        }

        /// <summary>A saved level's index after the choice, or -1 when Delete template and its blocks deletes it.</summary>
        public int GetWrittenIndex(int savedIndex, LevelPieceDeleteChoice choice)
        {
            if (savedIndex < 0 || savedIndex >= m_saved.Count)
            {
                return -1;
            }

            if (choice != LevelPieceDeleteChoice.DeleteBlocks)
            {
                return savedIndex;
            }

            if (m_deleted[savedIndex])
            {
                return -1;
            }

            int written = 0;
            for (int index = 0; index < savedIndex; index++)
            {
                written += m_deleted[index] ? 0 : 1;
            }

            return written;
        }

        /// <summary>
        /// The level to open after the choice deleted the saved level at this index, as Delete Level does: the next
        /// remaining level, else the last one before it; -1 when none remains.
        /// </summary>
        public int GetNearestWrittenIndex(int savedIndex, LevelPieceDeleteChoice choice)
        {
            for (int index = Mathf.Max(0, savedIndex); index < m_saved.Count; index++)
            {
                int written = GetWrittenIndex(index, choice);
                if (written >= 0)
                {
                    return written;
                }
            }

            for (int index = Mathf.Min(savedIndex, m_saved.Count) - 1; index >= 0; index--)
            {
                int written = GetWrittenIndex(index, choice);
                if (written >= 0)
                {
                    return written;
                }
            }

            return -1;
        }

        /// <summary>
        /// The choice applied to one copy of a level, such as the open draft or its saved baseline: Delete template
        /// names the template's key after the template in the level's pieces table, so its blocks keep their cells,
        /// colour, origin and effects as that level piece; Delete template and its blocks removes those blocks. The same
        /// instance comes back when no block uses the piece or for Cancel.
        /// </summary>
        public LevelData Apply(LevelData level, LevelPieceDeleteChoice choice)
        {
            if (level == null || choice == LevelPieceDeleteChoice.Cancel || CountBlocks(level, TemplateKey) == 0)
            {
                return level;
            }

            return choice == LevelPieceDeleteChoice.DeleteTemplate ? KeepAsLevelPieces(level) : RemoveBlocks(level);
        }

        /// <summary>The status when neither option is possible.</summary>
        public string GetRefusalStatus()
        {
            string text = $"Deleted nothing. {DisplayName} is used by {BlockCount} block(s), and neither choice is " +
                          "possible.";
            text += " Delete template: " + string.Join(" ", m_keepRefusals);
            text += " Delete template and its blocks: " + string.Join(" ", m_deleteRefusals);
            return text;
        }

        /// <summary>The status after the levels were written and the piece deleted.</summary>
        public string GetDoneStatus(LevelPieceDeleteChoice choice)
        {
            string text = choice == LevelPieceDeleteChoice.DeleteTemplate
                ? $"Deleted {DisplayName} from the piece library. {DescribeKeptBlocks()}."
                : $"Deleted {DisplayName} and {DescribeRemovedBlocks()}.";
            return text + GetRestoreNote();
        }

        /// <summary>The status when the levels were written but the library delete failed.</summary>
        public string GetLibraryFailedStatus(LevelPieceDeleteChoice choice, string issue)
        {
            string result = choice == LevelPieceDeleteChoice.DeleteTemplate
                ? DescribeKeptBlocks()
                : "Removed " + DescribeRemovedBlocks();
            return $"{result}, but {DisplayName} is still in the piece library: {issue} Press its × again to delete it.";
        }

        /// <summary>
        /// The status when the level JSON was written but the Level Editor could not read it back: the levels changed
        /// on disk, the draft and the library did not, and Reload JSON must run before the piece can be deleted.
        /// </summary>
        public string GetNotReloadedStatus(LevelPieceDeleteChoice choice, string reason)
        {
            string result = choice == LevelPieceDeleteChoice.DeleteTemplate
                ? DescribeKeptBlocks()
                : "removed " + DescribeRemovedBlocks();
            if (string.IsNullOrEmpty(reason))
            {
                reason = "The Level Editor could not reload it.";
            }

            return $"Wrote the level JSON: {result}. {reason} {DisplayName} is still in the piece library and the " +
                   $"open draft is unchanged. Press Reload JSON, then delete {DisplayName} again.";
        }

        private string DescribeKeptBlocks()
        {
            return $"{BlockCount} block(s) in {m_levels.Count} level(s) keep their cells as the level piece {DisplayName}";
        }

        private string DescribeRemovedBlocks()
        {
            string text = $"its {BlockCount} block(s) from {m_levels.Count} level(s)";
            if (m_deletedNames.Count > 0)
            {
                text += $"; deleted {string.Join(", ", m_deletedNames)} because no block remained";
            }

            return text;
        }

        private string GetRestoreNote()
        {
            return ChangesSavedLevels ? " Restore Previous Save brings back the previous level JSON." : string.Empty;
        }

        private void RequireAllowed(LevelPieceDeleteChoice choice)
        {
            if (!Allows(choice))
            {
                throw new InvalidOperationException($"{choice} is not an option of this template delete.");
            }
        }

        // Counts every level's blocks and computes both options for each saved record that uses the piece; only the
        // records that are written are validated.
        private void PlanLevels(LevelData draft)
        {
            int count = m_saved.Count;
            m_kept = new LevelData[count];
            m_removed = new LevelData[count];
            m_deleted = new bool[count];
            int remaining = count;
            for (int index = 0; index < count; index++)
            {
                LevelData saved = m_saved[index];
                bool isOpen = index == OpenIndex && draft != null;
                int savedCount = CountBlocks(saved, TemplateKey);
                int shownCount = isOpen ? CountBlocks(draft, TemplateKey) : savedCount;
                if (shownCount > 0 || savedCount > 0)
                {
                    m_levels.Add(new LevelUse(index, GetName(saved), shownCount, savedCount, index == OpenIndex));
                    BlockCount += shownCount;
                }

                if (savedCount == 0)
                {
                    continue;
                }

                ChangesSavedLevels = true;
                string name = GetName(saved);
                m_kept[index] = KeepAsLevelPieces(saved);
                ValidateWritten(m_kept[index], name, m_keepRefusals);
                m_removed[index] = RemoveBlocks(saved);
                if (m_removed[index].BlockCount == 0)
                {
                    m_deleted[index] = true;
                    m_deletedNames.Add(index == OpenIndex ? name + " (open level)" : name);
                    remaining--;
                }
                else
                {
                    ValidateWritten(m_removed[index], name, m_deleteRefusals);
                }
            }

            if (count > 0 && remaining == 0)
            {
                m_deleteRefusals.Add("It would delete every level of the database. Level database has no levels.");
            }
        }

        private static void ValidateWritten(LevelData level, string name, List<string> refusals)
        {
            if (!level.Validate(out string error))
            {
                refusals.Add($"{name}: {error}");
            }
        }

        // The level whose pieces table names the template's key after the template, so its blocks read as that level
        // piece once the template is gone.
        private LevelData KeepAsLevelPieces(LevelData level)
        {
            return LevelPieceNames.SetName(level, TemplateKey, DisplayName);
        }

        // Removes the piece's blocks back to front, so earlier indices stay valid.
        private LevelData RemoveBlocks(LevelData level)
        {
            LevelData result = level;
            for (int index = level.BlockCount - 1; index >= 0; index--)
            {
                if (level.GetBlockAt(index)?.Key == TemplateKey)
                {
                    LevelEditCommands.TryRemoveBlock(result, index, out result, out _);
                }
            }

            return result;
        }

        private string GetMessage(bool draftHasChanges, IReadOnlyList<OtherDatabase> others)
        {
            List<string> lines = new List<string>
            {
                $"Delete {DisplayName} from the piece library? {BlockCount} block(s) in {m_levels.Count} level(s) keep " +
                $"their cells as level pieces and take the name {DisplayName}.",
                string.Empty,
                $"{BlockCount} block(s) in {m_levels.Count} level(s) of {DatabaseName} use it:"
            };
            for (int index = 0; index < m_levels.Count; index++)
            {
                LevelUse use = m_levels[index];
                string line = $"- {use.Name}{(use.IsOpen ? " (open level)" : string.Empty)}: {use.BlockCount} block(s)";
                if (use.IsOpen && use.SavedBlockCount != use.BlockCount)
                {
                    line += $", {use.SavedBlockCount} in its saved copy";
                }

                lines.Add(line);
            }

            lines.Add(string.Empty);
            lines.Add(CanDeleteTemplate
                ? $"Delete template: those blocks keep their cells, colour, origin and effects as the level piece " +
                  $"{DisplayName}."
                : "Delete template is not possible: " + string.Join(" ", m_keepRefusals));
            lines.Add(CanDeleteBlocks
                ? "Delete template and its blocks: those blocks are removed from every level."
                : "Delete template and its blocks is not possible: " + string.Join(" ", m_deleteRefusals));
            if (CanDeleteBlocks && m_deletedNames.Count > 0)
            {
                lines.Add(string.Empty);
                lines.Add(k_emptiedHeading);
                for (int index = 0; index < m_deletedNames.Count; index++)
                {
                    lines.Add("- " + m_deletedNames[index]);
                }

                if (DeletesOpenLevel && draftHasChanges)
                {
                    lines.Add($"{GetName(m_saved[OpenIndex])} has unsaved changes, which will be lost.");
                }
            }

            AddOtherDatabases(lines, others);
            lines.Add(string.Empty);
            lines.Add(ChangesSavedLevels
                ? $"Either choice writes {DatabaseName} now; Restore Previous Save brings back the previous level JSON."
                : "Only the open level's draft changes; Undo brings it back.");
            return string.Join("\n", lines);
        }

        private static void AddOtherDatabases(List<string> lines, IReadOnlyList<OtherDatabase> others)
        {
            bool first = true;
            for (int index = 0; others != null && index < others.Count; index++)
            {
                OtherDatabase other = others[index];
                if (other.BlockCount == 0)
                {
                    continue;
                }

                if (first)
                {
                    lines.Add(string.Empty);
                    first = false;
                }

                lines.Add(other.BlockCount > 0
                    ? $"{other.Name}: {other.BlockCount} block(s) also use it and stay unchanged."
                    : $"{other.Name} could not be read, so its blocks were not counted; it stays unchanged.");
            }
        }

        private static string GetName(LevelData level)
        {
            if (level == null)
            {
                return "Untitled";
            }

            return string.IsNullOrWhiteSpace(level.Name) ? level.Id : level.Name;
        }
    }
}
