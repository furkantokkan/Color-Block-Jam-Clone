using System;
using System.Collections.Generic;
using ColorBlockJam.Runtime.Board;
using ColorBlockJam.Runtime.Levels;
using UnityEngine;

namespace ColorBlockJam.Editor.Levels
{
    /// <summary>
    /// What Save Level does with the pending template footprint changes (DEC-125): the footprints the piece library
    /// takes, the conflicts that refuse the whole save, the other levels of the active database that are re-stamped
    /// and those that cannot take the new footprint, and the block counts the top banner shows. Templates and their
    /// blocks are told apart by ShapeKey (DEC-193): a block of a template is a block whose cells form one of the
    /// template's known footprints. It only reads its inputs; no level, catalog or edit changes.
    /// </summary>
    /// <remarks>
    /// Surface:
    /// <list type="bullet">
    /// <item><c>static LevelTemplateSavePlan Create(LevelData candidate, IReadOnlyList&lt;OtherLevel&gt; otherLevels,
    /// IReadOnlyList&lt;LevelTemplateEdit&gt; edits, LevelShapeCatalog library,
    /// Func&lt;ShapeKey, IReadOnlyList&lt;Vector2Int&gt;&gt; getCommitted,
    /// Func&lt;ShapeKey, IReadOnlyList&lt;IReadOnlyList&lt;Vector2Int&gt;&gt;&gt; getPreviousFootprints,
    /// IReadOnlyList&lt;LevelTemplateEdit&gt; savedEdits = null)</c>. The candidate is the open level as it will be
    /// written; <c>edits</c> are the open level's unsaved (draft) edits; <c>savedEdits</c> are footprints an earlier
    /// Save Level already wrote to this database's levels while the library still lacks them. The library is the real
    /// piece library, not the pending overlay; <c>getCommitted</c> takes an edit's template key and
    /// <c>getPreviousFootprints</c> a template's current library key, and either may be null or return null. Edits of
    /// one template key (a saved one and a newer draft edit) act as one: the last one's cells are the new footprint,
    /// and every one's cells are known footprints. A template with saved edits only is a retry of the library commit:
    /// the library checks still refuse, but the candidate is not checked and no other level is re-stamped or asked
    /// about again, because the earlier save wrote them and the designer answered for the levels it left on the old
    /// footprint.</item>
    /// <item><c>OtherLevel(int index, string name, LevelData level)</c>: one other level of the active
    /// database.</item>
    /// <item><c>IReadOnlyList&lt;LevelTemplateEdit&gt; ToCommit</c>: the edits whose footprint the library still
    /// lacks.</item>
    /// <item><c>IReadOnlyList&lt;string&gt; Refusals</c>: whole-save refusals. When any exists, nothing is
    /// written.</item>
    /// <item><c>IReadOnlyList&lt;RestampedLevel&gt; Restamped</c>: each other level that changes (index, name, new
    /// level, re-stamped block count). Levels not listed are untouched and keep their reference.</item>
    /// <item><c>IReadOnlyList&lt;string&gt; Unfit</c> and <c>IReadOnlyList&lt;OtherLevel&gt; UnfitLevels</c>, in step:
    /// other levels that cannot take a new footprint, one message per level naming it and its blocks. Such a level
    /// keeps every block on the old footprint; it never refuses the save by itself.</item>
    /// <item><c>IReadOnlyList&lt;RestampedLevel&gt; Named</c>: the unfit levels as written, each old footprint their
    /// blocks keep named after the template in their pieces table (DEC-193), so those level pieces keep the
    /// template's name, as a template delete that keeps its blocks does (DEC-126). BlockCount is the blocks kept.</item>
    /// <item><c>bool GetCounts(ShapeKey templateKey, out int here, out int elsewhere, out int levels)</c>: blocks of
    /// the template in the candidate, blocks in the other levels and how many other levels hold them.</item>
    /// <item><c>bool HasWork</c>: a footprint to commit or another level to write.</item>
    /// </list>
    /// </remarks>
    internal sealed class LevelTemplateSavePlan
    {
        private readonly List<LevelTemplateEdit> m_toCommit = new List<LevelTemplateEdit>();
        private readonly List<string> m_refusals = new List<string>();
        private readonly List<RestampedLevel> m_restamped = new List<RestampedLevel>();
        private readonly List<string> m_unfit = new List<string>();
        private readonly List<OtherLevel> m_unfitLevels = new List<OtherLevel>();
        private readonly List<RestampedLevel> m_named = new List<RestampedLevel>();
        private readonly List<Entry> m_entries;

        private LevelTemplateSavePlan(List<Entry> entries)
        {
            m_entries = entries;
        }

        public IReadOnlyList<LevelTemplateEdit> ToCommit => m_toCommit;
        public IReadOnlyList<string> Refusals => m_refusals;
        public IReadOnlyList<RestampedLevel> Restamped => m_restamped;
        public IReadOnlyList<string> Unfit => m_unfit;
        public IReadOnlyList<OtherLevel> UnfitLevels => m_unfitLevels;
        public IReadOnlyList<RestampedLevel> Named => m_named;
        public bool HasWork => m_toCommit.Count > 0 || m_restamped.Count > 0 || m_named.Count > 0;

        /// <summary>Plans the save of every pending template edit; see the class remarks for the inputs.</summary>
        public static LevelTemplateSavePlan Create(LevelData candidate, IReadOnlyList<OtherLevel> otherLevels,
            IReadOnlyList<LevelTemplateEdit> edits, LevelShapeCatalog library,
            Func<ShapeKey, IReadOnlyList<Vector2Int>> getCommitted,
            Func<ShapeKey, IReadOnlyList<IReadOnlyList<Vector2Int>>> getPreviousFootprints,
            IReadOnlyList<LevelTemplateEdit> savedEdits = null)
        {
            List<Entry> entries = new List<Entry>();
            Group(entries, savedEdits, false);
            Group(entries, edits, true);
            LevelTemplateSavePlan plan = new LevelTemplateSavePlan(entries);
            if (plan.m_entries.Count == 0)
            {
                return plan;
            }

            if (library == null || library.Error != null)
            {
                string error = (library ?? LevelShapeCatalog.Missing).Error;
                plan.m_refusals.Add($"Save Level cannot write the template changes: {error}");
                plan.CountAll(candidate, otherLevels);
                return plan;
            }

            for (int index = 0; index < plan.m_entries.Count; index++)
            {
                Entry entry = plan.m_entries[index];
                entry.RecordIndex = library.IndexOfTemplate(entry.TemplateKey, entry.DisplayName);
                plan.CheckFootprint(entry);
            }

            plan.CheckPairs();
            LevelShapeCatalog unedited = GetUneditedCatalog(library, plan.m_entries);
            for (int index = 0; index < plan.m_entries.Count; index++)
            {
                plan.PlanEntry(plan.m_entries[index], candidate, library, unedited, getCommitted,
                    getPreviousFootprints);
            }

            plan.CountAll(candidate, otherLevels);
            plan.RestampOtherLevels(otherLevels);
            return plan;
        }

        /// <summary>
        /// Every level of <paramref name="levels"/> except the one at <paramref name="selectedIndex"/>, as the
        /// <see cref="OtherLevel"/> list <see cref="Create"/> and <see cref="LevelPieceEditController.UpdateOtherLevels"/>
        /// both need (Editor-Core-Levels-2-F4): it only reads the catalog.
        /// </summary>
        public static List<OtherLevel> GetOtherLevels(LevelCatalog levels, int selectedIndex)
        {
            List<OtherLevel> others = new List<OtherLevel>();
            for (int index = 0; index < levels.Count; index++)
            {
                if (index != selectedIndex)
                {
                    LevelData other = levels.GetAt(index);
                    others.Add(new OtherLevel(index, other?.Name, other));
                }
            }

            return others;
        }

        /// <summary>
        /// The banner counts for one template: blocks in the candidate, blocks in the other levels and the number of
        /// other levels that hold any. False, with zeros, when no edit names this template key.
        /// </summary>
        public bool GetCounts(ShapeKey templateKey, out int here, out int elsewhere, out int levels)
        {
            Entry entry = Find(m_entries, templateKey);
            here = entry?.Here ?? 0;
            elsewhere = entry?.Elsewhere ?? 0;
            levels = entry?.Levels ?? 0;
            return entry != null;
        }

        // One entry per template key in first-seen order; a later edit of the same template is the newer footprint.
        private static void Group(List<Entry> entries, IReadOnlyList<LevelTemplateEdit> edits, bool isDraft)
        {
            for (int index = 0; edits != null && index < edits.Count; index++)
            {
                LevelTemplateEdit edit = edits[index];
                if (edit == null || !edit.TemplateKey.IsValid)
                {
                    continue;
                }

                Entry entry = Find(entries, edit.TemplateKey);
                if (entry == null)
                {
                    entry = new Entry(edit.TemplateKey);
                    entries.Add(entry);
                }

                entry.Parts.Add(edit);
                entry.HasDraftPart |= isDraft;
            }
        }

        private void CountAll(LevelData candidate, IReadOnlyList<OtherLevel> otherLevels)
        {
            for (int index = 0; index < m_entries.Count; index++)
            {
                Entry entry = m_entries[index];
                entry.Here = CountTemplateBlocks(candidate, entry);
                for (int level = 0; otherLevels != null && level < otherLevels.Count; level++)
                {
                    int count = CountTemplateBlocks(otherLevels[level].Level, entry);
                    entry.Elsewhere += count;
                    entry.Levels += count > 0 ? 1 : 0;
                }
            }
        }

        private void CheckFootprint(Entry entry)
        {
            if (LevelCustomPiece.TryValidate(entry.Edit.Cells, out string issue))
            {
                entry.HasValidCells = true;
                return;
            }

            Refuse(entry, $"Template {entry.DisplayName}: {issue}");
        }

        // The library keeps one record per ShapeKey, so two templates may not end on one key (DEC-125, DEC-193).
        private void CheckPairs()
        {
            for (int first = 0; first < m_entries.Count; first++)
            {
                for (int second = first + 1; second < m_entries.Count; second++)
                {
                    Entry a = m_entries[first];
                    Entry b = m_entries[second];
                    if (a.HasValidCells && b.HasValidCells && a.Edit.Key == b.Edit.Key)
                    {
                        Refuse(a, $"Templates {a.DisplayName} and {b.DisplayName} would get the same footprint, "
                                  + "turned or mirrored; the piece library keeps one piece per shape.");
                        b.IsRefused = true;
                    }
                }
            }
        }

        // Refuses the entry for every check that applies, then decides what it commits: a retry entry (no draft part)
        // commits without gathering re-stamp sources, and a draft entry also gathers the footprints
        // RestampOtherLevels maps other levels' blocks from before it commits.
        private void PlanEntry(Entry entry, LevelData candidate, LevelShapeCatalog library,
            LevelShapeCatalog unedited, Func<ShapeKey, IReadOnlyList<Vector2Int>> getCommitted,
            Func<ShapeKey, IReadOnlyList<IReadOnlyList<Vector2Int>>> getPreviousFootprints)
        {
            if (!entry.HasValidCells)
            {
                return;
            }

            // A saved footprint is dropped with the banner's Keep library footprint, an unsaved one with Discard.
            string drop = entry.HasDraftPart
                ? "Discard the template change"
                : "Press Keep library footprint in the template banner";
            if (entry.RecordIndex < 0)
            {
                Refuse(entry, $"Template {entry.DisplayName} is no longer in the piece library, so Save Level cannot "
                              + $"write its new footprint. {drop}.");
                return;
            }

            LevelShapeChoice record = library.Get(entry.RecordIndex);
            ShapeKey newKey = entry.Edit.Key;
            IReadOnlyList<Vector2Int> committed = getCommitted?.Invoke(entry.TemplateKey);
            bool inLibrary = record.Key == newKey;
            if (!inLibrary && !IsKnownFootprint(entry, record.Key, committed))
            {
                Refuse(entry, $"Template {entry.DisplayName} changed in the piece library after you started editing "
                              + $"it here, for example in the Piece Editor. {drop}, then edit it again.");
            }

            // Templates with an edit of their own are left out: their library footprint is going away (CheckPairs).
            LevelShapeChoice other = unedited.Find(newKey);
            if (other != null)
            {
                Refuse(entry, $"The new footprint of template {entry.DisplayName} is {other.DisplayName} turned or "
                              + "mirrored; the piece library keeps one piece per shape.");
            }

            if (!entry.HasDraftPart)
            {
                // A retry of an earlier save's library commit: the levels it wrote, and the ones the designer chose to
                // leave on the old footprint, stay as they are. The open level may be one of those left.
                if (!entry.IsRefused && !inLibrary)
                {
                    m_toCommit.Add(entry.Edit);
                }

                return;
            }

            AddRestampSources(entry, record.Cells, committed, getPreviousFootprints?.Invoke(record.Key), unedited);
            string stray = FindBlockOnSource(candidate, entry);
            if (stray != null)
            {
                Refuse(entry, $"Block {stray} of template {entry.DisplayName} in this level does not match its new "
                              + "footprint. Re-stamp it, then Save Level again.");
            }

            entry.CanRestamp = true;
            if (!entry.IsRefused && !inLibrary)
            {
                m_toCommit.Add(entry.Edit);
            }
        }

        // The footprints another level's blocks of the template may carry now, in the order they are tried (DEC-125,
        // "Pen edit and template edits" in docs/Level-Editor-Design.md): the library's, every edit's base, the saved
        // edits' footprints, the one this window committed and the ones the template had before its last library
        // changes. A footprint another template holds is that template's, and the new footprint needs no re-stamp.
        private static void AddRestampSources(Entry entry, IReadOnlyList<Vector2Int> libraryCells,
            IReadOnlyList<Vector2Int> committed, IReadOnlyList<IReadOnlyList<Vector2Int>> previous,
            LevelShapeCatalog unedited)
        {
            AddSource(entry, libraryCells, unedited);
            for (int index = 0; index < entry.Parts.Count; index++)
            {
                AddSource(entry, entry.Parts[index].BaseCells, unedited);
            }

            for (int index = 0; index < entry.Parts.Count - 1; index++)
            {
                AddSource(entry, entry.Parts[index].Cells, unedited);
            }

            AddSource(entry, committed, unedited);
            for (int index = 0; previous != null && index < previous.Count; index++)
            {
                AddSource(entry, previous[index], unedited);
            }
        }

        private static void AddSource(Entry entry, IReadOnlyList<Vector2Int> footprint, LevelShapeCatalog unedited)
        {
            if (footprint == null || !ShapeKey.TryCreate(footprint, out ShapeKey key) || key == entry.Edit.Key
                || unedited.IndexOf(key) >= 0 || entry.FindSource(key) != null)
            {
                return;
            }

            entry.Sources.Add(footprint);
        }

        // A refused save still lists the other levels, so the banner can show every problem at once. Every block is
        // matched to a template by its key in the level as it was, so templates whose footprints trade places never
        // re-stamp a block twice.
        private void RestampOtherLevels(IReadOnlyList<OtherLevel> otherLevels)
        {
            for (int levelIndex = 0; otherLevels != null && levelIndex < otherLevels.Count; levelIndex++)
            {
                OtherLevel other = otherLevels[levelIndex];
                if (other.Level == null)
                {
                    continue;
                }

                LevelData current = other.Level;
                int blocks = 0;
                List<Entry> refusedEntries = null;
                List<List<string>> refusedBlocks = null;
                for (int blockIndex = 0; blockIndex < other.Level.BlockCount; blockIndex++)
                {
                    ShapeKey key = other.Level.GetBlockAt(blockIndex)?.Key ?? default;
                    Entry entry = FindRestampEntry(key, out IReadOnlyList<Vector2Int> source);
                    if (entry == null)
                    {
                        continue;
                    }

                    if (LevelShapeRestamp.TryRestampBlock(current, blockIndex, source, entry.Edit.Cells,
                            out LevelData next, out string issue))
                    {
                        current = next;
                        blocks++;
                        continue;
                    }

                    // The rest is still tried so the question can list every block in the way.
                    refusedEntries ??= new List<Entry>();
                    refusedBlocks ??= new List<List<string>>();
                    int position = refusedEntries.IndexOf(entry);
                    if (position < 0)
                    {
                        refusedEntries.Add(entry);
                        refusedBlocks.Add(new List<string>());
                        position = refusedEntries.Count - 1;
                    }

                    refusedBlocks[position].Add(issue);
                }

                List<string> issues = null;
                for (int index = 0; refusedEntries != null && index < refusedEntries.Count; index++)
                {
                    issues ??= new List<string>();
                    issues.Add($"{refusedBlocks[index].Count} block(s) of {refusedEntries[index].DisplayName} would not "
                               + "fit: " + string.Join("; ", refusedBlocks[index]));
                }

                // A re-stamp may overlap other blocks (D3-B); the written database must still validate.
                if (issues == null && !ReferenceEquals(current, other.Level) && !current.Validate(out string error))
                {
                    issues = new List<string> { error };
                }

                if (issues != null)
                {
                    m_unfitLevels.Add(other);
                    m_unfit.Add($"Level {other.Name}: " + string.Join("; ", issues));
                    AddNamedLevel(other);
                }
                else if (!ReferenceEquals(current, other.Level))
                {
                    m_restamped.Add(new RestampedLevel(other.Index, other.Name, current, blocks));
                }
            }
        }

        // An unfit level keeps every block on the old footprint; each old footprint its blocks keep is named after the
        // template in its pieces table, so they stay the template's name as level pieces instead of a default name
        // (DEC-193, as DEC-126's Delete template names kept blocks). A name the pieces table cannot hold is left out.
        private void AddNamedLevel(OtherLevel other)
        {
            LevelData named = other.Level;
            int kept = 0;
            for (int blockIndex = 0; blockIndex < other.Level.BlockCount; blockIndex++)
            {
                ShapeKey key = other.Level.GetBlockAt(blockIndex)?.Key ?? default;
                Entry entry = FindRestampEntry(key, out _);
                if (entry == null || !LevelPieceNames.CanName(entry.DisplayName))
                {
                    continue;
                }

                named = LevelPieceNames.SetName(named, key, entry.DisplayName);
                kept++;
            }

            if (!ReferenceEquals(named, other.Level))
            {
                m_named.Add(new RestampedLevel(other.Index, other.Name, named, kept));
            }
        }

        // The entry that re-stamps blocks with this key, and the footprint they carry, or null.
        private Entry FindRestampEntry(ShapeKey key, out IReadOnlyList<Vector2Int> source)
        {
            for (int index = 0; key.IsValid && index < m_entries.Count; index++)
            {
                Entry entry = m_entries[index];
                source = entry.CanRestamp ? entry.FindSource(key) : null;
                if (source != null)
                {
                    return entry;
                }
            }

            source = null;
            return null;
        }

        // The id of the first block of the template in the candidate (a block its draft edit records) on one of the
        // template's earlier footprints, or null: after the Pen's re-stamps every block of the template here carries the
        // new footprint. A block that merely shares an earlier footprint's key is not the template's (DEC-193).
        private static string FindBlockOnSource(LevelData level, Entry entry)
        {
            List<string> members = entry.Edit.GetMemberIds();
            for (int index = 0; level != null && index < level.BlockCount; index++)
            {
                LevelBlockData block = level.GetBlockAt(index);
                if (block != null && LevelBlockQueries.ContainsId(members, block.Id)
                    && entry.FindSource(block.Key) != null)
                {
                    return block.Id;
                }
            }

            return null;
        }

        // The blocks of the level whose cells form the template's library key, its new key or a re-stamp source's.
        private static int CountTemplateBlocks(LevelData level, Entry entry)
        {
            int count = 0;
            for (int index = 0; level != null && index < level.BlockCount; index++)
            {
                ShapeKey key = level.GetBlockAt(index)?.Key ?? default;
                if (key.IsValid && (key == entry.TemplateKey || key == entry.Edit.Key || entry.FindSource(key) != null))
                {
                    count++;
                }
            }

            return count;
        }

        // Base check (DEC-125): the library still holds a footprint this window started from or wrote itself.
        private static bool IsKnownFootprint(Entry entry, ShapeKey libraryKey, IReadOnlyList<Vector2Int> committed)
        {
            for (int index = 0; index < entry.Parts.Count; index++)
            {
                if (entry.Parts[index].BaseKey == libraryKey || entry.Parts[index].Key == libraryKey)
                {
                    return true;
                }
            }

            return committed != null && ShapeKey.TryCreate(committed, out ShapeKey committedKey)
                                     && committedKey == libraryKey;
        }

        // The library without the templates the entries change.
        private static LevelShapeCatalog GetUneditedCatalog(LevelShapeCatalog library, List<Entry> entries)
        {
            List<LevelShapeChoice> choices = new List<LevelShapeChoice>(library.Count);
            for (int index = 0; index < library.Count; index++)
            {
                bool edited = false;
                for (int entry = 0; entry < entries.Count && !edited; entry++)
                {
                    edited = entries[entry].RecordIndex == index;
                }

                if (!edited)
                {
                    choices.Add(library.Get(index));
                }
            }

            return LevelShapeCatalog.Create(choices.ToArray());
        }

        private static Entry Find(List<Entry> entries, ShapeKey templateKey)
        {
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index].TemplateKey == templateKey)
                {
                    return entries[index];
                }
            }

            return null;
        }

        private void Refuse(Entry entry, string message)
        {
            entry.IsRefused = true;
            m_refusals.Add(message);
        }

        /// <summary>One other level of the active database: its position, its name and its data.</summary>
        public readonly struct OtherLevel
        {
            public OtherLevel(int index, string name, LevelData level)
            {
                Index = index;
                Name = string.IsNullOrWhiteSpace(name) ? level?.Name ?? $"#{index + 1}" : name;
                Level = level;
            }

            public int Index { get; }
            public string Name { get; }
            public LevelData Level { get; }
        }

        /// <summary>Another level after the re-stamp, and how many of its blocks took a new footprint.</summary>
        public readonly struct RestampedLevel
        {
            public RestampedLevel(int index, string name, LevelData level, int blockCount)
            {
                Index = index;
                Name = name;
                Level = level;
                BlockCount = blockCount;
            }

            public int Index { get; }
            public string Name { get; }
            public LevelData Level { get; }
            public int BlockCount { get; }
        }

        // The edits of one template in this save, its counts and the footprints its blocks may carry elsewhere.
        private sealed class Entry
        {
            public Entry(ShapeKey templateKey)
            {
                TemplateKey = templateKey;
            }

            /// <summary>The key the library held for the template when its edits began.</summary>
            public ShapeKey TemplateKey { get; }
            public List<LevelTemplateEdit> Parts { get; } = new List<LevelTemplateEdit>();
            public List<IReadOnlyList<Vector2Int>> Sources { get; } = new List<IReadOnlyList<Vector2Int>>();
            /// <summary>The newest edit: its cells are the footprint this save writes.</summary>
            public LevelTemplateEdit Edit => Parts[Parts.Count - 1];
            public string DisplayName => Edit.DisplayName;
            /// <summary>The library record the edits change (by key, else by name), or -1.</summary>
            public int RecordIndex { get; set; } = -1;
            public int Here { get; set; }
            public int Elsewhere { get; set; }
            public int Levels { get; set; }
            /// <summary>True when an unsaved edit of the open level is part of this entry.</summary>
            public bool HasDraftPart { get; set; }
            public bool HasValidCells { get; set; }
            public bool CanRestamp { get; set; }
            public bool IsRefused { get; set; }

            // The re-stamp source whose footprint forms this key, or null.
            public IReadOnlyList<Vector2Int> FindSource(ShapeKey key)
            {
                for (int index = 0; key.IsValid && index < Sources.Count; index++)
                {
                    if (ShapeKey.TryCreate(Sources[index], out ShapeKey sourceKey) && sourceKey == key)
                    {
                        return Sources[index];
                    }
                }

                return null;
            }
        }
    }
}
